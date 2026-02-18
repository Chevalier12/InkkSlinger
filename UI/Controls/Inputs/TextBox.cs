using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class TextBox : Control, IRenderDirtyBoundsHintProvider
{
    private static readonly Lazy<Style> DefaultTextBoxStyle = new(BuildDefaultTextBoxStyle);

    public static readonly RoutedEvent TextChangedEvent =
        new(nameof(TextChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is TextBox textBox)
                    {
                        textBox.OnTextPropertyChanged(args.NewValue as string ?? string.Empty);
                    }
                },
                coerceValueCallback: static (dependencyObject, value) =>
                {
                    var text = value as string ?? string.Empty;
                    if (dependencyObject is TextBox textBox && textBox.MaxLength > 0 && text.Length > textBox.MaxLength)
                    {
                        return text[..textBox.MaxLength];
                    }

                    return text;
                }));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(TextBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(TextBox),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(TextBox),
            new FrameworkPropertyMetadata(new Color(28, 28, 28), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(TextBox),
            new FrameworkPropertyMetadata(new Color(162, 162, 162), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                new Thickness(8f, 5f, 8f, 5f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(
            nameof(TextWrapping),
            typeof(TextWrapping),
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                TextWrapping.Wrap,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                ScrollBarVisibility.Auto,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                ScrollBarVisibility.Auto,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaretBrushProperty =
        DependencyProperty.Register(
            nameof(CaretBrush),
            typeof(Color),
            typeof(TextBox),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionBrushProperty =
        DependencyProperty.Register(
            nameof(SelectionBrush),
            typeof(Color),
            typeof(TextBox),
            new FrameworkPropertyMetadata(new Color(66, 124, 211, 180), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(TextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxLengthProperty =
        DependencyProperty.Register(
            nameof(MaxLength),
            typeof(int),
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is TextBox textBox)
                    {
                        textBox.CoerceTextToMaxLength();
                    }
                },
                coerceValueCallback: static (_, value) => value is int length && length >= 0 ? length : 0));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(TextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsFocusedProperty =
        DependencyProperty.Register(
            nameof(IsFocused),
            typeof(bool),
            typeof(TextBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly TextEditingBuffer _editor = new();
    private bool _isUpdatingTextFromEditor;
    private bool _isDraggingVerticalThumb;
    private bool _isDraggingHorizontalThumb;
    private bool _isSelectingWithPointer;
    private float _verticalThumbDragOffset;
    private float _horizontalThumbDragOffset;
    private bool _hasPendingTextSync;
    private float _pendingTextSyncSeconds;
    private float _secondsSinceLastTextMutation = float.MaxValue;
    private string? _pendingTextSnapshot;
    private bool _hasPendingEnsureCaretVisible;
    private float _caretBlinkSeconds;
    private bool _isCaretVisible = true;
    private float _horizontalOffset;
    private float _verticalOffset;
    private float _preferredCaretX = -1f;
    private int _textVersion;
    private bool _hasLayoutCache;
    private int _layoutCacheTextVersion = -1;
    private float _layoutCacheWidth = float.NaN;
    private TextWrapping _layoutCacheWrapping = TextWrapping.Wrap;
    private SpriteFont? _layoutCacheFont;
    private LayoutResult _layoutCache;
    private readonly Dictionary<char, float> _glyphWidthCache = new();
    private readonly Dictionary<int, float[]> _linePrefixWidthCache = new();
    private readonly Dictionary<int, string> _lineTextCache = new();
    private bool _hasVisibleTextBatchCache;
    private int _visibleTextBatchCacheTextVersion = -1;
    private int _visibleTextBatchCacheFirstLine = -1;
    private int _visibleTextBatchCacheLastLine = -1;
    private int _visibleTextBatchCacheTotalLineCount = -1;
    private int _visibleTextBatchCacheFirstLineStartIndex = -1;
    private int _visibleTextBatchCacheLastLineStartIndex = -1;
    private string _visibleTextBatchCache = string.Empty;
    private bool _hasVirtualWrapCache;
    private int _virtualWrapCacheTextVersion = -1;
    private float _virtualWrapCacheWidth = float.NaN;
    private SpriteFont? _virtualWrapCacheFont;
    private readonly VirtualWrapTextSnapshot _virtualWrapText = new();
    private readonly List<WrappedLineCheckpoint> _virtualWrapCheckpoints = new();
    private readonly Dictionary<int, LayoutLine> _virtualWrapLineCache = new();
    private bool _virtualWrapHasExactLineCount;
    private int _virtualWrapExactLineCount = 1;
    private float _virtualWrapEstimatedLineCount = 1f;
    private float _virtualWrapAverageCharsPerLine = 48f;
    private float _virtualWrapMaxObservedLineWidth;
    private int _perfCommitCount;
    private int _perfDeferredSyncScheduledCount;
    private int _perfDeferredSyncFlushCount;
    private int _perfImmediateSyncCount;
    private int _perfIncrementalVirtualEditSuccessCount;
    private int _perfIncrementalVirtualEditFallbackCount;
    private int _perfIncrementalNoWrapEditAttemptCount;
    private int _perfIncrementalNoWrapEditSuccessCount;
    private int _perfLayoutCacheHitCount;
    private int _perfLayoutCacheMissCount;
    private int _perfViewportLayoutBuildCount;
    private int _perfFullLayoutBuildCount;
    private int _perfVirtualRangeBuildCount;
    private int _perfVirtualLineBuildCount;
    private long _perfTextSyncTicks;
    private int _perfInputMutationSampleCount;
    private long _perfInputMutationTotalTicks;
    private long _perfInputMutationMaxTicks;
    private long _perfLastInputMutationTicks;
    private long _perfLastInputEditTicks;
    private long _perfLastInputCommitTicks;
    private long _perfLastInputEnsureCaretTicks;
    private int _perfRenderSampleCount;
    private long _perfRenderTotalTicks;
    private long _perfRenderMaxTicks;
    private long _perfLastRenderTotalTicks;
    private long _perfLastRenderViewportTicks;
    private long _perfLastRenderSelectionTicks;
    private long _perfLastRenderTextTicks;
    private long _perfLastRenderCaretTicks;
    private int _perfViewportStateSampleCount;
    private int _perfViewportStateCacheHitCount;
    private int _perfViewportStateCacheMissCount;
    private long _perfViewportStateTotalTicks;
    private long _perfViewportStateMaxTicks;
    private long _perfLastViewportStateTicks;
    private int _perfEnsureCaretSampleCount;
    private int _perfEnsureCaretFastPathHitCount;
    private int _perfEnsureCaretFastPathMissCount;
    private long _perfEnsureCaretTotalTicks;
    private long _perfEnsureCaretMaxTicks;
    private long _perfLastEnsureCaretTotalTicks;
    private long _perfLastEnsureCaretViewportTicks;
    private long _perfLastEnsureCaretLineLookupTicks;
    private long _perfLastEnsureCaretWidthTicks;
    private long _perfLastEnsureCaretOffsetAdjustTicks;
    private bool _hasCaretLineHint;
    private int _caretLineHintIndex;
    private int _caretLineHintStart;
    private int _caretLineHintEnd;
    private bool _hasViewportLayoutCache;
    private int _viewportLayoutCacheTextVersion = -1;
    private LayoutRect _viewportLayoutCacheInnerRect;
    private ScrollBarVisibility _viewportLayoutCacheHorizontalVisibility;
    private ScrollBarVisibility _viewportLayoutCacheVerticalVisibility;
    private bool _hasPreviousScrollBarResolution;
    private bool _previousShowHorizontalScrollBar;
    private bool _previousShowVerticalScrollBar;
    private TextViewportLayoutState _viewportLayoutCache;
    private bool _hasPendingRenderDirtyBoundsHint;
    private bool _preserveRenderDirtyBoundsHint;
    private LayoutRect _pendingRenderDirtyBoundsHint;
    private const int VirtualWrapCheckpointInterval = 128;
    private const int VirtualWrapMinCacheLines = 64;
    private const int VirtualWrapTrimThreshold = 4096;
    private const int VirtualWrapTrimPadding = 512;
    private const int DeferredTextSyncLengthThreshold = 2048;
    private const float DeferredTextSyncDelaySeconds = 0.1f;
    private const float DeferredTextSyncIdleSeconds = 0.75f;
    private const float NativeScrollBarThickness = 16f;

    public TextBox()
    {
        _editor.SetText(Text, preserveCaret: false);
    }

    public event EventHandler<RoutedSimpleEventArgs> TextChanged
    {
        add => AddHandler(TextChangedEvent, value);
        remove => RemoveHandler(TextChangedEvent, value);
    }

    public string Text
    {
        get
        {
            if (_hasPendingTextSync)
            {
                if (_pendingTextSnapshot == null)
                {
                    _pendingTextSnapshot = _editor.Text;
                }

                return _pendingTextSnapshot;
            }

            return GetValue<string>(TextProperty) ?? string.Empty;
        }

        set => SetValue(TextProperty, value);
    }

    public SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => GetValue<TextWrapping>(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    public Color CaretBrush
    {
        get => GetValue<Color>(CaretBrushProperty);
        set => SetValue(CaretBrushProperty, value);
    }

    public Color SelectionBrush
    {
        get => GetValue<Color>(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public int MaxLength
    {
        get => GetValue<int>(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsFocused
    {
        get => GetValue<bool>(IsFocusedProperty);
        private set => SetValue(IsFocusedProperty, value);
    }

    internal bool IsRenderCacheStable => !IsFocused && Selection.IsEmpty;

    public int CaretIndex => _editor.CaretIndex;

    public int TextLength => _editor.Length;

    public int LogicalLineCount => _editor.LogicalLineCount;

    public TextBoxPerformanceSnapshot GetPerformanceSnapshot()
    {
        var syncMilliseconds = TicksToMilliseconds(_perfTextSyncTicks);
        return new TextBoxPerformanceSnapshot(
            _perfCommitCount,
            _perfDeferredSyncScheduledCount,
            _perfDeferredSyncFlushCount,
            _perfImmediateSyncCount,
            _perfIncrementalNoWrapEditAttemptCount,
            _perfIncrementalNoWrapEditSuccessCount,
            _perfIncrementalVirtualEditSuccessCount,
            _perfIncrementalVirtualEditFallbackCount,
            _perfLayoutCacheHitCount,
            _perfLayoutCacheMissCount,
            _perfViewportLayoutBuildCount,
            _perfFullLayoutBuildCount,
            _perfVirtualRangeBuildCount,
            _perfVirtualLineBuildCount,
            syncMilliseconds,
            _perfInputMutationSampleCount,
            TicksToMilliseconds(_perfLastInputMutationTicks),
            TicksToMilliseconds(_perfLastInputEditTicks),
            TicksToMilliseconds(_perfLastInputCommitTicks),
            TicksToMilliseconds(_perfLastInputEnsureCaretTicks),
            AverageTicksToMilliseconds(_perfInputMutationTotalTicks, _perfInputMutationSampleCount),
            TicksToMilliseconds(_perfInputMutationMaxTicks),
            _perfRenderSampleCount,
            TicksToMilliseconds(_perfLastRenderTotalTicks),
            TicksToMilliseconds(_perfLastRenderViewportTicks),
            TicksToMilliseconds(_perfLastRenderSelectionTicks),
            TicksToMilliseconds(_perfLastRenderTextTicks),
            TicksToMilliseconds(_perfLastRenderCaretTicks),
            AverageTicksToMilliseconds(_perfRenderTotalTicks, _perfRenderSampleCount),
            TicksToMilliseconds(_perfRenderMaxTicks),
            _perfViewportStateSampleCount,
            _perfViewportStateCacheHitCount,
            _perfViewportStateCacheMissCount,
            TicksToMilliseconds(_perfLastViewportStateTicks),
            AverageTicksToMilliseconds(_perfViewportStateTotalTicks, _perfViewportStateSampleCount),
            TicksToMilliseconds(_perfViewportStateMaxTicks),
            _perfEnsureCaretSampleCount,
            _perfEnsureCaretFastPathHitCount,
            _perfEnsureCaretFastPathMissCount,
            TicksToMilliseconds(_perfLastEnsureCaretTotalTicks),
            TicksToMilliseconds(_perfLastEnsureCaretViewportTicks),
            TicksToMilliseconds(_perfLastEnsureCaretLineLookupTicks),
            TicksToMilliseconds(_perfLastEnsureCaretWidthTicks),
            TicksToMilliseconds(_perfLastEnsureCaretOffsetAdjustTicks),
            AverageTicksToMilliseconds(_perfEnsureCaretTotalTicks, _perfEnsureCaretSampleCount),
            TicksToMilliseconds(_perfEnsureCaretMaxTicks),
            _editor.GetDiagnostics());
    }

    public void ResetPerformanceSnapshot()
    {
        _perfCommitCount = 0;
        _perfDeferredSyncScheduledCount = 0;
        _perfDeferredSyncFlushCount = 0;
        _perfImmediateSyncCount = 0;
        _perfIncrementalNoWrapEditAttemptCount = 0;
        _perfIncrementalNoWrapEditSuccessCount = 0;
        _perfIncrementalVirtualEditSuccessCount = 0;
        _perfIncrementalVirtualEditFallbackCount = 0;
        _perfLayoutCacheHitCount = 0;
        _perfLayoutCacheMissCount = 0;
        _perfViewportLayoutBuildCount = 0;
        _perfFullLayoutBuildCount = 0;
        _perfVirtualRangeBuildCount = 0;
        _perfVirtualLineBuildCount = 0;
        _perfTextSyncTicks = 0L;
        _perfInputMutationSampleCount = 0;
        _perfInputMutationTotalTicks = 0L;
        _perfInputMutationMaxTicks = 0L;
        _perfLastInputMutationTicks = 0L;
        _perfLastInputEditTicks = 0L;
        _perfLastInputCommitTicks = 0L;
        _perfLastInputEnsureCaretTicks = 0L;
        _perfRenderSampleCount = 0;
        _perfRenderTotalTicks = 0L;
        _perfRenderMaxTicks = 0L;
        _perfLastRenderTotalTicks = 0L;
        _perfLastRenderViewportTicks = 0L;
        _perfLastRenderSelectionTicks = 0L;
        _perfLastRenderTextTicks = 0L;
        _perfLastRenderCaretTicks = 0L;
        _perfViewportStateSampleCount = 0;
        _perfViewportStateCacheHitCount = 0;
        _perfViewportStateCacheMissCount = 0;
        _perfViewportStateTotalTicks = 0L;
        _perfViewportStateMaxTicks = 0L;
        _perfLastViewportStateTicks = 0L;
        _perfEnsureCaretSampleCount = 0;
        _perfEnsureCaretFastPathHitCount = 0;
        _perfEnsureCaretFastPathMissCount = 0;
        _perfEnsureCaretTotalTicks = 0L;
        _perfEnsureCaretMaxTicks = 0L;
        _perfLastEnsureCaretTotalTicks = 0L;
        _perfLastEnsureCaretViewportTicks = 0L;
        _perfLastEnsureCaretLineLookupTicks = 0L;
        _perfLastEnsureCaretWidthTicks = 0L;
        _perfLastEnsureCaretOffsetAdjustTicks = 0L;
        _editor.ResetDiagnostics();
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private static double AverageTicksToMilliseconds(long totalTicks, int sampleCount)
    {
        if (sampleCount <= 0)
        {
            return 0d;
        }

        return ((double)totalTicks * 1000d / Stopwatch.Frequency) / sampleCount;
    }

    private void RecordInputMutationTiming(long totalTicks, long editTicks, long commitTicks, long ensureCaretTicks)
    {
        if (totalTicks <= 0)
        {
            return;
        }

        _perfInputMutationSampleCount++;
        _perfInputMutationTotalTicks += totalTicks;
        _perfInputMutationMaxTicks = Math.Max(_perfInputMutationMaxTicks, totalTicks);
        _perfLastInputMutationTicks = totalTicks;
        _perfLastInputEditTicks = Math.Max(0, editTicks);
        _perfLastInputCommitTicks = Math.Max(0, commitTicks);
        _perfLastInputEnsureCaretTicks = Math.Max(0, ensureCaretTicks);
        TextBoxFrameworkDiagnostics.ObserveInputMutation(totalTicks, editTicks, commitTicks, ensureCaretTicks);
    }

    private void RecordRenderTiming(long totalTicks, long viewportTicks, long selectionTicks, long textTicks, long caretTicks)
    {
        if (totalTicks <= 0)
        {
            return;
        }

        _perfRenderSampleCount++;
        _perfRenderTotalTicks += totalTicks;
        _perfRenderMaxTicks = Math.Max(_perfRenderMaxTicks, totalTicks);
        _perfLastRenderTotalTicks = totalTicks;
        _perfLastRenderViewportTicks = Math.Max(0, viewportTicks);
        _perfLastRenderSelectionTicks = Math.Max(0, selectionTicks);
        _perfLastRenderTextTicks = Math.Max(0, textTicks);
        _perfLastRenderCaretTicks = Math.Max(0, caretTicks);
        TextBoxFrameworkDiagnostics.ObserveRender(totalTicks, viewportTicks, selectionTicks, textTicks, caretTicks);
    }

    private void RecordViewportStateTiming(long ticks, bool cacheHit)
    {
        if (ticks < 0)
        {
            return;
        }

        _perfViewportStateSampleCount++;
        if (cacheHit)
        {
            _perfViewportStateCacheHitCount++;
        }
        else
        {
            _perfViewportStateCacheMissCount++;
        }

        _perfViewportStateTotalTicks += ticks;
        _perfViewportStateMaxTicks = Math.Max(_perfViewportStateMaxTicks, ticks);
        _perfLastViewportStateTicks = ticks;
        TextBoxFrameworkDiagnostics.ObserveViewportState(ticks, cacheHit);
    }

    private void RecordEnsureCaretTiming(
        long totalTicks,
        long viewportTicks,
        long lineLookupTicks,
        long widthTicks,
        long offsetAdjustTicks,
        bool usedFastPath)
    {
        if (totalTicks <= 0)
        {
            return;
        }

        _perfEnsureCaretSampleCount++;
        if (usedFastPath)
        {
            _perfEnsureCaretFastPathHitCount++;
        }
        else
        {
            _perfEnsureCaretFastPathMissCount++;
        }

        _perfEnsureCaretTotalTicks += totalTicks;
        _perfEnsureCaretMaxTicks = Math.Max(_perfEnsureCaretMaxTicks, totalTicks);
        _perfLastEnsureCaretTotalTicks = totalTicks;
        _perfLastEnsureCaretViewportTicks = Math.Max(0, viewportTicks);
        _perfLastEnsureCaretLineLookupTicks = Math.Max(0, lineLookupTicks);
        _perfLastEnsureCaretWidthTicks = Math.Max(0, widthTicks);
        _perfLastEnsureCaretOffsetAdjustTicks = Math.Max(0, offsetAdjustTicks);
        TextBoxFrameworkDiagnostics.ObserveEnsureCaret(
            totalTicks,
            viewportTicks,
            lineLookupTicks,
            widthTicks,
            offsetAdjustTicks,
            usedFastPath);
    }

    private void ResetCaretLineHint()
    {
        _hasCaretLineHint = false;
        _caretLineHintIndex = 0;
        _caretLineHintStart = 0;
        _caretLineHintEnd = 0;
    }

    private void UpdateCaretLineHint(int lineIndex, LayoutLine line)
    {
        _hasCaretLineHint = true;
        _caretLineHintIndex = Math.Max(0, lineIndex);
        _caretLineHintStart = Math.Max(0, line.StartIndex);
        _caretLineHintEnd = Math.Max(_caretLineHintStart, line.StartIndex + line.Length);
    }

    public TextSelection Selection => _editor.Selection;
    protected internal float HorizontalOffsetForTesting => _horizontalOffset;
    protected internal float VerticalOffsetForTesting => _verticalOffset;

    internal void PrimeLayoutCacheForTests(float contentWidth)
    {
        _ = BuildLayoutLines(contentWidth);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        TextBoxFrameworkDiagnostics.Flush();
        _secondsSinceLastTextMutation += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_hasPendingEnsureCaretVisible)
        {
            EnsureCaretVisible();
            _hasPendingEnsureCaretVisible = false;
        }

        if (_hasPendingTextSync)
        {
            _pendingTextSyncSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (ShouldFlushPendingTextSyncInUpdate())
            {
                CommitPendingTextSync();
            }
        }

        if (!IsEnabled || !IsFocused)
        {
            return;
        }

        _caretBlinkSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_caretBlinkSeconds >= 0.53f)
        {
            _caretBlinkSeconds = 0f;
            _isCaretVisible = !_isCaretVisible;
            InvalidateCaretVisualRegion();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        var border = BorderThickness * 2f;
        var padding = Padding;

        var textWidth = 0f;
        var textHeight = GetLineHeight();
        if (Font != null)
        {
            var contentWidth = TextWrapping == TextWrapping.NoWrap
                ? float.PositiveInfinity
                : MathF.Max(0f, availableSize.X - padding.Horizontal - border);
            var layout = BuildLayoutLines(contentWidth);
            textWidth = layout.MaxLineWidth;
            var lineHeight = GetLineHeight();
            textHeight = MathF.Max(lineHeight, GetLineCount(layout) * lineHeight);
        }

        var minWidth = padding.Horizontal + border + 32f;
        var minHeight = padding.Vertical + border + MathF.Max(textHeight, 14f);
        if (VerticalScrollBarVisibility == ScrollBarVisibility.Visible)
        {
            minWidth += NativeScrollBarThickness;
        }

        if (HorizontalScrollBarVisibility == ScrollBarVisibility.Visible)
        {
            minHeight += NativeScrollBarThickness;
        }

        desired.X = MathF.Max(desired.X, MathF.Max(minWidth, padding.Horizontal + border + textWidth));
        desired.Y = MathF.Max(desired.Y, minHeight);
        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var renderStart = Stopwatch.GetTimestamp();
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        if (Font == null)
        {
            if (BorderThickness > 0f)
            {
                UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
            }

            return;
        }

        long viewportTicks;
        long selectionTicks = 0L;
        long textTicks = 0L;
        long caretTicks = 0L;
        var viewportStart = Stopwatch.GetTimestamp();
        var view = BuildTextViewportState();
        viewportTicks = Stopwatch.GetTimestamp() - viewportStart;
        ClampOffsets(view.Layout, view.ViewportRect);
        var lineHeight = GetLineHeight();
        var textY = view.ViewportRect.Y + GetTextRenderTopInset();
        var totalLineCount = GetLineCount(view.Layout);
        var firstVisibleLine = Math.Clamp((int)MathF.Floor(_verticalOffset / MathF.Max(1f, lineHeight)), 0, Math.Max(0, totalLineCount - 1));
        var lastVisibleLine = Math.Clamp(
            (int)MathF.Ceiling((_verticalOffset + view.ViewportRect.Height) / MathF.Max(1f, lineHeight)),
            0,
            Math.Max(0, totalLineCount - 1));

        UiDrawing.PushClip(spriteBatch, GetTextRenderClipRect(view.ViewportRect));
        try
        {
            if (_editor.HasSelection)
            {
                var selectionStartTicks = Stopwatch.GetTimestamp();
                var selection = _editor.Selection;
                for (var lineIndex = firstVisibleLine; lineIndex <= lastVisibleLine; lineIndex++)
                {
                    var line = GetLine(view.Layout, lineIndex);
                    var lineStart = line.StartIndex;
                    var lineEnd = line.StartIndex + line.Length;
                    var selectionStart = Math.Max(selection.Start, lineStart);
                    var selectionEnd = Math.Min(selection.End, lineEnd);
                    if (selectionStart >= selectionEnd)
                    {
                        continue;
                    }

                    var localStart = selectionStart - lineStart;
                    var localEnd = selectionEnd - lineStart;
                    var startX = view.ViewportRect.X + GetLineWidthAtColumn(view.Layout, lineIndex, localStart) - _horizontalOffset;
                    var endX = view.ViewportRect.X + GetLineWidthAtColumn(view.Layout, lineIndex, localEnd) - _horizontalOffset;
                    var selectionY = textY + (lineIndex * lineHeight) - _verticalOffset;
                    UiDrawing.DrawFilledRect(
                        spriteBatch,
                        new LayoutRect(startX, selectionY, MathF.Max(0f, endX - startX), lineHeight),
                        SelectionBrush,
                        Opacity);
                }

                selectionTicks = Stopwatch.GetTimestamp() - selectionStartTicks;
            }

            var textStartTicks = Stopwatch.GetTimestamp();
            if (CanUseVisibleTextBatchPath(firstVisibleLine, lastVisibleLine))
            {
                var batchText = GetVisibleTextBatch(view.Layout, firstVisibleLine, lastVisibleLine);
                if (batchText.Length > 0)
                {
                    DrawTextLine(
                        spriteBatch,
                        batchText,
                        new Vector2(view.ViewportRect.X - _horizontalOffset, textY + (firstVisibleLine * lineHeight) - _verticalOffset),
                        Foreground * Opacity);
                }
            }
            else
            {
                for (var lineIndex = firstVisibleLine; lineIndex <= lastVisibleLine; lineIndex++)
                {
                    var lineText = GetLineText(view.Layout, lineIndex);
                    if (lineText.Length == 0)
                    {
                        continue;
                    }

                    DrawTextLine(
                        spriteBatch,
                        lineText,
                        new Vector2(view.ViewportRect.X - _horizontalOffset, textY + (lineIndex * lineHeight) - _verticalOffset),
                        Foreground * Opacity);
                }
            }
            textTicks = Stopwatch.GetTimestamp() - textStartTicks;

            if (ShouldDrawCaret() && TryGetCaretRenderRect(view, lineHeight, textY, requireVisibleCaret: true, out var caretRect))
            {
                var caretStartTicks = Stopwatch.GetTimestamp();
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    caretRect,
                    CaretBrush,
                    Opacity);
                caretTicks = Stopwatch.GetTimestamp() - caretStartTicks;
            }
        }
        finally
        {
            UiDrawing.PopClip(spriteBatch);
        }

        DrawNativeScrollBars(spriteBatch, view);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
        }

        var renderTotalTicks = Stopwatch.GetTimestamp() - renderStart;
        RecordRenderTiming(renderTotalTicks, viewportTicks, selectionTicks, textTicks, caretTicks);
    }












    protected override Style? GetFallbackStyle()
    {
        return DefaultTextBoxStyle.Value;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, FontProperty) ||
            ReferenceEquals(args.Property, TextWrappingProperty) ||
            ReferenceEquals(args.Property, PaddingProperty) ||
            ReferenceEquals(args.Property, BorderThicknessProperty))
        {
            if (ReferenceEquals(args.Property, FontProperty))
            {
                _glyphWidthCache.Clear();
            }

            InvalidateLayoutCache();
        }
    }

    private static bool TryMapTextInput(char character, out string text)
    {
        text = string.Empty;

        if (character == '\r' || character == '\n' || character == '\t')
        {
            return false;
        }

        if (char.IsControl(character))
        {
            return false;
        }

        text = character.ToString();
        return true;
    }

    private void OnTextPropertyChanged(string text)
    {
        if (_isUpdatingTextFromEditor)
        {
            return;
        }

        ClearPendingTextSync();
        _editor.SetText(NormalizeLineEndings(text), preserveCaret: true);
        _textVersion++;
        InvalidateLayoutCache();
        EnsureCaretVisible();
        RaiseTextChangedEvent();
        InvalidateVisual();
    }

    private void CommitEditorText(TextEditDelta editDelta)
    {
        var previousLayout = _layoutCache;
        var previousLayoutWidth = _layoutCacheWidth;
        var previousLayoutWrapping = _layoutCacheWrapping;
        var previousLayoutFont = _layoutCacheFont;
        var hadPreviousLayout = _hasLayoutCache;

        _perfCommitCount++;
        _textVersion++;

        var appliedNoWrapEdit = false;
        var attemptedNoWrapEdit = false;
        var appliedVirtualWrapEdit = false;
        var usedVirtualWrapFallback = false;
        if (hadPreviousLayout &&
            previousLayoutWrapping == TextWrapping.NoWrap &&
            ReferenceEquals(previousLayoutFont, Font))
        {
            attemptedNoWrapEdit = true;
            _perfIncrementalNoWrapEditAttemptCount++;
            appliedNoWrapEdit = TryApplyIncrementalNoWrapLayoutEdit(editDelta, previousLayout, previousLayoutWidth);
            if (appliedNoWrapEdit)
            {
                _perfIncrementalNoWrapEditSuccessCount++;
            }
        }

        if (appliedNoWrapEdit)
        {
            InvalidateViewportLayoutCache();
            InvalidateVisibleTextBatchCache();
        }
        else
        {
            InvalidateNonVirtualLayoutCache();
            if (!TryApplyIncrementalVirtualWrapEdit(editDelta, out var firstInvalidLine))
            {
                usedVirtualWrapFallback = true;
                _perfIncrementalVirtualEditFallbackCount++;
                InvalidateLayoutCachesAfterInternalEdit();
                InvalidateVirtualWrapCache();
            }
            else
            {
                appliedVirtualWrapEdit = true;
                _perfIncrementalVirtualEditSuccessCount++;
                ResetCaretLineHint();
                InvalidateViewportLayoutCache();
                InvalidateLineCachesFromIndex(firstInvalidLine);
            }
        }

        var deferredSync = ShouldDeferTextSync(editDelta);
        if (deferredSync)
        {
            SchedulePendingTextSync();
        }
        else
        {
            CommitTextSyncNow();
            ClearPendingTextSync();
        }

        TextBoxFrameworkDiagnostics.ObserveCommit(
            deferredSync,
            attemptedNoWrapEdit,
            appliedNoWrapEdit,
            appliedVirtualWrapEdit,
            usedVirtualWrapFallback);

        RaiseTextChangedEvent();
        InvalidateVisual();
    }

    private void RaiseTextChangedEvent()
    {
        RaiseRoutedEvent(TextChangedEvent, new RoutedSimpleEventArgs(TextChangedEvent));
    }

    private bool InsertTextIntoEditor(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        text = NormalizeLineEndings(text);

        if (MaxLength <= 0)
        {
            return _editor.InsertText(text);
        }

        var selection = _editor.Selection;
        var remaining = MaxLength - (_editor.Length - selection.Length);
        if (remaining <= 0)
        {
            return false;
        }

        if (text.Length > remaining)
        {
            text = text[..remaining];
        }

        return _editor.InsertText(text);
    }

    private LayoutRect GetInnerContentRect()
    {
        var slot = LayoutSlot;
        var horizontalInset = Padding.Horizontal + (BorderThickness * 2f);
        var verticalInset = Padding.Vertical + (BorderThickness * 2f);

        return new LayoutRect(
            slot.X + Padding.Left + BorderThickness,
            slot.Y + Padding.Top + BorderThickness,
            MathF.Max(0f, slot.Width - horizontalInset),
            MathF.Max(0f, slot.Height - verticalInset));
    }

    private LayoutResult BuildViewportLayout(float contentWidth, float viewportHeight)
    {
        _perfViewportLayoutBuildCount++;
        if (CanUseVirtualWrappedLayout(contentWidth))
        {
            return BuildVirtualWrappedLayout(contentWidth, viewportHeight);
        }

        return BuildLayoutLines(contentWidth);
    }

    private bool CanUseVirtualWrappedLayout(float contentWidth)
    {
        // Virtual wrap currently produces incorrect line realization/extent in
        // resize scenarios (e.g. wrapped lines not reflowing as width changes).
        // Fall back to the full wrapping path for correctness.
        return false;
    }

    private TextViewportState BuildTextViewportState()
    {
        var timingStartTicks = Stopwatch.GetTimestamp();
        var innerRect = GetInnerContentRect();
        if (_hasViewportLayoutCache &&
            _viewportLayoutCacheTextVersion == _textVersion &&
            _viewportLayoutCacheHorizontalVisibility == HorizontalScrollBarVisibility &&
            _viewportLayoutCacheVerticalVisibility == VerticalScrollBarVisibility &&
            AreRectsClose(_viewportLayoutCacheInnerRect, innerRect))
        {
            var cachedState = ComposeViewportState(_viewportLayoutCache);
            RecordViewportStateTiming(Stopwatch.GetTimestamp() - timingStartTicks, cacheHit: true);
            return cachedState;
        }

        var showHorizontal = ResolveInitialHorizontalScrollBarVisibility();
        var showVertical = ResolveInitialVerticalScrollBarVisibility();

        for (var i = 0; i < 3; i++)
        {
            var viewportWidth = MathF.Max(0f, innerRect.Width - (showVertical ? NativeScrollBarThickness : 0f));
            var viewportHeight = MathF.Max(0f, innerRect.Height - (showHorizontal ? NativeScrollBarThickness : 0f));
            var viewportRect = new LayoutRect(innerRect.X, innerRect.Y, viewportWidth, viewportHeight);
            var layout = BuildViewportLayout(viewportRect.Width, viewportRect.Height);

            var needHorizontal = ShouldShowHorizontalBar(layout, viewportRect);
            var needVertical = ShouldShowVerticalBar(layout, viewportRect);
            if (needHorizontal == showHorizontal && needVertical == showVertical)
            {
                var finalized = BuildTextViewportLayoutState(innerRect, needHorizontal, needVertical, layout);
                CacheViewportLayoutState(finalized, innerRect);
                var resolved = ComposeViewportState(finalized);
                RecordViewportStateTiming(Stopwatch.GetTimestamp() - timingStartTicks, cacheHit: false);
                return resolved;
            }

            showHorizontal = needHorizontal;
            showVertical = needVertical;
        }

        var fallbackViewport = new LayoutRect(
            innerRect.X,
            innerRect.Y,
            MathF.Max(0f, innerRect.Width - (showVertical ? NativeScrollBarThickness : 0f)),
            MathF.Max(0f, innerRect.Height - (showHorizontal ? NativeScrollBarThickness : 0f)));
        var fallbackLayout = BuildViewportLayout(fallbackViewport.Width, fallbackViewport.Height);
        var fallback = BuildTextViewportLayoutState(innerRect, showHorizontal, showVertical, fallbackLayout);
        CacheViewportLayoutState(fallback, innerRect);
        var fallbackState = ComposeViewportState(fallback);
        RecordViewportStateTiming(Stopwatch.GetTimestamp() - timingStartTicks, cacheHit: false);
        return fallbackState;
    }

    private TextViewportLayoutState BuildTextViewportLayoutState(
        LayoutRect innerRect,
        bool showHorizontal,
        bool showVertical,
        LayoutResult layout)
    {
        var viewportRect = new LayoutRect(
            innerRect.X,
            innerRect.Y,
            MathF.Max(0f, innerRect.Width - (showVertical ? NativeScrollBarThickness : 0f)),
            MathF.Max(0f, innerRect.Height - (showHorizontal ? NativeScrollBarThickness : 0f)));

        var maxHorizontalOffset = GetMaxHorizontalOffset(layout, viewportRect);
        var maxVerticalOffset = GetMaxVerticalOffset(layout, viewportRect);
        var verticalTrackRect = showVertical
            ? new LayoutRect(viewportRect.X + viewportRect.Width, viewportRect.Y, NativeScrollBarThickness, viewportRect.Height)
            : default;
        var horizontalTrackRect = showHorizontal
            ? new LayoutRect(viewportRect.X, viewportRect.Y + viewportRect.Height, viewportRect.Width, NativeScrollBarThickness)
            : default;

        return new TextViewportLayoutState(
            viewportRect,
            layout,
            showHorizontal,
            showVertical,
            horizontalTrackRect,
            verticalTrackRect,
            maxHorizontalOffset,
            maxVerticalOffset);
    }

    private TextViewportState ComposeViewportState(TextViewportLayoutState layoutState)
    {
        var verticalThumbRect = layoutState.ShowVerticalScrollBar
            ? GetScrollThumbRect(layoutState.VerticalTrackRect, isVertical: true, layoutState.MaxVerticalOffset, _verticalOffset, layoutState.ViewportRect.Height)
            : default;
        var horizontalThumbRect = layoutState.ShowHorizontalScrollBar
            ? GetScrollThumbRect(layoutState.HorizontalTrackRect, isVertical: false, layoutState.MaxHorizontalOffset, _horizontalOffset, layoutState.ViewportRect.Width)
            : default;

        return new TextViewportState(
            layoutState.ViewportRect,
            layoutState.Layout,
            layoutState.ShowHorizontalScrollBar,
            layoutState.ShowVerticalScrollBar,
            layoutState.HorizontalTrackRect,
            layoutState.VerticalTrackRect,
            horizontalThumbRect,
            verticalThumbRect,
            layoutState.MaxHorizontalOffset,
            layoutState.MaxVerticalOffset);
    }

    private bool ShouldShowHorizontalBar(LayoutResult layout, LayoutRect viewportRect)
    {
        if (HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled)
        {
            return false;
        }

        if (HorizontalScrollBarVisibility == ScrollBarVisibility.Visible)
        {
            return true;
        }

        if (HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden)
        {
            return false;
        }

        return GetMaxHorizontalOffset(layout, viewportRect) > 0f;
    }

    private bool ShouldShowVerticalBar(LayoutResult layout, LayoutRect viewportRect)
    {
        if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
        {
            return false;
        }

        if (VerticalScrollBarVisibility == ScrollBarVisibility.Visible)
        {
            return true;
        }

        if (VerticalScrollBarVisibility == ScrollBarVisibility.Hidden)
        {
            return false;
        }

        return GetMaxVerticalOffset(layout, viewportRect) > 0f;
    }

    private int GetTextIndexFromPoint(Vector2 point)
    {
        if (Font == null || _editor.Length == 0)
        {
            return 0;
        }

        var view = BuildTextViewportState();
        var lineCount = GetLineCount(view.Layout);
        if (lineCount == 0)
        {
            return 0;
        }

        var lineHeight = GetLineHeight();
        var textY = view.ViewportRect.Y + GetTextRenderTopInset();
        var localY = MathF.Max(0f, point.Y - textY + _verticalOffset);
        var lineIndex = Math.Clamp((int)(localY / lineHeight), 0, lineCount - 1);
        var line = GetLine(view.Layout, lineIndex);
        var localX = MathF.Max(0f, point.X - view.ViewportRect.X + _horizontalOffset);
        var column = FindColumnFromX(view.Layout, lineIndex, localX);
        return line.StartIndex + column;
    }

    private float MeasureCharacterWidth(char ch)
    {
        if (Font == null && !FontStashTextRenderer.IsEnabled)
        {
            return 0f;
        }

        if (_glyphWidthCache.TryGetValue(ch, out var width))
        {
            return width;
        }

        width = FontStashTextRenderer.MeasureWidth(Font, ch.ToString());
        _glyphWidthCache[ch] = width;
        return width;
    }

    private void DrawTextLine(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        FontStashTextRenderer.DrawString(spriteBatch, Font, text, position, color);
    }

    private bool CanUseVisibleTextBatchPath(int firstVisibleLine, int lastVisibleLine)
    {
        if (firstVisibleLine < 0 || lastVisibleLine < firstVisibleLine)
        {
            return false;
        }

        var visibleLineCount = (lastVisibleLine - firstVisibleLine) + 1;
        return visibleLineCount > 1;
    }

    private string GetVisibleTextBatch(LayoutResult layout, int firstVisibleLine, int lastVisibleLine)
    {
        var totalLineCount = GetLineCount(layout);
        var hasVisibleLines = firstVisibleLine >= 0 &&
                              lastVisibleLine >= firstVisibleLine &&
                              firstVisibleLine < totalLineCount &&
                              lastVisibleLine < totalLineCount;
        var firstStartIndex = hasVisibleLines
            ? GetLine(layout, firstVisibleLine).StartIndex
            : -1;
        var lastStartIndex = hasVisibleLines
            ? GetLine(layout, lastVisibleLine).StartIndex
            : -1;

        if (_hasVisibleTextBatchCache &&
            _visibleTextBatchCacheTextVersion == _textVersion &&
            _visibleTextBatchCacheFirstLine == firstVisibleLine &&
            _visibleTextBatchCacheLastLine == lastVisibleLine &&
            _visibleTextBatchCacheTotalLineCount == totalLineCount &&
            _visibleTextBatchCacheFirstLineStartIndex == firstStartIndex &&
            _visibleTextBatchCacheLastLineStartIndex == lastStartIndex)
        {
            return _visibleTextBatchCache;
        }

        var visibleLineCount = Math.Max(0, (lastVisibleLine - firstVisibleLine) + 1);
        if (visibleLineCount == 0)
        {
            _visibleTextBatchCache = string.Empty;
            _visibleTextBatchCacheTextVersion = _textVersion;
            _visibleTextBatchCacheFirstLine = firstVisibleLine;
            _visibleTextBatchCacheLastLine = lastVisibleLine;
            _visibleTextBatchCacheTotalLineCount = totalLineCount;
            _visibleTextBatchCacheFirstLineStartIndex = firstStartIndex;
            _visibleTextBatchCacheLastLineStartIndex = lastStartIndex;
            _hasVisibleTextBatchCache = true;
            return _visibleTextBatchCache;
        }

        var builder = new StringBuilder(visibleLineCount * 48);
        for (var lineIndex = firstVisibleLine; lineIndex <= lastVisibleLine; lineIndex++)
        {
            var lineText = GetLineText(layout, lineIndex);
            if (lineText.Length > 0)
            {
                builder.Append(lineText);
            }

            if (lineIndex < lastVisibleLine)
            {
                builder.Append('\n');
            }
        }

        _visibleTextBatchCache = builder.ToString();
        _visibleTextBatchCacheTextVersion = _textVersion;
        _visibleTextBatchCacheFirstLine = firstVisibleLine;
        _visibleTextBatchCacheLastLine = lastVisibleLine;
        _visibleTextBatchCacheTotalLineCount = totalLineCount;
        _visibleTextBatchCacheFirstLineStartIndex = firstStartIndex;
        _visibleTextBatchCacheLastLineStartIndex = lastStartIndex;
        _hasVisibleTextBatchCache = true;
        return _visibleTextBatchCache;
    }

    private bool ShouldDrawCaret()
    {
        return IsEnabled && IsFocused && _isCaretVisible;
    }

    private void DrawNativeScrollBars(SpriteBatch spriteBatch, TextViewportState view)
    {
        if (view.ShowVerticalScrollBar)
        {
            UiDrawing.DrawFilledRect(spriteBatch, view.VerticalTrackRect, new Color(22, 22, 22), Opacity);
            UiDrawing.DrawRectStroke(spriteBatch, view.VerticalTrackRect, 1f, new Color(88, 88, 88), Opacity);
            var thumbColor = _isDraggingVerticalThumb
                ? new Color(145, 145, 145)
                : new Color(118, 118, 118);
            UiDrawing.DrawFilledRect(spriteBatch, view.VerticalThumbRect, thumbColor, Opacity);
            UiDrawing.DrawRectStroke(spriteBatch, view.VerticalThumbRect, 1f, new Color(88, 88, 88), Opacity);
        }

        if (view.ShowHorizontalScrollBar)
        {
            UiDrawing.DrawFilledRect(spriteBatch, view.HorizontalTrackRect, new Color(22, 22, 22), Opacity);
            UiDrawing.DrawRectStroke(spriteBatch, view.HorizontalTrackRect, 1f, new Color(88, 88, 88), Opacity);
            var thumbColor = _isDraggingHorizontalThumb
                ? new Color(145, 145, 145)
                : new Color(118, 118, 118);
            UiDrawing.DrawFilledRect(spriteBatch, view.HorizontalThumbRect, thumbColor, Opacity);
            UiDrawing.DrawRectStroke(spriteBatch, view.HorizontalThumbRect, 1f, new Color(88, 88, 88), Opacity);
        }
    }


    private void HandleScrollBarDrag(Vector2 pointer, TextViewportState view)
    {
        if (_isDraggingVerticalThumb)
        {
            var updated = DragScrollOffset(
                pointer.Y,
                _verticalThumbDragOffset,
                view.VerticalTrackRect,
                view.VerticalThumbRect,
                view.MaxVerticalOffset,
                isVertical: true);
            _verticalOffset = updated;
            InvalidateVisual();
            return;
        }

        if (_isDraggingHorizontalThumb)
        {
            var updated = DragScrollOffset(
                pointer.X,
                _horizontalThumbDragOffset,
                view.HorizontalTrackRect,
                view.HorizontalThumbRect,
                view.MaxHorizontalOffset,
                isVertical: false);
            _horizontalOffset = updated;
            InvalidateVisual();
        }
    }

    private float DragScrollOffset(
        float pointerAxis,
        float dragOffset,
        LayoutRect trackRect,
        LayoutRect thumbRect,
        float maxOffset,
        bool isVertical)
    {
        if (maxOffset <= 0f)
        {
            return 0f;
        }

        var trackStart = isVertical ? trackRect.Y : trackRect.X;
        var trackLength = isVertical ? trackRect.Height : trackRect.Width;
        var thumbLength = isVertical ? thumbRect.Height : thumbRect.Width;
        var travel = MathF.Max(1f, trackLength - thumbLength);
        var thumbStart = Math.Clamp(pointerAxis - trackStart - dragOffset, 0f, travel);
        var normalized = thumbStart / travel;
        return normalized * maxOffset;
    }

    private void EndScrollBarDrag()
    {
        _isDraggingVerticalThumb = false;
        _isDraggingHorizontalThumb = false;
        _verticalThumbDragOffset = 0f;
        _horizontalThumbDragOffset = 0f;
    }

    private static bool IsPointInside(Vector2 point, LayoutRect rect)
    {
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    private static LayoutRect GetScrollThumbRect(
        LayoutRect trackRect,
        bool isVertical,
        float maxOffset,
        float value,
        float viewportSize)
    {
        var trackLength = isVertical ? trackRect.Height : trackRect.Width;
        var minThumbLength = MathF.Min(trackLength, 10f);
        float thumbLength;
        if (maxOffset <= 0f)
        {
            thumbLength = trackLength;
        }
        else
        {
            thumbLength = MathF.Max(minThumbLength, trackLength * (viewportSize / (viewportSize + maxOffset)));
        }

        var travel = MathF.Max(0f, trackLength - thumbLength);
        var normalized = maxOffset > 0f ? Math.Clamp(value / maxOffset, 0f, 1f) : 0f;
        var thumbOffset = travel * normalized;

        if (isVertical)
        {
            return new LayoutRect(trackRect.X, trackRect.Y + thumbOffset, trackRect.Width, thumbLength);
        }

        return new LayoutRect(trackRect.X + thumbOffset, trackRect.Y, thumbLength, trackRect.Height);
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    public override void InvalidateVisual()
    {
        if (!_preserveRenderDirtyBoundsHint)
        {
            _hasPendingRenderDirtyBoundsHint = false;
        }

        base.InvalidateVisual();
    }

    private void ResetCaretBlink()
    {
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateCaretVisualRegion();
    }

    private void ScheduleEnsureCaretVisible()
    {
        _hasPendingEnsureCaretVisible = true;
    }

    private void InvalidateVisibleTextBatchCache()
    {
        _hasVisibleTextBatchCache = false;
        _visibleTextBatchCacheTextVersion = -1;
        _visibleTextBatchCacheFirstLine = -1;
        _visibleTextBatchCacheLastLine = -1;
        _visibleTextBatchCacheTotalLineCount = -1;
        _visibleTextBatchCacheFirstLineStartIndex = -1;
        _visibleTextBatchCacheLastLineStartIndex = -1;
        _visibleTextBatchCache = string.Empty;
    }

    bool IRenderDirtyBoundsHintProvider.TryConsumeRenderDirtyBoundsHint(out LayoutRect bounds)
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

    private void InvalidateLayoutCache()
    {
        ResetCaretLineHint();
        InvalidateVisibleTextBatchCache();
        InvalidateLayoutCachesAfterInternalEdit();
        InvalidateVirtualWrapCache();
    }

    private void InvalidateLayoutCachesAfterInternalEdit()
    {
        _hasLayoutCache = false;
        _layoutCacheTextVersion = -1;
        _layoutCacheWidth = float.NaN;
        _layoutCacheFont = null;
        _linePrefixWidthCache.Clear();
        _lineTextCache.Clear();
        InvalidateVisibleTextBatchCache();
        InvalidateViewportLayoutCache();
    }

    private void InvalidateViewportLayoutCache()
    {
        _hasViewportLayoutCache = false;
        _viewportLayoutCacheTextVersion = -1;
    }

    private void InvalidateNonVirtualLayoutCache()
    {
        _hasLayoutCache = false;
        _layoutCacheTextVersion = -1;
        _layoutCacheWidth = float.NaN;
        _layoutCacheFont = null;
    }

    private void InvalidateLineCachesFromIndex(int firstLine)
    {
        if (firstLine <= 0)
        {
            _linePrefixWidthCache.Clear();
            _lineTextCache.Clear();
            InvalidateVisibleTextBatchCache();
            return;
        }

        var lineKeysToRemove = new List<int>();
        foreach (var key in _linePrefixWidthCache.Keys)
        {
            if (key >= firstLine)
            {
                lineKeysToRemove.Add(key);
            }
        }

        for (var i = 0; i < lineKeysToRemove.Count; i++)
        {
            _linePrefixWidthCache.Remove(lineKeysToRemove[i]);
        }

        lineKeysToRemove.Clear();
        foreach (var key in _lineTextCache.Keys)
        {
            if (key >= firstLine)
            {
                lineKeysToRemove.Add(key);
            }
        }

        for (var i = 0; i < lineKeysToRemove.Count; i++)
        {
            _lineTextCache.Remove(lineKeysToRemove[i]);
        }

        InvalidateVisibleTextBatchCache();
    }

    private void InvalidateVirtualWrapCache()
    {
        ResetCaretLineHint();
        InvalidateVisibleTextBatchCache();
        _hasVirtualWrapCache = false;
        _virtualWrapCacheTextVersion = -1;
        _virtualWrapCacheWidth = float.NaN;
        _virtualWrapCacheFont = null;
        _virtualWrapText.Clear();
        _virtualWrapCheckpoints.Clear();
        _virtualWrapLineCache.Clear();
        _virtualWrapHasExactLineCount = false;
        _virtualWrapExactLineCount = 1;
        _virtualWrapEstimatedLineCount = 1f;
        _virtualWrapAverageCharsPerLine = 48f;
        _virtualWrapMaxObservedLineWidth = 0f;
    }

    private void InvalidateCaretVisualRegion()
    {
        if (!TryGetCaretRenderRect(requireVisibleCaret: false, out var caretRect))
        {
            InvalidateVisual();
            return;
        }

        var expanded = ExpandRect(caretRect, 2f);
        if (expanded.Width <= 0f || expanded.Height <= 0f)
        {
            InvalidateVisual();
            return;
        }

        if (TryProjectRectToRootSpace(expanded, out var rootSpaceBounds))
        {
            expanded = rootSpaceBounds;
        }

        _pendingRenderDirtyBoundsHint = NormalizeRect(expanded);
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

    private bool TryGetCaretRenderRect(bool requireVisibleCaret, out LayoutRect caretRect)
    {
        if (!IsEnabled || !IsFocused || (requireVisibleCaret && !_isCaretVisible))
        {
            caretRect = default;
            return false;
        }

        var view = BuildTextViewportState();
        ClampOffsets(view.Layout, view.ViewportRect);
        var lineHeight = GetLineHeight();
        return TryGetCaretRenderRect(view, lineHeight, view.ViewportRect.Y + GetTextRenderTopInset(), requireVisibleCaret, out caretRect);
    }

    private bool TryGetCaretRenderRect(
        TextViewportState view,
        float lineHeight,
        float textY,
        bool requireVisibleCaret,
        out LayoutRect caretRect)
    {
        caretRect = default;
        if (!IsEnabled || !IsFocused || (requireVisibleCaret && !_isCaretVisible))
        {
            return false;
        }

        var lineCount = GetLineCount(view.Layout);
        if (lineCount <= 0)
        {
            return false;
        }

        var caretLineIndex = FindLineIndexForTextIndexWithCaretHint(view.Layout, _editor.CaretIndex, out _);
        var caretLine = GetLine(view.Layout, caretLineIndex);
        UpdateCaretLineHint(caretLineIndex, caretLine);
        var caretColumn = Math.Clamp(_editor.CaretIndex - caretLine.StartIndex, 0, caretLine.Length);

        var caretX = view.ViewportRect.X + GetLineWidthAtColumn(view.Layout, caretLineIndex, caretColumn) - _horizontalOffset;
        var caretY = textY + (caretLineIndex * lineHeight) - _verticalOffset;
        var rawRect = new LayoutRect(caretX, caretY + 1f, 1f, MathF.Max(1f, lineHeight - 2f));
        caretRect = IntersectRect(rawRect, view.ViewportRect);
        return caretRect.Width > 0f && caretRect.Height > 0f;
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

    private bool ShouldDeferTextSync(TextEditDelta editDelta)
    {
        if (!editDelta.IsValid)
        {
            return false;
        }

        if (_editor.Length < DeferredTextSyncLengthThreshold)
        {
            return false;
        }

        if (Math.Abs(editDelta.DeltaLength) > 8)
        {
            return false;
        }

        return true;
    }

    private void SchedulePendingTextSync()
    {
        _hasPendingTextSync = true;
        _pendingTextSyncSeconds = 0f;
        _pendingTextSnapshot = null;
        _perfDeferredSyncScheduledCount++;
    }

    private void ClearPendingTextSync()
    {
        _hasPendingTextSync = false;
        _pendingTextSyncSeconds = 0f;
        _pendingTextSnapshot = null;
    }

    private void CommitPendingTextSync()
    {
        if (!_hasPendingTextSync)
        {
            return;
        }

        _perfDeferredSyncFlushCount++;
        CommitTextSyncNow(wasDeferredFlush: true);
        ClearPendingTextSync();
    }

    private void CommitTextSyncNow(bool wasDeferredFlush = false)
    {
        var start = Stopwatch.GetTimestamp();
        var text = _pendingTextSnapshot ?? _editor.Text;
        _isUpdatingTextFromEditor = true;
        try
        {
            try
            {
                SetValue(TextProperty, text);
            }
            catch (InvalidOperationException ex)
            {
                TextBoxFrameworkDiagnostics.ObserveInvalidOperation(ex, "CommitTextSyncNow.SetValue");
                throw;
            }
        }
        finally
        {
            _isUpdatingTextFromEditor = false;
        }

        _perfImmediateSyncCount++;
        var syncTicks = Stopwatch.GetTimestamp() - start;
        _perfTextSyncTicks += syncTicks;
        TextBoxFrameworkDiagnostics.ObserveTextSync(syncTicks, wasDeferredFlush);
    }

    private void MarkTextMutationActivity()
    {
        _secondsSinceLastTextMutation = 0f;
    }

    private bool ShouldFlushPendingTextSyncInUpdate()
    {
        if (!_hasPendingTextSync)
        {
            return false;
        }

        if (_pendingTextSyncSeconds < DeferredTextSyncDelaySeconds)
        {
            return false;
        }

        if (_secondsSinceLastTextMutation < DeferredTextSyncIdleSeconds)
        {
            return false;
        }

        // For large documents, avoid UI-thread full-text sync while actively editing.
        if (_editor.Length >= DeferredTextSyncLengthThreshold &&
            IsFocused)
        {
            return false;
        }

        return true;
    }

    private void CoerceTextToMaxLength()
    {
        if (MaxLength <= 0 || Text.Length <= MaxLength)
        {
            return;
        }

        Text = Text[..MaxLength];
    }

    private static Style BuildDefaultTextBoxStyle()
    {
        var style = new Style(typeof(TextBox));

        var hoverTrigger = new Trigger(IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(202, 202, 202)));

        var focusedTrigger = new Trigger(IsFocusedProperty, true);
        focusedTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(94, 168, 255)));

        var readOnlyTrigger = new Trigger(IsReadOnlyProperty, true);
        readOnlyTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(34, 34, 34)));

        var disabledTrigger = new Trigger(IsEnabledProperty, false);
        disabledTrigger.Setters.Add(new Setter(ForegroundProperty, new Color(168, 168, 168)));
        disabledTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(102, 102, 102)));

        style.Triggers.Add(hoverTrigger);
        style.Triggers.Add(focusedTrigger);
        style.Triggers.Add(readOnlyTrigger);
        style.Triggers.Add(disabledTrigger);

        return style;
    }

    private bool MoveCaretVertical(int deltaLines, bool extendSelection)
    {
        if (deltaLines == 0)
        {
            return false;
        }

        var view = BuildTextViewportState();
        var totalLineCount = GetLineCount(view.Layout);
        if (totalLineCount == 0)
        {
            return false;
        }

        var currentLineIndex = FindLineIndexForTextIndexWithCaretHint(view.Layout, _editor.CaretIndex, out _);
        var currentLine = GetLine(view.Layout, currentLineIndex);
        UpdateCaretLineHint(currentLineIndex, currentLine);
        var currentColumn = Math.Clamp(_editor.CaretIndex - currentLine.StartIndex, 0, currentLine.Length);

        var targetLineIndex = Math.Clamp(currentLineIndex + deltaLines, 0, totalLineCount - 1);
        if (targetLineIndex == currentLineIndex)
        {
            return false;
        }

        var targetLine = GetLine(view.Layout, targetLineIndex);
        UpdateCaretLineHint(targetLineIndex, targetLine);

        if (_preferredCaretX < 0f)
        {
            _preferredCaretX = GetLineWidthAtColumn(view.Layout, currentLineIndex, currentColumn);
        }

        var targetColumn = FindColumnFromX(view.Layout, targetLineIndex, _preferredCaretX);

        _editor.SetCaret(targetLine.StartIndex + targetColumn, extendSelection);
        return true;
    }

    private void EnsureCaretVisible()
    {
        _hasPendingEnsureCaretVisible = false;
        var ensureCaretStartTicks = Stopwatch.GetTimestamp();
        long viewportTicks = 0L;
        long lineLookupTicks = 0L;
        long widthTicks = 0L;
        long offsetAdjustTicks = 0L;
        var usedFastPath = false;

        var viewportStartTicks = Stopwatch.GetTimestamp();
        var view = BuildTextViewportState();
        viewportTicks = Stopwatch.GetTimestamp() - viewportStartTicks;
        if (GetLineCount(view.Layout) == 0)
        {
            _horizontalOffset = 0f;
            _verticalOffset = 0f;
            var totalTicksEarly = Stopwatch.GetTimestamp() - ensureCaretStartTicks;
            RecordEnsureCaretTiming(totalTicksEarly, viewportTicks, lineLookupTicks, widthTicks, offsetAdjustTicks, usedFastPath: false);
            return;
        }

        var lineHeight = GetLineHeight();

        var lineLookupStartTicks = Stopwatch.GetTimestamp();
        var caretLineIndex = FindLineIndexForTextIndexWithCaretHint(view.Layout, _editor.CaretIndex, out usedFastPath);
        var caretLine = GetLine(view.Layout, caretLineIndex);
        lineLookupTicks = Stopwatch.GetTimestamp() - lineLookupStartTicks;
        UpdateCaretLineHint(caretLineIndex, caretLine);

        var widthStartTicks = Stopwatch.GetTimestamp();
        var caretColumn = Math.Clamp(_editor.CaretIndex - caretLine.StartIndex, 0, caretLine.Length);
        var caretX = GetLineWidthAtColumn(view.Layout, caretLineIndex, caretColumn);
        var caretY = caretLineIndex * lineHeight;
        widthTicks = Stopwatch.GetTimestamp() - widthStartTicks;

        var adjustStartTicks = Stopwatch.GetTimestamp();
        var maxHorizontalOffset = GetMaxHorizontalOffset(view.Layout, view.ViewportRect);
        var maxVerticalOffset = GetMaxVerticalOffset(view.Layout, view.ViewportRect);

        if (caretX < _horizontalOffset)
        {
            _horizontalOffset = caretX;
        }
        else if (caretX > _horizontalOffset + MathF.Max(0f, view.ViewportRect.Width - 1f))
        {
            _horizontalOffset = caretX - MathF.Max(0f, view.ViewportRect.Width - 1f);
        }

        if (caretY < _verticalOffset)
        {
            _verticalOffset = caretY;
        }
        else if (caretY + lineHeight > _verticalOffset + view.ViewportRect.Height)
        {
            _verticalOffset = (caretY + lineHeight) - view.ViewportRect.Height;
        }

        _horizontalOffset = Math.Clamp(_horizontalOffset, 0f, maxHorizontalOffset);
        _verticalOffset = Math.Clamp(_verticalOffset, 0f, maxVerticalOffset);
        offsetAdjustTicks = Stopwatch.GetTimestamp() - adjustStartTicks;

        var totalTicks = Stopwatch.GetTimestamp() - ensureCaretStartTicks;
        RecordEnsureCaretTiming(totalTicks, viewportTicks, lineLookupTicks, widthTicks, offsetAdjustTicks, usedFastPath);
    }

    private void ClampOffsets(LayoutResult layout, LayoutRect contentRect)
    {
        _horizontalOffset = Math.Clamp(_horizontalOffset, 0f, GetMaxHorizontalOffset(layout, contentRect));
        _verticalOffset = Math.Clamp(_verticalOffset, 0f, GetMaxVerticalOffset(layout, contentRect));
    }

    private float GetMaxHorizontalOffset(LayoutResult layout, LayoutRect contentRect)
    {
        return MathF.Max(0f, layout.MaxLineWidth - contentRect.Width);
    }

    private float GetMaxVerticalOffset(LayoutResult layout, LayoutRect contentRect)
    {
        var extentHeight = GetLineCount(layout) * GetLineHeight();
        return MathF.Max(0f, extentHeight - contentRect.Height);
    }

    private int GetLineCount(LayoutResult layout)
    {
        if (layout.IsVirtualized)
        {
            return Math.Max(1, layout.TotalLineCount);
        }

        if (layout.TotalLineCount > 0)
        {
            return layout.TotalLineCount;
        }

        return layout.Lines.Count;
    }

    private LayoutLine GetLine(LayoutResult layout, int lineIndex)
    {
        var lineCount = GetLineCount(layout);
        if (lineCount <= 0)
        {
            return new LayoutLine(0, 0, 0f);
        }

        var clamped = Math.Clamp(lineIndex, 0, lineCount - 1);
        if (!layout.IsVirtualized)
        {
            return layout.Lines[clamped];
        }

        return GetVirtualWrappedLine(clamped);
    }

    private LayoutResult BuildVirtualWrappedLayout(float contentWidth, float viewportHeight)
    {
        EnsureVirtualWrapCache(contentWidth);

        var lineHeight = MathF.Max(1f, GetLineHeight());
        var visibleLineCount = Math.Max(1, (int)MathF.Ceiling(viewportHeight / lineHeight) + 1);
        var cachePadding = Math.Max(VirtualWrapMinCacheLines, visibleLineCount * 2);
        var firstVisibleLine = Math.Max(0, (int)MathF.Floor(_verticalOffset / lineHeight));
        var realizeStart = Math.Max(0, firstVisibleLine - cachePadding);
        var realizeEnd = firstVisibleLine + visibleLineCount + cachePadding;

        EnsureVirtualWrappedLineRange(realizeStart, realizeEnd);

        var totalLineCount = _virtualWrapHasExactLineCount
            ? _virtualWrapExactLineCount
            : EstimateVirtualWrappedLineCount();
        if (!_virtualWrapHasExactLineCount)
        {
            totalLineCount = Math.Max(totalLineCount, realizeEnd + 1);
        }

        totalLineCount = Math.Max(totalLineCount, 1);

        var maxLineWidth = _virtualWrapHasExactLineCount
            ? _virtualWrapMaxObservedLineWidth
            : MathF.Max(_virtualWrapMaxObservedLineWidth, contentWidth);

        return new LayoutResult(Array.Empty<LayoutLine>(), maxLineWidth, totalLineCount, IsVirtualized: true);
    }

    private void EnsureVirtualWrapCache(float contentWidth)
    {
        if (_hasVirtualWrapCache &&
            _virtualWrapCacheTextVersion == _textVersion &&
            ReferenceEquals(_virtualWrapCacheFont, Font) &&
            MathF.Abs(_virtualWrapCacheWidth - contentWidth) < 0.01f)
        {
            return;
        }

        _hasVirtualWrapCache = true;
        _virtualWrapCacheTextVersion = _textVersion;
        _virtualWrapCacheWidth = contentWidth;
        _virtualWrapCacheFont = Font;
        _virtualWrapText.SetText(_editor.Text);
        _virtualWrapCheckpoints.Clear();
        _virtualWrapLineCache.Clear();
        _virtualWrapHasExactLineCount = false;
        _virtualWrapExactLineCount = 1;
        _virtualWrapMaxObservedLineWidth = 0f;
        _virtualWrapAverageCharsPerLine = ResolveInitialCharsPerLineEstimate(contentWidth);
        _virtualWrapEstimatedLineCount = EstimateWrappedLineCount(_virtualWrapText.Length, _virtualWrapAverageCharsPerLine);
        _linePrefixWidthCache.Clear();
        _lineTextCache.Clear();

        _virtualWrapCheckpoints.Add(new WrappedLineCheckpoint(0, 0));

        if (_virtualWrapText.Length != 0)
        {
            return;
        }

        _virtualWrapLineCache[0] = new LayoutLine(0, 0, 0f);
        _virtualWrapHasExactLineCount = true;
        _virtualWrapExactLineCount = 1;
        _virtualWrapEstimatedLineCount = 1f;
    }

    private bool TryApplyIncrementalVirtualWrapEdit(TextEditDelta editDelta, out int firstInvalidLine)
    {
        firstInvalidLine = 0;
        if (!editDelta.IsValid || !_hasVirtualWrapCache)
        {
            return false;
        }

        if (Font == null || _virtualWrapCacheFont == null || !ReferenceEquals(_virtualWrapCacheFont, Font))
        {
            return false;
        }

        if (float.IsNaN(_virtualWrapCacheWidth) || float.IsInfinity(_virtualWrapCacheWidth) || _virtualWrapCacheWidth <= 1f)
        {
            return false;
        }

        var oldTextLength = _virtualWrapText.Length;
        var clampedStart = Math.Clamp(editDelta.Start, 0, oldTextLength);
        var affectedLine = FindVirtualWrappedLineIndexForTextIndex(clampedStart);
        var affectedLineStart = ResolveVirtualWrappedLineStart(affectedLine);
        firstInvalidLine = Math.Max(0, affectedLine);

        RemoveVirtualWrappedCachesFromLine(affectedLine);

        if (!_virtualWrapText.TryApplyDelta(editDelta))
        {
            return false;
        }

        var newTextLength = _virtualWrapText.Length;
        affectedLineStart = Math.Clamp(affectedLineStart, 0, newTextLength);

        AddVirtualWrapCheckpoint(affectedLine, affectedLineStart);

        _virtualWrapHasExactLineCount = false;
        _virtualWrapExactLineCount = Math.Max(affectedLine + 1, 1);
        _virtualWrapCacheTextVersion = _textVersion;
        _virtualWrapEstimatedLineCount = Math.Max(
            1f,
            _virtualWrapEstimatedLineCount + (editDelta.DeltaLength / MathF.Max(1f, _virtualWrapAverageCharsPerLine)));

        RecomputeVirtualWrapObservedStats();
        return true;
    }

    private bool TryApplyIncrementalNoWrapLayoutEdit(TextEditDelta editDelta, LayoutResult previousLayout, float previousLayoutWidth)
    {
        if (!editDelta.IsValid || TextWrapping != TextWrapping.NoWrap)
        {
            return false;
        }

        if (previousLayout.IsVirtualized || previousLayout.Lines.Count == 0)
        {
            return false;
        }

        var inserted = editDelta.InsertedText ?? string.Empty;
        var removed = editDelta.RemovedText ?? string.Empty;
        if (inserted.IndexOf('\n') >= 0 ||
            removed.IndexOf('\n') >= 0)
        {
            return false;
        }

        var lines = new List<LayoutLine>(previousLayout.Lines);
        var lineIndex = FindLineIndexForTextIndexNonVirtual(lines, editDelta.Start);
        if (lineIndex < 0 || lineIndex >= lines.Count)
        {
            return false;
        }

        var targetLine = lines[lineIndex];
        var lineStart = targetLine.StartIndex;
        var lineEnd = targetLine.StartIndex + targetLine.Length;
        var editEnd = editDelta.Start + editDelta.OldLength;
        if (editDelta.Start < lineStart || editEnd > lineEnd)
        {
            return false;
        }

        var removedWidth = MeasureTextSpanWidth(removed);
        var insertedWidth = MeasureTextSpanWidth(inserted);
        var updatedLength = targetLine.Length + editDelta.DeltaLength;
        if (updatedLength < 0)
        {
            return false;
        }

        var updatedWidth = MathF.Max(0f, targetLine.Width + insertedWidth - removedWidth);
        lines[lineIndex] = new LayoutLine(targetLine.StartIndex, updatedLength, updatedWidth);

        if (editDelta.DeltaLength != 0)
        {
            for (var i = lineIndex + 1; i < lines.Count; i++)
            {
                var current = lines[i];
                lines[i] = new LayoutLine(current.StartIndex + editDelta.DeltaLength, current.Length, current.Width);
            }
        }

        var maxLineWidth = 0f;
        for (var i = 0; i < lines.Count; i++)
        {
            maxLineWidth = MathF.Max(maxLineWidth, lines[i].Width);
        }

        _layoutCache = new LayoutResult(lines, maxLineWidth);
        _layoutCacheTextVersion = _textVersion;
        _layoutCacheWidth = previousLayoutWidth;
        _layoutCacheWrapping = TextWrapping.NoWrap;
        _layoutCacheFont = Font;
        _hasLayoutCache = true;
        _linePrefixWidthCache.Clear();
        _lineTextCache.Clear();
        return true;
    }

    private int FindLineIndexForTextIndexNonVirtual(IReadOnlyList<LayoutLine> lines, int textIndex)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        var low = 0;
        var high = lines.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var line = lines[mid];
            var lineEnd = line.StartIndex + line.Length;

            if (textIndex < line.StartIndex)
            {
                high = mid - 1;
            }
            else if (textIndex > lineEnd)
            {
                low = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return Math.Clamp(low, 0, lines.Count - 1);
    }

    private float MeasureTextSpanWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var width = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            width += MeasureCharacterWidth(text[i]);
        }

        return width;
    }

    private int ResolveVirtualWrappedLineStart(int lineIndex)
    {
        if (lineIndex <= 0)
        {
            return 0;
        }

        var previous = GetVirtualWrappedLine(lineIndex - 1);
        return GetNextVirtualWrappedStartIndex(previous);
    }

    private void RemoveVirtualWrappedCachesFromLine(int firstLine)
    {
        var removeLineKeys = new List<int>();
        foreach (var key in _virtualWrapLineCache.Keys)
        {
            if (key >= firstLine)
            {
                removeLineKeys.Add(key);
            }
        }

        for (var i = 0; i < removeLineKeys.Count; i++)
        {
            _virtualWrapLineCache.Remove(removeLineKeys[i]);
            _linePrefixWidthCache.Remove(removeLineKeys[i]);
            _lineTextCache.Remove(removeLineKeys[i]);
        }

        for (var i = _virtualWrapCheckpoints.Count - 1; i >= 0; i--)
        {
            if (_virtualWrapCheckpoints[i].LineIndex >= firstLine)
            {
                _virtualWrapCheckpoints.RemoveAt(i);
            }
        }

        if (_virtualWrapCheckpoints.Count == 0)
        {
            _virtualWrapCheckpoints.Add(new WrappedLineCheckpoint(0, 0));
        }
    }

    private void RecomputeVirtualWrapObservedStats()
    {
        var maxWidth = 0f;
        var sampledChars = 0f;
        var sampledCount = 0;
        foreach (var line in _virtualWrapLineCache.Values)
        {
            maxWidth = MathF.Max(maxWidth, line.Width);
            if (line.Length <= 0)
            {
                continue;
            }

            sampledChars += line.Length;
            sampledCount++;
        }

        _virtualWrapMaxObservedLineWidth = maxWidth;
        if (sampledCount > 0)
        {
            _virtualWrapAverageCharsPerLine = MathF.Max(1f, sampledChars / sampledCount);
            return;
        }

        _virtualWrapAverageCharsPerLine = ResolveInitialCharsPerLineEstimate(_virtualWrapCacheWidth);
    }

    private float ResolveInitialCharsPerLineEstimate(float contentWidth)
    {
        var sampleWidth = MeasureCharacterWidth('n');
        if (sampleWidth <= 0f || float.IsNaN(sampleWidth) || float.IsInfinity(sampleWidth))
        {
            sampleWidth = 8f;
        }

        return MathF.Max(1f, contentWidth / sampleWidth);
    }

    private static float EstimateWrappedLineCount(int textLength, float charsPerLine)
    {
        if (textLength <= 0)
        {
            return 1f;
        }

        return MathF.Max(1f, MathF.Ceiling(textLength / MathF.Max(1f, charsPerLine)));
    }

    private int EstimateVirtualWrappedLineCount()
    {
        if (_virtualWrapHasExactLineCount)
        {
            return _virtualWrapExactLineCount;
        }

        if (_virtualWrapCheckpoints.Count == 0)
        {
            return Math.Max(1, (int)MathF.Ceiling(_virtualWrapEstimatedLineCount));
        }

        var lastCheckpoint = _virtualWrapCheckpoints[_virtualWrapCheckpoints.Count - 1];
        var remainingChars = Math.Max(0, _virtualWrapText.Length - lastCheckpoint.TextIndex);
        var remainingLines = (int)MathF.Ceiling(remainingChars / MathF.Max(1f, _virtualWrapAverageCharsPerLine));
        var estimated = Math.Max(1, lastCheckpoint.LineIndex + remainingLines + 1);

        foreach (var cachedLine in _virtualWrapLineCache.Keys)
        {
            estimated = Math.Max(estimated, cachedLine + 1);
        }

        estimated = Math.Max(estimated, (int)MathF.Ceiling(_virtualWrapEstimatedLineCount));
        _virtualWrapEstimatedLineCount = estimated;
        return estimated;
    }

    private LayoutLine GetVirtualWrappedLine(int lineIndex)
    {
        if (_virtualWrapHasExactLineCount && _virtualWrapExactLineCount > 0)
        {
            lineIndex = Math.Clamp(lineIndex, 0, _virtualWrapExactLineCount - 1);
        }
        else
        {
            lineIndex = Math.Max(0, lineIndex);
        }

        if (_virtualWrapLineCache.TryGetValue(lineIndex, out var line))
        {
            return line;
        }

        var extra = Math.Max(8, VirtualWrapMinCacheLines / 2);
        EnsureVirtualWrappedLineRange(lineIndex, lineIndex + extra);
        if (_virtualWrapLineCache.TryGetValue(lineIndex, out line))
        {
            return line;
        }

        if (_virtualWrapHasExactLineCount && _virtualWrapExactLineCount > 0)
        {
            var fallbackIndex = _virtualWrapExactLineCount - 1;
            EnsureVirtualWrappedLineRange(fallbackIndex, fallbackIndex);
            if (_virtualWrapLineCache.TryGetValue(fallbackIndex, out line))
            {
                return line;
            }
        }

        return new LayoutLine(0, 0, 0f);
    }

    private void EnsureVirtualWrappedLineRange(int startLine, int endLine)
    {
        _perfVirtualRangeBuildCount++;
        if (startLine < 0)
        {
            startLine = 0;
        }

        if (endLine < startLine)
        {
            endLine = startLine;
        }

        if (_virtualWrapHasExactLineCount && startLine >= _virtualWrapExactLineCount)
        {
            return;
        }

        var checkpoint = FindVirtualWrapCheckpointForLine(startLine);
        var lineIndex = checkpoint.LineIndex;
        var currentStartIndex = checkpoint.TextIndex;

        while (lineIndex <= endLine)
        {
            if (_virtualWrapHasExactLineCount && lineIndex >= _virtualWrapExactLineCount)
            {
                break;
            }

            if (_virtualWrapLineCache.TryGetValue(lineIndex, out var cachedLine))
            {
                currentStartIndex = GetNextVirtualWrappedStartIndex(cachedLine);
                lineIndex++;
                continue;
            }

            if (!TryBuildVirtualWrappedLine(currentStartIndex, out var line, out var nextStartIndex))
            {
                _virtualWrapHasExactLineCount = true;
                _virtualWrapExactLineCount = Math.Max(1, lineIndex);
                _virtualWrapEstimatedLineCount = _virtualWrapExactLineCount;
                break;
            }

            _virtualWrapLineCache[lineIndex] = line;
            _perfVirtualLineBuildCount++;
            _virtualWrapMaxObservedLineWidth = MathF.Max(_virtualWrapMaxObservedLineWidth, line.Width);
            if (line.Length > 0)
            {
                _virtualWrapAverageCharsPerLine = ((_virtualWrapAverageCharsPerLine * 7f) + line.Length) / 8f;
            }

            if (lineIndex == 0 || (lineIndex % VirtualWrapCheckpointInterval) == 0)
            {
                AddVirtualWrapCheckpoint(lineIndex, line.StartIndex);
            }

            currentStartIndex = nextStartIndex;
            if (nextStartIndex > _virtualWrapText.Length)
            {
                _virtualWrapHasExactLineCount = true;
                _virtualWrapExactLineCount = lineIndex + 1;
                _virtualWrapEstimatedLineCount = _virtualWrapExactLineCount;
                break;
            }

            lineIndex++;
        }

        if (!_virtualWrapHasExactLineCount)
        {
            _virtualWrapEstimatedLineCount = Math.Max(_virtualWrapEstimatedLineCount, endLine + 1);
        }

        TrimVirtualWrappedLineCache(startLine, endLine);
    }

    private bool TryBuildVirtualWrappedLine(int startIndex, out LayoutLine line, out int nextStartIndex)
    {
        var textLength = _virtualWrapText.Length;

        if (textLength == 0)
        {
            if (startIndex > 0)
            {
                line = default;
                nextStartIndex = startIndex;
                return false;
            }

            line = new LayoutLine(0, 0, 0f);
            nextStartIndex = 1;
            return true;
        }

        if (startIndex > textLength)
        {
            line = default;
            nextStartIndex = startIndex;
            return false;
        }

        if (startIndex == textLength)
        {
            if (!TryGetVirtualWrapChar(textLength - 1, out var trailing) || trailing != '\n')
            {
                line = default;
                nextStartIndex = startIndex;
                return false;
            }

            line = new LayoutLine(startIndex, 0, 0f);
            nextStartIndex = startIndex + 1;
            return true;
        }

        var lineStart = startIndex;
        var lineLength = 0;
        var lineWidth = 0f;

        for (var i = startIndex; i < textLength; i++)
        {
            if (!TryGetVirtualWrapChar(i, out var ch))
            {
                line = default;
                nextStartIndex = i;
                return false;
            }

            if (ch == '\n')
            {
                line = new LayoutLine(lineStart, lineLength, lineWidth);
                nextStartIndex = i + 1;
                return true;
            }

            var chWidth = MeasureCharacterWidth(ch);
            if (lineLength > 0 && (lineWidth + chWidth) > _virtualWrapCacheWidth)
            {
                line = new LayoutLine(lineStart, lineLength, lineWidth);
                nextStartIndex = i;
                return true;
            }

            lineLength++;
            lineWidth += chWidth;
        }

        line = new LayoutLine(lineStart, lineLength, lineWidth);
        nextStartIndex = textLength;
        return true;
    }

    private int GetNextVirtualWrappedStartIndex(LayoutLine line)
    {
        var textLength = _virtualWrapText.Length;
        var lineEnd = line.StartIndex + line.Length;

        if (textLength == 0)
        {
            return 1;
        }

        if (line.StartIndex == textLength)
        {
            return TryGetVirtualWrapChar(textLength - 1, out var trailing) && trailing == '\n'
                ? textLength + 1
                : textLength;
        }

        if (lineEnd < textLength && TryGetVirtualWrapChar(lineEnd, out var delimiter) && delimiter == '\n')
        {
            return lineEnd + 1;
        }

        if (lineEnd >= textLength)
        {
            return textLength;
        }

        return lineEnd;
    }

    private void AddVirtualWrapCheckpoint(int lineIndex, int textIndex)
    {
        if (lineIndex < 0 || textIndex < 0)
        {
            return;
        }

        var low = 0;
        var high = _virtualWrapCheckpoints.Count;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (_virtualWrapCheckpoints[mid].LineIndex < lineIndex)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        if (low < _virtualWrapCheckpoints.Count && _virtualWrapCheckpoints[low].LineIndex == lineIndex)
        {
            _virtualWrapCheckpoints[low] = new WrappedLineCheckpoint(lineIndex, textIndex);
            return;
        }

        _virtualWrapCheckpoints.Insert(low, new WrappedLineCheckpoint(lineIndex, textIndex));
    }

    private WrappedLineCheckpoint FindVirtualWrapCheckpointForLine(int lineIndex)
    {
        if (_virtualWrapCheckpoints.Count == 0)
        {
            return new WrappedLineCheckpoint(0, 0);
        }

        var low = 0;
        var high = _virtualWrapCheckpoints.Count - 1;
        var best = 0;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var checkpoint = _virtualWrapCheckpoints[mid];
            if (checkpoint.LineIndex <= lineIndex)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return _virtualWrapCheckpoints[best];
    }

    private WrappedLineCheckpoint FindVirtualWrapCheckpointForTextIndex(int textIndex)
    {
        if (_virtualWrapCheckpoints.Count == 0)
        {
            return new WrappedLineCheckpoint(0, 0);
        }

        var low = 0;
        var high = _virtualWrapCheckpoints.Count - 1;
        var best = 0;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var checkpoint = _virtualWrapCheckpoints[mid];
            if (checkpoint.TextIndex <= textIndex)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return _virtualWrapCheckpoints[best];
    }

    private int FindVirtualWrappedLineIndexForTextIndex(int textIndex)
    {
        var textLength = _virtualWrapText.Length;
        textIndex = Math.Clamp(textIndex, 0, textLength);

        var checkpoint = FindVirtualWrapCheckpointForTextIndex(textIndex);
        var lineIndex = checkpoint.LineIndex;
        var currentStartIndex = checkpoint.TextIndex;

        while (true)
        {
            if (_virtualWrapHasExactLineCount && lineIndex >= _virtualWrapExactLineCount)
            {
                return Math.Max(0, _virtualWrapExactLineCount - 1);
            }

            if (!_virtualWrapLineCache.TryGetValue(lineIndex, out var line))
            {
                if (!TryBuildVirtualWrappedLine(currentStartIndex, out line, out var nextStartIndex))
                {
                    if (_virtualWrapHasExactLineCount)
                    {
                        return Math.Max(0, _virtualWrapExactLineCount - 1);
                    }

                    return Math.Max(0, lineIndex - 1);
                }

                _virtualWrapLineCache[lineIndex] = line;
                _perfVirtualLineBuildCount++;
                _virtualWrapMaxObservedLineWidth = MathF.Max(_virtualWrapMaxObservedLineWidth, line.Width);
                if (line.Length > 0)
                {
                    _virtualWrapAverageCharsPerLine = ((_virtualWrapAverageCharsPerLine * 7f) + line.Length) / 8f;
                }

                if (lineIndex == 0 || (lineIndex % VirtualWrapCheckpointInterval) == 0)
                {
                    AddVirtualWrapCheckpoint(lineIndex, line.StartIndex);
                }

                currentStartIndex = nextStartIndex;
                if (nextStartIndex > textLength)
                {
                    _virtualWrapHasExactLineCount = true;
                    _virtualWrapExactLineCount = lineIndex + 1;
                    _virtualWrapEstimatedLineCount = _virtualWrapExactLineCount;
                }
            }
            else
            {
                currentStartIndex = GetNextVirtualWrappedStartIndex(line);
            }

            var lineEnd = line.StartIndex + line.Length;
            if (textIndex <= lineEnd)
            {
                return lineIndex;
            }

            if (currentStartIndex > textLength)
            {
                return lineIndex;
            }

            lineIndex++;
        }
    }

    private void TrimVirtualWrappedLineCache(int startLine, int endLine)
    {
        if (_virtualWrapLineCache.Count <= VirtualWrapTrimThreshold)
        {
            return;
        }

        var keepStart = Math.Max(0, startLine - VirtualWrapTrimPadding);
        var keepEnd = endLine + VirtualWrapTrimPadding;
        var toRemove = new List<int>();
        foreach (var key in _virtualWrapLineCache.Keys)
        {
            if (key < keepStart || key > keepEnd)
            {
                toRemove.Add(key);
            }
        }

        for (var i = 0; i < toRemove.Count; i++)
        {
            _virtualWrapLineCache.Remove(toRemove[i]);
            _linePrefixWidthCache.Remove(toRemove[i]);
            _lineTextCache.Remove(toRemove[i]);
        }
    }

    private LayoutResult BuildLayoutLines(float contentWidth)
    {
        var widthMatches = _layoutCacheWrapping == TextWrapping.NoWrap ||
                           MathF.Abs(_layoutCacheWidth - contentWidth) < 0.01f;
        if (_hasLayoutCache &&
            _layoutCacheTextVersion == _textVersion &&
            ReferenceEquals(_layoutCacheFont, Font) &&
            _layoutCacheWrapping == TextWrapping &&
            widthMatches)
        {
            _perfLayoutCacheHitCount++;
            return _layoutCache;
        }

        var text = _editor.Text;
        _perfLayoutCacheMissCount++;
        _perfFullLayoutBuildCount++;

        if (Font == null)
        {
            var noFontLayout = new LayoutResult(BuildRawLinesWithoutWrapping(text), 0f);
            UpdateLayoutCache(contentWidth, noFontLayout);
            return noFontLayout;
        }

        var lines = new List<LayoutLine>();
        if (text.Length == 0)
        {
            lines.Add(new LayoutLine(0, 0, 0f));
            var emptyLayout = new LayoutResult(lines, 0f);
            UpdateLayoutCache(contentWidth, emptyLayout);
            return emptyLayout;
        }

        var wrapEnabled = TextWrapping != TextWrapping.NoWrap &&
                          !float.IsInfinity(contentWidth) &&
                          !float.IsNaN(contentWidth) &&
                          contentWidth > 1f;
        if (!wrapEnabled)
        {
            var noWrapLayout = BuildNoWrapLayoutLines(text);
            UpdateLayoutCache(contentWidth, noWrapLayout);
            return noWrapLayout;
        }

        var maxLineWidth = 0f;
        var lineStart = 0;
        var lineLength = 0;
        var lineWidth = 0f;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\n')
            {
                lines.Add(new LayoutLine(lineStart, lineLength, lineWidth));
                maxLineWidth = MathF.Max(maxLineWidth, lineWidth);

                lineStart = i + 1;
                lineLength = 0;
                lineWidth = 0f;
                continue;
            }

            var chWidth = MeasureCharacterWidth(ch);
            if (lineLength > 0 && (lineWidth + chWidth) > contentWidth)
            {
                lines.Add(new LayoutLine(lineStart, lineLength, lineWidth));
                maxLineWidth = MathF.Max(maxLineWidth, lineWidth);

                lineStart = i;
                lineLength = 1;
                lineWidth = chWidth;
                continue;
            }

            lineLength++;
            lineWidth += chWidth;
        }

        lines.Add(new LayoutLine(lineStart, lineLength, lineWidth));
        maxLineWidth = MathF.Max(maxLineWidth, lineWidth);

        var result = new LayoutResult(lines, maxLineWidth);
        UpdateLayoutCache(contentWidth, result);
        return result;
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private List<LayoutLine> BuildRawLinesWithoutWrapping(string text)
    {
        var lines = new List<LayoutLine>();
        if (text.Length == 0)
        {
            lines.Add(new LayoutLine(0, 0, 0f));
            return lines;
        }

        var lineStart = 0;
        var lineLength = 0;
        var lineWidth = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines.Add(new LayoutLine(lineStart, lineLength, lineWidth));
                lineStart = i + 1;
                lineLength = 0;
                lineWidth = 0f;
                continue;
            }

            lineLength++;
            lineWidth += MeasureCharacterWidth(text[i]);
        }

        lines.Add(new LayoutLine(lineStart, lineLength, lineWidth));
        return lines;
    }

    private LayoutResult BuildNoWrapLayoutLines(string text)
    {
        var lines = new List<LayoutLine>();
        if (text.Length == 0)
        {
            lines.Add(new LayoutLine(0, 0, 0f));
            return new LayoutResult(lines, 0f);
        }

        var maxLineWidth = 0f;
        var lineStart = 0;
        var lineLength = 0;
        var lineWidth = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\n')
            {
                lines.Add(new LayoutLine(lineStart, lineLength, lineWidth));
                maxLineWidth = MathF.Max(maxLineWidth, lineWidth);
                lineStart = i + 1;
                lineLength = 0;
                lineWidth = 0f;
                continue;
            }

            lineLength++;
            lineWidth += MeasureCharacterWidth(ch);
        }

        lines.Add(new LayoutLine(lineStart, lineLength, lineWidth));
        maxLineWidth = MathF.Max(maxLineWidth, lineWidth);
        return new LayoutResult(lines, maxLineWidth);
    }

    private void UpdateLayoutCache(float contentWidth, LayoutResult layout)
    {
        _layoutCacheTextVersion = _textVersion;
        _layoutCacheWidth = contentWidth;
        _layoutCacheWrapping = TextWrapping;
        _layoutCacheFont = Font;
        _layoutCache = layout;
        _hasLayoutCache = true;
        _linePrefixWidthCache.Clear();
        _lineTextCache.Clear();
    }

    private string GetLineText(LayoutResult layout, int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= GetLineCount(layout))
        {
            return string.Empty;
        }

        if (_lineTextCache.TryGetValue(lineIndex, out var cached))
        {
            return cached;
        }

        var line = GetLine(layout, lineIndex);
        if (line.Length <= 0)
        {
            _lineTextCache[lineIndex] = string.Empty;
            return string.Empty;
        }

        string value;
        if (layout.IsVirtualized && _hasVirtualWrapCache)
        {
            value = _virtualWrapText.BuildRangeText(line.StartIndex, line.Length);
        }
        else
        {
            var text = _editor.Text;
            if (line.StartIndex < 0 || line.StartIndex + line.Length > text.Length)
            {
                _lineTextCache[lineIndex] = string.Empty;
                return string.Empty;
            }

            value = text.Substring(line.StartIndex, line.Length);
        }

        _lineTextCache[lineIndex] = value;
        return value;
    }

    private float[] GetLinePrefixWidths(LayoutResult layout, int lineIndex)
    {
        if (_linePrefixWidthCache.TryGetValue(lineIndex, out var cached))
        {
            return cached;
        }

        var line = GetLine(layout, lineIndex);
        var widths = new float[line.Length + 1];
        if (line.Length > 0)
        {
            var offset = 0f;
            if (layout.IsVirtualized && _hasVirtualWrapCache)
            {
                for (var i = 0; i < line.Length; i++)
                {
                    if (!TryGetVirtualWrapChar(line.StartIndex + i, out var ch))
                    {
                        break;
                    }

                    offset += MeasureCharacterWidth(ch);
                    widths[i + 1] = offset;
                }
            }
            else
            {
                var text = _editor.Text;
                if (line.StartIndex < 0 || line.StartIndex + line.Length > text.Length)
                {
                    _linePrefixWidthCache[lineIndex] = widths;
                    return widths;
                }

                for (var i = 0; i < line.Length; i++)
                {
                    offset += MeasureCharacterWidth(text[line.StartIndex + i]);
                    widths[i + 1] = offset;
                }
            }
        }

        _linePrefixWidthCache[lineIndex] = widths;
        return widths;
    }

    private bool TryGetVirtualWrapChar(int index, out char ch)
    {
        return _virtualWrapText.TryGetCharAt(index, out ch);
    }

    private float GetLineWidthAtColumn(LayoutResult layout, int lineIndex, int column)
    {
        var line = GetLine(layout, lineIndex);
        if (line.Length <= 0)
        {
            return 0f;
        }

        var clamped = Math.Clamp(column, 0, line.Length);
        var widths = GetLinePrefixWidths(layout, lineIndex);
        return widths[clamped];
    }

    private int FindColumnFromX(LayoutResult layout, int lineIndex, float localX)
    {
        var line = GetLine(layout, lineIndex);
        if (line.Length <= 0)
        {
            return 0;
        }

        var widths = GetLinePrefixWidths(layout, lineIndex);
        var x = Math.Max(0f, localX);

        var low = 0;
        var high = line.Length;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            var midpoint = (widths[mid] + widths[mid + 1]) * 0.5f;
            if (x < midpoint)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return low;
    }

    private int FindLineIndexForTextIndexWithCaretHint(LayoutResult layout, int textIndex, out bool usedFastPath)
    {
        usedFastPath = false;
        if (TryFindVirtualizedLineIndexWithCaretHint(layout, textIndex, out var lineIndex))
        {
            usedFastPath = true;
            return lineIndex;
        }

        return FindLineIndexForTextIndex(layout, textIndex);
    }

    private bool TryFindVirtualizedLineIndexWithCaretHint(LayoutResult layout, int textIndex, out int lineIndex)
    {
        lineIndex = 0;
        if (!layout.IsVirtualized || !_hasCaretLineHint)
        {
            return false;
        }

        var lineCount = GetLineCount(layout);
        if (lineCount <= 0)
        {
            return false;
        }

        var clampedTextIndex = Math.Clamp(textIndex, 0, _editor.Length);
        var probeIndex = Math.Clamp(_caretLineHintIndex, 0, lineCount - 1);
        const int maxProbeSteps = 12;

        bool TryMatch(int index, out LayoutLine line)
        {
            line = GetLine(layout, index);
            var lineStart = line.StartIndex;
            var lineEnd = line.StartIndex + line.Length;
            return clampedTextIndex >= lineStart && clampedTextIndex <= lineEnd;
        }

        if (TryMatch(probeIndex, out _))
        {
            lineIndex = probeIndex;
            return true;
        }

        if (clampedTextIndex > _caretLineHintEnd)
        {
            for (var step = 1; step <= maxProbeSteps; step++)
            {
                var candidate = probeIndex + step;
                if (candidate >= lineCount)
                {
                    break;
                }

                if (TryMatch(candidate, out _))
                {
                    lineIndex = candidate;
                    return true;
                }
            }

            return false;
        }

        if (clampedTextIndex < _caretLineHintStart)
        {
            for (var step = 1; step <= maxProbeSteps; step++)
            {
                var candidate = probeIndex - step;
                if (candidate < 0)
                {
                    break;
                }

                if (TryMatch(candidate, out _))
                {
                    lineIndex = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private int FindLineIndexForTextIndex(LayoutResult layout, int textIndex)
    {
        if (layout.IsVirtualized)
        {
            return FindVirtualWrappedLineIndexForTextIndex(textIndex);
        }

        if (layout.Lines.Count == 0)
        {
            return 0;
        }

        var low = 0;
        var high = layout.Lines.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var line = layout.Lines[mid];
            var lineEnd = line.StartIndex + line.Length;

            if (textIndex < line.StartIndex)
            {
                high = mid - 1;
            }
            else if (textIndex > lineEnd)
            {
                low = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return Math.Clamp(low, 0, layout.Lines.Count - 1);
    }

    private float GetLineHeight()
    {
        return FontStashTextRenderer.GetLineHeight(Font);
    }

    private float GetTextRenderTopInset()
    {
        // FontStash glyph rasterization can sit slightly above the provided origin.
        // Offset the text baseline so the first row is fully visible within the viewport clip.
        return FontStashTextRenderer.IsEnabled ? 3f : 0f;
    }

    private LayoutRect GetTextRenderClipRect(LayoutRect viewportRect)
    {
        var verticalBleed = FontStashTextRenderer.IsEnabled ? 6f : 0f;
        var top = MathF.Max(LayoutSlot.Y, viewportRect.Y - verticalBleed);
        var bottom = MathF.Min(LayoutSlot.Y + LayoutSlot.Height, viewportRect.Y + viewportRect.Height + verticalBleed);
        return new LayoutRect(
            viewportRect.X,
            top,
            viewportRect.Width,
            MathF.Max(0f, bottom - top));
    }

    private void CacheViewportLayoutState(TextViewportLayoutState state, LayoutRect innerRect)
    {
        _viewportLayoutCache = state;
        _viewportLayoutCacheInnerRect = innerRect;
        _viewportLayoutCacheHorizontalVisibility = HorizontalScrollBarVisibility;
        _viewportLayoutCacheVerticalVisibility = VerticalScrollBarVisibility;
        _viewportLayoutCacheTextVersion = _textVersion;
        _hasViewportLayoutCache = true;
        _hasPreviousScrollBarResolution = true;
        _previousShowHorizontalScrollBar = state.ShowHorizontalScrollBar;
        _previousShowVerticalScrollBar = state.ShowVerticalScrollBar;
    }

    private bool ResolveInitialHorizontalScrollBarVisibility()
    {
        return HorizontalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && _previousShowHorizontalScrollBar,
            _ => false
        };
    }

    private bool ResolveInitialVerticalScrollBarVisibility()
    {
        return VerticalScrollBarVisibility switch
        {
            ScrollBarVisibility.Visible => true,
            ScrollBarVisibility.Disabled => false,
            ScrollBarVisibility.Hidden => false,
            ScrollBarVisibility.Auto => _hasPreviousScrollBarResolution && _previousShowVerticalScrollBar,
            _ => false
        };
    }

    private static bool AreRectsClose(LayoutRect a, LayoutRect b)
    {
        const float epsilon = 0.01f;
        return MathF.Abs(a.X - b.X) < epsilon &&
               MathF.Abs(a.Y - b.Y) < epsilon &&
               MathF.Abs(a.Width - b.Width) < epsilon &&
               MathF.Abs(a.Height - b.Height) < epsilon;
    }

    private sealed class VirtualWrapTextSnapshot
    {
        private readonly List<Segment> _segments = [];
        private int _length;
        private int _cursorSegmentIndex;
        private int _cursorGlobalStart;
        private bool _hasCursor;

        public int Length => _length;

        public void Clear()
        {
            _segments.Clear();
            _length = 0;
            _hasCursor = false;
            _cursorSegmentIndex = 0;
            _cursorGlobalStart = 0;
        }

        public void SetText(string text)
        {
            Clear();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _segments.Add(new Segment(text, 0, text.Length));
            _length = text.Length;
        }

        public bool TryApplyDelta(TextEditDelta editDelta)
        {
            if (!editDelta.IsValid)
            {
                return false;
            }

            if (editDelta.Start < 0 || editDelta.Start > _length)
            {
                return false;
            }

            if (editDelta.OldLength < 0 || editDelta.Start + editDelta.OldLength > _length)
            {
                return false;
            }

            var insert = editDelta.InsertedText ?? string.Empty;
            var removed = editDelta.RemovedText ?? string.Empty;
            if (insert.Length != editDelta.NewLength || removed.Length != editDelta.OldLength)
            {
                return false;
            }

            if (editDelta.OldLength > 0 && removed.Length > 0)
            {
                var removedSnapshot = BuildRangeText(editDelta.Start, editDelta.OldLength);
                if (!string.Equals(removedSnapshot, removed, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            SplitAt(editDelta.Start + editDelta.OldLength);
            SplitAt(editDelta.Start);

            var removeStart = GetBoundarySegmentIndex(editDelta.Start);
            var removeEnd = GetBoundarySegmentIndex(editDelta.Start + editDelta.OldLength);
            if (removeEnd > removeStart)
            {
                _segments.RemoveRange(removeStart, removeEnd - removeStart);
            }

            if (insert.Length > 0)
            {
                _segments.Insert(removeStart, new Segment(insert, 0, insert.Length));
            }

            CoalesceAround(Math.Max(0, removeStart - 1));
            _length = _length - editDelta.OldLength + editDelta.NewLength;
            SeedCursorNearIndex(editDelta.Start);
            return true;
        }

        public bool TryGetCharAt(int index, out char ch)
        {
            ch = '\0';
            if (index < 0 || index >= _length || _segments.Count == 0)
            {
                return false;
            }

            if (_hasCursor)
            {
                var cursorSegment = _segments[_cursorSegmentIndex];
                var cursorEnd = _cursorGlobalStart + cursorSegment.Length;
                if (index >= _cursorGlobalStart && index < cursorEnd)
                {
                    ch = cursorSegment.Source[cursorSegment.Start + (index - _cursorGlobalStart)];
                    return true;
                }

                if (index > _cursorGlobalStart)
                {
                    var cumulative = cursorEnd;
                    for (var i = _cursorSegmentIndex + 1; i < _segments.Count; i++)
                    {
                        var segment = _segments[i];
                        var segmentEnd = cumulative + segment.Length;
                        if (index < segmentEnd)
                        {
                            _cursorSegmentIndex = i;
                            _cursorGlobalStart = cumulative;
                            ch = segment.Source[segment.Start + (index - cumulative)];
                            return true;
                        }

                        cumulative = segmentEnd;
                    }
                }

                if (index < _cursorGlobalStart)
                {
                    var cumulative = _cursorGlobalStart;
                    for (var i = _cursorSegmentIndex - 1; i >= 0; i--)
                    {
                        var segment = _segments[i];
                        cumulative -= segment.Length;
                        if (index >= cumulative)
                        {
                            _cursorSegmentIndex = i;
                            _cursorGlobalStart = cumulative;
                            ch = segment.Source[segment.Start + (index - cumulative)];
                            return true;
                        }
                    }
                }
            }

            var running = 0;
            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                var segmentEnd = running + segment.Length;
                if (index < segmentEnd)
                {
                    _cursorSegmentIndex = i;
                    _cursorGlobalStart = running;
                    _hasCursor = true;
                    ch = segment.Source[segment.Start + (index - running)];
                    return true;
                }

                running = segmentEnd;
            }

            return false;
        }

        public string BuildRangeText(int start, int length)
        {
            if (length <= 0 || _length == 0)
            {
                return string.Empty;
            }

            start = Math.Clamp(start, 0, _length);
            var end = Math.Clamp(start + length, 0, _length);
            if (end <= start)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(end - start);
            var cumulative = 0;
            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                var segmentStart = cumulative;
                var segmentEnd = cumulative + segment.Length;
                if (segmentEnd <= start)
                {
                    cumulative = segmentEnd;
                    continue;
                }

                if (segmentStart >= end)
                {
                    break;
                }

                var localStart = Math.Max(start, segmentStart) - segmentStart;
                var localEnd = Math.Min(end, segmentEnd) - segmentStart;
                var localLength = localEnd - localStart;
                if (localLength > 0)
                {
                    builder.Append(segment.Source, segment.Start + localStart, localLength);
                }

                cumulative = segmentEnd;
            }

            return builder.ToString();
        }

        private void SplitAt(int index)
        {
            if (index <= 0 || index >= _length)
            {
                return;
            }

            var cumulative = 0;
            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                var segmentEnd = cumulative + segment.Length;
                if (index == cumulative || index == segmentEnd)
                {
                    return;
                }

                if (index > cumulative && index < segmentEnd)
                {
                    var leftLength = index - cumulative;
                    var rightLength = segment.Length - leftLength;
                    _segments[i] = new Segment(segment.Source, segment.Start, leftLength);
                    _segments.Insert(i + 1, new Segment(segment.Source, segment.Start + leftLength, rightLength));
                    return;
                }

                cumulative = segmentEnd;
            }
        }

        private int GetBoundarySegmentIndex(int index)
        {
            if (index <= 0)
            {
                return 0;
            }

            if (index >= _length)
            {
                return _segments.Count;
            }

            var cumulative = 0;
            for (var i = 0; i < _segments.Count; i++)
            {
                if (cumulative == index)
                {
                    return i;
                }

                cumulative += _segments[i].Length;
                if (cumulative == index)
                {
                    return i + 1;
                }
            }

            return _segments.Count;
        }

        private void CoalesceAround(int index)
        {
            if (_segments.Count < 2)
            {
                return;
            }

            var i = Math.Clamp(index, 0, _segments.Count - 2);
            while (i < _segments.Count - 1)
            {
                var left = _segments[i];
                var right = _segments[i + 1];
                var canMerge = ReferenceEquals(left.Source, right.Source) &&
                               left.Length > 0 &&
                               right.Length > 0 &&
                               left.Start + left.Length == right.Start;
                if (!canMerge)
                {
                    i++;
                    continue;
                }

                _segments[i] = new Segment(left.Source, left.Start, left.Length + right.Length);
                _segments.RemoveAt(i + 1);
                if (i > 0)
                {
                    i--;
                }
            }
        }

        private void ResetCursor()
        {
            _hasCursor = false;
            _cursorSegmentIndex = 0;
            _cursorGlobalStart = 0;
        }

        private void SeedCursorNearIndex(int index)
        {
            if (_segments.Count == 0 || _length == 0)
            {
                ResetCursor();
                return;
            }

            var clamped = Math.Clamp(index, 0, _length - 1);
            var running = 0;
            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                var segmentEnd = running + segment.Length;
                if (clamped < segmentEnd)
                {
                    _cursorSegmentIndex = i;
                    _cursorGlobalStart = running;
                    _hasCursor = true;
                    return;
                }

                running = segmentEnd;
            }

            _cursorSegmentIndex = _segments.Count - 1;
            _cursorGlobalStart = Math.Max(0, _length - _segments[_cursorSegmentIndex].Length);
            _hasCursor = true;
        }

        private readonly record struct Segment(string Source, int Start, int Length);
    }

    internal void SetMouseOverFromInput(bool isMouseOver)
    {
        if (IsMouseOver == isMouseOver)
        {
            return;
        }

        IsMouseOver = isMouseOver;
    }

    internal void SetFocusedFromInput(bool isFocused)
    {
        if (IsFocused == isFocused)
        {
            return;
        }

        IsFocused = isFocused;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = isFocused;
        _isSelectingWithPointer = false;
        if (!isFocused)
        {
            CommitPendingTextSync();
            EndScrollBarDrag();
        }

        InvalidateVisual();
    }

    internal bool HandleTextInputFromInput(char character)
    {
        if (!IsEnabled || !IsFocused || IsReadOnly)
        {
            return false;
        }

        if (!TryMapTextInput(character, out var text))
        {
            return false;
        }

        var mutationStart = Stopwatch.GetTimestamp();
        long editTicks;
        long commitTicks;
        long ensureTicks;

        var editStart = Stopwatch.GetTimestamp();
        if (!InsertTextIntoEditor(text))
        {
            return false;
        }
        editTicks = Stopwatch.GetTimestamp() - editStart;

        var commitStart = Stopwatch.GetTimestamp();
        CommitEditorText(_editor.ConsumeLastEditDelta());
        commitTicks = Stopwatch.GetTimestamp() - commitStart;
        MarkTextMutationActivity();
        _preferredCaretX = -1f;
        var ensureStart = Stopwatch.GetTimestamp();
        ScheduleEnsureCaretVisible();
        ensureTicks = Stopwatch.GetTimestamp() - ensureStart;
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        RecordInputMutationTiming(
            Stopwatch.GetTimestamp() - mutationStart,
            editTicks,
            commitTicks,
            ensureTicks);
        return true;
    }

    internal bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!IsEnabled || !IsFocused)
        {
            return false;
        }

        var ctrl = (modifiers & ModifierKeys.Control) != 0;
        var shift = (modifiers & ModifierKeys.Shift) != 0;
        var changed = false;
        var moved = false;
        var mutationStart = Stopwatch.GetTimestamp();
        long editTicks = 0L;
        long commitTicks = 0L;
        long ensureTicks = 0L;

        switch (key)
        {
            case Keys.Left:
                moved = _editor.MoveCaretLeft(extendSelection: shift, byWord: ctrl);
                break;
            case Keys.Right:
                moved = _editor.MoveCaretRight(extendSelection: shift, byWord: ctrl);
                break;
            case Keys.Home:
                moved = _editor.MoveCaretHome(extendSelection: shift);
                break;
            case Keys.End:
                moved = _editor.MoveCaretEnd(extendSelection: shift);
                break;
            case Keys.Up:
                moved = MoveCaretVertical(-1, extendSelection: shift);
                break;
            case Keys.Down:
                moved = MoveCaretVertical(1, extendSelection: shift);
                break;
            case Keys.A:
                if (ctrl)
                {
                    _editor.SelectAll();
                    moved = true;
                }

                break;
            case Keys.Back:
                if (!IsReadOnly)
                {
                    var editStart = Stopwatch.GetTimestamp();
                    changed = _editor.Backspace(byWord: ctrl);
                    editTicks = Stopwatch.GetTimestamp() - editStart;
                }

                break;
            case Keys.Delete:
                if (!IsReadOnly)
                {
                    var editStart = Stopwatch.GetTimestamp();
                    changed = _editor.Delete(byWord: ctrl);
                    editTicks = Stopwatch.GetTimestamp() - editStart;
                }

                break;
            case Keys.Enter:
                if (!IsReadOnly)
                {
                    var editStart = Stopwatch.GetTimestamp();
                    changed = InsertTextIntoEditor(Environment.NewLine);
                    editTicks = Stopwatch.GetTimestamp() - editStart;
                }

                break;
        }

        if (changed)
        {
            var commitStart = Stopwatch.GetTimestamp();
            CommitEditorText(_editor.ConsumeLastEditDelta());
            commitTicks = Stopwatch.GetTimestamp() - commitStart;
            MarkTextMutationActivity();
        }

        if (changed || moved)
        {
            _preferredCaretX = -1f;
            var ensureStart = Stopwatch.GetTimestamp();
            EnsureCaretVisible();
            ensureTicks = Stopwatch.GetTimestamp() - ensureStart;
            _caretBlinkSeconds = 0f;
            _isCaretVisible = true;
            InvalidateVisual();
        }

        if (changed)
        {
            RecordInputMutationTiming(
                Stopwatch.GetTimestamp() - mutationStart,
                editTicks,
                commitTicks,
                ensureTicks);
        }

        return changed || moved;
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var index = GetTextIndexFromPoint(pointerPosition);
        _editor.SetCaret(index, extendSelection);
        _isSelectingWithPointer = true;
        _preferredCaretX = -1f;
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
        return true;
    }

    internal bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!IsEnabled || !IsFocused || !_isSelectingWithPointer)
        {
            return false;
        }

        var index = GetTextIndexFromPoint(pointerPosition);
        _editor.SetCaret(index, extendSelection: true);
        _preferredCaretX = -1f;
        EnsureCaretVisible();
        _caretBlinkSeconds = 0f;
        _isCaretVisible = true;
        InvalidateVisual();
        return true;
    }

    internal bool HandlePointerUpFromInput()
    {
        if (!_isSelectingWithPointer)
        {
            return false;
        }

        _isSelectingWithPointer = false;
        return true;
    }

    internal bool HandleMouseWheelFromInput(int delta)
    {
        if (!IsEnabled || delta == 0)
        {
            return false;
        }

        var before = _verticalOffset;
        var direction = delta > 0 ? -1f : 1f;
        _verticalOffset += direction * MathF.Max(16f, GetLineHeight() * 3f);
        var view = BuildTextViewportState();
        ClampOffsets(view.Layout, view.ViewportRect);
        if (MathF.Abs(before - _verticalOffset) <= 0.001f)
        {
            return false;
        }

        InvalidateVisual();
        return true;
    }

    private readonly record struct TextViewportLayoutState(
        LayoutRect ViewportRect,
        LayoutResult Layout,
        bool ShowHorizontalScrollBar,
        bool ShowVerticalScrollBar,
        LayoutRect HorizontalTrackRect,
        LayoutRect VerticalTrackRect,
        float MaxHorizontalOffset,
        float MaxVerticalOffset);

    private readonly record struct TextViewportState(
        LayoutRect ViewportRect,
        LayoutResult Layout,
        bool ShowHorizontalScrollBar,
        bool ShowVerticalScrollBar,
        LayoutRect HorizontalTrackRect,
        LayoutRect VerticalTrackRect,
        LayoutRect HorizontalThumbRect,
        LayoutRect VerticalThumbRect,
        float MaxHorizontalOffset,
        float MaxVerticalOffset);

    private readonly record struct LayoutLine(int StartIndex, int Length, float Width);
    private readonly record struct LayoutResult(
        IReadOnlyList<LayoutLine> Lines,
        float MaxLineWidth,
        int TotalLineCount = -1,
        bool IsVirtualized = false);
    private readonly record struct WrappedLineCheckpoint(int LineIndex, int TextIndex);
}

