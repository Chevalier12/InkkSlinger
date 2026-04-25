using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class RichTextBox
{
    public bool TryConsumeRenderDirtyBoundsHint(out LayoutRect bounds)
    {
        if (!_hasPendingRenderDirtyBoundsHint)
        {
            bounds = default;
            return false;
        }

        bounds = _pendingRenderDirtyBoundsHint;
        _hasPendingRenderDirtyBoundsHint = false;
        return true;
    }

    public override void InvalidateVisual()
    {
        _pendingInvalidationReason = "Unspecified";
        if (!_preserveRenderDirtyBoundsHint)
        {
            _hasPendingRenderDirtyBoundsHint = false;
        }

        base.InvalidateVisual();
    }

    private void InvalidateVisualWithReason(string reason)
    {
        _pendingInvalidationReason = reason;
        InvalidateVisual();
    }

    private void InvalidateAfterTextMutation(string reason)
    {
        InvalidateMeasure();
        _scrollContentPresenter.InvalidateMeasure();
        _contentHost?.InvalidateScrollInfo();

        // Hosted document children only need a local placement refresh when the editor slot is stable.
        if (ContainsHostedDocumentChildren(Document) &&
            !TryRefreshHostedDocumentChildLayoutAfterTextMutation())
        {
            InvalidateArrange();
        }

        if (TryGetLocalizedTextDirtyBoundsHint(out var dirtyBounds))
        {
            InvalidateVisualWithDirtyBoundsHint(dirtyBounds, $"Edit:{reason}");
            return;
        }

        InvalidateVisualWithDirtyBoundsHint(LayoutSlot, $"Edit:{reason}");
    }

    private void InvalidateAfterDocumentChange()
    {
        if (_suppressMeasureInvalidationForDocumentBatch)
        {
            if (ContainsHostedDocumentChildren(Document) &&
                !TryRefreshHostedDocumentChildLayoutAfterTextMutation())
            {
                InvalidateArrange();
            }
        }
        else
        {
            InvalidateMeasure();
        }

        if (TryGetLocalizedTextDirtyBoundsHint(out var dirtyBounds))
        {
            InvalidateVisualWithDirtyBoundsHint(dirtyBounds, "DocumentChange");
            return;
        }

        InvalidateVisualWithDirtyBoundsHint(LayoutSlot, "DocumentChange");
    }

    private void InvalidateVisualWithDirtyBoundsHint(LayoutRect bounds, string reason)
    {
        _pendingInvalidationReason = reason;
        _pendingRenderDirtyBoundsHint = NormalizeRect(bounds);
        _hasPendingRenderDirtyBoundsHint = true;
        _preserveRenderDirtyBoundsHint = true;
        try
        {
            base.InvalidateVisual();
        }
        finally
        {
            _preserveRenderDirtyBoundsHint = false;
        }
    }

    private bool TryRefreshHostedDocumentChildLayoutAfterTextMutation()
    {
        if (_documentHostedVisualChildren.Count == 0 || NeedsMeasure || NeedsArrange)
        {
            return false;
        }

        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return false;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        EnsureHostedDocumentChildLayout(textRect, layout);
        return true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        var textWidth = TextWrapping == TextWrapping.NoWrap
            ? float.PositiveInfinity
            : Math.Max(0f, availableSize.X - Padding.Horizontal - (BorderThickness * 2f));
        var layout = BuildOrGetLayout(textWidth);
        _lastMeasuredLayout = layout;
        desired.X = Math.Max(desired.X, layout.ContentWidth + Padding.Horizontal + (BorderThickness * 2f));
        desired.Y = Math.Max(desired.Y, layout.ContentHeight + Padding.Vertical + (BorderThickness * 2f));
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        EnsureHostedDocumentChildLayout();
        QueueViewportChangedNotification();
        return arranged;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        UpdateRichTextState(gameTime);
    }

    bool IUiRootUpdateParticipant.IsFrameUpdateActive => _hasPendingViewportChangedNotification || IsFocused;

    void IUiRootUpdateParticipant.UpdateFromUiRoot(GameTime gameTime)
    {
        RecordUpdateCallFromUiRoot();
        UpdateRichTextState(gameTime);
    }

    private void UpdateRichTextState(GameTime gameTime)
    {
        FlushPendingViewportChangedNotification();

        if (!IsFocused)
        {
            return;
        }

        _caretBlinkSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_caretBlinkSeconds >= 0.5f)
        {
            _caretBlinkSeconds = 0f;
            _isCaretVisible = !_isCaretVisible;
            InvalidateVisual();
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var hasTemplateRoot = HasTemplateRoot;
        if (hasTemplateRoot)
        {
            DrawTemplateVisualTree(spriteBatch);
        }
        else
        {
            UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background * Opacity);
            if (BorderThickness > 0f)
            {
                UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush * Opacity);
            }
        }

        if (_contentHost != null)
        {
            var hostedRootRenderStartTicks = Stopwatch.GetTimestamp();
            _diagHostedRootRenderCallCount++;
            _runtimeHostedRootRenderCallCount++;
            var hostedTextRect = GetTextRect();
            if (hostedTextRect.Width > 0f && hostedTextRect.Height > 0f)
            {
                var hostedRootLayoutResolveStartTicks = Stopwatch.GetTimestamp();
                var hostedLayout = BuildOrGetLayout(hostedTextRect.Width);
                ClampScrollOffsets(hostedLayout, hostedTextRect);
                var hostedRootLayoutResolveElapsedTicks = Stopwatch.GetTimestamp() - hostedRootLayoutResolveStartTicks;
                _diagHostedRootRenderLayoutResolveElapsedTicks += hostedRootLayoutResolveElapsedTicks;
                _runtimeHostedRootRenderLayoutResolveElapsedTicks += hostedRootLayoutResolveElapsedTicks;
                EnsureHostedDocumentChildLayout(hostedTextRect, hostedLayout);
                CaptureDirtyHint(hostedLayout, hostedTextRect);
                _lastRenderedLayout = hostedLayout;
            }

            var hostedRootRenderElapsedTicks = Stopwatch.GetTimestamp() - hostedRootRenderStartTicks;
            _diagHostedRootRenderElapsedTicks += hostedRootRenderElapsedTicks;
            _runtimeHostedRootRenderElapsedTicks += hostedRootRenderElapsedTicks;
            return;
        }

        var renderStartTicks = Stopwatch.GetTimestamp();
        var textRect = GetTextRect();

        var layoutResolveStart = Stopwatch.GetTimestamp();
        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        var layoutResolveMs = Stopwatch.GetElapsedTime(layoutResolveStart).TotalMilliseconds;

        RenderDocumentSurface(
            spriteBatch,
            textRect,
            layout,
            GetEffectiveHorizontalOffset(),
            GetEffectiveVerticalOffset(),
            includeHostedChildren: hasTemplateRoot,
            out var selectionMs,
            out var runsMs,
            out var runCount,
            out var runCharacterCount,
            out var tableBordersMs,
            out var caretMs,
            out var hostedLayoutMs,
            out var hostedChildrenDrawMs,
            out var hostedChildrenDrawCount);

        CaptureDirtyHint(layout, textRect);
        _lastRenderedLayout = layout;
        _perfTracker.RecordRenderBreakdown(
            layoutResolveMs,
            selectionMs,
            runsMs,
            runCount,
            runCharacterCount,
            tableBordersMs,
            caretMs,
            hostedLayoutMs,
            hostedChildrenDrawMs,
            hostedChildrenDrawCount);
        _perfTracker.RecordRender(Stopwatch.GetElapsedTime(renderStartTicks).TotalMilliseconds);
    }

    protected override bool ShouldAutoDrawVisualChildren => !HasTemplateRoot;

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        if (_documentHostedVisualChildren.Count > 0)
        {
            EnsureHostedDocumentChildLayout();
            yield return _hostedDocumentVisualHost;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        EnsureHostedDocumentChildLayout();
        return base.GetVisualChildCountForTraversal() + _documentHostedVisualChildren.Count;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        EnsureHostedDocumentChildLayout();

        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            return base.GetVisualChildAtForTraversal(index);
        }

        var hostedIndex = index - baseCount;
        if ((uint)hostedIndex < (uint)_documentHostedVisualChildren.Count)
        {
            return _documentHostedVisualChildren[hostedIndex];
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    internal override IEnumerable<UIElement> GetRetainedRenderChildren()
    {
        if (!HasTemplateRoot)
        {
            foreach (var child in base.GetRetainedRenderChildren())
            {
                yield return child;
            }
        }

        if (_documentHostedVisualChildren.Count > 0)
        {
            EnsureHostedDocumentChildLayout();
            yield return _hostedDocumentVisualHost;
        }
    }

    private void DrawTemplateVisualTree(SpriteBatch spriteBatch)
    {
        foreach (var child in base.GetVisualChildren())
        {
            child.Draw(spriteBatch);
        }
    }

    private void RenderDocumentSurface(
        SpriteBatch spriteBatch,
        LayoutRect textRect,
        DocumentLayoutResult layout,
        float horizontalOffset,
        float verticalOffset,
        bool includeHostedChildren)
    {
        RenderDocumentSurface(
            spriteBatch,
            textRect,
            layout,
            horizontalOffset,
            verticalOffset,
            includeHostedChildren,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);
    }

    private void RenderDocumentSurface(
        SpriteBatch spriteBatch,
        LayoutRect textRect,
        DocumentLayoutResult layout,
        float horizontalOffset,
        float verticalOffset,
        bool includeHostedChildren,
        out double selectionMs,
        out double runsMs,
        out int runCount,
        out int runCharacterCount,
        out double tableBordersMs,
        out double caretMs,
        out double hostedLayoutMs,
        out double hostedChildrenDrawMs,
        out int hostedChildrenDrawCount)
    {
        selectionMs = 0d;
        runsMs = 0d;
        runCount = 0;
        runCharacterCount = 0;
        tableBordersMs = 0d;
        caretMs = 0d;
        hostedLayoutMs = 0d;
        hostedChildrenDrawMs = 0d;
        hostedChildrenDrawCount = 0;

        UiDrawing.PushClip(spriteBatch, textRect);
        try
        {
            var selectionStart = Stopwatch.GetTimestamp();
            DrawSelection(spriteBatch, textRect, layout, horizontalOffset, verticalOffset);
            selectionMs = Stopwatch.GetElapsedTime(selectionStart).TotalMilliseconds;

            var runsStart = Stopwatch.GetTimestamp();
            if (TryGetVisibleLineRange(layout, textRect, verticalOffset, out var firstVisibleLineIndex, out var lastVisibleLineIndex))
            {
                for (var lineIndex = firstVisibleLineIndex; lineIndex <= lastVisibleLineIndex; lineIndex++)
                {
                    var line = layout.Lines[lineIndex];
                    var lineBottom = textRect.Y + line.Bounds.Y + line.Bounds.Height - verticalOffset;
                    var lineTop = textRect.Y + line.Bounds.Y - verticalOffset;
                    if (lineBottom < textRect.Y || lineTop > textRect.Y + textRect.Height)
                    {
                        continue;
                    }

                    for (var runIndex = 0; runIndex < line.Runs.Count; runIndex++)
                    {
                        var run = line.Runs[runIndex];
                        if (string.IsNullOrEmpty(run.Text))
                        {
                            continue;
                        }

                        var color = ResolveRunColor(run.Style);
                        runCount++;
                        runCharacterCount += run.Text.Length;

                        var position = new Vector2(textRect.X + run.Bounds.X - horizontalOffset, textRect.Y + run.Bounds.Y - verticalOffset);
                        DrawRunText(spriteBatch, run, position, color);
                        DrawRunUnderline(spriteBatch, run, position, color);
                    }
                }
            }

            runsMs = Stopwatch.GetElapsedTime(runsStart).TotalMilliseconds;

            var tableBordersStart = Stopwatch.GetTimestamp();
            DrawTableBorders(spriteBatch, textRect, layout, horizontalOffset, verticalOffset);
            tableBordersMs = Stopwatch.GetElapsedTime(tableBordersStart).TotalMilliseconds;
            if (IsFocused && _isCaretVisible && (!IsReadOnly || IsReadOnlyCaretVisible))
            {
                var caretStart = Stopwatch.GetTimestamp();
                DrawCaret(spriteBatch, textRect, layout, horizontalOffset, verticalOffset);
                caretMs = Stopwatch.GetElapsedTime(caretStart).TotalMilliseconds;
            }

            if (includeHostedChildren)
            {
                var hostedLayoutStart = Stopwatch.GetTimestamp();
                EnsureHostedDocumentChildLayout(textRect, layout);
                hostedLayoutMs = Stopwatch.GetElapsedTime(hostedLayoutStart).TotalMilliseconds;
                var hostedChildrenDrawStart = Stopwatch.GetTimestamp();
                if (_documentHostedVisualChildren.Count > 0)
                {
                    hostedChildrenDrawCount = _documentHostedVisualChildren.Count;
                    _hostedDocumentVisualHost.Draw(spriteBatch);
                }

                hostedChildrenDrawMs = Stopwatch.GetElapsedTime(hostedChildrenDrawStart).TotalMilliseconds;
            }
        }
        finally
        {
            UiDrawing.PopClip(spriteBatch);
        }
    }

    private void SyncHostedDocumentChildren()
    {
        var nextChildren = new List<UIElement>();
        CollectHostedInlineChildren(Document, nextChildren);

        for (var i = 0; i < _documentHostedVisualChildren.Count; i++)
        {
            var child = _documentHostedVisualChildren[i];
            if (nextChildren.Contains(child))
            {
                continue;
            }

            if (ReferenceEquals(child.VisualParent, _hostedDocumentVisualHost))
            {
                child.SetVisualParent(null);
            }

            if (ReferenceEquals(child.LogicalParent, this))
            {
                child.SetLogicalParent(null);
            }
        }

        if (nextChildren.Count > 0 && !ReferenceEquals(_hostedDocumentVisualHost.VisualParent, this))
        {
            _hostedDocumentVisualHost.SetVisualParent(this);
        }
        else if (nextChildren.Count == 0 && ReferenceEquals(_hostedDocumentVisualHost.VisualParent, this))
        {
            _hostedDocumentVisualHost.SetVisualParent(null);
        }

        for (var i = 0; i < nextChildren.Count; i++)
        {
            var child = nextChildren[i];
            if (!ReferenceEquals(child.VisualParent, _hostedDocumentVisualHost))
            {
                child.SetVisualParent(_hostedDocumentVisualHost);
            }

            if (!ReferenceEquals(child.LogicalParent, this))
            {
                child.SetLogicalParent(this);
            }
        }

        _documentHostedVisualChildren.Clear();
        _documentHostedVisualChildren.AddRange(nextChildren);
        _hostedDocumentVisualHost.SetChildren(_documentHostedVisualChildren);
    }

    private void EnsureHostedDocumentChildLayout()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagEnsureHostedDocumentChildLayoutCallCount++;
        _runtimeEnsureHostedDocumentChildLayoutCallCount++;
        if (_documentHostedVisualChildren.Count == 0)
        {
            _diagEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount++;
            _runtimeEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount++;
            var skippedElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagEnsureHostedDocumentChildLayoutElapsedTicks += skippedElapsedTicks;
            _runtimeEnsureHostedDocumentChildLayoutElapsedTicks += skippedElapsedTicks;
            return;
        }

        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            _diagEnsureHostedDocumentChildLayoutSkippedZeroTextRectCount++;
            _runtimeEnsureHostedDocumentChildLayoutSkippedZeroTextRectCount++;
            var skippedElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagEnsureHostedDocumentChildLayoutElapsedTicks += skippedElapsedTicks;
            _runtimeEnsureHostedDocumentChildLayoutElapsedTicks += skippedElapsedTicks;
            return;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        EnsureHostedDocumentChildLayout(textRect, layout);
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagEnsureHostedDocumentChildLayoutElapsedTicks += elapsedTicks;
        _runtimeEnsureHostedDocumentChildLayoutElapsedTicks += elapsedTicks;
    }

    private void EnsureHostedDocumentChildLayout(LayoutRect textRect, DocumentLayoutResult layout)
    {
        if (_documentHostedVisualChildren.Count == 0)
        {
            _diagEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount++;
            _runtimeEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount++;
            return;
        }

        var horizontalOffset = GetEffectiveHorizontalOffset();
        var verticalOffset = GetEffectiveVerticalOffset();

        var hostedLayoutChanged = !AreLayoutRectsEquivalent(_hostedDocumentVisualHost.LayoutSlot, textRect);
        _hostedDocumentVisualHost.SetLayoutSlot(textRect);

        for (var i = 0; i < layout.HostedElements.Count; i++)
        {
            var placement = layout.HostedElements[i];
            var rect = new LayoutRect(
                textRect.X + placement.Bounds.X - horizontalOffset,
                textRect.Y + placement.Bounds.Y - verticalOffset,
                placement.Bounds.Width,
                placement.Bounds.Height);

            if (placement.Child is FrameworkElement arrangedFrameworkChild)
            {
                var previousSlot = arrangedFrameworkChild.LayoutSlot;
                arrangedFrameworkChild.Arrange(rect);
                hostedLayoutChanged |= !AreLayoutRectsEquivalent(previousSlot, arrangedFrameworkChild.LayoutSlot);
            }
            else
            {
                var previousSlot = placement.Child.LayoutSlot;
                placement.Child.SetLayoutSlot(rect);
                hostedLayoutChanged |= !AreLayoutRectsEquivalent(previousSlot, placement.Child.LayoutSlot);
            }
        }

        if (hostedLayoutChanged)
        {
            _hostedDocumentVisualHost.InvalidateVisual();
            for (var i = 0; i < _documentHostedVisualChildren.Count; i++)
            {
                _documentHostedVisualChildren[i].InvalidateVisual();
            }
        }
    }

    private static bool AreLayoutRectsEquivalent(LayoutRect left, LayoutRect right)
    {
        return Math.Abs(left.X - right.X) < 0.01f &&
               Math.Abs(left.Y - right.Y) < 0.01f &&
               Math.Abs(left.Width - right.Width) < 0.01f &&
               Math.Abs(left.Height - right.Height) < 0.01f;
    }

    private static void CollectHostedInlineChildren(FlowDocument document, List<UIElement> children)
    {
        CollectHostedBlockChildren(document.Blocks, children);
    }

    private static void CollectHostedBlockChildren(IEnumerable<Block> blocks, List<UIElement> children)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    CollectHostedInlineChildren(paragraph.Inlines, children);
                    break;
                case BlockUIContainer blockUiContainer when blockUiContainer.Child != null:
                    children.Add(blockUiContainer.Child);
                    break;
                case Section section:
                    CollectHostedBlockChildren(section.Blocks, children);
                    break;
                case InkkSlinger.List list:
                    for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                    {
                        CollectHostedBlockChildren(list.Items[itemIndex].Blocks, children);
                    }

                    break;
                case Table table:
                    for (var rowGroupIndex = 0; rowGroupIndex < table.RowGroups.Count; rowGroupIndex++)
                    {
                        var rowGroup = table.RowGroups[rowGroupIndex];
                        for (var rowIndex = 0; rowIndex < rowGroup.Rows.Count; rowIndex++)
                        {
                            var row = rowGroup.Rows[rowIndex];
                            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                            {
                                CollectHostedBlockChildren(row.Cells[cellIndex].Blocks, children);
                            }
                        }
                    }

                    break;
            }
        }
    }

    private static void CollectHostedInlineChildren(IEnumerable<Inline> inlines, List<UIElement> children)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case InlineUIContainer inlineUiContainer when inlineUiContainer.Child != null:
                    children.Add(inlineUiContainer.Child);
                    break;
                case Span span:
                    CollectHostedInlineChildren(span.Inlines, children);
                    break;
            }
        }
    }

    private static List<HostedInlinePlacement> CollectHostedInlinePlacements(FlowDocument document)
    {
        var placements = new List<HostedInlinePlacement>();
        var logicalBlocks = new List<TextElement>();
        CollectHostedLogicalBlocks(document.Blocks, logicalBlocks);
        var offset = 0;
        for (var blockIndex = 0; blockIndex < logicalBlocks.Count; blockIndex++)
        {
            switch (logicalBlocks[blockIndex])
            {
                case Paragraph paragraph:
                {
                    var localOffset = 0;
                    CollectHostedInlinePlacements(paragraph.Inlines, offset, ref localOffset, placements);
                    offset += GetParagraphLogicalLength(paragraph);
                    break;
                }
                case BlockUIContainer blockUiContainer:
                    if (blockUiContainer.Child != null)
                    {
                        placements.Add(new HostedInlinePlacement(blockUiContainer.Child, offset));
                    }

                    offset += 1;
                    break;
            }

            if (blockIndex < logicalBlocks.Count - 1)
            {
                offset++;
            }
        }

        return placements;
    }

    private static void CollectHostedLogicalBlocks(IEnumerable<Block> blocks, List<TextElement> logicalBlocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    logicalBlocks.Add(paragraph);
                    break;
                case BlockUIContainer blockUiContainer:
                    logicalBlocks.Add(blockUiContainer);
                    break;
                case Section section:
                    CollectHostedLogicalBlocks(section.Blocks, logicalBlocks);
                    break;
                case InkkSlinger.List list:
                    for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                    {
                        CollectHostedLogicalBlocks(list.Items[itemIndex].Blocks, logicalBlocks);
                    }

                    break;
                case Table table:
                    for (var rowGroupIndex = 0; rowGroupIndex < table.RowGroups.Count; rowGroupIndex++)
                    {
                        var rowGroup = table.RowGroups[rowGroupIndex];
                        for (var rowIndex = 0; rowIndex < rowGroup.Rows.Count; rowIndex++)
                        {
                            var row = rowGroup.Rows[rowIndex];
                            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                            {
                                CollectHostedLogicalBlocks(row.Cells[cellIndex].Blocks, logicalBlocks);
                            }
                        }
                    }

                    break;
            }
        }
    }

    private static void CollectHostedInlinePlacements(
        IEnumerable<Inline> inlines,
        int paragraphOffset,
        ref int localOffset,
        List<HostedInlinePlacement> placements)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    localOffset += run.Text.Length;
                    break;
                case LineBreak:
                    localOffset++;
                    break;
                case InlineUIContainer inlineUiContainer:
                    if (inlineUiContainer.Child != null)
                    {
                        placements.Add(new HostedInlinePlacement(inlineUiContainer.Child, paragraphOffset + localOffset));
                    }

                    localOffset++;
                    break;
                case Span span:
                    CollectHostedInlinePlacements(span.Inlines, paragraphOffset, ref localOffset, placements);
                    break;
            }
        }
    }

    private readonly record struct HostedInlinePlacement(UIElement Child, int Offset);

    private sealed class HostedDocumentVisualHost : UIElement
    {
        private IReadOnlyList<UIElement> _children = Array.Empty<UIElement>();

        public HostedDocumentVisualHost()
        {
            ClipToBounds = true;
        }

        public void SetChildren(IReadOnlyList<UIElement> children)
        {
            _children = children;
        }

        public override IEnumerable<UIElement> GetVisualChildren()
        {
            for (var i = 0; i < _children.Count; i++)
            {
                yield return _children[i];
            }
        }

        internal override int GetVisualChildCountForTraversal()
        {
            return _children.Count;
        }

        internal override UIElement GetVisualChildAtForTraversal(int index)
        {
            if ((uint)index < (uint)_children.Count)
            {
                return _children[index];
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private int GetTextIndexFromPoint(Vector2 point)
    {
        var textRect = GetTextRect();
        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        var hit = layout.HitTestOffset(new Vector2(
            (point.X - textRect.X) + GetEffectiveHorizontalOffset(),
            (point.Y - textRect.Y) + GetEffectiveVerticalOffset()));
        _lastSelectionHitTestOffset = hit;
        return hit;
    }

    private void DrawCaret(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        DrawCaret(spriteBatch, textRect, layout, GetEffectiveHorizontalOffset(), GetEffectiveVerticalOffset());
    }

    private void DrawCaret(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout, float horizontalOffset, float verticalOffset)
    {
        if (!TryGetCaretRenderRect(textRect, layout, horizontalOffset, verticalOffset, out var caretRect))
        {
            return;
        }

        UiDrawing.DrawFilledRect(
            spriteBatch,
            caretRect,
            CaretBrush * Opacity);
    }

    private void DrawSelection(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout)
    {
        DrawSelection(spriteBatch, textRect, layout, GetEffectiveHorizontalOffset(), GetEffectiveVerticalOffset());
    }

    private void DrawSelection(SpriteBatch spriteBatch, LayoutRect textRect, DocumentLayoutResult layout, float horizontalOffset, float verticalOffset)
    {
        if (SelectionLength <= 0 || (!IsFocused && !IsInactiveSelectionHighlightEnabled))
        {
            return;
        }

        var selectionStartTicks = Stopwatch.GetTimestamp();
        var rects = layout.BuildSelectionRects(SelectionStart, SelectionLength);
        for (var i = 0; i < rects.Count; i++)
        {
            var rect = rects[i];
            UiDrawing.DrawFilledRect(
                spriteBatch,
                new LayoutRect(textRect.X + rect.X - horizontalOffset, textRect.Y + rect.Y - verticalOffset, rect.Width, rect.Height),
                SelectionBrush * (Opacity * SelectionOpacity));
        }

        _perfTracker.RecordSelectionGeometry(Stopwatch.GetElapsedTime(selectionStartTicks).TotalMilliseconds);
    }

    private bool TryGetCaretRenderRect(
        LayoutRect textRect,
        DocumentLayoutResult layout,
        float horizontalOffset,
        float verticalOffset,
        out LayoutRect caretRect)
    {
        caretRect = default;
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return false;
        }

        if (!layout.TryGetCaretPosition(_caretIndex, out var caret))
        {
            return false;
        }

        var lineHeight = Math.Max(1f, UiTextRenderer.GetLineHeight(this, FontSize));
        var rawRect = new LayoutRect(
            textRect.X + caret.X - horizontalOffset,
            textRect.Y + caret.Y - verticalOffset,
            1f,
            lineHeight);
        caretRect = IntersectRect(rawRect, textRect);
        return caretRect.Width > 0f && caretRect.Height > 0f;
    }

    private void DrawRunText(SpriteBatch spriteBatch, DocumentLayoutRun run, Vector2 position, Color baseColor)
    {
        if (run.Text.Length == 0)
        {
            return;
        }

        var overlapStart = Math.Max(SelectionStart, run.StartOffset);
        var overlapEnd = Math.Min(SelectionStart + SelectionLength, run.StartOffset + run.Length);
        var styleOverride = ToStyleOverride(run.Style);
        if (SelectionLength <= 0 || overlapEnd <= overlapStart)
        {
            UiTextRenderer.DrawString(
                spriteBatch,
                this,
                run.Text,
                position,
                baseColor * Opacity,
                FontSize,
                opaqueBackground: false,
                styleOverride: styleOverride);
            return;
        }

        var prefixLength = overlapStart - run.StartOffset;
        var selectedLength = overlapEnd - overlapStart;
        var prefixText = prefixLength > 0 ? run.Text[..prefixLength] : string.Empty;
        var selectedText = selectedLength > 0 ? run.Text.Substring(prefixLength, selectedLength) : string.Empty;
        var suffixText = (prefixLength + selectedLength) < run.Text.Length
            ? run.Text[(prefixLength + selectedLength)..]
            : string.Empty;
        var typography = UiTextRenderer.ResolveTypography(this, FontSize, styleOverride);
        var currentX = position.X;

        if (prefixText.Length > 0)
        {
            UiTextRenderer.DrawString(spriteBatch, typography, prefixText, new Vector2(currentX, position.Y), baseColor * Opacity);
            currentX += UiTextRenderer.MeasureWidth(typography, prefixText);
        }

        if (selectedText.Length > 0)
        {
            UiTextRenderer.DrawString(spriteBatch, typography, selectedText, new Vector2(currentX, position.Y), SelectionTextBrush * Opacity);
            currentX += UiTextRenderer.MeasureWidth(typography, selectedText);
        }

        if (suffixText.Length > 0)
        {
            UiTextRenderer.DrawString(spriteBatch, typography, suffixText, new Vector2(currentX, position.Y), baseColor * Opacity);
        }
    }

    private void DrawRunUnderline(SpriteBatch spriteBatch, DocumentLayoutRun run, Vector2 position, Color color)
    {
        if (!run.Style.IsUnderline)
        {
            return;
        }

        var underlineY = position.Y + run.Bounds.Height - 1f;
        UiDrawing.DrawFilledRect(
            spriteBatch,
            new LayoutRect(position.X, underlineY, Math.Max(1f, run.Bounds.Width), 1f),
            color * Opacity);
    }

    private static bool TryGetVisibleLineRange(
        DocumentLayoutResult layout,
        LayoutRect textRect,
        float verticalOffset,
        out int firstVisibleLineIndex,
        out int lastVisibleLineIndex)
    {
        firstVisibleLineIndex = -1;
        lastVisibleLineIndex = -1;
        if (layout.Lines.Count == 0 || textRect.Height <= 0f)
        {
            return false;
        }

        var visibleTop = verticalOffset;
        var visibleBottom = verticalOffset + textRect.Height;
        firstVisibleLineIndex = FindFirstVisibleLineIndex(layout.Lines, visibleTop);
        if (firstVisibleLineIndex < 0)
        {
            return false;
        }

        lastVisibleLineIndex = FindLastVisibleLineIndex(layout.Lines, visibleBottom, firstVisibleLineIndex);
        return lastVisibleLineIndex >= firstVisibleLineIndex;
    }

    private static int FindFirstVisibleLineIndex(IReadOnlyList<DocumentLayoutLine> lines, float visibleTop)
    {
        var low = 0;
        var high = lines.Count - 1;
        var result = -1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var line = lines[mid];
            var lineBottom = line.Bounds.Y + line.Bounds.Height;
            if (lineBottom >= visibleTop)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    private static int FindLastVisibleLineIndex(IReadOnlyList<DocumentLayoutLine> lines, float visibleBottom, int firstVisibleLineIndex)
    {
        var low = firstVisibleLineIndex;
        var high = lines.Count - 1;
        var result = firstVisibleLineIndex;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var line = lines[mid];
            if (line.Bounds.Y <= visibleBottom)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return result;
    }

    private Color ResolveRunColor(DocumentLayoutStyle style)
    {
        if (style.ForegroundOverride.HasValue)
        {
            return style.ForegroundOverride.Value;
        }

        if (style.IsHyperlink)
        {
            return new Color(117, 181, 255);
        }

        return Foreground;
    }

    private static UiTextStyleOverride ToStyleOverride(DocumentLayoutStyle style)
    {
        var value = UiTextStyleOverride.None;
        if (style.IsBold)
        {
            value |= UiTextStyleOverride.Bold;
        }

        if (style.IsItalic)
        {
            value |= UiTextStyleOverride.Italic;
        }

        return value;
    }

    private void CaptureDirtyHint(DocumentLayoutResult current, LayoutRect textRect)
    {
        if (!TryBuildLocalizedTextDirtyBoundsHint(current, textRect, out var dirtyBounds))
        {
            return;
        }

        _pendingRenderDirtyBoundsHint = dirtyBounds;
        _hasPendingRenderDirtyBoundsHint = true;
    }

    private bool TryGetLocalizedTextDirtyBoundsHint(out LayoutRect bounds)
    {
        bounds = default;
        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            return false;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        return TryBuildLocalizedTextDirtyBoundsHint(layout, textRect, out bounds);
    }

    private bool TryBuildLocalizedTextDirtyBoundsHint(DocumentLayoutResult current, LayoutRect textRect, out LayoutRect bounds)
    {
        bounds = default;
        if (_lastRenderedLayout == null || _lastRenderedLayout.Lines.Count == 0 || current.Lines.Count == 0)
        {
            return false;
        }

        var dirty = false;
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var lineCount = Math.Max(_lastRenderedLayout.Lines.Count, current.Lines.Count);
        for (var i = 0; i < lineCount; i++)
        {
            var hasOld = i < _lastRenderedLayout.Lines.Count;
            var hasNew = i < current.Lines.Count;
            if (hasOld && hasNew)
            {
                var oldLine = _lastRenderedLayout.Lines[i];
                var newLine = current.Lines[i];
                if (string.Equals(oldLine.Text, newLine.Text, StringComparison.Ordinal) &&
                    oldLine.StartOffset == newLine.StartOffset &&
                    Math.Abs(oldLine.Bounds.Y - newLine.Bounds.Y) < 0.01f)
                {
                    continue;
                }

                Include(oldLine.Bounds);
                Include(newLine.Bounds);
                continue;
            }

            if (hasOld)
            {
                Include(_lastRenderedLayout.Lines[i].Bounds);
            }

            if (hasNew)
            {
                Include(current.Lines[i].Bounds);
            }
        }

        if (!dirty)
        {
            return false;
        }

        var local = new LayoutRect(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
        var absolute = new LayoutRect(
            textRect.X + local.X - GetEffectiveHorizontalOffset(),
            textRect.Y + local.Y - GetEffectiveVerticalOffset(),
            local.Width,
            local.Height);
        absolute = IntersectRect(ExpandRect(absolute, 2f), textRect);
        if (absolute.Width <= 0f || absolute.Height <= 0f)
        {
            return false;
        }

        if (TryProjectRectToRootSpace(absolute, out var rootSpaceBounds))
        {
            absolute = rootSpaceBounds;
        }

        bounds = NormalizeRect(absolute);
        return bounds.Width > 0f && bounds.Height > 0f;

        void Include(LayoutRect rect)
        {
            dirty = true;
            minX = Math.Min(minX, rect.X);
            minY = Math.Min(minY, rect.Y);
            maxX = Math.Max(maxX, rect.X + rect.Width);
            maxY = Math.Max(maxY, rect.Y + rect.Height);
        }
    }

    private LayoutRect GetTextRect()
    {
        if (_contentHost != null && _contentHost.TryGetContentViewportClipRect(out var clipRect))
        {
            return clipRect;
        }

        return new LayoutRect(
            LayoutSlot.X + BorderThickness + Padding.Left,
            LayoutSlot.Y + BorderThickness + Padding.Top,
            Math.Max(0f, LayoutSlot.Width - (BorderThickness * 2f) - Padding.Horizontal),
            Math.Max(0f, LayoutSlot.Height - (BorderThickness * 2f) - Padding.Vertical));
    }

    private bool TryProjectRectToRootSpace(LayoutRect rect, out LayoutRect projectedRect)
    {
        projectedRect = rect;
        var transform = Matrix.Identity;
        var hasTransform = false;
        for (var current = this as UIElement; current != null; current = current.VisualParent)
        {
            if (!current.TryGetLocalRenderTransformSnapshot(out var localTransform))
            {
                continue;
            }

            transform *= localTransform;
            hasTransform = true;
        }

        if (!hasTransform)
        {
            return true;
        }

        var topLeft = Vector2.Transform(new Vector2(rect.X, rect.Y), transform);
        var topRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(rect.X, rect.Y + rect.Height), transform);
        var bottomRight = Vector2.Transform(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), transform);

        var minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        var minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        var maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));

        projectedRect = new LayoutRect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
        return projectedRect.Width > 0f && projectedRect.Height > 0f;
    }

    private static LayoutRect IntersectRect(LayoutRect left, LayoutRect right)
    {
        var x = MathF.Max(left.X, right.X);
        var y = MathF.Max(left.Y, right.Y);
        var rightEdge = MathF.Min(left.X + left.Width, right.X + right.Width);
        var bottomEdge = MathF.Min(left.Y + left.Height, right.Y + right.Height);
        return new LayoutRect(x, y, MathF.Max(0f, rightEdge - x), MathF.Max(0f, bottomEdge - y));
    }

    private static LayoutRect ExpandRect(LayoutRect rect, float padding)
    {
        var safePadding = MathF.Max(0f, padding);
        return new LayoutRect(
            rect.X - safePadding,
            rect.Y - safePadding,
            MathF.Max(0f, rect.Width + (safePadding * 2f)),
            MathF.Max(0f, rect.Height + (safePadding * 2f)));
    }

    private static LayoutRect NormalizeRect(LayoutRect rect)
    {
        var x = rect.X;
        var y = rect.Y;
        var width = rect.Width;
        var height = rect.Height;
        if (width < 0f)
        {
            x += width;
            width = -width;
        }

        if (height < 0f)
        {
            y += height;
            height = -height;
        }

        return new LayoutRect(x, y, width, height);
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }
}