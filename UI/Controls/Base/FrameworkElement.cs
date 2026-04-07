using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public class FrameworkElement : UIElement
{
    static FrameworkElement()
    {
        ClipToBoundsProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    }

    [ThreadStatic]
    private static List<long>? _activeMeasureChildTickStack;

    private readonly Dictionary<DependencyProperty, object> _dynamicResourceBindings = new();
    private FrameworkElement? _resourceParent;
    private NameScope? _nameScope;
    private Style? _activeImplicitStyle;
    private bool _isApplyingImplicitStyle;
    private Vector2 _lastArrangedDesiredSize = new(float.NaN, float.NaN);

    public static readonly DependencyProperty NameProperty =
        DependencyProperty.Register(nameof(Name), typeof(string), typeof(FrameworkElement), new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty DataContextProperty =
        DependencyProperty.Register(
            nameof(DataContext),
            typeof(object),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(
            nameof(Width),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && (float.IsNaN(f) || f >= 0f));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(
            nameof(Height),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && (float.IsNaN(f) || f >= 0f));

    public static readonly DependencyProperty MinWidthProperty =
        DependencyProperty.Register(
            nameof(MinWidth),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && f >= 0f);

    public static readonly DependencyProperty MinHeightProperty =
        DependencyProperty.Register(
            nameof(MinHeight),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && f >= 0f);

    public static readonly DependencyProperty MaxWidthProperty =
        DependencyProperty.Register(
            nameof(MaxWidth),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(float.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && f > 0f);

    public static readonly DependencyProperty MaxHeightProperty =
        DependencyProperty.Register(
            nameof(MaxHeight),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(float.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure),
            static value => value is float f && f > 0f);

    public static readonly DependencyProperty MarginProperty =
        DependencyProperty.Register(
            nameof(Margin),
            typeof(Thickness),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(Thickness.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty HorizontalAlignmentProperty =
        DependencyProperty.Register(
            nameof(HorizontalAlignment),
            typeof(HorizontalAlignment),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(HorizontalAlignment.Stretch, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalAlignmentProperty =
        DependencyProperty.Register(
            nameof(VerticalAlignment),
            typeof(VerticalAlignment),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(VerticalAlignment.Stretch, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty StyleProperty =
        DependencyProperty.Register(
            nameof(Style),
            typeof(Style),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BindingGroupProperty =
        DependencyProperty.Register(
            nameof(BindingGroup),
            typeof(BindingGroup),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(
            nameof(FontFamily),
            typeof(FontFamily),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(FontFamily.Empty, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(
            nameof(FontSize),
            typeof(float),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                12f,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(
            nameof(FontWeight),
            typeof(string),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata("Normal", FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontStyleProperty =
        DependencyProperty.Register(
            nameof(FontStyle),
            typeof(string),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata("Normal", FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty CursorProperty =
        DependencyProperty.Register(
            nameof(Cursor),
            typeof(string),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty SnapsToDevicePixelsProperty =
        DependencyProperty.Register(
            nameof(SnapsToDevicePixels),
            typeof(bool),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty UseLayoutRoundingProperty =
        DependencyProperty.Register(
            nameof(UseLayoutRounding),
            typeof(bool),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FocusableProperty =
        DependencyProperty.Register(
            nameof(Focusable),
            typeof(bool),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty RecognizesAccessKeyProperty =
        DependencyProperty.Register(
            nameof(RecognizesAccessKey),
            typeof(bool),
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(false));

    private bool _isMeasureValid;
    private bool _isArrangeValid;
    private Vector2 _previousAvailableSize = new(float.NaN, float.NaN);
    private LayoutRect _arrangeRect;
    private int _measureCallCount;
    private int _arrangeCallCount;
    private int _measureWorkCount;
    private int _arrangeWorkCount;
    private long _measureElapsedTicks;
    private long _measureExclusiveElapsedTicks;
    private long _arrangeElapsedTicks;
    private long _runtimeMeasureSkippedInvisibleCount;
    private long _runtimeMeasureCachedReuseCount;
    private long _runtimeMeasureReusableSizeReuseCount;
    private long _runtimeMeasureParentInvalidationCount;
    private long _runtimeMeasureInvalidatedDuringMeasureCount;
    private long _runtimeArrangeCachedReuseCount;
    private long _runtimeArrangeSkippedInvisibleCount;
    private long _runtimeArrangeRemeasureCount;
    private long _runtimeArrangeParentInvalidationCount;
    private long _runtimeArrangeInvalidatedDuringArrangeCount;
    private long _runtimeLayoutUpdatedRaiseCount;
    private long _runtimeUpdateLayoutCallCount;
    private long _runtimeUpdateLayoutPassCount;
    private long _runtimeUpdateLayoutRecursiveChildCount;
    private long _runtimeUpdateLayoutStableExitCount;
    private long _runtimeUpdateLayoutMaxPassExitCount;
    private long _runtimeUpdateLayoutMeasureRepairCount;
    private long _runtimeUpdateLayoutArrangeRepairCount;
    private long _runtimeInvalidateMeasureCallCount;
    private long _runtimeInvalidateArrangeCallCount;
    private long _runtimeInvalidateVisualCallCount;
    private long _runtimeInvalidateArrangeDirectLayoutOnlyCallCount;
    private long _runtimeInvalidateArrangeDirectLayoutOnlyWithoutRenderCount;
    private long _runtimeSetResourceReferenceCallCount;
    private long _runtimeRefreshResourceBindingsCallCount;
    private long _runtimeResourceBindingRefreshEntryCount;
    private long _runtimeUpdateResourceBindingCallCount;
    private long _runtimeUpdateResourceBindingHitCount;
    private long _runtimeUpdateResourceBindingMissCount;
    private long _runtimeLocalResourcesChangedCallCount;
    private long _runtimeParentResourcesChangedCallCount;
    private long _runtimeApplicationResourcesChangedCallCount;
    private long _runtimeResourceScopeInvalidatedRaiseCount;
    private long _runtimeResourceParentAttachCount;
    private long _runtimeResourceParentDetachCount;
    private long _runtimeDescendantResourcesChangedNotifyCallCount;
    private long _runtimeDescendantDirectResourceRefreshCount;
    private long _runtimeImplicitStyleUpdateCallCount;
    private long _runtimeImplicitStyleSkipControlTypeCount;
    private long _runtimeImplicitStyleSkipPolicyCount;
    private long _runtimeImplicitStyleResourceFoundCount;
    private long _runtimeImplicitStyleAppliedCount;
    private long _runtimeImplicitStyleClearedCount;
    private long _runtimeImplicitStyleNoChangeCount;
    private long _runtimeVisualParentChangedCallCount;
    private long _runtimeVisualParentResourceScopeChangedCount;
    private long _runtimeVisualParentTriggeredUnloadCount;
    private long _runtimeVisualParentTriggeredLoadCount;
    private long _runtimeLogicalParentChangedCallCount;
    private long _runtimeLogicalParentSkippedDueToVisualParentCount;
    private long _runtimeLogicalParentResourceScopeChangedCount;
    private long _runtimeRaiseInitializedCallCount;
    private long _runtimeRaiseLoadedCallCount;
    private long _runtimeRaiseLoadedNoOpCount;
    private long _runtimeRaiseUnloadedCallCount;
    private long _runtimeRaiseUnloadedNoOpCount;
    private long _runtimeDependencyPropertyChangedCallCount;
    private long _runtimeVisibilityPropertyChangedCount;
    private long _runtimeStylePropertyChangedCount;
    private long _runtimeStyleDetachCount;
    private long _runtimeStyleApplyCount;
    private static long _frameMeasureElapsedTicks;
    private static long _frameMeasureExclusiveElapsedTicks;
    private static long _frameArrangeElapsedTicks;
    private static string _frameHottestMeasureElementType = "none";
    private static string _frameHottestMeasureElementName = string.Empty;
    private static string _frameHottestMeasureElementPath = "none";
    private static long _frameHottestMeasureElapsedTicks;
    private static string _frameHottestArrangeElementType = "none";
    private static string _frameHottestArrangeElementName = string.Empty;
    private static string _frameHottestArrangeElementPath = "none";
    private static long _frameHottestArrangeElapsedTicks;
    private static long _diagMeasureCallCount;
    private static long _diagMeasureWorkCount;
    private static long _diagMeasureElapsedTicks;
    private static long _diagMeasureExclusiveElapsedTicks;
    private static long _diagMeasureSkippedInvisibleCount;
    private static long _diagMeasureCachedReuseCount;
    private static long _diagMeasureReusableSizeReuseCount;
    private static long _diagMeasureParentInvalidationCount;
    private static long _diagMeasureInvalidatedDuringMeasureCount;
    private static long _diagArrangeCallCount;
    private static long _diagArrangeWorkCount;
    private static long _diagArrangeElapsedTicks;
    private static long _diagArrangeCachedReuseCount;
    private static long _diagArrangeSkippedInvisibleCount;
    private static long _diagArrangeRemeasureCount;
    private static long _diagArrangeParentInvalidationCount;
    private static long _diagArrangeInvalidatedDuringArrangeCount;
    private static long _diagLayoutUpdatedRaiseCount;
    private static long _diagUpdateLayoutCallCount;
    private static long _diagUpdateLayoutPassCount;
    private static long _diagUpdateLayoutRecursiveChildCount;
    private static long _diagUpdateLayoutStableExitCount;
    private static long _diagUpdateLayoutMaxPassExitCount;
    private static long _diagUpdateLayoutMeasureRepairCount;
    private static long _diagUpdateLayoutArrangeRepairCount;
    private static long _diagInvalidateMeasureCallCount;
    private static long _diagInvalidateArrangeCallCount;
    private static long _diagInvalidateVisualCallCount;
    private static long _diagInvalidateArrangeDirectLayoutOnlyCallCount;
    private static long _diagInvalidateArrangeDirectLayoutOnlyWithoutRenderCount;
    private static long _diagSetResourceReferenceCallCount;
    private static long _diagRefreshResourceBindingsCallCount;
    private static long _diagResourceBindingRefreshEntryCount;
    private static long _diagUpdateResourceBindingCallCount;
    private static long _diagUpdateResourceBindingHitCount;
    private static long _diagUpdateResourceBindingMissCount;
    private static long _diagLocalResourcesChangedCallCount;
    private static long _diagParentResourcesChangedCallCount;
    private static long _diagApplicationResourcesChangedCallCount;
    private static long _diagResourceScopeInvalidatedRaiseCount;
    private static long _diagResourceParentAttachCount;
    private static long _diagResourceParentDetachCount;
    private static long _diagDescendantResourcesChangedNotifyCallCount;
    private static long _diagDescendantDirectResourceRefreshCount;
    private static long _diagImplicitStyleUpdateCallCount;
    private static long _diagImplicitStyleSkipControlTypeCount;
    private static long _diagImplicitStyleSkipPolicyCount;
    private static long _diagImplicitStyleResourceFoundCount;
    private static long _diagImplicitStyleAppliedCount;
    private static long _diagImplicitStyleClearedCount;
    private static long _diagImplicitStyleNoChangeCount;
    private static long _diagVisualParentChangedCallCount;
    private static long _diagVisualParentResourceScopeChangedCount;
    private static long _diagVisualParentTriggeredUnloadCount;
    private static long _diagVisualParentTriggeredLoadCount;
    private static long _diagLogicalParentChangedCallCount;
    private static long _diagLogicalParentSkippedDueToVisualParentCount;
    private static long _diagLogicalParentResourceScopeChangedCount;
    private static long _diagRaiseInitializedCallCount;
    private static long _diagRaiseLoadedCallCount;
    private static long _diagRaiseLoadedNoOpCount;
    private static long _diagRaiseUnloadedCallCount;
    private static long _diagRaiseUnloadedNoOpCount;
    private static long _diagDependencyPropertyChangedCallCount;
    private static long _diagVisibilityPropertyChangedCount;
    private static long _diagStylePropertyChangedCount;
    private static long _diagStyleDetachCount;
    private static long _diagStyleApplyCount;

    public FrameworkElement()
    {
        Resources.Changed += OnResourcesChanged;
    }

    public event EventHandler? Initialized;

    public event EventHandler? Loaded;

    public event EventHandler? Unloaded;

    public event EventHandler? LayoutUpdated;

    internal event EventHandler? ResourceScopeInvalidated;

    public string Name
    {
        get => GetValue<string>(NameProperty) ?? string.Empty;
        set => SetValue(NameProperty, value);
    }

    public object? DataContext
    {
        get => GetValue(DataContextProperty);
        set => SetValue(DataContextProperty, value);
    }

    public float Width
    {
        get => GetValue<float>(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public float Height
    {
        get => GetValue<float>(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    public float MinWidth
    {
        get => GetValue<float>(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    public float MinHeight
    {
        get => GetValue<float>(MinHeightProperty);
        set => SetValue(MinHeightProperty, value);
    }

    public float MaxWidth
    {
        get => GetValue<float>(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    public float MaxHeight
    {
        get => GetValue<float>(MaxHeightProperty);
        set => SetValue(MaxHeightProperty, value);
    }

    public Thickness Margin
    {
        get => GetValue<Thickness>(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    public HorizontalAlignment HorizontalAlignment
    {
        get => GetValue<HorizontalAlignment>(HorizontalAlignmentProperty);
        set => SetValue(HorizontalAlignmentProperty, value);
    }

    public VerticalAlignment VerticalAlignment
    {
        get => GetValue<VerticalAlignment>(VerticalAlignmentProperty);
        set => SetValue(VerticalAlignmentProperty, value);
    }

    public Style? Style
    {
        get => GetValue<Style>(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    public BindingGroup? BindingGroup
    {
        get => GetValue<BindingGroup>(BindingGroupProperty);
        set => SetValue(BindingGroupProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue<FontFamily>(FontFamilyProperty) ?? FontFamily.Empty;
        set => SetValue(FontFamilyProperty, value);
    }

    public float FontSize
    {
        get => GetValue<float>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public string FontWeight
    {
        get => GetValue<string>(FontWeightProperty) ?? "Normal";
        set => SetValue(FontWeightProperty, value);
    }

    public string FontStyle
    {
        get => GetValue<string>(FontStyleProperty) ?? "Normal";
        set => SetValue(FontStyleProperty, value);
    }

    public string Cursor
    {
        get => GetValue<string>(CursorProperty) ?? string.Empty;
        set => SetValue(CursorProperty, value);
    }

    public bool SnapsToDevicePixels
    {
        get => GetValue<bool>(SnapsToDevicePixelsProperty);
        set => SetValue(SnapsToDevicePixelsProperty, value);
    }

    public bool UseLayoutRounding
    {
        get => GetValue<bool>(UseLayoutRoundingProperty);
        set => SetValue(UseLayoutRoundingProperty, value);
    }

    public bool Focusable
    {
        get => GetValue<bool>(FocusableProperty);
        set => SetValue(FocusableProperty, value);
    }

    public bool RecognizesAccessKey
    {
        get => GetValue<bool>(RecognizesAccessKeyProperty);
        set => SetValue(RecognizesAccessKeyProperty, value);
    }

    public InkkSlinger.ContextMenu? ContextMenu
    {
        get => InkkSlinger.ContextMenu.GetContextMenu(this);
        set => InkkSlinger.ContextMenu.SetContextMenu(this, value);
    }

    public ResourceDictionary Resources { get; } = new();

    public Vector2 DesiredSize { get; private set; }

    public Vector2 RenderSize { get; private set; }

    public float ActualWidth => RenderSize.X;

    public float ActualHeight => RenderSize.Y;

    public bool IsLoaded { get; private set; }

    public int MeasureCallCount => _measureCallCount;

    public int ArrangeCallCount => _arrangeCallCount;

    internal int MeasureWorkCount => _measureWorkCount;

    internal int ArrangeWorkCount => _arrangeWorkCount;

    internal Vector2 PreviousAvailableSizeForTests => _previousAvailableSize;

    internal bool IsMeasureValidForTests => _isMeasureValid;

    internal bool IsArrangeValidForTests => _isArrangeValid;

    internal long MeasureElapsedTicksForTests => _measureElapsedTicks;

    internal long MeasureExclusiveElapsedTicksForTests => _measureExclusiveElapsedTicks;

    internal long ArrangeElapsedTicksForTests => _arrangeElapsedTicks;

    internal static FrameworkLayoutTimingSnapshot GetFrameTimingSnapshotForTests()
    {
        return new FrameworkLayoutTimingSnapshot(
            _frameMeasureElapsedTicks,
            _frameMeasureExclusiveElapsedTicks,
            _frameArrangeElapsedTicks,
            _frameHottestMeasureElementType,
            _frameHottestMeasureElementName,
            _frameHottestMeasureElementPath,
            _frameHottestMeasureElapsedTicks,
            _frameHottestArrangeElementType,
            _frameHottestArrangeElementName,
            _frameHottestArrangeElementPath,
            _frameHottestArrangeElapsedTicks);
    }

    internal FrameworkElementDiagnosticsSnapshot GetFrameworkElementSnapshotForDiagnostics()
    {
        return new FrameworkElementDiagnosticsSnapshot(
            LayoutSlot,
            DesiredSize,
            RenderSize,
            _previousAvailableSize,
            _arrangeRect,
            _lastArrangedDesiredSize,
            _isMeasureValid,
            _isArrangeValid,
            IsLoaded,
            IsVisible,
            IsEnabled,
            UseLayoutRounding,
            _measureCallCount,
            _measureWorkCount,
            _arrangeCallCount,
            _arrangeWorkCount,
            TicksToMilliseconds(_measureElapsedTicks),
            TicksToMilliseconds(_measureExclusiveElapsedTicks),
            TicksToMilliseconds(_arrangeElapsedTicks),
            _runtimeMeasureSkippedInvisibleCount,
            _runtimeMeasureCachedReuseCount,
            _runtimeMeasureReusableSizeReuseCount,
            _runtimeMeasureParentInvalidationCount,
            _runtimeMeasureInvalidatedDuringMeasureCount,
            _runtimeArrangeCachedReuseCount,
            _runtimeArrangeSkippedInvisibleCount,
            _runtimeArrangeRemeasureCount,
            _runtimeArrangeParentInvalidationCount,
            _runtimeArrangeInvalidatedDuringArrangeCount,
            _runtimeLayoutUpdatedRaiseCount,
            _runtimeUpdateLayoutCallCount,
            _runtimeUpdateLayoutPassCount,
            _runtimeUpdateLayoutRecursiveChildCount,
            _runtimeUpdateLayoutStableExitCount,
            _runtimeUpdateLayoutMaxPassExitCount,
            _runtimeUpdateLayoutMeasureRepairCount,
            _runtimeUpdateLayoutArrangeRepairCount,
            _runtimeInvalidateMeasureCallCount,
            _runtimeInvalidateArrangeCallCount,
            _runtimeInvalidateVisualCallCount,
            _runtimeInvalidateArrangeDirectLayoutOnlyCallCount,
            _runtimeInvalidateArrangeDirectLayoutOnlyWithoutRenderCount,
            _runtimeSetResourceReferenceCallCount,
            _runtimeRefreshResourceBindingsCallCount,
            _runtimeResourceBindingRefreshEntryCount,
            _runtimeUpdateResourceBindingCallCount,
            _runtimeUpdateResourceBindingHitCount,
            _runtimeUpdateResourceBindingMissCount,
            _runtimeLocalResourcesChangedCallCount,
            _runtimeParentResourcesChangedCallCount,
            _runtimeApplicationResourcesChangedCallCount,
            _runtimeResourceScopeInvalidatedRaiseCount,
            _runtimeResourceParentAttachCount,
            _runtimeResourceParentDetachCount,
            _runtimeDescendantResourcesChangedNotifyCallCount,
            _runtimeDescendantDirectResourceRefreshCount,
            _runtimeImplicitStyleUpdateCallCount,
            _runtimeImplicitStyleSkipControlTypeCount,
            _runtimeImplicitStyleSkipPolicyCount,
            _runtimeImplicitStyleResourceFoundCount,
            _runtimeImplicitStyleAppliedCount,
            _runtimeImplicitStyleClearedCount,
            _runtimeImplicitStyleNoChangeCount,
            _runtimeVisualParentChangedCallCount,
            _runtimeVisualParentResourceScopeChangedCount,
            _runtimeVisualParentTriggeredUnloadCount,
            _runtimeVisualParentTriggeredLoadCount,
            _runtimeLogicalParentChangedCallCount,
            _runtimeLogicalParentSkippedDueToVisualParentCount,
            _runtimeLogicalParentResourceScopeChangedCount,
            _runtimeRaiseInitializedCallCount,
            _runtimeRaiseLoadedCallCount,
            _runtimeRaiseLoadedNoOpCount,
            _runtimeRaiseUnloadedCallCount,
            _runtimeRaiseUnloadedNoOpCount,
            _runtimeDependencyPropertyChangedCallCount,
            _runtimeVisibilityPropertyChangedCount,
            _runtimeStylePropertyChangedCount,
            _runtimeStyleDetachCount,
            _runtimeStyleApplyCount,
            InvalidationDiagnosticsForTests);
    }

    internal static FrameworkElementTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateAggregateTelemetrySnapshot(reset: true);
    }

    internal static FrameworkElementTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot(reset: false);
    }

    private static FrameworkElementTelemetrySnapshot CreateAggregateTelemetrySnapshot(bool reset)
    {
        return new FrameworkElementTelemetrySnapshot(
            ReadOrReset(ref _diagMeasureCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureElapsedTicks, reset)),
            ReadOrReset(ref _diagMeasureWorkCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureExclusiveElapsedTicks, reset)),
            ReadOrReset(ref _diagMeasureSkippedInvisibleCount, reset),
            ReadOrReset(ref _diagMeasureCachedReuseCount, reset),
            ReadOrReset(ref _diagMeasureReusableSizeReuseCount, reset),
            ReadOrReset(ref _diagMeasureParentInvalidationCount, reset),
            ReadOrReset(ref _diagMeasureInvalidatedDuringMeasureCount, reset),
            ReadOrReset(ref _diagArrangeCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagArrangeElapsedTicks, reset)),
            ReadOrReset(ref _diagArrangeWorkCount, reset),
            ReadOrReset(ref _diagArrangeCachedReuseCount, reset),
            ReadOrReset(ref _diagArrangeSkippedInvisibleCount, reset),
            ReadOrReset(ref _diagArrangeRemeasureCount, reset),
            ReadOrReset(ref _diagArrangeParentInvalidationCount, reset),
            ReadOrReset(ref _diagArrangeInvalidatedDuringArrangeCount, reset),
            ReadOrReset(ref _diagLayoutUpdatedRaiseCount, reset),
            ReadOrReset(ref _diagUpdateLayoutCallCount, reset),
            ReadOrReset(ref _diagUpdateLayoutPassCount, reset),
            ReadOrReset(ref _diagUpdateLayoutRecursiveChildCount, reset),
            ReadOrReset(ref _diagUpdateLayoutStableExitCount, reset),
            ReadOrReset(ref _diagUpdateLayoutMaxPassExitCount, reset),
            ReadOrReset(ref _diagUpdateLayoutMeasureRepairCount, reset),
            ReadOrReset(ref _diagUpdateLayoutArrangeRepairCount, reset),
            ReadOrReset(ref _diagInvalidateMeasureCallCount, reset),
            ReadOrReset(ref _diagInvalidateArrangeCallCount, reset),
            ReadOrReset(ref _diagInvalidateVisualCallCount, reset),
            ReadOrReset(ref _diagInvalidateArrangeDirectLayoutOnlyCallCount, reset),
            ReadOrReset(ref _diagInvalidateArrangeDirectLayoutOnlyWithoutRenderCount, reset),
            ReadOrReset(ref _diagSetResourceReferenceCallCount, reset),
            ReadOrReset(ref _diagRefreshResourceBindingsCallCount, reset),
            ReadOrReset(ref _diagResourceBindingRefreshEntryCount, reset),
            ReadOrReset(ref _diagUpdateResourceBindingCallCount, reset),
            ReadOrReset(ref _diagUpdateResourceBindingHitCount, reset),
            ReadOrReset(ref _diagUpdateResourceBindingMissCount, reset),
            ReadOrReset(ref _diagLocalResourcesChangedCallCount, reset),
            ReadOrReset(ref _diagParentResourcesChangedCallCount, reset),
            ReadOrReset(ref _diagApplicationResourcesChangedCallCount, reset),
            ReadOrReset(ref _diagResourceScopeInvalidatedRaiseCount, reset),
            ReadOrReset(ref _diagResourceParentAttachCount, reset),
            ReadOrReset(ref _diagResourceParentDetachCount, reset),
            ReadOrReset(ref _diagDescendantResourcesChangedNotifyCallCount, reset),
            ReadOrReset(ref _diagDescendantDirectResourceRefreshCount, reset),
            ReadOrReset(ref _diagImplicitStyleUpdateCallCount, reset),
            ReadOrReset(ref _diagImplicitStyleSkipControlTypeCount, reset),
            ReadOrReset(ref _diagImplicitStyleSkipPolicyCount, reset),
            ReadOrReset(ref _diagImplicitStyleResourceFoundCount, reset),
            ReadOrReset(ref _diagImplicitStyleAppliedCount, reset),
            ReadOrReset(ref _diagImplicitStyleClearedCount, reset),
            ReadOrReset(ref _diagImplicitStyleNoChangeCount, reset),
            ReadOrReset(ref _diagVisualParentChangedCallCount, reset),
            ReadOrReset(ref _diagVisualParentResourceScopeChangedCount, reset),
            ReadOrReset(ref _diagVisualParentTriggeredUnloadCount, reset),
            ReadOrReset(ref _diagVisualParentTriggeredLoadCount, reset),
            ReadOrReset(ref _diagLogicalParentChangedCallCount, reset),
            ReadOrReset(ref _diagLogicalParentSkippedDueToVisualParentCount, reset),
            ReadOrReset(ref _diagLogicalParentResourceScopeChangedCount, reset),
            ReadOrReset(ref _diagRaiseInitializedCallCount, reset),
            ReadOrReset(ref _diagRaiseLoadedCallCount, reset),
            ReadOrReset(ref _diagRaiseLoadedNoOpCount, reset),
            ReadOrReset(ref _diagRaiseUnloadedCallCount, reset),
            ReadOrReset(ref _diagRaiseUnloadedNoOpCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedCallCount, reset),
            ReadOrReset(ref _diagVisibilityPropertyChangedCount, reset),
            ReadOrReset(ref _diagStylePropertyChangedCount, reset),
            ReadOrReset(ref _diagStyleDetachCount, reset),
            ReadOrReset(ref _diagStyleApplyCount, reset));
    }

    internal static void ResetFrameTimingForTests()
    {
        _frameMeasureElapsedTicks = 0L;
        _frameMeasureExclusiveElapsedTicks = 0L;
        _frameArrangeElapsedTicks = 0L;
        _frameHottestMeasureElementType = "none";
        _frameHottestMeasureElementName = string.Empty;
        _frameHottestMeasureElementPath = "none";
        _frameHottestMeasureElapsedTicks = 0L;
        _frameHottestArrangeElementType = "none";
        _frameHottestArrangeElementName = string.Empty;
        _frameHottestArrangeElementPath = "none";
        _frameHottestArrangeElapsedTicks = 0L;
    }

    private static string BuildDiagnosticElementPath(FrameworkElement element)
    {
        const int maxSegments = 6;
        var segments = new List<string>(maxSegments);
        FrameworkElement? current = element;
        while (current != null && segments.Count < maxSegments)
        {
            segments.Add(DescribeElementForTiming(current));
            current = current.VisualParent as FrameworkElement;
        }

        segments.Reverse();
        return string.Join(" > ", segments);
    }

    private static string DescribeElementForTiming(FrameworkElement element)
    {
        return string.IsNullOrEmpty(element.Name)
            ? $"{element.GetType().Name}#"
            : $"{element.GetType().Name}#{element.Name}";
    }

    private static void IncrementAggregate(ref long counter)
    {
        Interlocked.Increment(ref counter);
    }

    private static void AddAggregate(ref long counter, long value)
    {
        Interlocked.Add(ref counter, value);
    }

    private static void IncrementDiagnostic(ref long runtimeCounter, ref long aggregateCounter)
    {
        runtimeCounter++;
        Interlocked.Increment(ref aggregateCounter);
    }

    private static long ReadAggregate(ref long counter)
    {
        return Interlocked.Read(ref counter);
    }

    private static long ResetAggregate(ref long counter)
    {
        return Interlocked.Exchange(ref counter, 0L);
    }

    private static long ReadOrReset(ref long counter, bool reset)
    {
        return reset ? ResetAggregate(ref counter) : ReadAggregate(ref counter);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    internal NameScope? GetLocalNameScope()
    {
        return _nameScope;
    }

    internal void EnsureNameScope()
    {
        _nameScope ??= new NameScope();
    }

    internal void RegisterNameInLocalScope(string name, object value)
    {
        EnsureNameScope();
        _nameScope!.RegisterName(name, value);
    }

    public object FindResource(object key)
    {
        if (TryFindResource(key, out var value))
        {
            return value!;
        }

        throw new KeyNotFoundException($"Resource key '{key}' was not found.");
    }

    public bool TryFindResource(object key, out object? resource)
    {
        return ResourceResolver.TryFindResource(this, key, out resource);
    }

    public void SetResourceReference(DependencyProperty dependencyProperty, object resourceKey)
    {
        IncrementDiagnostic(ref _runtimeSetResourceReferenceCallCount, ref _diagSetResourceReferenceCallCount);
        _dynamicResourceBindings[dependencyProperty] = resourceKey;
        UpdateResourceBinding(dependencyProperty, resourceKey);
    }

    public void Measure(Vector2 availableSize)
    {
        Dispatcher.VerifyAccess();
        _measureCallCount++;
        IncrementAggregate(ref _diagMeasureCallCount);
        var useLayoutRounding = UseLayoutRounding;
        var effectiveAvailableSize = useLayoutRounding
            ? RoundLayoutSize(availableSize)
            : availableSize;

        if (!IsVisible)
        {
            _measureWorkCount++;
            IncrementAggregate(ref _diagMeasureWorkCount);
            IncrementDiagnostic(ref _runtimeMeasureSkippedInvisibleCount, ref _diagMeasureSkippedInvisibleCount);
            DesiredSize = new Vector2(
                0f,
                0f);
            _previousAvailableSize = effectiveAvailableSize;
            _isMeasureValid = true;
            ClearMeasureInvalidation();
            return;
        }

        if (_isMeasureValid && _previousAvailableSize == effectiveAvailableSize)
        {
            IncrementDiagnostic(ref _runtimeMeasureCachedReuseCount, ref _diagMeasureCachedReuseCount);
            return;
        }

        if (_isMeasureValid &&
            CanReuseMeasureForAvailableSizeChange(_previousAvailableSize, effectiveAvailableSize))
        {
            IncrementDiagnostic(ref _runtimeMeasureReusableSizeReuseCount, ref _diagMeasureReusableSizeReuseCount);
            _previousAvailableSize = effectiveAvailableSize;
            return;
        }

        _measureWorkCount++;
        IncrementAggregate(ref _diagMeasureWorkCount);
        _previousAvailableSize = effectiveAvailableSize;
        var previousDesiredSize = DesiredSize;
        var measureStart = Stopwatch.GetTimestamp();
        var measureChildTickStack = _activeMeasureChildTickStack ??= new List<long>();
        measureChildTickStack.Add(0L);

        try
        {
            var measureInvalidationCountBeforeOverride = MeasureInvalidationCount;
            var arrangeInvalidationCountBeforeOverride = ArrangeInvalidationCount;
            var margin = Margin;
            var innerAvailable = new Vector2(
                MathF.Max(0f, effectiveAvailableSize.X - margin.Horizontal),
                MathF.Max(0f, effectiveAvailableSize.Y - margin.Vertical));
            innerAvailable = ConstrainMeasureAvailableSize(innerAvailable);

            var measured = MeasureOverride(innerAvailable);
            measured = ApplyExplicitConstraints(measured);
            if (useLayoutRounding)
            {
                measured = RoundLayoutSize(measured);
            }

            var desired = new Vector2(
                measured.X + margin.Horizontal,
                measured.Y + margin.Vertical);
            if (useLayoutRounding)
            {
                desired = RoundLayoutSize(desired);
            }

            DesiredSize = desired;
            if (!AreSizesEqual(previousDesiredSize, DesiredSize) &&
                _measureCallCount > 1 &&
                VisualParent is FrameworkElement parent &&
                !parent.NeedsMeasure)
            {
                IncrementDiagnostic(ref _runtimeMeasureParentInvalidationCount, ref _diagMeasureParentInvalidationCount);
                parent.InvalidateMeasure();
            }

            var invalidatedDuringMeasure =
                MeasureInvalidationCount != measureInvalidationCountBeforeOverride ||
                ArrangeInvalidationCount != arrangeInvalidationCountBeforeOverride;
            if (!invalidatedDuringMeasure)
            {
                _isMeasureValid = true;
                ClearMeasureInvalidation();
            }
            else
            {
                IncrementDiagnostic(ref _runtimeMeasureInvalidatedDuringMeasureCount, ref _diagMeasureInvalidatedDuringMeasureCount);
            }
        }
        finally
        {
            var totalMeasureTicks = Stopwatch.GetTimestamp() - measureStart;
            var lastIndex = measureChildTickStack.Count - 1;
            var childMeasureTicks = measureChildTickStack[lastIndex];
            measureChildTickStack.RemoveAt(lastIndex);

            _measureElapsedTicks += totalMeasureTicks;
            var exclusiveMeasureTicks = Math.Max(0L, totalMeasureTicks - childMeasureTicks);
            _measureExclusiveElapsedTicks += exclusiveMeasureTicks;
            AddAggregate(ref _diagMeasureElapsedTicks, totalMeasureTicks);
            AddAggregate(ref _diagMeasureExclusiveElapsedTicks, exclusiveMeasureTicks);
            _frameMeasureElapsedTicks += totalMeasureTicks;
            _frameMeasureExclusiveElapsedTicks += exclusiveMeasureTicks;
            if (totalMeasureTicks > _frameHottestMeasureElapsedTicks)
            {
                _frameHottestMeasureElapsedTicks = totalMeasureTicks;
                _frameHottestMeasureElementType = GetType().Name;
                _frameHottestMeasureElementName = Name;
                _frameHottestMeasureElementPath = BuildDiagnosticElementPath(this);
            }

            if (measureChildTickStack.Count > 0)
            {
                measureChildTickStack[^1] += totalMeasureTicks;
            }
        }
    }

    public void Arrange(LayoutRect finalRect)
    {
        Dispatcher.VerifyAccess();
        _arrangeCallCount++;
        IncrementAggregate(ref _diagArrangeCallCount);
        var useLayoutRounding = UseLayoutRounding;
        var effectiveFinalRect = useLayoutRounding
            ? RoundLayoutRect(finalRect)
            : finalRect;
        var arrangeAvailableSize = new Vector2(effectiveFinalRect.Width, effectiveFinalRect.Height);
        var requiresArrangeRemeasure =
            _isMeasureValid &&
            ShouldReMeasureForArrange(_previousAvailableSize, arrangeAvailableSize) &&
            !CanReuseMeasureForAvailableSizeChange(_previousAvailableSize, arrangeAvailableSize);

        if (_isArrangeValid &&
            _isMeasureValid &&
            AreRectsEqual(_arrangeRect, effectiveFinalRect) &&
            AreSizesEqual(_lastArrangedDesiredSize, DesiredSize) &&
            !requiresArrangeRemeasure)
        {
            IncrementDiagnostic(ref _runtimeArrangeCachedReuseCount, ref _diagArrangeCachedReuseCount);
            return;
        }

        _arrangeWorkCount++;
        IncrementAggregate(ref _diagArrangeWorkCount);
        _arrangeRect = effectiveFinalRect;
        var arrangeStart = Stopwatch.GetTimestamp();

        if (!IsVisible)
        {
            IncrementDiagnostic(ref _runtimeArrangeSkippedInvisibleCount, ref _diagArrangeSkippedInvisibleCount);
            SetLayoutSlot(effectiveFinalRect);
            RenderSize = Vector2.Zero;
            _isArrangeValid = true;
            ClearArrangeInvalidation();
            var invisibleArrangeTicks = Stopwatch.GetTimestamp() - arrangeStart;
            _arrangeElapsedTicks += invisibleArrangeTicks;
            AddAggregate(ref _diagArrangeElapsedTicks, invisibleArrangeTicks);
            _frameArrangeElapsedTicks += invisibleArrangeTicks;
            if (invisibleArrangeTicks > _frameHottestArrangeElapsedTicks)
            {
                _frameHottestArrangeElapsedTicks = invisibleArrangeTicks;
                _frameHottestArrangeElementType = GetType().Name;
                _frameHottestArrangeElementName = Name;
                _frameHottestArrangeElementPath = BuildDiagnosticElementPath(this);
            }
            return;
        }

        if (!_isMeasureValid)
        {
            Measure(new Vector2(effectiveFinalRect.Width, effectiveFinalRect.Height));
        }
        else
        {
            if (ShouldReMeasureForArrange(_previousAvailableSize, arrangeAvailableSize) &&
                !CanReuseMeasureForAvailableSizeChange(_previousAvailableSize, arrangeAvailableSize))
            {
                IncrementDiagnostic(ref _runtimeArrangeRemeasureCount, ref _diagArrangeRemeasureCount);
                var previousDesiredSize = DesiredSize;
                Measure(arrangeAvailableSize);
                if (!AreSizesEqual(previousDesiredSize, DesiredSize) &&
                    VisualParent is FrameworkElement parent &&
                    !parent.NeedsMeasure)
                {
                    IncrementDiagnostic(ref _runtimeArrangeParentInvalidationCount, ref _diagArrangeParentInvalidationCount);
                    parent.InvalidateMeasure();
                }
            }
        }

        var margin = Margin;
        var clientX = effectiveFinalRect.X + margin.Left;
        var clientY = effectiveFinalRect.Y + margin.Top;
        var clientWidth = MathF.Max(0f, effectiveFinalRect.Width - margin.Horizontal);
        var clientHeight = MathF.Max(0f, effectiveFinalRect.Height - margin.Vertical);

        var arrangedWidth = ResolveAlignedSize(clientWidth, DesiredSize.X - margin.Horizontal, Width, HorizontalAlignment);
        var arrangedHeight = ResolveAlignedSize(clientHeight, DesiredSize.Y - margin.Vertical, Height, VerticalAlignment);

        arrangedWidth = Clamp(arrangedWidth, MinWidth, MaxWidth);
        arrangedHeight = Clamp(arrangedHeight, MinHeight, MaxHeight);

        var arrangedX = ResolveAlignedPosition(clientX, clientWidth, arrangedWidth, HorizontalAlignment);
        var arrangedY = ResolveAlignedPosition(clientY, clientHeight, arrangedHeight, VerticalAlignment);
        if (useLayoutRounding)
        {
            var roundedAlignedRect = RoundLayoutRect(new LayoutRect(arrangedX, arrangedY, arrangedWidth, arrangedHeight));
            arrangedX = roundedAlignedRect.X;
            arrangedY = roundedAlignedRect.Y;
            arrangedWidth = roundedAlignedRect.Width;
            arrangedHeight = roundedAlignedRect.Height;
        }

        // ArrangeOverride needs the final aligned origin for child layout decisions.
        SetLayoutSlot(new LayoutRect(arrangedX, arrangedY, arrangedWidth, arrangedHeight));
        var measureInvalidationCountBeforeOverride = MeasureInvalidationCount;
        var arrangeInvalidationCountBeforeOverride = ArrangeInvalidationCount;
        RenderSize = ArrangeOverride(new Vector2(arrangedWidth, arrangedHeight));
        if (useLayoutRounding)
        {
            RenderSize = RoundLayoutSize(RenderSize);
        }

        var finalLayoutSlot = new LayoutRect(arrangedX, arrangedY, RenderSize.X, RenderSize.Y);
        if (useLayoutRounding)
        {
            finalLayoutSlot = RoundLayoutRect(finalLayoutSlot);
            RenderSize = new Vector2(finalLayoutSlot.Width, finalLayoutSlot.Height);
        }

        SetLayoutSlot(finalLayoutSlot);
        _lastArrangedDesiredSize = DesiredSize;

        var invalidatedDuringArrange =
            MeasureInvalidationCount != measureInvalidationCountBeforeOverride ||
            ArrangeInvalidationCount != arrangeInvalidationCountBeforeOverride;
        if (!invalidatedDuringArrange)
        {
            _isArrangeValid = true;
            ClearArrangeInvalidation();
            IncrementDiagnostic(ref _runtimeLayoutUpdatedRaiseCount, ref _diagLayoutUpdatedRaiseCount);
            LayoutUpdated?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            IncrementDiagnostic(ref _runtimeArrangeInvalidatedDuringArrangeCount, ref _diagArrangeInvalidatedDuringArrangeCount);
        }
        var arrangeTicks = Stopwatch.GetTimestamp() - arrangeStart;
        _arrangeElapsedTicks += arrangeTicks;
        AddAggregate(ref _diagArrangeElapsedTicks, arrangeTicks);
        _frameArrangeElapsedTicks += arrangeTicks;
        if (arrangeTicks > _frameHottestArrangeElapsedTicks)
        {
            _frameHottestArrangeElapsedTicks = arrangeTicks;
            _frameHottestArrangeElementType = GetType().Name;
            _frameHottestArrangeElementName = Name;
            _frameHottestArrangeElementPath = BuildDiagnosticElementPath(this);
        }
    }

    public override void InvalidateMeasure()
    {
        Dispatcher.VerifyAccess();
        IncrementDiagnostic(ref _runtimeInvalidateMeasureCallCount, ref _diagInvalidateMeasureCallCount);
        _isMeasureValid = false;
        base.InvalidateMeasure();
    }

    public override void InvalidateArrange()
    {
        Dispatcher.VerifyAccess();
        IncrementDiagnostic(ref _runtimeInvalidateArrangeCallCount, ref _diagInvalidateArrangeCallCount);
        _isArrangeValid = false;
        base.InvalidateArrange();
    }

    public override void InvalidateVisual()
    {
        Dispatcher.VerifyAccess();
        IncrementDiagnostic(ref _runtimeInvalidateVisualCallCount, ref _diagInvalidateVisualCallCount);
        base.InvalidateVisual();
    }

    internal void InvalidateArrangeForDirectLayoutOnly(bool invalidateRender = true)
    {
        IncrementDiagnostic(ref _runtimeInvalidateArrangeDirectLayoutOnlyCallCount, ref _diagInvalidateArrangeDirectLayoutOnlyCallCount);
        _isArrangeValid = false;
        if (invalidateRender)
        {
            PrepareArrangeForDirectLayoutOnly();
            return;
        }

        IncrementDiagnostic(ref _runtimeInvalidateArrangeDirectLayoutOnlyWithoutRenderCount, ref _diagInvalidateArrangeDirectLayoutOnlyWithoutRenderCount);
        PrepareArrangeForDirectLayoutWithoutRenderInvalidation();
    }

    public void UpdateLayout()
    {
        Dispatcher.VerifyAccess();
        IncrementDiagnostic(ref _runtimeUpdateLayoutCallCount, ref _diagUpdateLayoutCallCount);
        const int maxPasses = 8;
        for (var pass = 0; pass < maxPasses; pass++)
        {
            _runtimeUpdateLayoutPassCount++;
            IncrementAggregate(ref _diagUpdateLayoutPassCount);
            if (NeedsMeasure && _isMeasureValid)
            {
                IncrementDiagnostic(ref _runtimeUpdateLayoutMeasureRepairCount, ref _diagUpdateLayoutMeasureRepairCount);
                _isMeasureValid = false;
            }

            if (NeedsArrange && _isArrangeValid)
            {
                IncrementDiagnostic(ref _runtimeUpdateLayoutArrangeRepairCount, ref _diagUpdateLayoutArrangeRepairCount);
                _isArrangeValid = false;
            }

            if (!_isMeasureValid)
            {
                Measure(new Vector2(_arrangeRect.Width, _arrangeRect.Height));
            }

            if (!_isArrangeValid)
            {
                Arrange(_arrangeRect);
            }

            foreach (var child in GetVisualChildren())
            {
                if (child is FrameworkElement frameworkChild)
                {
                    if (!RequiresUpdateLayoutTraversal(frameworkChild))
                    {
                        continue;
                    }

                    IncrementDiagnostic(ref _runtimeUpdateLayoutRecursiveChildCount, ref _diagUpdateLayoutRecursiveChildCount);
                    frameworkChild.UpdateLayout();
                }
            }

            if (_isMeasureValid && _isArrangeValid)
            {
                IncrementDiagnostic(ref _runtimeUpdateLayoutStableExitCount, ref _diagUpdateLayoutStableExitCount);
                return;
            }
        }

        IncrementDiagnostic(ref _runtimeUpdateLayoutMaxPassExitCount, ref _diagUpdateLayoutMaxPassExitCount);
    }

    private static bool RequiresUpdateLayoutTraversal(FrameworkElement element)
    {
        if (element.NeedsMeasure ||
            element.NeedsArrange ||
            !element._isMeasureValid ||
            !element._isArrangeValid)
        {
            return true;
        }

        foreach (var child in element.GetVisualChildren())
        {
            if (child is FrameworkElement frameworkChild && RequiresUpdateLayoutTraversal(frameworkChild))
            {
                return true;
            }
        }

        return false;
    }

    public void RaiseInitialized()
    {
        Dispatcher.VerifyAccess();
        IncrementDiagnostic(ref _runtimeRaiseInitializedCallCount, ref _diagRaiseInitializedCallCount);
        Initialized?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseLoaded()
    {
        Dispatcher.VerifyAccess();
        if (IsLoaded)
        {
            IncrementDiagnostic(ref _runtimeRaiseLoadedNoOpCount, ref _diagRaiseLoadedNoOpCount);
            return;
        }

        IncrementDiagnostic(ref _runtimeRaiseLoadedCallCount, ref _diagRaiseLoadedCallCount);
        IsLoaded = true;
        AttachResourceParent(VisualParent as FrameworkElement);
        UiApplication.Current.Resources.Changed += OnApplicationResourcesChanged;
        RefreshResourceBindings();
        UpdateImplicitStyle();
        Loaded?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseUnloaded()
    {
        Dispatcher.VerifyAccess();
        if (!IsLoaded)
        {
            IncrementDiagnostic(ref _runtimeRaiseUnloadedNoOpCount, ref _diagRaiseUnloadedNoOpCount);
            return;
        }

        IncrementDiagnostic(ref _runtimeRaiseUnloadedCallCount, ref _diagRaiseUnloadedCallCount);
        IsLoaded = false;
        DetachResourceParent();
        UiApplication.Current.Resources.Changed -= OnApplicationResourcesChanged;
        Unloaded?.Invoke(this, EventArgs.Empty);
    }

    protected virtual Vector2 MeasureOverride(Vector2 availableSize)
    {
        return Vector2.Zero;
    }

    protected virtual bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        _ = previousAvailableSize;
        _ = nextAvailableSize;
        return false;
    }

    internal bool CanReuseMeasureForAvailableSizeChangeForParentLayout(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
    {
        return CanReuseMeasureForAvailableSizeChange(previousAvailableSize, nextAvailableSize);
    }

    protected virtual Vector2 ArrangeOverride(Vector2 finalSize)
    {
        return finalSize;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        IncrementDiagnostic(ref _runtimeDependencyPropertyChangedCallCount, ref _diagDependencyPropertyChangedCallCount);
        base.OnDependencyPropertyChanged(args);

        var metadata = args.Property.GetMetadata(this);
        var options = metadata.Options;
        if ((options & FrameworkPropertyMetadataOptions.AffectsMeasure) != 0 && NeedsMeasure)
        {
            _isMeasureValid = false;
        }

        if ((options & FrameworkPropertyMetadataOptions.AffectsArrange) != 0 && NeedsArrange)
        {
            _isArrangeValid = false;
        }

        if (ReferenceEquals(args.Property, IsVisibleProperty))
        {
            IncrementDiagnostic(ref _runtimeVisibilityPropertyChangedCount, ref _diagVisibilityPropertyChangedCount);
            var visibilityMetadata = VisibilityProperty.GetMetadata(this);
            if (visibilityMetadata.VisibilityAffectsMeasure)
            {
                InvalidateMeasure();
            }
        }

        if (args.Property == StyleProperty)
        {
            IncrementDiagnostic(ref _runtimeStylePropertyChangedCount, ref _diagStylePropertyChangedCount);
            if (!IsControlType() && !_isApplyingImplicitStyle)
            {
                _activeImplicitStyle = null;
            }

            if (args.OldValue is Style oldStyle)
            {
                IncrementDiagnostic(ref _runtimeStyleDetachCount, ref _diagStyleDetachCount);
                oldStyle.Detach(this);
            }

            if (args.NewValue is Style newStyle)
            {
                IncrementDiagnostic(ref _runtimeStyleApplyCount, ref _diagStyleApplyCount);
                newStyle.Apply(this);
            }
        }
    }

    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        IncrementDiagnostic(ref _runtimeVisualParentChangedCallCount, ref _diagVisualParentChangedCallCount);
        base.OnVisualParentChanged(oldParent, newParent);

        var resourceScopeChanged = HasMaterialResourceScopeChange(oldParent, newParent);
        if (resourceScopeChanged)
        {
            IncrementDiagnostic(ref _runtimeVisualParentResourceScopeChangedCount, ref _diagVisualParentResourceScopeChangedCount);
        }
        DetachResourceParent();
        AttachResourceParent(newParent as FrameworkElement);
        if (resourceScopeChanged)
        {
            RefreshResourceBindings();
            UpdateImplicitStyle();
            RaiseResourceScopeInvalidated();
        }
        if (oldParent is FrameworkElement oldFrameworkParent && oldFrameworkParent.IsLoaded && IsLoaded)
        {
            IncrementDiagnostic(ref _runtimeVisualParentTriggeredUnloadCount, ref _diagVisualParentTriggeredUnloadCount);
            RaiseUnloaded();
        }

        if (newParent is FrameworkElement newFrameworkParent && newFrameworkParent.IsLoaded && !IsLoaded)
        {
            IncrementDiagnostic(ref _runtimeVisualParentTriggeredLoadCount, ref _diagVisualParentTriggeredLoadCount);
            RaiseLoaded();
        }
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        IncrementDiagnostic(ref _runtimeLogicalParentChangedCallCount, ref _diagLogicalParentChangedCallCount);
        base.OnLogicalParentChanged(oldParent, newParent);

        if (VisualParent != null)
        {
            IncrementDiagnostic(ref _runtimeLogicalParentSkippedDueToVisualParentCount, ref _diagLogicalParentSkippedDueToVisualParentCount);
            return;
        }

        var resourceScopeChanged = HasMaterialResourceScopeChange(oldParent, newParent);
        if (resourceScopeChanged)
        {
            IncrementDiagnostic(ref _runtimeLogicalParentResourceScopeChangedCount, ref _diagLogicalParentResourceScopeChangedCount);
        }
        DetachResourceParent();
        AttachResourceParent(newParent as FrameworkElement);
        if (resourceScopeChanged)
        {
            RefreshResourceBindings();
            UpdateImplicitStyle();
            RaiseResourceScopeInvalidated();
        }
        if (oldParent is FrameworkElement oldFrameworkParent && oldFrameworkParent.IsLoaded && IsLoaded)
        {
            RaiseUnloaded();
        }

        if (newParent is FrameworkElement newFrameworkParent && newFrameworkParent.IsLoaded && !IsLoaded)
        {
            RaiseLoaded();
        }
    }

    private void RefreshResourceBindings()
    {
        IncrementDiagnostic(ref _runtimeRefreshResourceBindingsCallCount, ref _diagRefreshResourceBindingsCallCount);
        _runtimeResourceBindingRefreshEntryCount += _dynamicResourceBindings.Count;
        AddAggregate(ref _diagResourceBindingRefreshEntryCount, _dynamicResourceBindings.Count);
        foreach (var pair in _dynamicResourceBindings)
        {
            UpdateResourceBinding(pair.Key, pair.Value);
        }
    }

    private void UpdateResourceBinding(DependencyProperty dependencyProperty, object resourceKey)
    {
        IncrementDiagnostic(ref _runtimeUpdateResourceBindingCallCount, ref _diagUpdateResourceBindingCallCount);
        if (TryFindResource(resourceKey, out var value))
        {
            IncrementDiagnostic(ref _runtimeUpdateResourceBindingHitCount, ref _diagUpdateResourceBindingHitCount);
            SetValue(dependencyProperty, value);
            return;
        }

        IncrementDiagnostic(ref _runtimeUpdateResourceBindingMissCount, ref _diagUpdateResourceBindingMissCount);
    }

    private void OnResourcesChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        IncrementDiagnostic(ref _runtimeLocalResourcesChangedCallCount, ref _diagLocalResourcesChangedCallCount);
        RefreshResourceBindings();
        UpdateImplicitStyle();
        RaiseResourceScopeInvalidated();
        NotifyDescendantResourcesChanged();
    }

    private void OnParentResourcesChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        IncrementDiagnostic(ref _runtimeParentResourcesChangedCallCount, ref _diagParentResourcesChangedCallCount);
        RefreshResourceBindings();
        UpdateImplicitStyle();
        RaiseResourceScopeInvalidated();
        NotifyDescendantResourcesChanged();
    }

    private void OnApplicationResourcesChanged(object? sender, ResourceDictionaryChangedEventArgs e)
    {
        IncrementDiagnostic(ref _runtimeApplicationResourcesChangedCallCount, ref _diagApplicationResourcesChangedCallCount);
        RefreshResourceBindings();
        UpdateImplicitStyle();
        RaiseResourceScopeInvalidated();
        NotifyDescendantResourcesChanged();
    }

    private void UpdateImplicitStyle()
    {
        IncrementDiagnostic(ref _runtimeImplicitStyleUpdateCallCount, ref _diagImplicitStyleUpdateCallCount);
        if (IsControlType())
        {
            IncrementDiagnostic(ref _runtimeImplicitStyleSkipControlTypeCount, ref _diagImplicitStyleSkipControlTypeCount);
            return;
        }

        if (!ImplicitStylePolicy.ShouldApply(Style, _activeImplicitStyle))
        {
            IncrementDiagnostic(ref _runtimeImplicitStyleSkipPolicyCount, ref _diagImplicitStyleSkipPolicyCount);
            return;
        }

        if (TryFindResource(GetType(), out var resource) && resource is Style style)
        {
            IncrementDiagnostic(ref _runtimeImplicitStyleResourceFoundCount, ref _diagImplicitStyleResourceFoundCount);
            if (!ReferenceEquals(Style, style))
            {
                IncrementDiagnostic(ref _runtimeImplicitStyleAppliedCount, ref _diagImplicitStyleAppliedCount);
                _isApplyingImplicitStyle = true;
                try
                {
                    Style = style;
                }
                finally
                {
                    _isApplyingImplicitStyle = false;
                }
            }
            else
            {
                IncrementDiagnostic(ref _runtimeImplicitStyleNoChangeCount, ref _diagImplicitStyleNoChangeCount);
            }

            _activeImplicitStyle = style;
            return;
        }

        if (ImplicitStylePolicy.CanClearImplicit(Style, _activeImplicitStyle))
        {
            IncrementDiagnostic(ref _runtimeImplicitStyleClearedCount, ref _diagImplicitStyleClearedCount);
            _isApplyingImplicitStyle = true;
            try
            {
                Style = null;
            }
            finally
            {
                _isApplyingImplicitStyle = false;
            }
        }
        else
        {
            IncrementDiagnostic(ref _runtimeImplicitStyleNoChangeCount, ref _diagImplicitStyleNoChangeCount);
        }

        _activeImplicitStyle = null;
    }

    private bool IsControlType()
    {
        return this is Control;
    }

    private void AttachResourceParent(FrameworkElement? parent)
    {
        if (ReferenceEquals(_resourceParent, parent))
        {
            return;
        }

        if (_resourceParent != null)
        {
            _resourceParent.Resources.Changed -= OnParentResourcesChanged;
        }

        _resourceParent = parent;

        if (_resourceParent != null)
        {
            IncrementDiagnostic(ref _runtimeResourceParentAttachCount, ref _diagResourceParentAttachCount);
            _resourceParent.Resources.Changed += OnParentResourcesChanged;
        }
    }

    private void DetachResourceParent()
    {
        if (_resourceParent == null)
        {
            return;
        }

        IncrementDiagnostic(ref _runtimeResourceParentDetachCount, ref _diagResourceParentDetachCount);
        _resourceParent.Resources.Changed -= OnParentResourcesChanged;
        _resourceParent = null;
    }

    private void NotifyDescendantResourcesChanged()
    {
        IncrementDiagnostic(ref _runtimeDescendantResourcesChangedNotifyCallCount, ref _diagDescendantResourcesChangedNotifyCallCount);
        foreach (var child in GetVisualChildren())
        {
            if (child is FrameworkElement frameworkChild)
            {
                if (frameworkChild.RequiresDirectResourceScopeRefresh())
                {
                    IncrementDiagnostic(ref _runtimeDescendantDirectResourceRefreshCount, ref _diagDescendantDirectResourceRefreshCount);
                    frameworkChild.RefreshResourceBindings();
                    frameworkChild.RaiseResourceScopeInvalidated();
                }

                frameworkChild.NotifyDescendantResourcesChanged();
            }
        }
    }

    private bool RequiresDirectResourceScopeRefresh()
    {
        return _dynamicResourceBindings.Count > 0 ||
               HasResourceScopeInvalidatedSubscribers() ||
               ShouldRefreshImplicitStyleFromResourceScope();
    }

    private bool HasResourceScopeInvalidatedSubscribers()
    {
        return ResourceScopeInvalidated != null;
    }

    private void RaiseResourceScopeInvalidated()
    {
        IncrementDiagnostic(ref _runtimeResourceScopeInvalidatedRaiseCount, ref _diagResourceScopeInvalidatedRaiseCount);
        ResourceScopeInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private bool ShouldRefreshImplicitStyleFromResourceScope()
    {
        return !IsControlType() && ImplicitStylePolicy.ShouldApply(Style, _activeImplicitStyle);
    }

    private static bool HasMaterialResourceScopeChange(UIElement? oldParent, UIElement? newParent)
    {
        var oldAncestors = GetEffectiveResourceAncestors(oldParent);
        var newAncestors = GetEffectiveResourceAncestors(newParent);
        if (oldAncestors.Count != newAncestors.Count)
        {
            return true;
        }

        for (var index = 0; index < oldAncestors.Count; index++)
        {
            if (!ReferenceEquals(oldAncestors[index], newAncestors[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static List<FrameworkElement> GetEffectiveResourceAncestors(UIElement? parent)
    {
        var ancestors = new List<FrameworkElement>();
        for (var current = parent; current != null; current = current.VisualParent)
        {
            if (current is not FrameworkElement frameworkElement)
            {
                continue;
            }

            if (!HasMaterialResources(frameworkElement.Resources))
            {
                continue;
            }

            ancestors.Add(frameworkElement);
        }

        return ancestors;
    }

    private static bool HasMaterialResources(ResourceDictionary resources)
    {
        return resources.Count > 0 || resources.MergedDictionaries.Count > 0;
    }

    private static float ResolveAlignedSize(
        float available,
        float desired,
        float explicitSize,
        HorizontalAlignment alignment)
    {
        if (!float.IsNaN(explicitSize))
        {
            return explicitSize;
        }

        if (alignment == HorizontalAlignment.Stretch)
        {
            return available;
        }

        return MathF.Min(available, MathF.Max(0f, desired));
    }

    private static float ResolveAlignedSize(
        float available,
        float desired,
        float explicitSize,
        VerticalAlignment alignment)
    {
        if (!float.IsNaN(explicitSize))
        {
            return explicitSize;
        }

        if (alignment == VerticalAlignment.Stretch)
        {
            return available;
        }

        return MathF.Min(available, MathF.Max(0f, desired));
    }

    private static float ResolveAlignedPosition(float start, float available, float size, HorizontalAlignment alignment)
    {
        return alignment switch
        {
            HorizontalAlignment.Center => start + ((available - size) / 2f),
            HorizontalAlignment.Right => start + (available - size),
            _ => start
        };
    }

    private static float ResolveAlignedPosition(float start, float available, float size, VerticalAlignment alignment)
    {
        return alignment switch
        {
            VerticalAlignment.Center => start + ((available - size) / 2f),
            VerticalAlignment.Bottom => start + (available - size),
            _ => start
        };
    }

    private Vector2 ApplyExplicitConstraints(Vector2 measured)
    {
        var width = float.IsNaN(Width) ? measured.X : Width;
        var height = float.IsNaN(Height) ? measured.Y : Height;

        width = Clamp(width, MinWidth, MaxWidth);
        height = Clamp(height, MinHeight, MaxHeight);

        return new Vector2(width, height);
    }

    private Vector2 ConstrainMeasureAvailableSize(Vector2 available)
    {
        return new Vector2(
            ResolveMeasureConstraint(available.X, Width, MinWidth, MaxWidth),
            ResolveMeasureConstraint(available.Y, Height, MinHeight, MaxHeight));
    }

    private static float ResolveMeasureConstraint(float available, float explicitSize, float min, float max)
    {
        var constraint = float.IsNaN(explicitSize) ? available : explicitSize;

        if (float.IsFinite(max))
        {
            constraint = MathF.Min(constraint, max);
        }

        if (float.IsFinite(constraint))
        {
            constraint = MathF.Max(constraint, min);
        }

        return constraint;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static bool AreRectsEqual(LayoutRect left, LayoutRect right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon &&
               MathF.Abs(left.Width - right.Width) <= epsilon &&
               MathF.Abs(left.Height - right.Height) <= epsilon;
    }

    private static bool AreSizesEqual(Vector2 left, Vector2 right)
    {
        const float epsilon = 0.0001f;
        return MathF.Abs(left.X - right.X) <= epsilon &&
               MathF.Abs(left.Y - right.Y) <= epsilon;
    }

    private static bool ShouldReMeasureForArrange(Vector2 measuredAvailableSize, Vector2 arrangedAvailableSize)
    {
        return IsFiniteShrink(measuredAvailableSize.X, arrangedAvailableSize.X) ||
               IsFiniteShrink(measuredAvailableSize.Y, arrangedAvailableSize.Y) ||
               IsFiniteZeroGrowth(measuredAvailableSize.X, arrangedAvailableSize.X) ||
               IsFiniteZeroGrowth(measuredAvailableSize.Y, arrangedAvailableSize.Y);
    }

    private static bool IsFiniteShrink(float previous, float next)
    {
        return float.IsFinite(previous) &&
               float.IsFinite(next) &&
               next + 0.0001f < previous;
    }

    private static bool IsFiniteZeroGrowth(float previous, float next)
    {
        return float.IsFinite(previous) &&
               float.IsFinite(next) &&
               previous <= 0.0001f &&
               next > previous + 0.0001f;
    }

    private static LayoutRect RoundLayoutRect(LayoutRect rect)
    {
        if (!IsFinite(rect.X) || !IsFinite(rect.Y) || !IsFinite(rect.Width) || !IsFinite(rect.Height))
        {
            return new LayoutRect(
                RoundLayoutScalar(rect.X),
                RoundLayoutScalar(rect.Y),
                RoundLayoutScalar(rect.Width),
                RoundLayoutScalar(rect.Height));
        }

        var left = RoundLayoutScalar(rect.X);
        var top = RoundLayoutScalar(rect.Y);
        var right = RoundLayoutScalar(rect.X + rect.Width);
        var bottom = RoundLayoutScalar(rect.Y + rect.Height);
        return new LayoutRect(
            left,
            top,
            MathF.Max(0f, right - left),
            MathF.Max(0f, bottom - top));
    }

    private static Vector2 RoundLayoutSize(Vector2 size)
    {
        return new Vector2(
            RoundLayoutSizeScalar(size.X),
            RoundLayoutSizeScalar(size.Y));
    }

    private static float RoundLayoutSizeScalar(float value)
    {
        if (!IsFinite(value))
        {
            return value;
        }

        return MathF.Max(0f, MathF.Round(value));
    }

    private static float RoundLayoutScalar(float value)
    {
        if (!IsFinite(value))
        {
            return value;
        }

        return MathF.Round(value);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

}