public readonly record struct TextBoxPerformanceSnapshot(
    int CommitCount,
    int DeferredSyncScheduledCount,
    int DeferredSyncFlushCount,
    int ImmediateSyncCount,
    int IncrementalNoWrapEditAttemptCount,
    int IncrementalNoWrapEditSuccessCount,
    int IncrementalVirtualEditSuccessCount,
    int IncrementalVirtualEditFallbackCount,
    int LayoutCacheHitCount,
    int LayoutCacheMissCount,
    int ViewportLayoutBuildCount,
    int FullLayoutBuildCount,
    int VirtualRangeBuildCount,
    int VirtualLineBuildCount,
    double TextSyncMilliseconds,
    int InputMutationSampleCount,
    double LastInputMutationMilliseconds,
    double LastInputEditMilliseconds,
    double LastInputCommitMilliseconds,
    double LastInputEnsureCaretMilliseconds,
    double AverageInputMutationMilliseconds,
    double MaxInputMutationMilliseconds,
    int RenderSampleCount,
    double LastRenderMilliseconds,
    double LastRenderViewportMilliseconds,
    double LastRenderSelectionMilliseconds,
    double LastRenderTextMilliseconds,
    double LastRenderCaretMilliseconds,
    double AverageRenderMilliseconds,
    double MaxRenderMilliseconds,
    int ViewportStateSampleCount,
    int ViewportStateCacheHitCount,
    int ViewportStateCacheMissCount,
    double LastViewportStateMilliseconds,
    double AverageViewportStateMilliseconds,
    double MaxViewportStateMilliseconds,
    int EnsureCaretSampleCount,
    int EnsureCaretFastPathHitCount,
    int EnsureCaretFastPathMissCount,
    double LastEnsureCaretMilliseconds,
    double LastEnsureCaretViewportMilliseconds,
    double LastEnsureCaretLineLookupMilliseconds,
    double LastEnsureCaretWidthMilliseconds,
    double LastEnsureCaretOffsetAdjustMilliseconds,
    double AverageEnsureCaretMilliseconds,
    double MaxEnsureCaretMilliseconds,
    TextEditingBufferDiagnostics BufferDiagnostics);
