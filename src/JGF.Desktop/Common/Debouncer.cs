namespace JGF.Desktop.Common;

/// <summary>
/// Debounces rapid UI input events (such as typing into a search box) by a configurable delay (default 300ms).
/// Automatically cancels previous pending tasks when a new input arrives.
/// </summary>
public class Debouncer
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;

    public Debouncer(int milliseconds = 300)
    {
        _delay = TimeSpan.FromMilliseconds(milliseconds);
    }

    public async Task RunAsync(Func<CancellationToken, Task> action)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;

        try
        {
            await Task.Delay(_delay, token);
            if (!token.IsCancellationRequested)
            {
                await action(token);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignored - superseded by subsequent keystroke
        }
    }
}
