using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace InkkSlinger;

public sealed class CollectionViewSource : INotifyPropertyChanged
{
    private object? _source;
    private Predicate<object?>? _filter;
    private ICollectionView? _view;

    public CollectionViewSource()
    {
        SortDescriptions.CollectionChanged += OnDescriptorCollectionChanged;
        GroupDescriptions.CollectionChanged += OnDescriptorCollectionChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }

            _source = value;
            RebuildView();
            OnPropertyChanged(nameof(Source));
        }
    }

    public Predicate<object?>? Filter
    {
        get => _filter;
        set
        {
            if (ReferenceEquals(_filter, value))
            {
                return;
            }

            _filter = value;
            if (_view != null)
            {
                _view.Filter = value;
                _view.Refresh();
            }

            OnPropertyChanged(nameof(Filter));
        }
    }

    public ObservableCollection<SortDescription> SortDescriptions { get; } = [];

    public ObservableCollection<GroupDescription> GroupDescriptions { get; } = [];

    public ICollectionView? View
    {
        get => _view;
        private set
        {
            if (ReferenceEquals(_view, value))
            {
                return;
            }

            _view = value;
            OnPropertyChanged(nameof(View));
        }
    }

    public void Refresh()
    {
        _view?.Refresh();
    }

    private void RebuildView()
    {
        var view = CollectionViewFactory.GetDefaultView(_source);
        if (view == null)
        {
            View = null;
            return;
        }

        view.SortDescriptions.Clear();
        for (var i = 0; i < SortDescriptions.Count; i++)
        {
            view.SortDescriptions.Add(SortDescriptions[i]);
        }

        view.GroupDescriptions.Clear();
        for (var i = 0; i < GroupDescriptions.Count; i++)
        {
            view.GroupDescriptions.Add(GroupDescriptions[i]);
        }

        view.Filter = _filter;
        view.Refresh();
        View = view;
    }

    private void OnDescriptorCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_view == null)
        {
            return;
        }

        RebuildView();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
