using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using AsyncAutoResetEventLib;
using DtoLib;

namespace SignalRBaseHubServerLib
{
    public class RpcAndStreamingHub<T> : Hub, ISetEvent
    {
        #region Vars

        protected readonly IStreamingDataProvider<T> _streamingDataProvider;
        private readonly AsyncAutoResetEvent _aev = new();

        private int _isValid = 0;

        private readonly static Dictionary<string, BaseInterfaceDescriptor> _dctInterface = new() 
        { 
            { 
                "_", new BaseInterfaceDescriptor 
                { 
                    dctType = new() 
                    {
                        { "System.String", typeof(string) }
                    } 
                } 
            } 
        };

        protected readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;

        private static Timer _timer;

        #endregion // Vars

        #region Ctor

        protected RpcAndStreamingHub(ILoggerFactory loggerFactory, StreamingDataProvider<T> streamingDataProvider)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<RpcAndStreamingHub<T>>();           
            IsValid = true;
            streamingDataProvider.Add(this);
            _streamingDataProvider = streamingDataProvider;
        }

        #endregion // Ctor

        #region Register

        public static void RegisterSingleton<TInterface>(TInterface ob) 
        {
            var @interface = typeof(TInterface); 
            _dctInterface[@interface.Name] = new InterfaceDescriptorSingleton
                {
                    ob = ob,
                    instanceType = InstanceType.Singleton,
                    dctType = GetTypeDictionary(@interface),
                };
        }

        public static void RegisterPerCall<TInterface, TImpl>() where TImpl : TInterface, new() =>
            Register(typeof(TInterface), typeof(TImpl), InstanceType.PerCall);

        public static void RegisterPerSession<TInterface, TImpl>(int sessionLifeTimeInMin = -1) where TImpl : TInterface, new() =>
            Register(typeof(TInterface), typeof(TImpl), InstanceType.PerSession, sessionLifeTimeInMin);

        private static void Register(Type @interface, Type implType, InstanceType instanceType, int sessionLifeTimeInMin = -1)
        {
            var isPerSession = instanceType == InstanceType.PerSession;           
            _dctInterface[@interface.Name] = BaseInterfaceDescriptor.InterfaceDescriptorFactory(implType, instanceType, GetTypeDictionary(@interface));

            if (instanceType == InstanceType.PerSession && sessionLifeTimeInMin > 0 && _timer == null)
            {
                var sessionLifeTime = TimeSpan.FromMinutes(sessionLifeTimeInMin);
                _timer = new(_ =>
                {
                    var now = DateTime.UtcNow;
                    foreach (var cdct in _dctInterface.Values?
                                .Where(d => d.instanceType == InstanceType.PerSession)?
                                .Select(d => ((InterfaceDescriptorPerSession)d).cdctSession))
                    {
                        foreach (var clientId in cdct?.Keys?.ToArray())
                            if (now - new DateTime(cdct[clientId].lastActivationInTicks) > sessionLifeTime)
                                cdct.Remove(clientId, out SessionDescriptor psd);
                    }
                },
                null, TimeSpan.Zero, TimeSpan.FromMinutes(sessionLifeTimeInMin));
            }
        }

        #endregion // Register

        #region Type manipulations

        private static Dictionary<string, Type> GetTypeDictionary(Type interfaceType)
        {
            Dictionary<string, Type> dctType = new();
            foreach (var mi in interfaceType.GetMethods())
                foreach (var pi in mi.GetParameters())
                    dctType[pi.ParameterType.FullName] = pi.ParameterType;

            return dctType;
        }

        private static object[] GetMethodArguments(RpcDtoRequest arg)
        {
            if (!_dctInterface.TryGetValue(arg.InterfaceName, out BaseInterfaceDescriptor descriptor))
                return null;

            List<object> methodParams = new();
            foreach (var dtoData in arg?.Args)
            {
                var je = (JsonElement)dtoData.Data;

                if (!descriptor.dctType.TryGetValue(dtoData.TypeName, out Type type))
                    throw new Exception($"Type '{dtoData.TypeName}' is not registered");

                methodParams.Add(JsonSerializer.Deserialize(je.GetRawText(), type, new() { PropertyNameCaseInsensitive = true }));
            }

            return methodParams.ToArray();
        }

        #endregion // Type manipulations

        #region Resolve, CreateInstance

        private object Resolve(string interafceName, string clientId = null)
        {
            if (!_dctInterface.TryGetValue(interafceName, out BaseInterfaceDescriptor descriptor))
                return null;

            if (descriptor.instanceType == InstanceType.Singleton)
                // Singleton
                return ((InterfaceDescriptorSingleton)descriptor).ob;

