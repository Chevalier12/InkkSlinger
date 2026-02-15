using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class GridSplitter : Control
{
    public static readonly DependencyProperty ResizeDirectionProperty =
        DependencyProperty.Register(
            nameof(ResizeDirection),
            typeof(GridResizeDirection),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(
                GridResizeDirection.Auto,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ResizeBehaviorProperty =
        DependencyProperty.Register(
            nameof(ResizeBehavior),
            typeof(GridResizeBehavior),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(GridResizeBehavior.BasedOnAlignment));

    public static readonly DependencyProperty KeyboardIncrementProperty =
        DependencyProperty.Register(
            nameof(KeyboardIncrement),
            typeof(float),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(
                10f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float step && step > 0f ? step : 1f));

    public static readonly DependencyProperty DragIncrementProperty =
        DependencyProperty.Register(
            nameof(DragIncrement),
            typeof(float),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float step && step > 0f ? step : 1f));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(new Color(90, 90, 90), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(new Color(152, 152, 152), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(
            nameof(IsMouseOver),
            typeof(bool),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDraggingProperty =
        DependencyProperty.Register(
            nameof(IsDragging),
            typeof(bool),
            typeof(GridSplitter),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private Grid? _activeGrid;
    private GridResizeDirection _activeDirection;
    private int _definitionIndexA = -1;
    private int _definitionIndexB = -1;
    private float _startPointer;
    private float _startSizeA;
    private float _startSizeB;

    public GridSplitter()
    {
    }

    public GridResizeDirection ResizeDirection
    {
        get => GetValue<GridResizeDirection>(ResizeDirectionProperty);
        set => SetValue(ResizeDirectionProperty, value);
    }

    public GridResizeBehavior ResizeBehavior
    {
        get => GetValue<GridResizeBehavior>(ResizeBehaviorProperty);
        set => SetValue(ResizeBehaviorProperty, value);
    }

    public float KeyboardIncrement
    {
        get => GetValue<float>(KeyboardIncrementProperty);
        set => SetValue(KeyboardIncrementProperty, value);
    }

    public float DragIncrement
    {
        get => GetValue<float>(DragIncrementProperty);
        set => SetValue(DragIncrementProperty, value);
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

    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsDragging
    {
        get => GetValue<bool>(IsDraggingProperty);
        private set => SetValue(IsDraggingProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        var direction = ResolveEffectiveResizeDirection();
        if (direction == GridResizeDirection.Columns)
        {
            desired.X = MathF.Max(desired.X, 5f);
            desired.Y = MathF.Max(desired.Y, 20f);
        }
        else
        {
            desired.X = MathF.Max(desired.X, 20f);
            desired.Y = MathF.Max(desired.Y, 5f);
        }

        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        var fill = IsDragging
            ? new Color(112, 170, 220)
            : IsMouseOver ? new Color(104, 104, 104) : Background;

        UiDrawing.DrawFilledRect(spriteBatch, slot, fill, Opacity);
        UiDrawing.DrawRectStroke(spriteBatch, slot, 1f, BorderBrush, Opacity);
    }








    private bool BeginDrag(Vector2 pointerPosition)
    {
        if (VisualParent is not Grid grid)
        {
            return false;
        }

        var direction = ResolveEffectiveResizeDirection();
        if (!TryResolveResizeTargets(grid, direction, out var indexA, out var indexB))
        {
            return false;
        }

        _activeGrid = grid;
        _activeDirection = direction;
        _definitionIndexA = indexA;
        _definitionIndexB = indexB;
        _startPointer = direction == GridResizeDirection.Columns ? pointerPosition.X : pointerPosition.Y;
        _startSizeA = direction == GridResizeDirection.Columns
            ? ResolveColumnSize(grid, indexA)
            : ResolveRowSize(grid, indexA);
        _startSizeB = direction == GridResizeDirection.Columns
            ? ResolveColumnSize(grid, indexB)
            : ResolveRowSize(grid, indexB);

        IsDragging = true;
        return true;
    }

    private void EndDrag(bool releaseCapture)
    {
        IsDragging = false;
        _activeGrid = null;
        _definitionIndexA = -1;
        _definitionIndexB = -1;

        _ = releaseCapture;
    }

    private void ApplyResizeDelta(float delta)
    {
        if (_activeGrid == null || _definitionIndexA < 0 || _definitionIndexB < 0)
        {
            return;
        }

        ApplyResize(
            _activeGrid,
            _activeDirection,
            _definitionIndexA,
            _definitionIndexB,
            _startSizeA,
            _startSizeB,
            delta);
    }

    private static void ApplyResize(
        Grid grid,
        GridResizeDirection direction,
        int indexA,
        int indexB,
        float startSizeA,
        float startSizeB,
        float delta)
    {
        if (direction == GridResizeDirection.Columns)
        {
            if (indexA < 0 || indexA >= grid.ColumnDefinitions.Count ||
                indexB < 0 || indexB >= grid.ColumnDefinitions.Count)
            {
                return;
            }

            var left = grid.ColumnDefinitions[indexA];
            var right = grid.ColumnDefinitions[indexB];
            var minDelta = left.MinWidth - startSizeA;
            var maxDelta = startSizeB - right.MinWidth;
            var bounded = Clamp(delta, minDelta, maxDelta);
            var newA = Clamp(startSizeA + bounded, left.MinWidth, left.MaxWidth);
            var newB = Clamp(startSizeB - bounded, right.MinWidth, right.MaxWidth);
            var total = startSizeA + startSizeB;
            if (newA + newB > 0f && MathF.Abs((newA + newB) - total) > 0.001f)
            {
                newB = MathF.Max(right.MinWidth, total - newA);
            }

            left.Width = new GridLength(newA, GridUnitType.Pixel);
            right.Width = new GridLength(newB, GridUnitType.Pixel);
            return;
        }

        if (indexA < 0 || indexA >= grid.RowDefinitions.Count ||
            indexB < 0 || indexB >= grid.RowDefinitions.Count)
        {
            return;
        }

        var top = grid.RowDefinitions[indexA];
        var bottom = grid.RowDefinitions[indexB];
        var minDeltaRows = top.MinHeight - startSizeA;
        var maxDeltaRows = startSizeB - bottom.MinHeight;
        var boundedRows = Clamp(delta, minDeltaRows, maxDeltaRows);
        var newTop = Clamp(startSizeA + boundedRows, top.MinHeight, top.MaxHeight);
        var newBottom = Clamp(startSizeB - boundedRows, bottom.MinHeight, bottom.MaxHeight);
        var rowTotal = startSizeA + startSizeB;
        if (newTop + newBottom > 0f && MathF.Abs((newTop + newBottom) - rowTotal) > 0.001f)
        {
            newBottom = MathF.Max(bottom.MinHeight, rowTotal - newTop);
        }

        top.Height = new GridLength(newTop, GridUnitType.Pixel);
        bottom.Height = new GridLength(newBottom, GridUnitType.Pixel);
    }

    private bool TryResolveResizeTargets(Grid grid, GridResizeDirection direction, out int indexA, out int indexB)
    {
        indexA = -1;
        indexB = -1;

        if (direction == GridResizeDirection.Columns)
        {
            if (grid.ColumnDefinitions.Count < 2)
            {
                return false;
            }

            var column = ClampIndex(Grid.GetColumn(this), grid.ColumnDefinitions.Count);
            return ResolveDefinitionPair(
                column,
                grid.ColumnDefinitions.Count,
                HorizontalAlignment,
                ResizeBehavior,
                out indexA,
                out indexB);
        }

        if (grid.RowDefinitions.Count < 2)
        {
            return false;
        }

        var row = ClampIndex(Grid.GetRow(this), grid.RowDefinitions.Count);
        return ResolveDefinitionPair(
            row,
            grid.RowDefinitions.Count,
            VerticalAlignment,
            ResizeBehavior,
            out indexA,
            out indexB);
    }

    private GridResizeDirection ResolveEffectiveResizeDirection()
    {
        if (ResizeDirection != GridResizeDirection.Auto)
        {
            return ResizeDirection;
        }

        var width = float.IsNaN(Width) ? MathF.Max(0f, RenderSize.X) : Width;
        var height = float.IsNaN(Height) ? MathF.Max(0f, RenderSize.Y) : Height;
        if (width <= 0f && height <= 0f)
        {
            return HorizontalAlignment == HorizontalAlignment.Stretch
                ? GridResizeDirection.Columns
                : GridResizeDirection.Rows;
        }

        return width <= height ? GridResizeDirection.Columns : GridResizeDirection.Rows;
    }

    private static bool ResolveDefinitionPair(
        int currentIndex,
        int count,
        HorizontalAlignment alignment,
        GridResizeBehavior behavior,
        out int indexA,
        out int indexB)
    {
        if (behavior == GridResizeBehavior.BasedOnAlignment)
        {
            behavior = alignment switch
            {
                HorizontalAlignment.Left => GridResizeBehavior.PreviousAndCurrent,
                HorizontalAlignment.Right => GridResizeBehavior.CurrentAndNext,
                _ => GridResizeBehavior.PreviousAndNext
            };
        }

        indexA = -1;
        indexB = -1;
        switch (behavior)
        {
            case GridResizeBehavior.CurrentAndNext:
                indexA = currentIndex;
                indexB = currentIndex + 1;
                break;
            case GridResizeBehavior.PreviousAndCurrent:
                indexA = currentIndex - 1;
                indexB = currentIndex;
                break;
            case GridResizeBehavior.PreviousAndNext:
                indexA = currentIndex - 1;
                indexB = currentIndex + 1;
                break;
        }

        if (indexA < 0 || indexB < 0 || indexA >= count || indexB >= count || indexA == indexB)
        {
            return false;
        }

        return true;
    }

    private static bool ResolveDefinitionPair(
        int currentIndex,
        int count,
        VerticalAlignment alignment,
        GridResizeBehavior behavior,
        out int indexA,
        out int indexB)
    {
        if (behavior == GridResizeBehavior.BasedOnAlignment)
        {
            behavior = alignment switch
            {
                VerticalAlignment.Top => GridResizeBehavior.PreviousAndCurrent,
                VerticalAlignment.Bottom => GridResizeBehavior.CurrentAndNext,
                _ => GridResizeBehavior.PreviousAndNext
            };
        }

        indexA = -1;
        indexB = -1;
        switch (behavior)
        {
            case GridResizeBehavior.CurrentAndNext:
                indexA = currentIndex;
                indexB = currentIndex + 1;
                break;
            case GridResizeBehavior.PreviousAndCurrent:
                indexA = currentIndex - 1;
                indexB = currentIndex;
                break;
            case GridResizeBehavior.PreviousAndNext:
                indexA = currentIndex - 1;
                indexB = currentIndex + 1;
                break;
        }

        if (indexA < 0 || indexB < 0 || indexA >= count || indexB >= count || indexA == indexB)
        {
            return false;
        }

        return true;
    }

    private static float ResolveColumnSize(Grid grid, int index)
    {
        var definition = grid.ColumnDefinitions[index];
        if (definition.ActualWidth > 0f)
        {
            return definition.ActualWidth;
        }

        if (definition.Width.IsPixel)
        {
            return definition.Width.Value;
        }

        return 0f;
    }

    private static float ResolveRowSize(Grid grid, int index)
    {
        var definition = grid.RowDefinitions[index];
        if (definition.ActualHeight > 0f)
        {
            return definition.ActualHeight;
        }

        if (definition.Height.IsPixel)
        {
            return definition.Height.Value;
        }

        return 0f;
    }

    private static int ClampIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (index < 0)
        {
            return 0;
        }

        if (index >= count)
        {
            return count - 1;
        }

        return index;
    }

    private static float Snap(float delta, float increment)
    {
        if (increment <= 0f)
        {
            return delta;
        }

        return MathF.Round(delta / increment) * increment;
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
}
