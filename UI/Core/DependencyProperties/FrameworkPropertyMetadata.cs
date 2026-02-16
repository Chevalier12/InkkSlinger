namespace InkkSlinger;

public class FrameworkPropertyMetadata
{
    public FrameworkPropertyMetadata(
        object? defaultValue = null,
        FrameworkPropertyMetadataOptions options = FrameworkPropertyMetadataOptions.None,
        PropertyChangedCallback? propertyChangedCallback = null,
        CoerceValueCallback? coerceValueCallback = null)
    {
        DefaultValue = defaultValue;
        Options = options;
        PropertyChangedCallback = propertyChangedCallback;
        CoerceValueCallback = coerceValueCallback;
    }

    public object? DefaultValue { get; }

    public FrameworkPropertyMetadataOptions Options { get; }

    public PropertyChangedCallback? PropertyChangedCallback { get; }

    public CoerceValueCallback? CoerceValueCallback { get; }

    public bool Inherits => (Options & FrameworkPropertyMetadataOptions.Inherits) != 0;

    public bool AffectsMeasure => (Options & FrameworkPropertyMetadataOptions.AffectsMeasure) != 0;

    public bool AffectsArrange => (Options & FrameworkPropertyMetadataOptions.AffectsArrange) != 0;

    public bool AffectsRender => (Options & FrameworkPropertyMetadataOptions.AffectsRender) != 0;
}
