using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tdx.Core.Utils
{
    public class HeartbeatService : IDisposable
    {
        private readonly Func<Task> _heartbeatAction;
        private readonly TimeSpan _heartbeatInterval;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private long _lastAckTimeTicks;
        private Task? _heartbeatTask;

        public HeartbeatService(Func<Task> heartbeatAction, TimeSpan? heartbeatInterval = null)
        {
            _heartbeatAction = heartbeatAction;
            _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(15);
            _cancellationTokenSource = new CancellationTokenSource();
            _lastAckTimeTicks = DateTime.UtcNow.Ticks;
        }

        public void Start()
        {
            if (_heartbeatTask != null)
            {
                throw new InvalidOperationException("Heartbeat service is already running.");
            }
            _heartbeatTask = RunAsync(_cancellationTokenSource.Token);
        }

        public void UpdateLastAckTime()
        {
            Interlocked.Exchange(ref _lastAckTimeTicks, DateTime.UtcNow.Ticks);
        }

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_heartbeatInterval, token);
                }
                catch (TaskCanceledException)
                {
                    break; // Exit loop if cancellation is requested
                }

                var lastAckTime = new DateTime(Interlocked.Read(ref _lastAckTimeTicks), DateTimeKind.Utc);
                if (DateTime.UtcNow - lastAckTime > _heartbeatInterval)
                {
                    try
                    {
                        await _heartbeatAction();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Heartbeat action failed.", ex);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
            _cancellationTokenSource.Dispose();
            // It is generally not recommended to dispose a Task.
            // The task will complete and be garbage collected once it observes the cancellation.
        }
    }
}
