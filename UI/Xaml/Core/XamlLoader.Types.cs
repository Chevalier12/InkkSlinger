using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static Type ResolveTypeReference(string rawValue)
    {
        var trimmed = rawValue.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) &&
            trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            var body = trimmed[1..^1].Trim();
            if (body.StartsWith("x:Type", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = body["x:Type".Length..].Trim();
            }
        }

        if (trimmed.StartsWith("x:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        if (string.Equals(trimmed, "String", StringComparison.OrdinalIgnoreCase))
        {
            return typeof(string);
        }

        if (string.Equals(trimmed, "Int32", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Int", StringComparison.OrdinalIgnoreCase))
        {
            return typeof(int);
        }

        if (string.Equals(trimmed, "Single", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Float", StringComparison.OrdinalIgnoreCase))
        {
            return typeof(float);
        }

        if (string.Equals(trimmed, "Boolean", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Bool", StringComparison.OrdinalIgnoreCase))
        {
            return typeof(bool);
        }

        if (string.Equals(trimmed, "Object", StringComparison.OrdinalIgnoreCase))
        {
            return typeof(object);
        }

        return ResolveElementType(trimmed);
    }

    private static object? ResolveBindingSourceValue(string rawValue, FrameworkElement? resourceScope, XObject? location = null)
    {
        if (TryParseStaticResourceKey(rawValue, out var staticResourceKey))
        {
            return ResolveStaticResourceValue(staticResourceKey, resourceScope);
        }

        if (TryResolveXStatic(rawValue, out var xStaticResolved))
        {
            return xStaticResolved;
        }

        if (TryResolveXType(rawValue, out var xType))
        {
            return xType;
        }

        if (TryResolveXNull(rawValue, out var isXNull) && isXNull)
        {
            return null;
        }

        if (TryResolveXReference(rawValue, resourceScope, out var xReferenceValue, location))
        {
            return xReferenceValue;
        }

        if (IsMarkupExtensionSyntax(rawValue))
        {
            ThrowUnsupportedMarkupExtension(rawValue, $"binding option '{nameof(Binding.Source)}' or '{nameof(Binding.ConverterParameter)}'");
        }

        return ParseLooseValue(rawValue);
    }


    private static object CreateObjectInstance(Type type, XObject? location)
    {
        var instance = XamlObjectFactory.CreateInstance(type);
        if (instance != null)
        {
            return instance;
        }

        throw CreateXamlException($"Could not create instance of '{type.Name}'.", location);
    }


    private static Type ResolveElementType(string elementName)
    {
        if (TypeByName.TryGetValue(elementName, out var type))
        {
            return type;
        }

        throw CreateXamlException(
            $"XAML element '{elementName}' could not be resolved.",
            code: XamlDiagnosticCode.UnknownElement,
            elementName: elementName,
            hint: "Ensure the element type is public and mapped in the XAML type map.");
    }


    private static Dictionary<string, Type> BuildTypeMap()
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in UiAssembly.GetTypes())
        {
            if (!type.IsPublic)
            {
                continue;
            }

            map[type.Name] = type;
        }

        map["Rectangle"] = typeof(RectangleShape);
        map["Ellipse"] = typeof(EllipseShape);
        map["Line"] = typeof(LineShape);
        map["Polygon"] = typeof(PolygonShape);
        map["Polyline"] = typeof(PolylineShape);
        map["Path"] = typeof(PathShape);
        map["Geometry"] = typeof(Geometry);
        map["Color"] = typeof(Color);
        map[nameof(SolidColorBrush)] = typeof(SolidColorBrush);
        map[nameof(GridViewRowPresenter)] = typeof(GridViewRowPresenter);

        return map;
    }


}
