using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class StyleValueCloneUtility
{
    public static object? CloneForAssignment(object? value)
    {
        return value switch
        {
            null => null,
            Transform transform => CloneTransform(transform),
            Brush brush => CloneBrush(brush),
            Effect effect => CloneEffect(effect),
            _ => value
        };
    }

    private static Transform CloneTransform(Transform transform)
    {
        return transform switch
        {
            MatrixTransform matrixTransform => new MatrixTransform(matrixTransform.Matrix),
            TranslateTransform translateTransform => new TranslateTransform
            {
                X = translateTransform.X,
                Y = translateTransform.Y
            },
            ScaleTransform scaleTransform => new ScaleTransform
            {
                ScaleX = scaleTransform.ScaleX,
                ScaleY = scaleTransform.ScaleY,
                CenterX = scaleTransform.CenterX,
                CenterY = scaleTransform.CenterY
            },
            RotateTransform rotateTransform => new RotateTransform
            {
                Angle = rotateTransform.Angle,
                CenterX = rotateTransform.CenterX,
                CenterY = rotateTransform.CenterY
            },
            SkewTransform skewTransform => new SkewTransform
            {
                AngleX = skewTransform.AngleX,
                AngleY = skewTransform.AngleY,
                CenterX = skewTransform.CenterX,
                CenterY = skewTransform.CenterY
            },
            TransformGroup transformGroup => CloneTransformGroup(transformGroup),
            _ => transform
        };
    }

    private static TransformGroup CloneTransformGroup(TransformGroup transformGroup)
    {
        var clone = new TransformGroup();
        foreach (var child in transformGroup.Children)
        {
            clone.Children.Add(CloneTransform(child));
        }

        return clone;
    }

    private static Brush CloneBrush(Brush brush)
    {
        return brush switch
        {
            SolidColorBrush solidColorBrush => new SolidColorBrush(solidColorBrush.Color),
            _ => brush
        };
    }

    private static Effect CloneEffect(Effect effect)
    {
        return effect switch
        {
            DropShadowEffect dropShadowEffect => new DropShadowEffect
            {
                Color = dropShadowEffect.Color,
                ShadowDepth = dropShadowEffect.ShadowDepth,
                BlurRadius = dropShadowEffect.BlurRadius,
                Opacity = dropShadowEffect.Opacity
            },
            _ => effect
        };
    }
}
