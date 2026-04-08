using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
    public static IDisposable PushDiagnosticSink(Action<XamlDiagnostic> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        CurrentDiagnosticSinks ??= new Stack<Action<XamlDiagnostic>>();
        CurrentDiagnosticSinks.Push(sink);
        return new DiagnosticSinkScope(sink);
    }

    private sealed class DiagnosticSinkScope : IDisposable
    {
        private readonly Action<XamlDiagnostic> _sink;
        private bool _disposed;

        public DiagnosticSinkScope(Action<XamlDiagnostic> sink)
        {
            _sink = sink;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (CurrentDiagnosticSinks == null || CurrentDiagnosticSinks.Count == 0)
            {
                return;
            }

            if (ReferenceEquals(CurrentDiagnosticSinks.Peek(), _sink))
            {
                CurrentDiagnosticSinks.Pop();
            }
            else
            {
                var preserved = new Stack<Action<XamlDiagnostic>>();
                var removed = false;
                while (CurrentDiagnosticSinks.Count > 0)
                {
                    var current = CurrentDiagnosticSinks.Pop();
                    if (!removed && ReferenceEquals(current, _sink))
                    {
                        removed = true;
                        continue;
                    }

                    preserved.Push(current);
                }

                while (preserved.Count > 0)
                {
                    CurrentDiagnosticSinks.Push(preserved.Pop());
                }
            }

            if (CurrentDiagnosticSinks.Count == 0)
            {
                CurrentDiagnosticSinks = null;
            }
        }
    }

    private static InvalidOperationException CreateXamlException(
        string message,
        XObject? location = null,
        Exception? inner = null,
        XamlDiagnosticCode code = XamlDiagnosticCode.GeneralFailure,
        string? propertyName = null,
        string? hint = null,
        string? elementName = null)
    {
        var diagnostic = BuildDiagnostic(message, location, code, propertyName, hint, elementName);
        EmitDiagnostic(diagnostic);

        var fullMessage = message + FormatLineInfo(location) + FormatDiagnosticContext(diagnostic);
        return inner == null
            ? new InvalidOperationException(fullMessage)
            : new InvalidOperationException(fullMessage, inner);
    }

    private static XamlDiagnostic BuildDiagnostic(
        string message,
        XObject? location,
        XamlDiagnosticCode code,
        string? propertyName,
        string? hint,
        string? elementName)
    {
        var resolvedElementName = elementName;
        var resolvedPropertyName = propertyName;
        if (location is XAttribute attribute)
        {
            resolvedElementName ??= attribute.Parent?.Name.LocalName;
            resolvedPropertyName ??= attribute.Name.LocalName;
        }
        else if (location is XElement element)
        {
            resolvedElementName ??= element.Name.LocalName;
        }
        else if (location is XNode node)
        {
            resolvedElementName ??= node.Parent?.Name.LocalName;
        }

        int? line = null;
        int? position = null;
        if (location is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            line = lineInfo.LineNumber;
            position = lineInfo.LinePosition;
        }

        return new XamlDiagnostic(
            code,
            message,
            resolvedElementName,
            resolvedPropertyName,
            line,
            position,
            hint);
    }

    private static void EmitDiagnostic(XamlDiagnostic diagnostic)
    {
        if (CurrentDiagnosticSinks == null || CurrentDiagnosticSinks.Count == 0)
        {
            return;
        }

        if (CurrentDiagnosticSinks.Count == 1)
        {
            CurrentDiagnosticSinks.Peek()(diagnostic);
            return;
        }

        var sinks = CurrentDiagnosticSinks.ToArray();
        foreach (var sink in sinks)
        {
            sink(diagnostic);
        }
    }

    private static string FormatLineInfo(XObject? location)
    {
        if (location is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo())
        {
            return string.Empty;
        }

        return $" (Line {lineInfo.LineNumber}, Position {lineInfo.LinePosition})";
    }

    private static string FormatDiagnosticContext(XamlDiagnostic diagnostic)
    {
        var parts = new List<string> { $"Code={diagnostic.Code}" };
        if (!string.IsNullOrWhiteSpace(diagnostic.ElementName))
        {
            parts.Add($"Element={diagnostic.ElementName}");
        }

        if (!string.IsNullOrWhiteSpace(diagnostic.PropertyName))
        {
            parts.Add($"Property={diagnostic.PropertyName}");
        }

        if (diagnostic.Line.HasValue)
        {
            parts.Add($"Line={diagnostic.Line.Value}");
        }

        if (diagnostic.Position.HasValue)
        {
            parts.Add($"Position={diagnostic.Position.Value}");
        }

        if (!string.IsNullOrWhiteSpace(diagnostic.Hint))
        {
            parts.Add($"Hint={diagnostic.Hint}");
        }

        return $" [Diagnostic: {string.Join(", ", parts)}]";
    }
}
