using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JGF.Desktop.Common;

public abstract class ValidatableViewModelBase : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => _errors.Any(kv => kv.Value.Count > 0);

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return _errors.Values.SelectMany(e => e).ToList();

        if (_errors.TryGetValue(propertyName, out var errors))
            return errors;

        return Enumerable.Empty<string>();
    }

    public string? GetFirstError(string propertyName)
    {
        if (_errors.TryGetValue(propertyName, out var errors) && errors.Count > 0)
            return errors[0];
        return null;
    }

    protected void SetErrors(string propertyName, IEnumerable<string> errors)
    {
        var errorList = errors.ToList();
        if (errorList.Count == 0)
        {
            ClearErrors(propertyName);
        }
        else
        {
            _errors[propertyName] = errorList;
            OnErrorsChanged(propertyName);
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    protected void ClearErrors([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == null) return;

        if (_errors.Remove(propertyName))
        {
            OnErrorsChanged(propertyName);
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    protected void ClearAllErrors()
    {
        var keys = _errors.Keys.ToList();
        _errors.Clear();
        foreach (var key in keys)
        {
            OnErrorsChanged(key);
        }
        OnPropertyChanged(nameof(HasErrors));
    }

    protected virtual void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
}

