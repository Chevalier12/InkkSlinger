namespace InkkSlinger;

internal interface IRenderDirtyBoundsHintProvider
{
    bool TryConsumeRenderDirtyBoundsHint(out LayoutRect bounds);
}
