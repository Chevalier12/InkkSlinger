namespace InkkSlinger;

public sealed class UiApplication
{
    private static readonly UiApplication Instance = new();

    private UiApplication()
    {
    }

    public static UiApplication Current => Instance;

    public ResourceDictionary Resources { get; } = new();
}
