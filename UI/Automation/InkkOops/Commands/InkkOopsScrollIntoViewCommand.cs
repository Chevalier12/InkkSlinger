using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace InkkSlinger;

public sealed class InkkOopsScrollIntoViewCommand : IInkkOopsCommand
{
    public InkkOopsScrollIntoViewCommand(
        InkkOopsTargetReference ownerTarget,
        InkkOopsTargetReference itemTarget,
        float padding = 8f)
        : this(ownerTarget, InkkOopsScrollLocator.ForElement(itemTarget.Selector), padding)
    {
    }

    public InkkOopsScrollIntoViewCommand(
        InkkOopsTargetReference ownerTarget,
        InkkOopsScrollLocator locator,
        float padding = 8f)
    {
        OwnerTarget = ownerTarget ?? throw new ArgumentNullException(nameof(ownerTarget));
        Locator = locator ?? throw new ArgumentNullException(nameof(locator));
        Padding = MathF.Max(0f, padding);
    }

    public InkkOopsTargetReference OwnerTarget { get; }

    public InkkOopsScrollLocator Locator { get; }

    public float Padding { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Semantic;

    public string Describe()
    {
        return $"ScrollIntoView({OwnerTarget}, locator: {Locator}, padding: {Padding:0.###})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await session.ExecuteOnUiThreadAsync(() => ScrollCore(session), cancellationToken).ConfigureAwait(false);
        await session.WaitFramesAsync(4, cancellationToken).ConfigureAwait(false);
        await session.ExecuteOnUiThreadAsync(() => VerifyCore(session), cancellationToken).ConfigureAwait(false);
    }

    private void ScrollCore(InkkOopsSession session)
    {
        var ownerElement = session.ResolveRequiredTarget(OwnerTarget);
        switch (ownerElement)
        {
            case ListBox listBox when TryResolveListBoxItem(listBox, out var listBoxItem):
                listBox.ScrollIntoView(listBoxItem);
                return;
            case DataGrid dataGrid when TryResolveDataGridItem(dataGrid, out var dataGridItem):
                dataGrid.ScrollIntoView(dataGridItem);
                return;
            case ScrollViewer scrollViewer when Locator.Kind == InkkOopsScrollLocatorKind.ElementSelector:
                ScrollRealizedElementIntoView(session, scrollViewer, new InkkOopsTargetReference(Locator.ElementSelector!), Padding);
                return;
            case ScrollViewer:
                throw new InkkOopsCommandException(
                    InkkOopsFailureCategory.Unrealized,
                    $"Locator '{Locator}' requires an items control owner, not a plain ScrollViewer.");
            default:
                throw new InkkOopsCommandException(
                    InkkOopsFailureCategory.SemanticProviderMissing,
                    $"Target '{OwnerTarget.Name}' resolved to '{ownerElement.GetType().Name}', which does not support ScrollIntoView.");
        }
    }

    private void VerifyCore(InkkOopsSession session)
    {
        var ownerElement = session.ResolveRequiredTarget(OwnerTarget);
        switch (ownerElement)
        {
            case ListBox listBox:
                if (!TryGetNestedScrollViewer(listBox, out var listScrollViewer))
                {
                    throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "ListBox viewport is unavailable after ScrollIntoView.");
                }

                if (TryFindListBoxContainer(listBox, out var listContainer))
                {
                    if (!listContainer.TryGetRenderBoundsInRootSpace(out var listBounds))
                    {
                        throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "ListBox item does not expose render bounds after ScrollIntoView.");
                    }

                    if (TryIsVisibleInViewport(listScrollViewer, listBounds))
                    {
                        return;
                    }
                }

                if (!TryGetListBoxTargetIndex(listBox, out var listIndex))
                {
                    throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "ListBox item was not realized after ScrollIntoView.");
                }

                if (TryEstimateListBoxIndexInViewport(listBox, listScrollViewer, listIndex))
                {
                    return;
                }

                EnsureOwnerScrollChanged(listScrollViewer.VerticalOffset, "ListBox item");
                return;
            case DataGrid dataGrid:
                if (!TryFindDataGridRow(dataGrid, out var row))
                {
                    EnsureOwnerScrollChanged(dataGrid.ScrollViewerForTesting.VerticalOffset, "DataGrid row");
                    return;
                }

