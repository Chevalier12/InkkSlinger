using System;

namespace InkkSlinger;

public sealed class Game1 : InkkSlingerGameHost
{
    private const string CatalogWindowTitle = "InkkSlinger Controls Catalog";

    public Game1()
        : this(new InkkOopsRuntimeOptions())
    {
    }

    public Game1(InkkOopsRuntimeOptions inkkOopsOptions)
        : base(
            static () => new ControlsCatalogView(),
            new InkkSlingerOptions
            {
                WindowTitle = CatalogWindowTitle,
                FpsEnabled = true,
                InkkOopsRuntimeOptions = inkkOopsOptions ?? throw new ArgumentNullException(nameof(inkkOopsOptions))
            })
    {
    }
}
