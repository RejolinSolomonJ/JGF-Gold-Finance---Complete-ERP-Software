using System.Windows.Input;
using Serilog;

namespace JGF.Desktop.Common;

/// <summary>
/// A reusable, thread-safe asynchronous ICommand implementation that prevents concurrent execution
/// and safely handles exceptions without crashing the UI thread.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, CancellationToken, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<Exception>? _onException;
    private bool _isExecuting;
    private CancellationTokenSource? _cts;

    public AsyncRelayCommand(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute = null,
        Action<Exception>? onException = null)
        : this((_, ct) => execute(ct), canExecute != null ? _ => canExecute() : null, onException)
    {
    }

    public AsyncRelayCommand(
        Func<object?, CancellationToken, Task> execute,
        Func<object?, bool>? canExecute = null,
        Action<Exception>? onException = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onException = onException;
    }

    public bool IsExecuting => _isExecuting;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute == null || _canExecute(parameter));
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        _cts = new CancellationTokenSource();

        try
        {
            await _execute(parameter, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Graceful cancellation, do not throw
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing AsyncRelayCommand: {Message}", ex.Message);
            if (_onException != null)
            {
                _onException(ex);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"Command failed: {ex.Message}",
                    "Operation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        finally
        {
            _isExecuting = false;
            _cts?.Dispose();
            _cts = null;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }
}