                if (!row.TryGetRenderBoundsInRootSpace(out var rowBounds))
                {
                    EnsureOwnerScrollChanged(dataGrid.ScrollViewerForTesting.VerticalOffset, "DataGrid row");
                    return;
                }

                if (!TryIsVisibleInViewport(dataGrid.ScrollViewerForTesting, rowBounds))
                {
                    EnsureOwnerScrollChanged(dataGrid.ScrollViewerForTesting.VerticalOffset, "DataGrid row");
                }

                return;
            case ScrollViewer scrollViewer when Locator.Kind == InkkOopsScrollLocatorKind.ElementSelector:
                var itemElement = session.ResolveRequiredTarget(new InkkOopsTargetReference(Locator.ElementSelector!));
                if (!itemElement.TryGetRenderBoundsInRootSpace(out var itemBounds))
                {
                    throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "Target element does not expose render bounds after ScrollIntoView.");
                }

                VerifyBoundsInViewport(scrollViewer, itemBounds, "target element");
                return;
            default:
                return;
        }
    }

    private static void ScrollRealizedElementIntoView(InkkOopsSession session, ScrollViewer scrollViewer, InkkOopsTargetReference itemTarget, float padding)
    {
        var itemElement = session.ResolveRequiredTarget(itemTarget);
        if (!itemElement.TryGetRenderBoundsInRootSpace(out var itemBounds))
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, $"Target '{itemTarget.Name}' does not expose render bounds.");
        }

        if (!scrollViewer.TryGetContentViewportClipRect(out var viewportBounds))
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, $"ScrollViewer '{scrollViewer.GetType().Name}' does not have an active viewport.");
        }

        var nextVerticalOffset = scrollViewer.VerticalOffset;
        var targetTop = itemBounds.Y - padding;
        var targetBottom = itemBounds.Y + itemBounds.Height + padding;
        var viewportTop = viewportBounds.Y;
        var viewportBottom = viewportBounds.Y + viewportBounds.Height;

        if (targetTop < viewportTop)
        {
            nextVerticalOffset -= viewportTop - targetTop;
        }
        else if (targetBottom > viewportBottom)
        {
            nextVerticalOffset += targetBottom - viewportBottom;
        }

        scrollViewer.ScrollToVerticalOffset(nextVerticalOffset);
    }

    private bool TryResolveListBoxItem(ListBox listBox, out object? item)
    {
        item = null;
        return TryResolveItemsControlItem(listBox.Items, out item);
    }

    private bool TryResolveDataGridItem(DataGrid dataGrid, out object? item)
    {
        item = null;
        return TryResolveItemsControlItem(dataGrid.Items, out item);
    }

    private bool TryResolveItemsControlItem(System.Collections.Generic.IList<object> items, out object? item)
    {
        item = null;

        if (Locator.Kind == InkkOopsScrollLocatorKind.ItemIndex)
        {
            var index = Locator.ItemIndex.GetValueOrDefault(-1);
            if (index < 0)
            {
                return false;
            }

            if (index < items.Count)
            {
                item = items[index];
                return true;
            }

            return false;
        }

        if (Locator.Kind == InkkOopsScrollLocatorKind.ItemText)
        {
            var text = Locator.ItemText;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(Convert.ToString(items[i], CultureInfo.InvariantCulture), text, StringComparison.Ordinal))
                {
                    item = items[i];
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    private bool TryFindListBoxContainer(ListBox listBox, out ListBoxItem container)
    {
        container = null!;
        object? expectedItem = null;
        if (Locator.Kind == InkkOopsScrollLocatorKind.ItemIndex)
        {
            var index = Locator.ItemIndex.GetValueOrDefault(-1);
            if (index >= 0 && index < listBox.Items.Count)
            {
                expectedItem = listBox.Items[index];
            }
        }

        foreach (var element in EnumerateVisuals(listBox))
        {
            if (element is not ListBoxItem listBoxItem)
            {
                continue;
            }

            if (Locator.Kind == InkkOopsScrollLocatorKind.ItemIndex)
            {
                if (expectedItem != null && MatchesListBoxItem(listBoxItem, expectedItem))
                {
                    container = listBoxItem;
                    return true;
                }
            }
            else if (Locator.Kind == InkkOopsScrollLocatorKind.ItemText &&
                     MatchesListBoxItem(listBoxItem, Locator.ItemText))
            {
                container = listBoxItem;
                return true;
            }
        }

        return false;
    }

    private bool TryFindDataGridRow(DataGrid dataGrid, out DataGridRow row)
    {
        row = null!;
        var rows = dataGrid.RowsForTesting;
        if (Locator.Kind == InkkOopsScrollLocatorKind.ItemIndex)
        {
            var index = Locator.ItemIndex.GetValueOrDefault(-1);
            if (index >= 0 && index < rows.Count)
            {
                row = rows[index];
                return true;
            }

            return false;
        }

        if (Locator.Kind == InkkOopsScrollLocatorKind.ItemText)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (string.Equals(Convert.ToString(rows[i].Item, CultureInfo.InvariantCulture), Locator.ItemText, StringComparison.Ordinal))
                {
                    row = rows[i];
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetListBoxTargetIndex(ListBox listBox, out int index)
    {
        index = -1;
        if (Locator.Kind == InkkOopsScrollLocatorKind.ItemIndex)
        {
            index = Locator.ItemIndex.GetValueOrDefault(-1);
            return index >= 0 && index < listBox.Items.Count;
        }

        if (Locator.Kind == InkkOopsScrollLocatorKind.ItemText)
        {
            for (var i = 0; i < listBox.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(listBox.Items[i], CultureInfo.InvariantCulture), Locator.ItemText, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
        }

        return false;
    }

    private static void VerifyBoundsInViewport(ScrollViewer scrollViewer, LayoutRect bounds, string subject)
    {
        if (!TryIsVisibleInViewport(scrollViewer, bounds))
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Offscreen,
                $"{subject} is still not visible after ScrollIntoView.");
        }
    }

    private static System.Collections.Generic.IEnumerable<UIElement> EnumerateVisuals(UIElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
        {
            foreach (var descendant in EnumerateVisuals(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool TryGetNestedScrollViewer(UIElement root, out ScrollViewer scrollViewer)
    {
        foreach (var element in EnumerateVisuals(root))
        {
            if (element is ScrollViewer nested)
            {
                scrollViewer = nested;
                return true;
            }
        }

        scrollViewer = null!;
        return false;
    }

    private static bool TryEstimateListBoxIndexInViewport(ListBox listBox, ScrollViewer scrollViewer, int index)
    {
        var sampleHeight = 0f;
        foreach (var element in EnumerateVisuals(listBox))
        {
            if (element is ListBoxItem item && item.LayoutSlot.Height > 0f)
            {
                sampleHeight = item.LayoutSlot.Height;
                break;
            }
        }

        if (sampleHeight <= 0f)
        {
            return false;
        }

        var itemTop = index * sampleHeight;
        var itemBottom = itemTop + sampleHeight;
        var viewportTop = scrollViewer.VerticalOffset;
        var viewportBottom = viewportTop + scrollViewer.ViewportHeight;
        return itemBottom >= viewportTop - 0.5f && itemTop <= viewportBottom + 0.5f;
    }

    private static bool MatchesListBoxItem(ListBoxItem item, object expected)
    {
        if (Equals(item.Content, expected))
        {
            return true;
        }

        if (item.Content is Label label && Equals(label.Content, expected))
        {
            return true;
        }

        return string.Equals(
            Convert.ToString(item.Content, CultureInfo.InvariantCulture),
            Convert.ToString(expected, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool TryIsVisibleInViewport(ScrollViewer scrollViewer, LayoutRect bounds)
    {
        if (!scrollViewer.TryGetContentViewportClipRect(out var viewportBounds))
        {
            return false;
        }

        return bounds.Y + bounds.Height >= viewportBounds.Y - 0.5f &&
               bounds.Y <= viewportBounds.Y + viewportBounds.Height + 0.5f;
    }

    private static void EnsureOwnerScrollChanged(float verticalOffset, string subject)
    {
        if (verticalOffset <= 0f)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Offscreen,
                $"{subject} did not become visible and the owner viewport did not scroll.");
        }
    }
}
