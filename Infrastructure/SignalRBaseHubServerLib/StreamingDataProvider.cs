using System.Collections.Generic;

namespace SignalRBaseHubServerLib
{
    public class StreamingDataProvider<T> : IStreamingDataProvider<T>
    {
        private List<ISetEvent> _lstSetEvent = new();

        private T _dto;

        public T Current
        {
            get
            {
                lock (this)
                    return _dto;
            }
            set
            {
                lock (this)
                {
                    _dto = value;
                    foreach (var se in _lstSetEvent.ToArray())
                    {
                        if (se.IsValid)
                            se.SetEvent();
                        else
                            _lstSetEvent.Remove(se);
                    }
                }
            }
        }

        public void Add(ISetEvent se)
        {
            lock (this)
            {
                if (!_lstSetEvent.Contains(se))
                    _lstSetEvent.Add(se);
            }
        }
    }
}

