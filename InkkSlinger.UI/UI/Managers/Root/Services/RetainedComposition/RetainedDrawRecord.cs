namespace InkkSlinger;

internal readonly record struct RetainedDrawRecord(
    UIElement Visual,
    int ContentVersion,
    VisualCommandList Commands);
