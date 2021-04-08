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
        #region Inner Descriptor classes

        class InterfaceDescriptor
        {
            public Type type;
            public object ob;
            public bool isPerSession = false;
            public ConcurrentDictionary<string, SessionDescriptor> dctSession;
            public Dictionary<string, Type> dctType;
        }

        class SessionDescriptor
        {
            public object ob;

            private long _lastActivationInTicks;
            public long lastActivationInTicks
            {
                get => Interlocked.Read(ref _lastActivationInTicks);
                set => Interlocked.Exchange(ref _lastActivationInTicks, value);
            }
        }

        #endregion // Inner Descriptor classes

        #region Vars

        protected readonly IStreamingDataProvider<T> _streamingDataProvider;
        private readonly AsyncAutoResetEvent _aev = new();

        private int _isValid = 0;

        private readonly static Dictionary<string, InterfaceDescriptor> _dctInterface = new() 
        { 
            { 
                "_", new InterfaceDescriptor 
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
            _dctInterface[@interface.Name] = new()
            {
                ob = ob,
                isPerSession = false,
                dctType = GetTypeDictionary(@interface),
            };
        }

        public static void RegisterPerCall<TInterface, TImpl>() where TImpl : TInterface, new() =>
            Register(typeof(TInterface), typeof(TImpl));

        public static void RegisterPerSession<TInterface, TImpl>(int sessionLifeTimeInMin = -1) where TImpl : TInterface, new() =>
            Register(typeof(TInterface), typeof(TImpl), true, sessionLifeTimeInMin);

        private static void Register(Type @interface, Type impl, bool isPerSession = false, int sessionLifeTimeInMin = -1)
        {
            //_logger.LogInformation($"About to register interface '{@interface.Name}' with type '{impl.Name}', isPerSession = {isPerSession}, sessionLifeTimeInMin = {sessionLifeTimeInMin}");
            _dctInterface[@interface.Name] = new()
            {
                type = impl,
                isPerSession = isPerSession,
                dctType = GetTypeDictionary(@interface),
            };

            if (isPerSession && sessionLifeTimeInMin > 0 && _timer == null)
            {
                var sessionLifeTime = TimeSpan.FromMinutes(sessionLifeTimeInMin);
                _timer = new(_ =>
                {
                    var now = DateTime.UtcNow;
                    foreach (var dict in _dctInterface.Values?.Where(d => d.isPerSession)?.Select(d => d.dctSession))
                    {
                        if (dict == null || dict.Count == 0)
                            continue;

                        foreach (var clientId in dict.Keys.ToArray())
                            if (now - new DateTime(dict[clientId].lastActivationInTicks) > sessionLifeTime)
                                dict.Remove(clientId, out SessionDescriptor psd);
                    }
                },
                null, TimeSpan.Zero, TimeSpan.FromMinutes(sessionLifeTimeInMin));
            }

            //_logger.LogInformation($"Registered interface '{@interface.Name}' with type '{impl.Name}', isPerSession = {isPerSession}, sessionLifeTimeInMin = {sessionLifeTimeInMin}");
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
            if (!_dctInterface.TryGetValue(arg.InterfaceName, out InterfaceDescriptor descriptor))
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
            if (!_dctInterface.TryGetValue(interafceName, out InterfaceDescriptor descriptor))
                return null;

            if (descriptor.ob != null)
                // Singleton
                return descriptor.ob;

            if (descriptor.type != null)
            {
                if (!descriptor.isPerSession || string.IsNullOrEmpty(clientId))
                    // Per Call
                    return CreateInstanceWithLoggerIfSupported(descriptor.type);

                // Per Session
                if (descriptor.dctSession == null)
                    descriptor.dctSession = new();

                if (descriptor.dctSession.TryGetValue(clientId, out SessionDescriptor perSessionDescriptor))
                {
                    perSessionDescriptor.lastActivationInTicks = DateTime.UtcNow.Ticks;
                    return perSessionDescriptor.ob;
                }

                descriptor.dctSession[clientId] = perSessionDescriptor = new()
                {
                    ob = CreateInstanceWithLoggerIfSupported(descriptor.type),
                    lastActivationInTicks = DateTime.UtcNow.Ticks,
                };

                return perSessionDescriptor.ob;
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
                if (descriptor.isPerSession && descriptor.dctSession != null && descriptor.dctSession.TryRemove(clientId, out SessionDescriptor psd))
                {
                    interfacesCount++;
                    sb.Append($"'{k}', ");
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
