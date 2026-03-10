using System;
using System.Collections.Generic;

namespace InkkSlinger;

internal sealed class XamlLoadSession
{
    [ThreadStatic]
    private static XamlLoadSession? _current;

    public static XamlLoadSession Current => _current ??= new XamlLoadSession();

    public FrameworkElement? LoadRootScope { get; set; }

    public Stack<FrameworkElement>? ConstructionScopes { get; set; }

    public Stack<string>? XamlBaseDirectories { get; set; }

    public Stack<string>? ResourceDictionarySourcePaths { get; set; }

    public Stack<Action<XamlDiagnostic>>? DiagnosticSinks { get; set; }

    public Stack<List<Action>>? DeferredFinalizeActions { get; set; }
}
