using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using InkkSlinger;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class IDEEditorMinimap : Control, ITextInputControl, IUiRootUpdateParticipant
{
    private const float HorizontalPadding = 6f;
    private const float VerticalPadding = 6f;
    private const float MiniatureFontSize = 4f;
    private const float MiniatureLineHeight = 6f;
    private const float MinimumViewportOverlayHeight = 24f;
    private const float OverlayFadeInPerSecond = 8f;
    private const float OverlayFadeOutPerSecond = 5f;
    private bool _isDragging;
    private float _viewportOverlayOpacity;
    private IReadOnlyList<string> _lines = new[] { string.Empty };
    private IReadOnlyList<IDEEditorXmlSyntaxToken> _syntaxTokens = Array.Empty<IDEEditorXmlSyntaxToken>();
    private int[] _lineStarts = new[] { 0 };
    private int[] _lineTokenStartIndices = new[] { 0 };
    private int[] _lineTokenEndIndices = new[] { 0 };
    private int _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private int _runtimeLastRenderVisibleLineCount;
    private int _runtimeLastRenderTokenIterationCount;
    private int _runtimeLastRenderSegmentDrawCount;

    public static readonly DependencyProperty SourceTextProperty =
        DependencyProperty.Register(
            nameof(SourceText),
            typeof(string),
            typeof(IDEEditorMinimap),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is IDEEditorMinimap minimap)
                    {
                        minimap.RebuildLineCache(args.NewValue as string);
                    }
                }));

    public static readonly DependencyProperty EditorVerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(EditorVerticalOffset),
            typeof(float),
            typeof(IDEEditorMinimap),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EditorViewportHeightProperty =
        DependencyProperty.Register(
            nameof(EditorViewportHeight),
            typeof(float),
            typeof(IDEEditorMinimap),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EditorEstimatedLineHeightProperty =
        DependencyProperty.Register(
            nameof(EditorEstimatedLineHeight),
            typeof(float),
            typeof(IDEEditorMinimap),
            new FrameworkPropertyMetadata(16f, FrameworkPropertyMetadataOptions.AffectsRender));

    public IDEEditorMinimap()
    {
        IsHitTestVisible = true;
        Background = Color.Transparent;
        FontFamily = "Consolas";
        RebuildLineCache(SourceText);
        AddHandler<MouseRoutedEventArgs>(UIElement.MouseEnterEvent, OnMouseEnter);
        AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeaveEvent, OnMouseLeave);
        AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonDownEvent, OnMouseLeftButtonDown);
        AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeftButtonDownEvent, OnMouseLeftButtonDown);
        AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseMoveEvent, OnMouseMove);
        AddHandler<MouseRoutedEventArgs>(UIElement.MouseMoveEvent, OnMouseMove);
        AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonUpEvent, OnMouseLeftButtonUp);
        AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeftButtonUpEvent, OnMouseLeftButtonUp);
    }

    public event EventHandler<IDEEditorMinimapNavigateEventArgs>? NavigateRequested;

    public string SourceText
    {
        get => GetValue<string>(SourceTextProperty) ?? string.Empty;
        set => SetValue(SourceTextProperty, value ?? string.Empty);
    }

    public float EditorVerticalOffset
    {
        get => GetValue<float>(EditorVerticalOffsetProperty);
        set => SetValue(EditorVerticalOffsetProperty, value);
    }

    public float EditorViewportHeight
    {
        get => GetValue<float>(EditorViewportHeightProperty);
        set => SetValue(EditorViewportHeightProperty, value);
    }

    public float EditorEstimatedLineHeight
    {
        get => GetValue<float>(EditorEstimatedLineHeightProperty);
        set => SetValue(EditorEstimatedLineHeightProperty, value);
    }

    public int LineCount => _lines.Count;

    public int NavigateRequestCount { get; private set; }

    public int LastRequestedLineNumber { get; private set; }

    public float LastRequestedVerticalOffset { get; private set; }

    internal float ViewportOverlayOpacityForTests => _viewportOverlayOpacity;

    internal float MinimapVerticalOffsetForTests => ResolveMinimapVerticalOffset(GetContentRect(LayoutSlot));

    internal LayoutRect ViewportOverlayRectForTests => ResolveViewportOverlayRect(GetContentRect(LayoutSlot), ResolveMinimapVerticalOffset(GetContentRect(LayoutSlot)), _lines.Count);

    internal IDEEditorMinimapRuntimeDiagnosticsSnapshot GetIDEEditorMinimapSnapshotForDiagnostics()
    {
        return new IDEEditorMinimapRuntimeDiagnosticsSnapshot(
            RenderCallCount: _runtimeRenderCallCount,
            RenderMilliseconds: TicksToMilliseconds(_runtimeRenderElapsedTicks),
            LastRenderVisibleLineCount: _runtimeLastRenderVisibleLineCount,
            LastRenderTokenIterationCount: _runtimeLastRenderTokenIterationCount,
            LastRenderSegmentDrawCount: _runtimeLastRenderSegmentDrawCount,
            LastLineCount: _lines.Count,
            LastTokenCount: _syntaxTokens.Count);
    }

    public bool HandleTextInputFromInput(char character)
    {
        _ = character;
        return false;
    }

    public bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        _ = key;
        _ = modifiers;
        return false;
    }

    public bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        _ = extendSelection;
        _isDragging = BeginPointerNavigation(pointerPosition);
        return _isDragging;
    }

    public bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        return _isDragging && ContinuePointerNavigation(pointerPosition);
    }

    public bool HandlePointerUpFromInput()
    {
        var wasDragging = _isDragging;
        _isDragging = false;
        EndPointerNavigation();
        return wasDragging;
    }

    public bool HandleMouseWheelFromInput(int delta)
    {
        _ = delta;
        return false;
    }

    public void SetMouseOverFromInput(bool isMouseOver)
    {
        ApplyMouseOverState(isMouseOver);
    }

    public void SetFocusedFromInput(bool isFocused)
    {
        IsFocused = isFocused;
    }

    public bool TryNavigateFromPointer(Vector2 pointer)
    {
        if (!IsVisible || !IsEnabled || !IsHitTestVisible || !Contains(LayoutSlot, pointer))
        {
            return false;
        }

        NavigateToPointer(pointer);
        return true;
    }

    public bool BeginPointerNavigation(Vector2 pointer)
    {
        if (!IsVisible || !IsEnabled || !IsHitTestVisible || !Contains(LayoutSlot, pointer))
        {
            return false;
        }

        var content = GetContentRect(LayoutSlot);
        _viewportOverlayOpacity = MathF.Max(_viewportOverlayOpacity, 0.65f);
        NavigateToPointer(pointer);
        return true;
    }

    public bool ContinuePointerNavigation(Vector2 pointer)
    {
        if (!IsVisible || !IsEnabled || !IsHitTestVisible || !Contains(LayoutSlot, pointer))
        {
            return false;
        }

        NavigateToPointer(pointer);
        return true;
    }

    public void EndPointerNavigation()
    {
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsFinite(availableSize.X) ? MathF.Max(0f, availableSize.X) : 96f;
        var height = float.IsFinite(availableSize.Y) ? MathF.Max(0f, availableSize.Y) : 240f;
        return new Vector2(width, height);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        UpdateViewportOverlayFade(gameTime);
    }

    bool IUiRootUpdateParticipant.IsFrameUpdateActive => IsViewportOverlayFadeActive();

    void IUiRootUpdateParticipant.UpdateFromUiRoot(GameTime gameTime)
    {
        RecordUpdateCallFromUiRoot();
        UpdateViewportOverlayFade(gameTime);
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeRenderCallCount++;
        _runtimeLastRenderVisibleLineCount = 0;
        _runtimeLastRenderTokenIterationCount = 0;
        _runtimeLastRenderSegmentDrawCount = 0;

        var slot = LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            _runtimeRenderElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        UiDrawing.DrawFilledRect(spriteBatch, slot, new Color(8, 14, 22), Opacity);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y, 1f, slot.Height), new Color(24, 36, 52), Opacity);

        var content = GetContentRect(slot);
        if (_lines.Count == 0 || content.Width <= 0f || content.Height <= 0f)
        {
            _runtimeRenderElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return;
        }

        var scrollOffset = ResolveMinimapVerticalOffset(content);
        DrawMiniatureText(spriteBatch, content, scrollOffset);
        DrawViewport(spriteBatch, content, scrollOffset, _lines.Count);
        _runtimeRenderElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    private void DrawMiniatureText(SpriteBatch spriteBatch, LayoutRect content, float scrollOffset)
    {
        var yOffset = MathF.Max(0f, (MiniatureLineHeight - MiniatureFontSize) * 0.5f);
        var approximateCharacterWidth = MathF.Max(1f, MiniatureFontSize * 0.58f);
        var maxCharacters = Math.Clamp((int)MathF.Ceiling(content.Width / approximateCharacterWidth) + 2, 1, 512);
        var firstLine = Math.Clamp((int)MathF.Floor(scrollOffset / MiniatureLineHeight), 0, Math.Max(0, _lines.Count - 1));
        var lastLine = Math.Clamp((int)MathF.Ceiling((scrollOffset + content.Height) / MiniatureLineHeight), firstLine, Math.Max(0, _lines.Count - 1));

        for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            var line = _lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var y = content.Y + (lineIndex * MiniatureLineHeight) - scrollOffset + yOffset;
            DrawMiniatureLine(spriteBatch, content.X, y, line, lineIndex, MiniatureFontSize, approximateCharacterWidth, maxCharacters);
            _runtimeLastRenderVisibleLineCount++;
        }
    }

    private void DrawMiniatureLine(
        SpriteBatch spriteBatch,
        float x,
        float y,
        string line,
        int lineIndex,
        float fontSize,
        float approximateCharacterWidth,
        int maxCharacters)
    {
        var visibleLineLength = Math.Min(line.Length, maxCharacters);
        if (visibleLineLength <= 0 || lineIndex < 0 || lineIndex >= _lineStarts.Length)
        {
            return;
        }

        var lineStart = _lineStarts[lineIndex];
        var lineEnd = lineStart + visibleLineLength;
        var cursor = lineStart;
        var tokenStart = _lineTokenStartIndices.Length > lineIndex ? _lineTokenStartIndices[lineIndex] : 0;
        var tokenEnd = _lineTokenEndIndices.Length > lineIndex ? _lineTokenEndIndices[lineIndex] : _syntaxTokens.Count;
        for (var i = tokenStart; i < tokenEnd; i++)
        {
            _runtimeLastRenderTokenIterationCount++;
            var token = _syntaxTokens[i];
            if (token.End <= lineStart)
            {
                continue;
            }

            if (token.Start >= lineEnd)
            {
                break;
            }

            if (token.Start > cursor)
            {
                DrawMiniatureSegment(spriteBatch, x, y, line, cursor - lineStart, token.Start - cursor, ResolveMiniatureColor(IDEEditorXmlSyntaxTokenKind.Text), fontSize, approximateCharacterWidth);
                _runtimeLastRenderSegmentDrawCount++;
            }

            var highlightStart = Math.Max(token.Start, lineStart);
            var highlightEnd = Math.Min(token.End, lineEnd);
            if (highlightEnd > highlightStart)
            {
                DrawMiniatureSegment(spriteBatch, x, y, line, highlightStart - lineStart, highlightEnd - highlightStart, ResolveMiniatureColor(token.Kind), fontSize, approximateCharacterWidth);
                _runtimeLastRenderSegmentDrawCount++;
            }

            cursor = Math.Max(cursor, highlightEnd);
        }

        if (cursor < lineEnd)
        {
            DrawMiniatureSegment(spriteBatch, x, y, line, cursor - lineStart, lineEnd - cursor, ResolveMiniatureColor(IDEEditorXmlSyntaxTokenKind.Text), fontSize, approximateCharacterWidth);
            _runtimeLastRenderSegmentDrawCount++;
        }
    }

    private void DrawMiniatureSegment(
        SpriteBatch spriteBatch,
        float x,
        float y,
        string line,
        int start,
        int length,
        Color color,
        float fontSize,
        float approximateCharacterWidth)
    {
        if (length <= 0 || start < 0 || start >= line.Length)
        {
            return;
        }

        var clampedLength = Math.Min(length, line.Length - start);
        var text = line.Substring(start, clampedLength);
        UiTextRenderer.DrawString(spriteBatch, this, text, new Vector2(x + (start * approximateCharacterWidth), y), color * Opacity, fontSize);
    }

    private void DrawViewport(SpriteBatch spriteBatch, LayoutRect content, float scrollOffset, int lineCount)
    {
        if (_viewportOverlayOpacity <= 0.001f)
        {
            return;
        }

        var rect = ResolveViewportOverlayRect(content, scrollOffset, lineCount);
        var overlayOpacity = Opacity * _viewportOverlayOpacity;
        UiDrawing.DrawFilledRect(spriteBatch, rect, new Color(42, 48, 54, 76), overlayOpacity);
        UiDrawing.DrawRectStroke(spriteBatch, rect, 1f, new Color(116, 126, 136, 82), overlayOpacity);
    }

    private LayoutRect ResolveViewportOverlayRect(LayoutRect content, float scrollOffset, int lineCount)
    {
        _ = scrollOffset;
        var editorLineHeight = MathF.Max(1f, EditorEstimatedLineHeight);
        var documentHeight = MathF.Max(editorLineHeight, lineCount * editorLineHeight);
        var viewportHeight = Math.Clamp(MathF.Max(0f, EditorViewportHeight), 0f, documentHeight);
        if (content.Height <= 0f || documentHeight <= viewportHeight)
        {
            return new LayoutRect(content.X, content.Y, content.Width, content.Height);
        }

        var height = Math.Clamp(content.Height * (viewportHeight / documentHeight), MinimumViewportOverlayHeight, content.Height);
        var maxEditorOffset = MathF.Max(1f, documentHeight - viewportHeight);
        var trackRatio = Math.Clamp(MathF.Max(0f, EditorVerticalOffset) / maxEditorOffset, 0f, 1f);
        var top = content.Y + (trackRatio * MathF.Max(0f, content.Height - height));
        return new LayoutRect(content.X, top, content.Width, height);
    }

    private void UpdateViewportOverlayFade(GameTime gameTime)
    {
        var targetOpacity = IsMouseOver || _isDragging ? 1f : 0f;
        var previousOpacity = _viewportOverlayOpacity;
        var rate = targetOpacity > _viewportOverlayOpacity ? OverlayFadeInPerSecond : OverlayFadeOutPerSecond;
        var delta = MathF.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds) * rate;
        _viewportOverlayOpacity = MoveTowards(_viewportOverlayOpacity, targetOpacity, delta);
        if (MathF.Abs(previousOpacity - _viewportOverlayOpacity) > 0.001f)
        {
            InvalidateVisual();
        }
    }

    private bool IsViewportOverlayFadeActive()
    {
        var targetOpacity = IsMouseOver || _isDragging ? 1f : 0f;
        return MathF.Abs(_viewportOverlayOpacity - targetOpacity) > 0.001f;
    }

    private void OnMouseLeftButtonDown(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        if (BeginPointerNavigation(args.Position))
        {
            _isDragging = true;
            args.Handled = true;
        }
    }

    private void OnMouseEnter(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyMouseOverState(true);
    }

    private void OnMouseMove(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        ApplyMouseOverState(Contains(LayoutSlot, args.Position));
        if (!_isDragging)
        {
            return;
        }

        if (ContinuePointerNavigation(args.Position))
        {
            args.Handled = true;
        }
    }

    private void OnMouseLeave(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (!_isDragging)
        {
            ApplyMouseOverState(false);
        }
    }

    private void OnMouseLeftButtonUp(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        _isDragging = false;
        EndPointerNavigation();
        ApplyMouseOverState(Contains(LayoutSlot, args.Position));
        args.Handled = true;
    }

    private void NavigateToPointer(Vector2 pointer)
    {
        if (_lines.Count == 0)
        {
            return;
        }

        var content = GetContentRect(LayoutSlot);
        var requestedVerticalOffset = ResolveEditorVerticalOffsetFromPointer(content, pointer);
        var editorLineHeight = MathF.Max(1f, EditorEstimatedLineHeight);
        var line = (int)MathF.Floor(requestedVerticalOffset / editorLineHeight) + 1;
        var oneBasedLine = Math.Clamp(line, 1, _lines.Count);
        NavigateRequestCount++;
        LastRequestedLineNumber = oneBasedLine;
        LastRequestedVerticalOffset = requestedVerticalOffset;
        NavigateRequested?.Invoke(this, new IDEEditorMinimapNavigateEventArgs(oneBasedLine, requestedVerticalOffset));
    }

    private void RebuildLineCache(string? sourceText)
    {
        var normalized = IDEEditorTextCommandService.Normalize(sourceText);
        _lines = normalized.Split('\n');
        _syntaxTokens = IDEEditorXmlSyntaxClassifier.Classify(normalized);
        _lineStarts = ComputeLineStarts(_lines);
        (_lineTokenStartIndices, _lineTokenEndIndices) = ComputeLineTokenRanges(_lineStarts, _lines, _syntaxTokens);
    }

    private static int[] ComputeLineStarts(IReadOnlyList<string> lines)
    {
        var starts = new int[Math.Max(1, lines.Count)];
        var offset = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            starts[i] = offset;
            offset += lines[i].Length + 1;
        }

        return starts;
    }

    private static (int[] Starts, int[] Ends) ComputeLineTokenRanges(
        IReadOnlyList<int> lineStarts,
        IReadOnlyList<string> lines,
        IReadOnlyList<IDEEditorXmlSyntaxToken> tokens)
    {
        var starts = new int[Math.Max(1, lines.Count)];
        var ends = new int[Math.Max(1, lines.Count)];
        var tokenIndex = 0;
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var lineStart = lineStarts[lineIndex];
            var lineEnd = lineStart + lines[lineIndex].Length;
            while (tokenIndex < tokens.Count && tokens[tokenIndex].End <= lineStart)
            {
                tokenIndex++;
            }

            starts[lineIndex] = tokenIndex;
            var lineTokenEnd = tokenIndex;
            while (lineTokenEnd < tokens.Count && tokens[lineTokenEnd].Start < lineEnd)
            {
                lineTokenEnd++;
            }

            ends[lineIndex] = lineTokenEnd;
        }

        return (starts, ends);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static Color ResolveMiniatureColor(IDEEditorXmlSyntaxTokenKind kind)
    {
        return kind switch
        {
            IDEEditorXmlSyntaxTokenKind.ControlTypeName => new Color(139, 204, 255, 150),
            IDEEditorXmlSyntaxTokenKind.PropertyName => new Color(255, 203, 111, 150),
            IDEEditorXmlSyntaxTokenKind.ElementName => new Color(139, 204, 255, 120),
            IDEEditorXmlSyntaxTokenKind.Delimiter or IDEEditorXmlSyntaxTokenKind.Equals => new Color(102, 161, 218, 150),
            IDEEditorXmlSyntaxTokenKind.String => new Color(183, 221, 151, 150),
            IDEEditorXmlSyntaxTokenKind.Comment or IDEEditorXmlSyntaxTokenKind.CData => new Color(106, 153, 85, 125),
            IDEEditorXmlSyntaxTokenKind.NamespaceDeclaration or IDEEditorXmlSyntaxTokenKind.ProcessingInstruction or IDEEditorXmlSyntaxTokenKind.Declaration => new Color(146, 169, 191, 135),
            _ => new Color(138, 160, 181, 120)
        };
    }

    private static LayoutRect GetContentRect(LayoutRect slot)
    {
        return new LayoutRect(
            slot.X + HorizontalPadding,
            slot.Y + VerticalPadding,
            MathF.Max(0f, slot.Width - (HorizontalPadding * 2f)),
            MathF.Max(0f, slot.Height - (VerticalPadding * 2f)));
    }

    private float ResolveMinimapVerticalOffset(LayoutRect content)
    {
        var documentHeight = _lines.Count * MiniatureLineHeight;
        var maxOffset = MathF.Max(0f, documentHeight - content.Height);
        if (maxOffset <= 0f)
        {
            return 0f;
        }

        var maxEditorOffset = ResolveEditorMaxVerticalOffset();
        if (maxEditorOffset <= 0f)
        {
            return 0f;
        }

        var trackRatio = Math.Clamp(MathF.Max(0f, EditorVerticalOffset) / maxEditorOffset, 0f, 1f);
        return trackRatio * maxOffset;
    }

    private float ResolveEditorVerticalOffsetFromPointer(LayoutRect content, Vector2 pointer)
    {
        var maxEditorOffset = ResolveEditorMaxVerticalOffset();
        if (maxEditorOffset <= 0f || content.Height <= 0f)
        {
            return 0f;
        }

        var clampedY = Math.Clamp(pointer.Y, content.Y, content.Y + content.Height);
        var trackRatio = Math.Clamp((clampedY - content.Y) / content.Height, 0f, 1f);
        return trackRatio * maxEditorOffset;
    }

    private float ResolveEditorMaxVerticalOffset()
    {
        var editorLineHeight = MathF.Max(1f, EditorEstimatedLineHeight);
        var documentHeight = _lines.Count * editorLineHeight;
        return MathF.Max(0f, documentHeight - MathF.Max(0f, EditorViewportHeight));
    }

    private static bool Contains(LayoutRect rect, Vector2 point)
    {
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    private void ApplyMouseOverState(bool isMouseOver)
    {
        if (IsMouseOver == isMouseOver)
        {
            return;
        }

        IsMouseOver = isMouseOver;
        InvalidateVisual();
    }

    private static float MoveTowards(float current, float target, float delta)
    {
        if (current < target)
        {
            return MathF.Min(target, current + delta);
        }

        if (current > target)
        {
            return MathF.Max(target, current - delta);
        }

        return current;
    }

}

public sealed class IDEEditorMinimapNavigateEventArgs : EventArgs
{
    public IDEEditorMinimapNavigateEventArgs(int lineNumber)
        : this(lineNumber, 0f)
    {
    }

    public IDEEditorMinimapNavigateEventArgs(int lineNumber, float verticalOffset)
    {
        LineNumber = Math.Max(1, lineNumber);
        VerticalOffset = MathF.Max(0f, verticalOffset);
    }

    public int LineNumber { get; }

    public float VerticalOffset { get; }
}
