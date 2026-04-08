using System;

namespace InkkSlinger;

public static class InkkSlingerUI
{
    public static void Initialize(UIElement rootContent, InkkSlingerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rootContent);
        Initialize(() => rootContent, options);
    }

    public static void Initialize(Func<UIElement> rootContentFactory, InkkSlingerOptions? options = null)
    {
        using var host = new InkkSlingerGameHost(rootContentFactory, options);
        host.Run();
    }
}