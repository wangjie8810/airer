using System.Diagnostics;
using Avalonia.Threading;

namespace Airer.Views;

static class Motion
{
    public static Task Tween(Action<double> apply, double from, double to, int ms, bool easeOut, CancellationToken token)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (token.IsCancellationRequested)
        {
            done.TrySetCanceled(token);
            return done.Task;
        }

        if (ms <= 0 || Math.Abs(to - from) < 0.01)
        {
            apply(to);
            done.TrySetResult();
            return done.Task;
        }

        var clock = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            if (token.IsCancellationRequested)
            {
                timer.Stop();
                done.TrySetCanceled(token);
                return;
            }

            var t = Math.Clamp(clock.Elapsed.TotalMilliseconds / ms, 0, 1);
            var eased = easeOut ? 1 - Math.Pow(1 - t, 3) : Math.Pow(t, 3);
            apply(from + (to - from) * eased);
            if (t < 1)
                return;
            timer.Stop();
            done.TrySetResult();
        };
        timer.Start();
        return done.Task;
    }
}
