using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace InkkSlinger;

public interface ICollectionView : IEnumerable, INotifyCollectionChanged, INotifyPropertyChanged
{
    IEnumerable SourceCollection { get; }

    Predicate<object?>? Filter { get; set; }

    ObservableCollection<SortDescription> SortDescriptions { get; }

    ObservableCollection<GroupDescription> GroupDescriptions { get; }

    ReadOnlyCollection<CollectionViewGroup> Groups { get; }

    object? CurrentItem { get; }

    int CurrentPosition { get; }

    bool IsCurrentBeforeFirst { get; }

    bool IsCurrentAfterLast { get; }

    event EventHandler? CurrentChanged;

    bool MoveCurrentTo(object? item);

    bool MoveCurrentToFirst();

    bool MoveCurrentToLast();

    bool MoveCurrentToNext();

    bool MoveCurrentToPrevious();

    bool MoveCurrentToPosition(int position);

    void Refresh();
}
