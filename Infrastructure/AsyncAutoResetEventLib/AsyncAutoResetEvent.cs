// https://blogs.msdn.microsoft.com/pfxteam/2012/02/11/building-async-coordination-primitives-part-2-asyncautoresetevent/

using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsyncAutoResetEventLib
{
    public class AsyncAutoResetEvent
    {
        private readonly Queue<TaskCompletionSource<bool>> _waits = new();
        private bool _signaled;

        public Task WaitAsync()
        {
            lock (_waits)
            {
                if (_signaled)
                {
                    _signaled = false;
                    return Task.FromResult(true);
                }
                else
                {
                    TaskCompletionSource<bool> tcs = new();
                    _waits.Enqueue(tcs);
                    return tcs.Task;
                }
            }
        }

        public void Set()
        {
            TaskCompletionSource<bool> toRelease = null;
            lock (_waits)
            {
                if (_waits.Count > 0)
                    toRelease = _waits.Dequeue();
                else
                    if (!_signaled)
                        _signaled = true;
            }

            if (toRelease != null)
                toRelease.SetResult(true);
        }
    }
}