            if (descriptor.type != null)
            {
                if (descriptor.instanceType == InstanceType.PerCall)
                    // Per Call
                    return CreateInstanceWithLoggerIfSupported(descriptor.type);

                if (descriptor.instanceType == InstanceType.PerSession)
                {
                    // Per Session
                    var psd = (InterfaceDescriptorPerSession)descriptor;
                    if (psd.cdctSession.TryGetValue(clientId, out SessionDescriptor sd))
                    {
                        sd.lastActivationInTicks = DateTime.UtcNow.Ticks;
                        return sd.ob;
                    }

                    psd.cdctSession[clientId] = sd = new()
                        {
                            ob = CreateInstanceWithLoggerIfSupported(psd.type),
                            lastActivationInTicks = DateTime.UtcNow.Ticks,
                        };

                    return sd.ob;
                }
            }

            return null;
        }

        private object CreateInstanceWithLoggerIfSupported(Type type) =>
            AssignLoggerIfSupported(Activator.CreateInstance(type));

        private object AssignLoggerIfSupported(object ob)
        {
            var log = ob as ILog;
            if (log != null)
                log.LoggerFactory = _loggerFactory;
            return ob;
        }

        #endregion Resolve, CreateInstance

        #region Rpc, StartStreaming, KillClientSessionsIfExist

        public RpcDtoResponse Rpc(RpcDtoRequest arg) => Rpc(arg, false);

        public void RpcOneWay(RpcDtoRequest arg) => Rpc(arg, true);

        private RpcDtoResponse Rpc(RpcDtoRequest arg, bool isOneWay)
        {
            if (!_dctInterface.ContainsKey(arg.InterfaceName))
                throw new Exception($"Interface '{arg.InterfaceName}' is not regidtered");

            var methodArgs = GetMethodArguments(arg);
            var localOb = Resolve(arg.InterfaceName, arg.ClientId);

            IDirectCall directCall = null;
            if (localOb == null)
                localOb = this;
            else
                directCall = localOb as IDirectCall;

            object result;
            try
            {
                if (directCall != null)
                {
                    _logger.LogInformation($"Before calling method '{arg.MethodName}()' of interface '{arg.InterfaceName}' - direct call");
                    result = directCall.DirectCall(arg.MethodName, methodArgs);
                    _logger.LogInformation($"After calling method '{arg.MethodName}()' of interface '{arg.InterfaceName}' - direct call");
                }
                else
                {
                    _logger.LogInformation($"Before calling method '{arg.MethodName}()' of interface '{arg.InterfaceName}' - call with reflection");
                    var methodInfo = localOb?.GetType().GetMethod(arg.MethodName);
                    result = methodInfo?.Invoke(localOb, methodArgs);
                    _logger.LogInformation($"After calling method '{arg.MethodName}()' of interface '{arg.InterfaceName}' - call with reflection");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed method '{arg.InterfaceName}.{arg.MethodName}()'", e);
            }

            return isOneWay 
                    ? null
                    : new RpcDtoResponse
                    {
                        ClientId = arg.ClientId,
                        Id = arg.Id,
                        InterfaceName = arg.InterfaceName,
                        MethodName = arg.MethodName,
                        Status = DtoStatus.Processed,
                        Result = new() { TypeName = result.GetType().FullName, Data = result }
                    };

            //await Clients.All.SendAsync("ReceiveMessage", "...", retOb.ToString());
        }

        public ChannelReader<T> StartStreaming() =>
            Observable.Create<T>(async observer =>
            {
                while (!Context.ConnectionAborted.IsCancellationRequested)
                {               
                    await _aev.WaitAsync();
                    observer.OnNext(_streamingDataProvider.Current);
                }
            }).AsChannelReader();

        public int KillClientSessionsIfExist(string clientId)
        {
            var interfacesCount = 0;
            StringBuilder sb = new();
            foreach (var k in _dctInterface.Keys)
            {
                var descriptor = _dctInterface[k];
                if (descriptor.instanceType == InstanceType.PerSession)
                {
                    var psd = (InterfaceDescriptorPerSession)descriptor;
                    if (psd.cdctSession != null && psd.cdctSession.TryRemove(clientId, out SessionDescriptor sd))
                    {
                        interfacesCount++;
                        sb.Append($"'{k}', ");
                    }
                }
            }

            if (sb.Length > 0)
            {
                var tempStr = sb.ToString().Substring(0, sb.Length - 2);
                _logger.LogInformation($"Sessions for client '{clientId}' have been deleted for interfaces {tempStr}");
            }

            return interfacesCount;
        }

        #endregion // Rpc, StartStreaming, KillClientSessionsIfExist

        #region Aux

        public bool IsValid
        {
            get => Interlocked.Exchange(ref _isValid, _isValid) == 1;
            private set => Interlocked.Exchange(ref _isValid, value ? 1 : 0);
        }
        
        public void SetEvent() =>
            _aev.Set();

        #endregion // Aux

        #region Dispose

        protected override void Dispose(bool disposing)
        {
            IsValid = false;
            base.Dispose(disposing);
        }

        #endregion // Dispose
    }
}
