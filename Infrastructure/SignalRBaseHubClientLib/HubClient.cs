using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using DtoLib;

namespace SignalRBaseHubClientLib
{
    public class HubClient : IDisposable
    {
        public string Url { get; }
        public string ClientId { get; }

        public HubConnection Connection { get; protected set; }
        protected CancellationTokenSource _cts = new CancellationTokenSource();
        protected ILogger _logger;

        private Dictionary<string, Type> _dctType = new Dictionary<string, Type>();

        private readonly Action<ILogger, bool, RpcDtoRequest> _beforeCall;
        private readonly Action<ILogger, bool, RpcDtoRequest, object, TimeSpan, Exception> _afterCall;

        #region Ctor

        public HubClient(string url, 
                         ILoggerFactory loggerFactory = null, 
                         string clientId = null,
                         Action<ILogger, bool, RpcDtoRequest> beforeCall = null,
                         Action<ILogger, bool, RpcDtoRequest, object, TimeSpan, Exception> afterCall = null)
        {
            _logger = loggerFactory?.CreateLogger<HubClient>();
            Url = url;
            ClientId = string.IsNullOrWhiteSpace(clientId) ? $"{Guid.NewGuid()}" : clientId;

            _beforeCall = beforeCall;
            _afterCall = afterCall;
        }

        #endregion // Ctor

        #region Type manipulations

        public HubClient RegisterInterface<TInterface>() where TInterface : class
        {
            foreach (var mi in typeof(TInterface).GetMethods())
            {
                var rt = mi.ReturnType;
                _dctType[rt.FullName] = rt;
            }

            return this;
        }

        private object GetResult(JObject jo)
        {
            if (jo == null)
                return null;

            if (!jo.TryGetValue("result", out JToken jt))
                return null;

            var typFullName = jt.First().Values<string>().First();
            var result = jt.Last().Values<object>().First();

            object jOb = result as JArray;
            if (jOb == null)
                jOb = result as JObject;
            if (jOb == null)
                jOb = result as JValue;

            if (jOb != null) 
            {
                var type = _dctType[typFullName];
                var jVal = jOb as JValue;
                return jVal != null && jVal?.Value.GetType() == type
                        ? jVal.Value
                        : JsonSerializer.Deserialize($"{jOb}", _dctType[typFullName], new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }

        #endregion // Type manipulations

        #region StartConnection, Subscribe

        public async Task<HubClient> StartConnectionAsync(int retryIntervalMs = 0, int numOfAttempts = 0)
        {
            Connection = new HubConnectionBuilder().WithUrl(Url, options =>
                options.HttpMessageHandlerFactory = (message) =>
                {
                    if (message is HttpClientHandler clientHandler)
                        // bypass TLS certificate
                        clientHandler.ServerCertificateCustomValidationCallback += 
                            (sender, certificate, chain, sslPolicyErrors) => true;
                    return message;
                })             
                .Build();

            for (var i = 0; i < numOfAttempts; i++)
            {
                try
                {
                    await Connection.StartAsync();
                    return this;
                }
                catch (Exception e)
                {
                    if (i < numOfAttempts - 1)
                        await Task.Delay(numOfAttempts);
                    else
                    {
                        var errMessage = $"Hub connection on '{Url}' had failed. ";
                        _logger?.LogError(errMessage, e);
                        throw new Exception(errMessage, e);
                    }
                }
            }

            return null;
        }

        public async void Subscribe<T>(Action<T> callback)
        {
            if (Connection == null || _cts.Token.IsCancellationRequested || callback == null)
                return;

            try
            {
                var channel = await Connection.StreamAsChannelAsync<T>("StartStreaming", _cts.Token);
                while (await channel.WaitToReadAsync())
                    while (channel.TryRead(out var t))
                    {
                        try
                        {
                            callback(t);
                        }
                        catch (Exception e)
                        {
                            var errMessage = $"Hub '{Url}' Subscribe(): callback had failed. ";
                            _logger.LogError(errMessage, e);
                            throw new Exception(errMessage, e);
                        }
                    }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation($"Hub '{Url}': cancellation");
            }
        }

        #endregion // StartConnection, Subscribe

        #region Rpc, Invoke 

        public Task<object> RpcAsync(string interfaceName, string methodName, params object[] args) =>
            RpcAsync(false, interfaceName, methodName, args);

        public async void RpcOneWay(string interfaceName, string methodName, params object[] args) =>
            await RpcAsync(true, interfaceName, methodName, args);

        private async Task<object> RpcAsync(bool isOneWay, string interfaceName, string methodName, object[] args)
        {
            if (!IsReady)
                return null;

            var rpcArgs = new RpcDtoRequest
            {
                ClientId = ClientId,
                Id = $"{Guid.NewGuid()}",
                Status = DtoStatus.Created,
                InterfaceName = interfaceName,
                MethodName = methodName,
                Args = args?.Select(a => new DtoData { TypeName = a.GetType().FullName, Data = a })?.ToArray()
            };

            object obResult = null;
            Exception ex = null;
            var sw = new Stopwatch();
            try
            {
                _beforeCall?.Invoke(_logger, isOneWay, rpcArgs);

                sw.Start();
                var result = await Connection.InvokeAsync<object>(isOneWay ? "RpcOneWay" : "Rpc", rpcArgs, _cts.Token);
                obResult = isOneWay ? null : GetResult((JObject)result);
            }
            catch (Exception e)
            {
                ex = e;
                var errMessage = $"Hub '{Url}' InvokeAsync() of method '{methodName}()' had failed. ";
                _logger?.LogError(errMessage, e);
                throw new Exception(errMessage, e);
            }
            finally 
            {
                sw.Stop();
                _afterCall?.Invoke(_logger, isOneWay, rpcArgs, obResult, sw.Elapsed, ex);
            }

            return obResult;
        }

        public async Task<object> InvokeAsync(string methodName, params object[] args)
        {
            if (!IsReady)
                return null;

            try
            {
                return await Connection.InvokeAsync<object>(methodName, args, _cts.Token);
            }
            catch (Exception e)
            {
                var errMessage = $"Hub '{Url}' InvokeAsync() of method '{methodName}()' had failed. ";
                _logger?.LogError(errMessage, e);
                throw new Exception(errMessage, e);
            }
        }

        private bool IsReady => Connection != null && !_cts.Token.IsCancellationRequested;

        #endregion // Rpc, Invoke

        #region Cancel, Dispose

        public async Task Cancel() 
        {
            _logger?.LogInformation($"Hub '{Url}': 'Cancel()' is called");
            var count = await RpcAsync("_", "KillClientSessionsIfExist", ClientId);
            _cts.Cancel();
        }

        public void Dispose() 
        {
            _logger?.LogInformation($"Hub '{Url}': 'Dispose()' is called");

            if (!_cts.IsCancellationRequested)
                Cancel().Wait();

            Connection.DisposeAsync().Wait();
        }

        #endregion // Cancel, Dispose
    }
}
