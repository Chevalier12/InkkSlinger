using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class UniformGrid : Panel
{
    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(
            nameof(Rows),
            typeof(int),
            typeof(UniformGrid),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange),
            static value => value is int rows && rows >= 0);

    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(
            nameof(Columns),
            typeof(int),
            typeof(UniformGrid),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange),
            static value => value is int columns && columns >= 0);

    public static readonly DependencyProperty FirstColumnProperty =
        DependencyProperty.Register(
            nameof(FirstColumn),
            typeof(int),
            typeof(UniformGrid),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange),
            static value => value is int firstColumn && firstColumn >= 0);

    public int Rows
    {
        get => GetValue<int>(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public int Columns
    {
        get => GetValue<int>(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public int FirstColumn
    {
        get => GetValue<int>(FirstColumnProperty);
        set => SetValue(FirstColumnProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var childCount = GetMeasurableChildrenCount();
        if (childCount == 0)
        {
            return Vector2.Zero;
        }

        var (rows, columns) = ResolveGridDimensions(childCount);
        if (rows <= 0 || columns <= 0)
        {
            return Vector2.Zero;
        }

        var cellAvailable = new Vector2(
            columns > 0 ? availableSize.X / columns : availableSize.X,
            rows > 0 ? availableSize.Y / rows : availableSize.Y);

        var maxChildWidth = 0f;
        var maxChildHeight = 0f;
        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            frameworkChild.Measure(cellAvailable);
            maxChildWidth = MathF.Max(maxChildWidth, frameworkChild.DesiredSize.X);
            maxChildHeight = MathF.Max(maxChildHeight, frameworkChild.DesiredSize.Y);
        }

        return new Vector2(maxChildWidth * columns, maxChildHeight * rows);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var childCount = GetMeasurableChildrenCount();
        if (childCount == 0)
        {
            return finalSize;
        }

        var (rows, columns) = ResolveGridDimensions(childCount);
        if (rows <= 0 || columns <= 0)
        {
            return finalSize;
        }

        var cellWidth = finalSize.X / columns;
        var cellHeight = finalSize.Y / rows;
        var startIndex = Math.Min(FirstColumn, columns - 1);

        var visualIndex = 0;
        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var offsetIndex = visualIndex + startIndex;
            var row = offsetIndex / columns;
            var column = offsetIndex % columns;

            frameworkChild.Arrange(new LayoutRect(
                LayoutSlot.X + (column * cellWidth),
                LayoutSlot.Y + (row * cellHeight),
                cellWidth,
                cellHeight));

            visualIndex++;
        }

        return finalSize;
    }

    private int GetMeasurableChildrenCount()
    {
        var count = 0;
        foreach (var child in Children)
        {
            if (child is FrameworkElement)
            {
                count++;
            }
        }

        return count;
    }

    private (int Rows, int Columns) ResolveGridDimensions(int childCount)
    {
        var rows = Rows;
        var columns = Columns;

        if (rows == 0 && columns == 0)
        {
            columns = (int)MathF.Ceiling(MathF.Sqrt(childCount));
            rows = (int)MathF.Ceiling(childCount / (float)columns);
        }
        else if (rows == 0)
        {
            rows = (int)MathF.Ceiling((childCount + Math.Min(FirstColumn, columns)) / (float)columns);
        }
        else if (columns == 0)
        {
            columns = (int)MathF.Ceiling(childCount / (float)rows);
        }

        rows = Math.Max(1, rows);
        columns = Math.Max(1, columns);
        return (rows, columns);
    }
}
