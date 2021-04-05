using Microsoft.Extensions.Logging;
using RemoteInterfaces;
using SignalRBaseHubServerLib;

namespace RemoteImplementations
{
    public class RemoteCall2 : IRemoteCall2, IDirectCall, ILog
    {
        private static int objectsCount = 0;

        private int _id;
        private ILogger _logger;

        public ILoggerFactory LoggerFactory { set => _logger = value?.CreateLogger<RemoteCall2>(); }

        public RemoteCall2()
        {
            _id = ++objectsCount;
            _logger?.LogDebug($"*** RemoteCall2.Ctor() -> {_id}");
        }

        #region IRemoteCall2 implementation

        public int Foo(string name, Arg1[] arg1s)
        {
            _logger?.LogDebug($"*** RemoteCall2.Foo() -> {_id}");
            return _id;
        }

        public string Echo(string text)
        {
            var echo = $"Echo2: {text}";
            _logger?.LogDebug($"*** Echo -> {echo}");
            return echo;
        }

        #endregion // IRemoteCall2 implementation

        public object DirectCall(string methodName, params object[] args)
        {
            switch (methodName)
            {
                case "Foo":
                    return Foo((string)args[0], (Arg1[])args[1]);

                case "Echo":
                    return Echo((string)args[0]);
            }

            return null;
        }
    }
}
