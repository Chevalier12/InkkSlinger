using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace InkkSlinger;

public sealed class CollectionViewParityDemoViewModel : INotifyPropertyChanged
{
    private string _status = "Ready.";
    private string _currentItemText = "Current: none";

    public CollectionViewParityDemoViewModel()
    {
        SourceItems = [];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CollectionDemoItem> SourceItems { get; }

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

    public string CurrentItemText
    {
        get => _currentItemText;
        set
        {
            if (string.Equals(_currentItemText, value, StringComparison.Ordinal))
            {
                return;
            }

            _currentItemText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentItemText)));
        }
    }
}

public sealed class CollectionDemoItem
{
    public CollectionDemoItem(int id, string name, string category, int priority)
    {
        Id = id;
        Name = name;
        Category = category;
        Priority = priority;
    }

    public int Id { get; set; }

    public string Name { get; set; }

    public string Category { get; set; }

    public int Priority { get; set; }

    public override string ToString()
    {
        return $"#{Id} {Name} [{Category}] P{Priority}";
    }
}
