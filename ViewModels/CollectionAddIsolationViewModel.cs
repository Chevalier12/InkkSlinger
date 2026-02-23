using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace InkkSlinger;

public sealed class CollectionAddIsolationViewModel : INotifyPropertyChanged
{
    private string _status = "Ready";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> SharedItems { get; } = [];

    public string Status
    {
        get => _status;
        set
        {
            if (string.Equals(_status, value, StringComparison.Ordinal))
            {
                return;
            }

            _status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }
}
