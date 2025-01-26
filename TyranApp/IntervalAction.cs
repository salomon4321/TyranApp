namespace TyranApp
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class IntervalAction : IDisposable
    {
        private readonly Func<Task> _action;  // Action to execute periodically
        private readonly int _intervalMs;    // Interval in milliseconds
        private CancellationTokenSource _cts; // Token to cancel the loop
        private Task _runningTask;           // Task running the loop

        public event EventHandler<LogEventArgs> Log;

        public IntervalAction(Func<Task> action, int intervalMs)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _intervalMs = intervalMs;
        }

        // Starts or resumes the interval execution
        public void Start()
        {
            if (_runningTask != null)
            {
                throw new InvalidOperationException("IntervalAction is already running.");
            }

            {
                _cts = new CancellationTokenSource();
                _runningTask = Task.Run(() => RunAsync(_cts.Token));
            }
        }

        // Pauses the interval execution
        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _runningTask?.Wait(); // Wait for the task to finish gracefully
                _runningTask = null;
            }
        }

        // Main loop to execute the action
        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _action(); // Execute the action
                }
                catch (Exception ex)
                {
                    AddLog($"Error in interval action: {ex.Message}");
                }

                // Wait for the interval or cancel if requested
                try
                {
                    await Task.Delay(_intervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    // Break the loop if canceled
                    break;
                }
            }
        }

        // Dispose resources
        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }

        private void AddLog(string message)
        {
#if RELEASE
                return;
#endif
            Log.Invoke(this, new LogEventArgs("IntervalAction message: " + message));
        }
    }

}
