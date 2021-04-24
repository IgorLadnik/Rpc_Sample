using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Text;
using System.Diagnostics;
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
        private readonly AsyncAutoResetEvent _aev = new AsyncAutoResetEvent();

        private int _isValid = 0;

        protected readonly ILogger _logger;

        private static readonly Container _container;

        private readonly Action<ILogger, bool, string, string, string, string, object[]> _beforeCall;
        private readonly Action<ILogger, bool, string, string, string, string, object[], object, TimeSpan, Exception> _afterCall;

        #endregion // Vars

        #region Ctor

        static RpcAndStreamingHub() => 
            _container = new Container();

        protected RpcAndStreamingHub(
            ILoggerFactory loggerFactory = null, 
            StreamingDataProvider<T> streamingDataProvider = null,
            Action<ILogger, bool, string, string, string, string, object[]> beforeCall = null,
            Action<ILogger, bool, string, string, string, string, object[], object, TimeSpan, Exception> afterCall = null)
        {
            _logger = loggerFactory?.CreateLogger<RpcAndStreamingHub<T>>();           
            IsValid = true;
            streamingDataProvider?.Add(this);
            _streamingDataProvider = streamingDataProvider;
            _container.SetLogger(loggerFactory);
            _beforeCall = beforeCall;
            _afterCall = afterCall;
        }

        #endregion // Ctor

        #region Register

        public static void RegisterSingleton<TInterface>(TInterface ob) =>
            _container.RegisterSingleton(ob);

        public static void RegisterPerCall<TInterface, TImpl>() where TImpl : TInterface, new() =>
            _container.Register(typeof(TInterface), typeof(TImpl), Instantiation.PerCall);

        public static void RegisterPerSession<TInterface, TImpl>(int sessionLifeTimeInMin = -1) where TImpl : TInterface, new() =>
            _container.Register(typeof(TInterface), typeof(TImpl), Instantiation.PerSession, sessionLifeTimeInMin);

        #endregion // Register

        #region Rpc, StartStreaming, KillClientSessionsIfExist

        public RpcDtoResponse Rpc(RpcDtoRequest arg) => Rpc(arg, false);

        public void RpcOneWay(RpcDtoRequest arg) => Rpc(arg, true);

        private RpcDtoResponse Rpc(RpcDtoRequest arg, bool isOneWay)
        {
            if (!_container.DctInterface.ContainsKey(arg.InterfaceName))
                throw new Exception($"Interface '{arg.InterfaceName}' is not regidtered");

            var methodArgs = _container.GetMethodArguments(arg);
            var localOb = _container.Resolve(arg.InterfaceName, arg.ClientId);

            IDirectCall directCall = null;
            if (localOb == null)
                localOb = this;
            else
                directCall = localOb as IDirectCall;

            object result = null;
            Exception ex = null;
            var isDirectCall = directCall != null;
            var sw = new Stopwatch();
            try
            {
                _beforeCall?.Invoke(_logger, isDirectCall, arg.Id, arg.ClientId, arg.InterfaceName, arg.MethodName, methodArgs);
                sw.Start();
                result = isDirectCall
                    ? directCall.DirectCall(arg.MethodName, methodArgs)
                    : localOb?.GetType().GetMethod(arg.MethodName)?.Invoke(localOb, methodArgs);
            }
            catch (Exception e)
            {
                _logger?.LogError($"Method '{arg.InterfaceName}.{arg.MethodName}()' failed", e);
                throw e;
            }
            finally 
            {
                sw.Stop();
                _afterCall?.Invoke(_logger, isDirectCall, arg.Id, arg.ClientId, arg.InterfaceName, arg.MethodName, methodArgs, result, sw.Elapsed, ex);
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
                        Result = new DtoData { TypeName = result.GetType().FullName, Data = result }
                    };

            //await Clients.All.SendAsync("ReceiveMessage", "...", retOb.ToString());
        }

        public ChannelReader<T> StartStreaming() =>
            _streamingDataProvider == null
                ? null 
                : Observable.Create<T>(async observer =>
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
            var sb = new StringBuilder();
            foreach (var k in _container.DctInterface.Keys)
            {
                var descriptor = _container.DctInterface[k];
                if (descriptor.IsPerSession)
                {
                    var psd = (InterfaceDescriptorPerSession)descriptor;
                    if (psd.CdctSession != null && psd.CdctSession.TryRemove(clientId, out SessionDescriptor sd))
                    {
                        interfacesCount++;
                        sb.Append($"'{k}', ");
                    }
                }
            }

            if (sb.Length > 0)
            {
                var tempStr = sb.ToString().Substring(0, sb.Length - 2);
                _logger?.LogInformation($"Sessions for client '{clientId}' have been deleted for interfaces {tempStr}");
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
