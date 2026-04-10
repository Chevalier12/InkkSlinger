using System;
using System.Collections.ObjectModel;

namespace InkkSlinger;

public sealed class SubspaceViewport2D
{
    private UIElement? _content;

    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; } = float.NaN;

    public float Height { get; set; } = float.NaN;

    public UIElement? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value))
            {
                return;
            }

            var previous = _content;
            _content = value;
            Host?.OnSubspaceViewport2DContentChanged(this, previous, value);
        }
    }

    internal RenderSurface? Host { get; set; }
}

internal sealed class SubspaceViewport2DCollection : Collection<SubspaceViewport2D>
{
    private readonly RenderSurface _owner;

    public SubspaceViewport2DCollection(RenderSurface owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    protected override void InsertItem(int index, SubspaceViewport2D item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.AttachSubspaceViewport2D(item);
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, SubspaceViewport2D item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var previous = this[index];
        if (ReferenceEquals(previous, item))
        {
            return;
        }

        _owner.DetachSubspaceViewport2D(previous);
        _owner.AttachSubspaceViewport2D(item);
        base.SetItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        var previous = this[index];
        _owner.DetachSubspaceViewport2D(previous);
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        for (var index = 0; index < Count; index++)
        {
            _owner.DetachSubspaceViewport2D(this[index]);
        }

        base.ClearItems();
    }
}