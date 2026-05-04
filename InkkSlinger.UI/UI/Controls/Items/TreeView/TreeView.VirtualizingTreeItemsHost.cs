using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public partial class TreeView
{
    private sealed class VirtualizingTreeItemsHost : VirtualizingStackPanel
    {
        public override IEnumerable<UIElement> GetVisualChildren()
        {
            if (!IsVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
            {
                foreach (var child in Children)
                {
                    yield return child;
                }

                yield break;
            }

            var first = Math.Max(0, FirstRealizedIndex);
            var last = Math.Min(Children.Count - 1, LastRealizedIndex);
            for (var index = first; index <= last; index++)
            {
                yield return Children[index];
            }
        }

        internal override int GetVisualChildCountForTraversal()
        {
            if (!IsVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
            {
                return Children.Count;
            }

            var first = Math.Max(0, FirstRealizedIndex);
            var last = Math.Min(Children.Count - 1, LastRealizedIndex);
            return Math.Max(0, last - first + 1);
        }

        internal override UIElement GetVisualChildAtForTraversal(int index)
        {
            if (!IsVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
            {
                if ((uint)index < (uint)Children.Count)
                {
                    return Children[index];
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var first = Math.Max(0, FirstRealizedIndex);
            var last = Math.Min(Children.Count - 1, LastRealizedIndex);
            var count = Math.Max(0, last - first + 1);
            if ((uint)index >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return Children[first + index];
        }

        public void SetVisibleItems(IReadOnlyList<VisibleTreeItemEntry> visibleItems)
        {
            var visibleSet = new HashSet<TreeViewItem>(visibleItems.Select(static entry => entry.Item));
            var changed = false;
            using (DeferChildMutationInvalidations())
            {
                for (var index = Children.Count - 1; index >= 0; index--)
                {
                    var child = Children[index];
                    if (child is TreeViewItem treeItem && visibleSet.Contains(treeItem))
                    {
                        continue;
                    }

                    changed |= RemoveChildAt(index);
                }

                if (Children.Count == 0)
                {
                    for (var index = 0; index < visibleItems.Count; index++)
                    {
                        var item = visibleItems[index].Item;
                        DetachFromCurrentParent(item);
                        InsertChild(index, item);
                        changed = true;
                    }
                }
                else
                {
                    for (var index = 0; index < visibleItems.Count; index++)
                    {
                        var item = visibleItems[index].Item;
                        var currentIndex = IndexOfChild(item);
                        if (currentIndex == index)
                        {
                            continue;
                        }

                        if (currentIndex >= 0)
                        {
                            changed |= MoveChildRange(currentIndex, 1, index);
                            continue;
                        }

                        DetachFromCurrentParent(item);
                        InsertChild(index, item);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                InvalidateMeasure();
                InvalidateArrange();
            }
        }

        private int IndexOfChild(UIElement child)
        {
            for (var index = 0; index < Children.Count; index++)
            {
                if (ReferenceEquals(Children[index], child))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void DetachFromCurrentParent(UIElement child)
        {
            if (child.VisualParent is Panel visualPanel)
            {
                visualPanel.RemoveChild(child);
            }
            else
            {
                child.SetVisualParent(null);
            }

            if (child.LogicalParent is Panel logicalPanel)
            {
                logicalPanel.RemoveChild(child);
            }
            else
            {
                child.SetLogicalParent(null);
            }
        }
    }
}
