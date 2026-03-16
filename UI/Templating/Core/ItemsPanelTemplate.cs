using System;

namespace InkkSlinger;

public sealed class ItemsPanelTemplate
{
    public ItemsPanelTemplate(Func<Panel> factory)
        : this(_ => factory())
    {
    }

    public ItemsPanelTemplate(Func<ItemsControl, Panel> factory)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public Func<ItemsControl, Panel> Factory { get; }

    public Panel Build(ItemsControl owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return Factory(owner);
    }
}