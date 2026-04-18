using System;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static FrameworkElement? CurrentLoadRootScope
    {
        get => XamlLoadSession.Current.LoadRootScope;
        set => XamlLoadSession.Current.LoadRootScope = value;
    }

    private static object? CurrentLoadCodeBehind
    {
        get => XamlLoadSession.Current.LoadCodeBehind;
        set => XamlLoadSession.Current.LoadCodeBehind = value;
    }

    private static Stack<FrameworkElement>? CurrentConstructionScopes
    {
        get => XamlLoadSession.Current.ConstructionScopes;
        set => XamlLoadSession.Current.ConstructionScopes = value;
    }

    private static FrameworkElement? CurrentConstructionRootScope
    {
        get => XamlLoadSession.Current.ConstructionRootScope;
        set => XamlLoadSession.Current.ConstructionRootScope = value;
    }

    private static Stack<string>? CurrentXamlBaseDirectories
    {
        get => XamlLoadSession.Current.XamlBaseDirectories;
        set => XamlLoadSession.Current.XamlBaseDirectories = value;
    }

    private static Stack<string>? CurrentResourceDictionarySourcePaths
    {
        get => XamlLoadSession.Current.ResourceDictionarySourcePaths;
        set => XamlLoadSession.Current.ResourceDictionarySourcePaths = value;
    }

    private static Stack<XamlResourceBuildContext>? CurrentResourceBuildContexts
    {
        get => XamlLoadSession.Current.ResourceBuildContexts;
        set => XamlLoadSession.Current.ResourceBuildContexts = value;
    }

    private static Stack<Action<XamlDiagnostic>>? CurrentDiagnosticSinks
    {
        get => XamlLoadSession.Current.DiagnosticSinks;
        set => XamlLoadSession.Current.DiagnosticSinks = value;
    }

    private static Stack<List<Action>>? CurrentDeferredFinalizeActions
    {
        get => XamlLoadSession.Current.DeferredFinalizeActions;
        set => XamlLoadSession.Current.DeferredFinalizeActions = value;
    }

    private static T RunWithinIsolatedTemplateInstantiationScope<T>(Func<T> factory)
    {
        var previousLoadRootScope = CurrentLoadRootScope;
        var previousLoadCodeBehind = CurrentLoadCodeBehind;
        var previousConstructionScopes = CurrentConstructionScopes;
        var previousConstructionRootScope = CurrentConstructionRootScope;

        CurrentLoadRootScope = null;
        CurrentLoadCodeBehind = null;
        CurrentConstructionScopes = null;
        CurrentConstructionRootScope = null;

        try
        {
            return factory();
        }
        finally
        {
            CurrentLoadRootScope = previousLoadRootScope;
            CurrentLoadCodeBehind = previousLoadCodeBehind;
            CurrentConstructionScopes = previousConstructionScopes;
            CurrentConstructionRootScope = previousConstructionRootScope;
        }
    }

    private static T RunWithinTemplateDeclarationScope<T>(
        FrameworkElement? declarationScope,
        IReadOnlyList<XamlResourceBuildContext>? declarationBuildContexts,
        Func<T> factory)
    {
        return RunWithinTemplateDeclarationScope(declarationScope, declarationBuildContexts, null, factory);
    }

    private static T RunWithinTemplateDeclarationScope<T>(
        FrameworkElement? declarationScope,
        IReadOnlyList<XamlResourceBuildContext>? declarationBuildContexts,
        object? declarationCodeBehind,
        Func<T> factory)
    {
        var previousLoadRootScope = CurrentLoadRootScope;
        var previousLoadCodeBehind = CurrentLoadCodeBehind;
        var previousResourceBuildContexts = CurrentResourceBuildContexts;

        if (declarationScope != null)
        {
            CurrentLoadRootScope = declarationScope;
        }

        if (declarationCodeBehind != null)
        {
            CurrentLoadCodeBehind = declarationCodeBehind;
        }

        if (declarationBuildContexts is { Count: > 0 })
        {
            CurrentResourceBuildContexts = new Stack<XamlResourceBuildContext>(declarationBuildContexts.Reverse());
        }

        try
        {
            return factory();
        }
        finally
        {
            CurrentLoadRootScope = previousLoadRootScope;
            CurrentLoadCodeBehind = previousLoadCodeBehind;
            CurrentResourceBuildContexts = previousResourceBuildContexts;
        }
    }
}
