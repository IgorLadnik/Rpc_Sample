using Microsoft.Extensions.Logging;

namespace SignalRBaseHubServerLib
{
    public interface IStreamingDataProvider<T>
    {
        T Current { get; }
    }

    public interface ISetEvent
    {
        void SetEvent();
        bool IsValid { get; }
    }

    public interface IDirectCall
    {
        object DirectCall(string methodName, params object[] args);
    }

    public interface ILog
    {
        ILoggerFactory LoggerFactory { set; }
    }
}
