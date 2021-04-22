using System;
using Microsoft.Extensions.Logging;
using RemoteInterfaces;
using SignalRBaseHubServerLib;

namespace RemoteImplementations
{
    public class RemoteCall3 : IRemoteCall3, ILog
    {
        private ILogger _logger;
        public ILoggerFactory LoggerFactory { set => _logger = value?.CreateLogger<RemoteCall2>(); }

        private Guid _id;
        private int _n;

        public RemoteCall3(int n)
        {
            _n = n;
            _id = Guid.NewGuid();
        }

        public Ret3 GetIdAndParam() => new Ret3 { Id = _id, Param = _n };
    }
}
