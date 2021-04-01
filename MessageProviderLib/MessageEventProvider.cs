using System;
using System.Threading;
using RemoteInterfaces;
using SignalRBaseHubServerLib;

namespace MessageProviderLib
{
    public class MessageEventProvider : StreamingDataProvider<Message>
    {
        private const int rndLowLimit = 0;
        private const int rndUpperLimit = 999;
        private const int intervalInMs = 3000;

        private Timer _timer;
        private Random _random = new(11);

        private static MessageEventProvider _helper;
        public static MessageEventProvider Instance => _helper ??= new();

        private MessageEventProvider() =>
            _timer = new Timer(_ => Current = new() { ClientId = $"{Guid.NewGuid()}", Data = _random.Next(rndLowLimit, rndUpperLimit) }, null, 0, intervalInMs);
    }
}
