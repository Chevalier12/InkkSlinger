namespace InkkSlinger;

public class ListView : ListBox
{
    public static readonly DependencyProperty ViewProperty =
        DependencyProperty.Register(
            nameof(View),
            typeof(object),
            typeof(ListView),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public object? View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is ListViewItem;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
        var container = new ListViewItem();

        if (item is UIElement element)
        {
            container.Content = element;
            return container;
        }

        container.Content = new Label
        {
            Text = item?.ToString() ?? string.Empty
        };

        return container;
    }

    protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);

        if (element is ListViewItem listViewItem)
        {
            listViewItem.IsSelected = IsSelectedIndex(SelectedIndices, index);
        }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs args)
    {
        base.OnSelectionChanged(args);

        var selectedIndices = SelectedIndices;
        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is ListViewItem listViewItem)
            {
                listViewItem.IsSelected = IsSelectedIndex(selectedIndices, i);
            }
        }
    }

    private static bool IsSelectedIndex(System.Collections.Generic.IReadOnlyList<int> selectedIndices, int index)
    {
        for (var i = 0; i < selectedIndices.Count; i++)
        {
            if (selectedIndices[i] == index)
            {
                return true;
            }
        }

        return false;
    }
}
