using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public static class XamlLoader
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Assembly UiAssembly = typeof(UIElement).Assembly;
    private static readonly Dictionary<string, Type> TypeByName = BuildTypeMap();
    [ThreadStatic]
    private static FrameworkElement? CurrentLoadRootScope;
    [ThreadStatic]
    private static Stack<FrameworkElement>? CurrentConstructionScopes;

    public static UIElement LoadFromFile(string path, object? codeBehind = null)
    {
        var fileReadStart = Stopwatch.GetTimestamp();
        var xaml = File.ReadAllText(path);
        UiFrameworkFileLoadDiagnostics.Observe(
            "XamlLoader.File.ReadAllText",
            Stopwatch.GetElapsedTime(fileReadStart).TotalMilliseconds,
            xaml.Length);

        var loadStart = Stopwatch.GetTimestamp();
        var root = LoadFromString(xaml, codeBehind);
        UiFrameworkFileLoadDiagnostics.Observe(
            "XamlLoader.LoadFromFile",
            Stopwatch.GetElapsedTime(loadStart).TotalMilliseconds);
        return root;
    }

    public static UIElement LoadFromString(string xaml, object? codeBehind = null)
    {
        var parseStart = Stopwatch.GetTimestamp();
        XDocument document;
        try
        {
            document = XDocument.Parse(xaml, LoadOptions.SetLineInfo);
        }
        catch (Exception ex)
        {
            throw CreateXamlException("Failed to parse XAML document.", null, ex);
        }

        if (document.Root == null)
        {
            throw CreateXamlException("XAML document has no root element.", document);
        }
        UiFrameworkFileLoadDiagnostics.Observe(
            "XamlLoader.ParseDocument",
            Stopwatch.GetElapsedTime(parseStart).TotalMilliseconds,
            xaml.Length);

        var loadStart = Stopwatch.GetTimestamp();
        var rootElement = document.Root;
        var rootType = ResolveElementType(rootElement.Name.LocalName);
        if (Activator.CreateInstance(rootType) is not UIElement uiRoot)
        {
            throw CreateXamlException($"Type '{rootType.Name}' is not a UIElement.", rootElement);
        }

        var previousRootScope = CurrentLoadRootScope;
        try
        {
            var rootScope = uiRoot as FrameworkElement;
            CurrentLoadRootScope = rootScope;
            RunWithinConstructionScope(rootScope, () =>
            {
                ApplyAttributes(uiRoot, rootElement, codeBehind, rootScope);
                ApplyChildren(uiRoot, rootElement, codeBehind, rootScope);
            });
            UiFrameworkFileLoadDiagnostics.Observe(
                "XamlLoader.LoadFromString",
                Stopwatch.GetElapsedTime(loadStart).TotalMilliseconds);
            UiFrameworkFileLoadDiagnostics.Flush();
            UiFrameworkPopulationPhaseDiagnostics.Flush();
            return uiRoot;
        }
        finally
        {
            CurrentLoadRootScope = previousRootScope;
        }
    }

    public static void LoadInto(UserControl target, string path, object? codeBehind = null)
    {
        var fileReadStart = Stopwatch.GetTimestamp();
        var xaml = File.ReadAllText(path);
        UiFrameworkFileLoadDiagnostics.Observe(
            "XamlLoader.File.ReadAllText",
            Stopwatch.GetElapsedTime(fileReadStart).TotalMilliseconds,
            xaml.Length);
        LoadIntoFromString(target, xaml, codeBehind);
    }

    public static void LoadIntoFromString(UserControl target, string xaml, object? codeBehind = null)
    {
        var parseStart = Stopwatch.GetTimestamp();
        XDocument document;
        try
        {
            document = XDocument.Parse(xaml, LoadOptions.SetLineInfo);
        }
        catch (Exception ex)
        {
            throw CreateXamlException("Failed to parse XAML document.", null, ex);
        }

        if (document.Root == null)
        {
            throw CreateXamlException("XAML document has no root element.", document);
        }
        UiFrameworkFileLoadDiagnostics.Observe(
            "XamlLoader.ParseDocument",
            Stopwatch.GetElapsedTime(parseStart).TotalMilliseconds,
            xaml.Length);

        var loadStart = Stopwatch.GetTimestamp();
        var root = document.Root;
        var rootType = ResolveElementType(root.Name.LocalName);
        if (!typeof(UserControl).IsAssignableFrom(rootType))
        {
            throw CreateXamlException("LoadInto expects a UserControl root element.", root);
        }

        var previousRootScope = CurrentLoadRootScope;
        CurrentLoadRootScope = target;
        try
        {
            RunWithinConstructionScope(target, () =>
            {
                ApplyAttributes(target, root, codeBehind, target);
                target.Content = null;
                ApplyChildren(target, root, codeBehind, target);
            });
            UiFrameworkFileLoadDiagnostics.Observe(
                "XamlLoader.LoadIntoFromString",
                Stopwatch.GetElapsedTime(loadStart).TotalMilliseconds);
            UiFrameworkFileLoadDiagnostics.Flush();
            UiFrameworkPopulationPhaseDiagnostics.Flush();
        }
        finally
        {
            CurrentLoadRootScope = previousRootScope;
        }
    }

    private static UIElement BuildElement(XElement element, object? codeBehind, FrameworkElement? resourceScope)
    {
        var type = ResolveElementType(element.Name.LocalName);
        if (Activator.CreateInstance(type) is not UIElement uiElement)
        {
            throw CreateXamlException($"Type '{type.Name}' is not a UIElement.", element);
        }

        RunWithinConstructionScope(uiElement as FrameworkElement, () =>
        {
            ApplyAttributes(uiElement, element, codeBehind, resourceScope);
            ApplyChildren(uiElement, element, codeBehind, uiElement as FrameworkElement ?? resourceScope);
        });
        return uiElement;
    }

    private static void ApplyChildren(UIElement parent, XElement parentElement, object? codeBehind, FrameworkElement? resourceScope)
    {
        var childElements = parentElement.Elements().ToList();
        if (childElements.Count == 0)
        {
            return;
        }

        var visualChildren = new List<XElement>(childElements.Count);
        foreach (var childElement in childElements)
        {
            if (TryApplyPropertyElement(parent, childElement, codeBehind, resourceScope))
            {
                continue;
            }

            visualChildren.Add(childElement);
        }

        if (visualChildren.Count == 0)
        {
            return;
        }

        if (parent is Panel panel)
        {
            foreach (var childElement in visualChildren)
            {
                panel.AddChild(BuildElement(childElement, codeBehind, panel));
            }

            return;
        }

        if (parent is Border border)
        {
            if (visualChildren.Count > 1)
            {
                throw CreateXamlException("Border supports a single child element.", parentElement);
            }

            border.Child = BuildElement(visualChildren[0], codeBehind, border);
            return;
        }

        if (parent is Decorator decorator)
        {
            if (visualChildren.Count > 1)
            {
                throw CreateXamlException("Decorator supports a single child element.", parentElement);
            }

            decorator.Child = BuildElement(visualChildren[0], codeBehind, decorator);
            return;
        }

        if (parent is UserControl userControl)
        {
            if (visualChildren.Count > 1)
            {
                throw CreateXamlException("UserControl supports a single content child.", parentElement);
            }

            userControl.Content = BuildElement(visualChildren[0], codeBehind, userControl);
            return;
        }

        if (parent is ContentControl contentControl)
        {
            if (visualChildren.Count > 1)
            {
                throw CreateXamlException("ContentControl supports a single content child.", parentElement);
            }

            contentControl.Content = BuildElement(visualChildren[0], codeBehind, contentControl);
            return;
        }

        if (parent is ScrollViewer scrollViewer)
        {
            if (visualChildren.Count > 1)
            {
                throw CreateXamlException("ScrollViewer supports a single content child.", parentElement);
            }

            scrollViewer.Content = BuildElement(visualChildren[0], codeBehind, scrollViewer);
            return;
        }

        if (parent is ItemsControl itemsControl)
        {
            foreach (var childElement in visualChildren)
            {
                itemsControl.Items.Add(BuildElement(childElement, codeBehind, itemsControl));
            }

            return;
        }

        throw CreateXamlException(
            $"Type '{parent.GetType().Name}' cannot host child elements in XAML.",
            parentElement);
    }

    private static bool TryApplyPropertyElement(object target, XElement propertyElement, object? codeBehind, FrameworkElement? resourceScope)
    {
        var propertyElementName = propertyElement.Name.LocalName;
        var separatorIndex = propertyElementName.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= propertyElementName.Length - 1)
        {
            return false;
        }

        var ownerTypeName = propertyElementName[..separatorIndex];
        var propertyName = propertyElementName[(separatorIndex + 1)..];
        var targetType = target.GetType();
        var ownerType = ResolveElementType(ownerTypeName);
        if (!ownerType.IsAssignableFrom(targetType))
        {
            return false;
        }

        var property = targetType.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

        if (property == null)
        {
            throw CreateXamlException(
                $"Property element '{propertyElementName}' could not be resolved on '{targetType.Name}'.",
                propertyElement);
        }

        if (TryApplyRichTextPropertyElement(target, propertyName, propertyElementName, propertyElement, codeBehind, resourceScope))
        {
            return true;
        }

        var propertyValue = property.GetValue(target);
        if (propertyValue is ResourceDictionary resourceDictionary)
        {
            var resourceScopeForEntries = target as FrameworkElement ?? resourceScope;
            foreach (var resourceElement in propertyElement.Elements())
            {
                AddResourceEntry(resourceDictionary, resourceElement, codeBehind, resourceScopeForEntries);
            }

            return true;
        }

        if (propertyValue is IList list)
        {
            var listItemScope = target as FrameworkElement ?? resourceScope;
            foreach (var itemElement in propertyElement.Elements())
            {
                var item = BuildObject(itemElement, codeBehind, listItemScope);
                list.Add(item);
            }

            return true;
        }

        var contentElements = propertyElement.Elements().ToList();
        if (contentElements.Count != 1)
        {
            throw CreateXamlException(
                $"Property element '{propertyElementName}' must contain exactly one child element.",
                propertyElement);
        }

        if (target is DependencyObject dependencyObject)
        {
            var childElement = contentElements[0];
            var childName = childElement.Name.LocalName;
            if (string.Equals(childName, nameof(Binding), StringComparison.Ordinal) ||
                string.Equals(childName, nameof(MultiBinding), StringComparison.Ordinal) ||
                string.Equals(childName, nameof(PriorityBinding), StringComparison.Ordinal))
            {
                var dependencyProperty = ResolveDependencyProperty(targetType, propertyName);
                if (dependencyProperty == null)
                {
                    throw CreateXamlException(
                        $"Property element '{propertyElementName}' requires a dependency property named '{propertyName}Property'.",
                        propertyElement);
                }

                var bindingScope = target as FrameworkElement ?? resourceScope;
                BindingBase bindingBase;
                if (string.Equals(childName, nameof(Binding), StringComparison.Ordinal))
                {
                    bindingBase = BuildBindingElement(childElement, dependencyProperty.PropertyType, bindingScope);
                }
                else if (string.Equals(childName, nameof(MultiBinding), StringComparison.Ordinal))
                {
                    bindingBase = BuildMultiBindingElement(childElement, dependencyProperty.PropertyType, bindingScope);
                }
                else
                {
                    bindingBase = BuildPriorityBindingElement(childElement, dependencyProperty.PropertyType, bindingScope);
                }

                BindingOperations.SetBinding(dependencyObject, dependencyProperty, bindingBase);
                return true;
            }
        }

        if (!property.CanWrite)
        {
            throw CreateXamlException(
                $"Property element '{propertyElementName}' targets a non-settable property.",
                propertyElement);
        }

        var converted = BuildObject(contentElements[0], codeBehind, target as FrameworkElement ?? resourceScope);
        property.SetValue(target, converted);
        return true;
    }

    private static object BuildObject(XElement element, object? codeBehind, FrameworkElement? resourceScope)
    {
        if (string.Equals(element.Name.LocalName, nameof(Style), StringComparison.Ordinal))
        {
            return BuildStyle(element, resourceScope);
        }

        if (string.Equals(element.Name.LocalName, nameof(ControlTemplate), StringComparison.Ordinal))
        {
            return BuildControlTemplate(element, codeBehind, resourceScope);
        }

        if (string.Equals(element.Name.LocalName, nameof(DataTemplate), StringComparison.Ordinal))
        {
            return BuildDataTemplate(element, codeBehind, resourceScope);
        }

        if (string.Equals(element.Name.LocalName, nameof(Binding), StringComparison.Ordinal))
        {
            return BuildBindingElement(element, typeof(object), resourceScope);
        }

        if (string.Equals(element.Name.LocalName, nameof(MultiBinding), StringComparison.Ordinal))
        {
            return BuildMultiBindingElement(element, typeof(object), resourceScope);
        }

        if (string.Equals(element.Name.LocalName, nameof(PriorityBinding), StringComparison.Ordinal))
        {
            return BuildPriorityBindingElement(element, typeof(object), resourceScope);
        }

        if (string.Equals(element.Name.LocalName, nameof(BindingGroup), StringComparison.Ordinal))
        {
            return BuildBindingGroupElement(element);
        }

        var type = ResolveElementType(element.Name.LocalName);
        var instance = CreateObjectInstance(type, element);

        if (instance is UIElement uiElement)
        {
            RunWithinConstructionScope(uiElement as FrameworkElement, () =>
            {
                ApplyAttributes(instance, element, codeBehind, resourceScope);
                ApplyChildren(uiElement, element, codeBehind, uiElement as FrameworkElement ?? resourceScope);
            });
        }
        else
        {
            ApplyAttributes(instance, element, codeBehind, resourceScope);
            ApplyObjectChildren(instance, element, codeBehind, resourceScope);
        }

        return instance;
    }

    private static void ApplyAttributes(object target, XElement element, object? codeBehind, FrameworkElement? resourceScope = null)
    {
        ValidateStrictRichTextAttributes(target, element);

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            var namespaceName = attribute.Name.NamespaceName;
            var localName = attribute.Name.LocalName;
            var value = attribute.Value;

            if (namespaceName == XamlNamespace.NamespaceName)
            {
                if (localName == "Name")
                {
                    AssignName(target, value, codeBehind);
                }

                if (localName == "Key")
                {
                    continue;
                }

                continue;
            }

            // Ignore foreign-namespace metadata attributes like xsi:schemaLocation.
            if (!string.IsNullOrEmpty(namespaceName))
            {
                continue;
            }

            try
            {
                if (localName.Contains('.', StringComparison.Ordinal))
                {
                    ApplyAttachedProperty(target, localName, value, resourceScope);
                    continue;
                }

                if (TryApplyBindingExpression(target, localName, value, resourceScope))
                {
                    continue;
                }

                if (TryApplyDynamicResourceExpression(target, localName, value))
                {
                    continue;
                }

                if (TryAttachEventHandler(target, localName, value, codeBehind))
                {
                    continue;
                }

                ApplyProperty(target, localName, value, resourceScope);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
            {
                throw CreateXamlException(
                    $"Failed to apply attribute '{attribute.Name.LocalName}' on '{element.Name.LocalName}': {ex.Message}",
                    attribute,
                    ex);
            }
        }
    }

    private static bool TryApplyRichTextPropertyElement(
        object target,
        string propertyName,
        string propertyElementName,
        XElement propertyElement,
        object? codeBehind,
        FrameworkElement? resourceScope)
    {
        if (target is FlowDocument flowDocument &&
            string.Equals(propertyName, nameof(FlowDocument.Blocks), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<Block>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => flowDocument.Blocks.Add(item));
            return true;
        }

        if (target is Section section &&
            string.Equals(propertyName, nameof(Section.Blocks), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<Block>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => section.Blocks.Add(item));
            return true;
        }

        if (target is Paragraph paragraph &&
            string.Equals(propertyName, nameof(Paragraph.Inlines), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<Inline>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => paragraph.Inlines.Add(item));
            return true;
        }

        if (target is Span span &&
            string.Equals(propertyName, nameof(Span.Inlines), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<Inline>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => span.Inlines.Add(item));
            return true;
        }

        if (target is InkkSlinger.List list &&
            string.Equals(propertyName, nameof(InkkSlinger.List.Items), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<ListItem>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => list.Items.Add(item));
            return true;
        }

        if (target is ListItem listItem &&
            string.Equals(propertyName, nameof(ListItem.Blocks), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<Block>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => listItem.Blocks.Add(item));
            return true;
        }

        if (target is Table table &&
            string.Equals(propertyName, nameof(Table.RowGroups), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<TableRowGroup>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => table.RowGroups.Add(item));
            return true;
        }

        if (target is TableRowGroup rowGroup &&
            string.Equals(propertyName, nameof(TableRowGroup.Rows), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<TableRow>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => rowGroup.Rows.Add(item));
            return true;
        }

        if (target is TableRow row &&
            string.Equals(propertyName, nameof(TableRow.Cells), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<TableCell>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => row.Cells.Add(item));
            return true;
        }

        if (target is TableCell cell &&
            string.Equals(propertyName, nameof(TableCell.Blocks), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<Block>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => cell.Blocks.Add(item));
            return true;
        }

        if (target is RichTextBox richTextBox &&
            string.Equals(propertyName, nameof(RichTextBox.Document), StringComparison.Ordinal))
        {
            var contentElements = propertyElement.Elements().ToList();
            if (contentElements.Count != 1)
            {
                throw CreateXamlException(
                    $"Property element '{propertyElementName}' must contain exactly one child element.",
                    propertyElement);
            }

            var built = BuildObject(contentElements[0], codeBehind, target as FrameworkElement ?? resourceScope);
            if (built is not FlowDocument document)
            {
                throw CreateXamlException(
                    $"Element '{contentElements[0].Name.LocalName}' is not valid inside property element '{propertyElementName}'. Expected '{nameof(FlowDocument)}'.",
                    contentElements[0]);
            }

            richTextBox.Document = document;
            return true;
        }

        return false;
    }

    private static void ApplyTypedCollectionPropertyElement<TExpected>(
        string propertyElementName,
        XElement propertyElement,
        object? codeBehind,
        FrameworkElement? resourceScope,
        Action<TExpected> addItem)
        where TExpected : class
    {
        var itemScope = resourceScope;
        foreach (var itemElement in propertyElement.Elements())
        {
            var item = BuildObject(itemElement, codeBehind, itemScope);
            if (item is not TExpected typed)
            {
                throw CreateXamlException(
                    $"Element '{itemElement.Name.LocalName}' is not valid inside property element '{propertyElementName}'. Expected '{typeof(TExpected).Name}'.",
                    itemElement);
            }

            addItem(typed);
        }
    }

    private static void ValidateStrictRichTextAttributes(object target, XElement element)
    {
        if (target is LineBreak)
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    continue;
                }

                var isSupportedMetadata =
                    attribute.Name.NamespaceName == XamlNamespace.NamespaceName &&
                    (string.Equals(attribute.Name.LocalName, "Name", StringComparison.Ordinal) ||
                     string.Equals(attribute.Name.LocalName, "Key", StringComparison.Ordinal));

                if (!isSupportedMetadata)
                {
                    throw CreateXamlException(
                        $"Attribute '{attribute.Name.LocalName}' is not supported on '{nameof(LineBreak)}'. Only x:Name/x:Key metadata attributes are allowed.",
                        attribute);
                }
            }
        }

        if (target is Hyperlink hyperlink &&
            element.Attribute(nameof(Hyperlink.NavigateUri)) is XAttribute navigateUriAttribute)
        {
            var rawValue = navigateUriAttribute.Value;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                throw CreateXamlException(
                    $"Attribute '{nameof(Hyperlink.NavigateUri)}' on '{nameof(Hyperlink)}' must be a non-empty URI string.",
                    navigateUriAttribute);
            }

            if (!Uri.TryCreate(rawValue.Trim(), UriKind.RelativeOrAbsolute, out _))
            {
                throw CreateXamlException(
                    $"Attribute '{nameof(Hyperlink.NavigateUri)}' on '{nameof(Hyperlink)}' is not a valid URI.",
                    navigateUriAttribute);
            }

            hyperlink.NavigateUri = rawValue.Trim();
        }

        if (target is TableCell)
        {
            ValidatePositiveIntegerAttribute(element, nameof(TableCell.RowSpan));
            ValidatePositiveIntegerAttribute(element, nameof(TableCell.ColumnSpan));
        }
    }

    private static void ValidatePositiveIntegerAttribute(XElement element, string attributeName)
    {
        var attribute = element.Attribute(attributeName);
        if (attribute == null)
        {
            return;
        }

        if (!int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw CreateXamlException(
                $"Attribute '{attributeName}' on '{element.Name.LocalName}' must be an integer greater than zero.",
                attribute);
        }
    }

    private static Style BuildStyle(XElement element, FrameworkElement? resourceScope)
    {
        var targetTypeText = GetRequiredAttributeValue(element, nameof(Style.TargetType));
        var targetType = ResolveStyleTargetType(targetTypeText);
        var style = new Style(targetType);

        var basedOnText = GetOptionalAttributeValue(element, nameof(Style.BasedOn));
        if (!string.IsNullOrWhiteSpace(basedOnText))
        {
            style.BasedOn = ResolveBasedOnStyle(basedOnText, resourceScope, targetType);
        }

        foreach (var child in element.Elements())
        {
            var childName = child.Name.LocalName;
            if (string.Equals(childName, "Style.Setters", StringComparison.Ordinal))
            {
                foreach (var setterElement in child.Elements())
                {
                    if (!string.Equals(setterElement.Name.LocalName, nameof(Setter), StringComparison.Ordinal))
                    {
                        throw CreateXamlException("Style.Setters can only contain Setter elements.", setterElement);
                    }

                    style.Setters.Add(BuildSetter(setterElement, targetType, resourceScope));
                }

                continue;
            }

            if (string.Equals(childName, "Style.Triggers", StringComparison.Ordinal))
            {
                foreach (var triggerElement in child.Elements())
                {
                    style.Triggers.Add(BuildTriggerBase(triggerElement, targetType, resourceScope));
                }

                continue;
            }

            if (string.Equals(childName, nameof(Setter), StringComparison.Ordinal))
            {
                style.Setters.Add(BuildSetter(child, targetType, resourceScope));
                continue;
            }

            if (string.Equals(childName, nameof(Trigger), StringComparison.Ordinal) ||
                string.Equals(childName, nameof(DataTrigger), StringComparison.Ordinal) ||
                string.Equals(childName, nameof(MultiDataTrigger), StringComparison.Ordinal) ||
                string.Equals(childName, nameof(EventTrigger), StringComparison.Ordinal))
            {
                style.Triggers.Add(BuildTriggerBase(child, targetType, resourceScope));
                continue;
            }

            throw CreateXamlException($"Element '{childName}' is not valid inside Style.", child);
        }

        return style;
    }

    private static Style ResolveBasedOnStyle(string basedOnText, FrameworkElement? resourceScope, Type styleTargetType)
    {
        if (!TryParseStaticResourceKey(basedOnText, out var staticResourceKey))
        {
            throw CreateXamlException(
                "Style BasedOn in XAML must use StaticResource markup, e.g. BasedOn=\"{StaticResource BaseStyle}\".");
        }

        var resolved = ResolveStaticResourceValue(staticResourceKey, resourceScope);
        if (resolved is not Style baseStyle)
        {
            throw CreateXamlException(
                $"Style BasedOn resource '{staticResourceKey}' must resolve to a Style, but resolved to '{resolved.GetType().Name}'.");
        }

        if (!baseStyle.TargetType.IsAssignableFrom(styleTargetType))
        {
            throw CreateXamlException(
                $"Style TargetType '{styleTargetType.Name}' is not compatible with BasedOn TargetType '{baseStyle.TargetType.Name}'.");
        }

        return baseStyle;
    }

    private static Binding BuildBindingElement(XElement element, Type targetPropertyType, FrameworkElement? resourceScope)
    {
        var binding = new Binding();
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(attribute.Name.NamespaceName))
            {
                continue;
            }

            var localName = attribute.Name.LocalName;
            var rawValue = attribute.Value;

            if (string.Equals(localName, nameof(Binding.Path), StringComparison.OrdinalIgnoreCase))
            {
                binding.Path = rawValue;
                continue;
            }

            if (TryApplyBindingOption(binding, localName, rawValue, targetPropertyType, resourceScope))
            {
                continue;
            }

            throw CreateXamlException($"Binding attribute '{localName}' is not supported.", attribute);
        }

        return binding;
    }

    private static MultiBinding BuildMultiBindingElement(XElement element, Type targetPropertyType, FrameworkElement? resourceScope)
    {
        var multiBinding = new MultiBinding();

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(attribute.Name.NamespaceName))
            {
                continue;
            }

            var localName = attribute.Name.LocalName;
            var rawValue = attribute.Value;
            if (TryApplyMultiBindingOption(multiBinding, localName, rawValue, targetPropertyType, resourceScope))
            {
                continue;
            }

            throw CreateXamlException($"MultiBinding attribute '{localName}' is not supported.", attribute);
        }

        foreach (var child in element.Elements())
        {
            if (!string.Equals(child.Name.LocalName, nameof(Binding), StringComparison.Ordinal))
            {
                throw CreateXamlException("MultiBinding can only contain Binding child elements.", child);
            }

            multiBinding.Bindings.Add(BuildBindingElement(child, typeof(object), resourceScope));
        }

        if (multiBinding.Bindings.Count == 0)
        {
            throw CreateXamlException("MultiBinding requires at least one child Binding.", element);
        }

        return multiBinding;
    }

    private static PriorityBinding BuildPriorityBindingElement(XElement element, Type targetPropertyType, FrameworkElement? resourceScope)
    {
        var priorityBinding = new PriorityBinding();

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(attribute.Name.NamespaceName))
            {
                continue;
            }

            var localName = attribute.Name.LocalName;
            var rawValue = attribute.Value;
            if (TryApplyPriorityBindingOption(priorityBinding, localName, rawValue, targetPropertyType, resourceScope))
            {
                continue;
            }

            throw CreateXamlException($"PriorityBinding attribute '{localName}' is not supported.", attribute);
        }

        foreach (var child in element.Elements())
        {
            if (!string.Equals(child.Name.LocalName, nameof(Binding), StringComparison.Ordinal))
            {
                throw CreateXamlException("PriorityBinding can only contain Binding child elements.", child);
            }

            priorityBinding.Bindings.Add(BuildBindingElement(child, typeof(object), resourceScope));
        }

        if (priorityBinding.Bindings.Count == 0)
        {
            throw CreateXamlException("PriorityBinding requires at least one child Binding.", element);
        }

        return priorityBinding;
    }

    private static BindingGroup BuildBindingGroupElement(XElement element)
    {
        var bindingGroup = new BindingGroup();
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || !string.IsNullOrEmpty(attribute.Name.NamespaceName))
            {
                continue;
            }

            var localName = attribute.Name.LocalName;
            var rawValue = attribute.Value;
            if (string.Equals(localName, nameof(BindingGroup.Name), StringComparison.OrdinalIgnoreCase))
            {
                bindingGroup.Name = rawValue;
                continue;
            }

            if (string.Equals(localName, nameof(BindingGroup.Culture), StringComparison.OrdinalIgnoreCase))
            {
                bindingGroup.Culture = CultureInfo.GetCultureInfo(rawValue);
                continue;
            }

            throw CreateXamlException($"BindingGroup attribute '{localName}' is not supported.", attribute);
        }

        return bindingGroup;
    }

    private static DataTemplate BuildDataTemplate(XElement element, object? codeBehind, FrameworkElement? resourceScope)
    {
        var templateVisuals = element.Elements().ToList();

        if (templateVisuals.Count > 1)
        {
            throw CreateXamlException("DataTemplate can only contain a single root visual.", element);
        }

        var rootVisual = templateVisuals.Count == 1 ? new XElement(templateVisuals[0]) : null;
        var template = new DataTemplate((item, scope) =>
        {
            if (rootVisual == null)
            {
                return null;
            }

            var built = BuildElement(new XElement(rootVisual), null, scope ?? resourceScope);
            if (built is FrameworkElement elementRoot)
            {
                elementRoot.DataContext = item;
            }

            return built;
        });

        var dataTypeText = GetOptionalAttributeValue(element, nameof(DataTemplate.DataType));
        if (!string.IsNullOrWhiteSpace(dataTypeText))
        {
            template.DataType = ResolveTypeReference(dataTypeText);
        }

        return template;
    }

    private static ControlTemplate BuildControlTemplate(XElement element, object? codeBehind, FrameworkElement? resourceScope)
    {
        var targetTypeText = GetOptionalAttributeValue(element, nameof(ControlTemplate.TargetType));
        var targetType = string.IsNullOrWhiteSpace(targetTypeText) ? typeof(Control) : ResolveStyleTargetType(targetTypeText);

        XElement? visualRoot = null;
        var triggerDefinitions = new List<XElement>();
        foreach (var child in element.Elements())
        {
            var childName = child.Name.LocalName;
            if (string.Equals(childName, "ControlTemplate.Triggers", StringComparison.Ordinal))
            {
                foreach (var triggerElement in child.Elements())
                {
                    triggerDefinitions.Add(new XElement(triggerElement));
                }

                continue;
            }

            if (string.Equals(childName, nameof(Trigger), StringComparison.Ordinal) ||
                string.Equals(childName, nameof(DataTrigger), StringComparison.Ordinal) ||
                string.Equals(childName, nameof(MultiDataTrigger), StringComparison.Ordinal))
            {
                triggerDefinitions.Add(new XElement(child));
                continue;
            }

            if (visualRoot != null)
            {
                throw CreateXamlException("ControlTemplate can only contain one visual root.", child);
            }

            visualRoot = new XElement(child);
        }

        if (visualRoot == null)
        {
            throw CreateXamlException("ControlTemplate requires a visual root element.", element);
        }

        var template = new ControlTemplate(owner =>
        {
            return BuildElement(new XElement(visualRoot), null, owner);
        })
        {
            TargetType = targetType
        };

        foreach (var triggerElement in triggerDefinitions)
        {
            template.Triggers.Add(BuildTriggerBase(triggerElement, targetType, resourceScope));
        }

        return template;
    }

    private static TriggerBase BuildTriggerBase(XElement element, Type styleTargetType, FrameworkElement? resourceScope)
    {
        if (string.Equals(element.Name.LocalName, nameof(Trigger), StringComparison.Ordinal))
        {
            return BuildPropertyTrigger(element, styleTargetType, resourceScope);
        }

        if (string.Equals(element.Name.LocalName, nameof(DataTrigger), StringComparison.Ordinal))
        {
            return BuildDataTrigger(element, styleTargetType, resourceScope);
        }

        if (string.Equals(element.Name.LocalName, nameof(MultiDataTrigger), StringComparison.Ordinal))
        {
            return BuildMultiDataTrigger(element, styleTargetType, resourceScope);
        }

        if (string.Equals(element.Name.LocalName, nameof(EventTrigger), StringComparison.Ordinal))
        {
            return BuildEventTrigger(element, styleTargetType, resourceScope);
        }

        throw CreateXamlException($"Element '{element.Name.LocalName}' is not a valid trigger.", element);
    }

    private static EventTrigger BuildEventTrigger(XElement element, Type styleTargetType, FrameworkElement? resourceScope)
    {
        var trigger = new EventTrigger
        {
            RoutedEvent = GetRequiredAttributeValue(element, nameof(EventTrigger.RoutedEvent))
        };

        var sourceName = GetOptionalAttributeValue(element, nameof(EventTrigger.SourceName));
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            trigger.SourceName = sourceName;
        }

        foreach (var actionElement in EnumerateEventActionElements(element))
        {
            trigger.Actions.Add(BuildTriggerAction(actionElement, styleTargetType, resourceScope));
        }

        return trigger;
    }

    private static Trigger BuildPropertyTrigger(XElement element, Type styleTargetType, FrameworkElement? resourceScope)
    {
        var propertyText = GetRequiredAttributeValue(element, nameof(Trigger.Property));
        var dependencyProperty = ResolveSetterProperty(styleTargetType, propertyText);
        var rawValue = GetRequiredAttributeValue(element, nameof(Trigger.Value));
        var triggerValue = ConvertValue(rawValue, dependencyProperty.PropertyType);

        var trigger = new Trigger(dependencyProperty, triggerValue);
        foreach (var setterElement in EnumerateSetterElements(element, "Trigger.Setters"))
        {
            trigger.Setters.Add(BuildSetter(setterElement, styleTargetType, resourceScope));
        }

        BuildTriggerActions(element, styleTargetType, resourceScope, trigger, "Trigger.EnterActions", "Trigger.ExitActions");

        return trigger;
    }

    private static DataTrigger BuildDataTrigger(XElement element, Type styleTargetType, FrameworkElement? resourceScope)
    {
        Binding? binding = null;
        var bindingText = GetOptionalAttributeValue(element, nameof(DataTrigger.Binding));
        if (!string.IsNullOrWhiteSpace(bindingText))
        {
            binding = ParseBindingMarkup(bindingText, resourceScope);
        }

        foreach (var child in element.Elements())
        {
            if (!string.Equals(child.Name.LocalName, "DataTrigger.Binding", StringComparison.Ordinal))
            {
                continue;
            }

            var bindingChildren = child.Elements().ToList();
            if (bindingChildren.Count != 1 || !string.Equals(bindingChildren[0].Name.LocalName, nameof(Binding), StringComparison.Ordinal))
            {
                throw CreateXamlException("DataTrigger.Binding must contain a single Binding element.", child);
            }

            binding = BuildBindingElement(bindingChildren[0], typeof(object), resourceScope);
        }

        if (binding == null)
        {
            throw CreateXamlException("DataTrigger requires a Binding.", element);
        }

        var valueText = GetRequiredAttributeValue(element, nameof(DataTrigger.Value));
        var dataTrigger = new DataTrigger(binding, ParseLooseValue(valueText));

        foreach (var setterElement in EnumerateSetterElements(element, "DataTrigger.Setters"))
        {
            dataTrigger.Setters.Add(BuildSetter(setterElement, styleTargetType, resourceScope));
        }

        BuildTriggerActions(element, styleTargetType, resourceScope, dataTrigger, "DataTrigger.EnterActions", "DataTrigger.ExitActions");

        return dataTrigger;
    }

    private static MultiDataTrigger BuildMultiDataTrigger(XElement element, Type styleTargetType, FrameworkElement? resourceScope)
    {
        var multiDataTrigger = new MultiDataTrigger();

        foreach (var conditionElement in EnumerateConditionElements(element, "MultiDataTrigger.Conditions"))
        {
            multiDataTrigger.Conditions.Add(BuildCondition(conditionElement, resourceScope));
        }

        if (multiDataTrigger.Conditions.Count == 0)
        {
            throw CreateXamlException("MultiDataTrigger requires at least one Condition.", element);
        }

        foreach (var setterElement in EnumerateSetterElements(element, "MultiDataTrigger.Setters"))
        {
            multiDataTrigger.Setters.Add(BuildSetter(setterElement, styleTargetType, resourceScope));
        }

        BuildTriggerActions(
            element,
            styleTargetType,
            resourceScope,
            multiDataTrigger,
            "MultiDataTrigger.EnterActions",
            "MultiDataTrigger.ExitActions");

        return multiDataTrigger;
    }

    private static IEnumerable<XElement> EnumerateSetterElements(XElement triggerElement, string propertyElementName)
    {
        foreach (var child in triggerElement.Elements())
        {
            if (string.Equals(child.Name.LocalName, nameof(Setter), StringComparison.Ordinal))
            {
                yield return child;
                continue;
            }

            if (!string.Equals(child.Name.LocalName, propertyElementName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var setterElement in child.Elements())
            {
                if (!string.Equals(setterElement.Name.LocalName, nameof(Setter), StringComparison.Ordinal))
                {
                    throw CreateXamlException($"{propertyElementName} can only contain Setter elements.", setterElement);
                }

                yield return setterElement;
            }
        }
    }

    private static IEnumerable<XElement> EnumerateConditionElements(XElement triggerElement, string propertyElementName)
    {
        foreach (var child in triggerElement.Elements())
        {
            if (string.Equals(child.Name.LocalName, nameof(Condition), StringComparison.Ordinal))
            {
                yield return child;
                continue;
            }

            if (!string.Equals(child.Name.LocalName, propertyElementName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var conditionElement in child.Elements())
            {
                if (!string.Equals(conditionElement.Name.LocalName, nameof(Condition), StringComparison.Ordinal))
                {
                    throw CreateXamlException($"{propertyElementName} can only contain Condition elements.", conditionElement);
                }

                yield return conditionElement;
            }
        }
    }

    private static Condition BuildCondition(XElement element, FrameworkElement? resourceScope)
    {
        Binding? binding = null;
        var bindingText = GetOptionalAttributeValue(element, nameof(Condition.Binding));
        if (!string.IsNullOrWhiteSpace(bindingText))
        {
            binding = ParseBindingMarkup(bindingText, resourceScope);
        }

        var value = ParseLooseValue(GetRequiredAttributeValue(element, nameof(Condition.Value)));

        foreach (var child in element.Elements())
        {
            if (!string.Equals(child.Name.LocalName, "Condition.Binding", StringComparison.Ordinal))
            {
                continue;
            }

            var bindingChildren = child.Elements().ToList();
            if (bindingChildren.Count != 1 || !string.Equals(bindingChildren[0].Name.LocalName, nameof(Binding), StringComparison.Ordinal))
            {
                throw CreateXamlException("Condition.Binding must contain a single Binding element.", child);
            }

            binding = BuildBindingElement(bindingChildren[0], typeof(object), resourceScope);
        }

        if (binding == null)
        {
            throw CreateXamlException("Condition requires a Binding.", element);
        }

        return new Condition
        {
            Binding = binding,
            Value = value
        };
    }

    private static void BuildTriggerActions(
        XElement triggerElement,
        Type styleTargetType,
        FrameworkElement? resourceScope,
        TriggerBase trigger,
        string enterActionsElementName,
        string exitActionsElementName)
    {
        foreach (var actionElement in EnumerateActionElements(triggerElement, enterActionsElementName))
        {
            trigger.EnterActions.Add(BuildTriggerAction(actionElement, styleTargetType, resourceScope));
        }

        foreach (var actionElement in EnumerateActionElements(triggerElement, exitActionsElementName))
        {
            trigger.ExitActions.Add(BuildTriggerAction(actionElement, styleTargetType, resourceScope));
        }
    }

    private static IEnumerable<XElement> EnumerateActionElements(XElement triggerElement, string propertyElementName)
    {
        foreach (var child in triggerElement.Elements())
        {
            if (!string.Equals(child.Name.LocalName, propertyElementName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var actionElement in child.Elements())
            {
                yield return actionElement;
            }
        }
    }

    private static IEnumerable<XElement> EnumerateEventActionElements(XElement triggerElement)
    {
        foreach (var child in triggerElement.Elements())
        {
            if (string.Equals(child.Name.LocalName, "EventTrigger.Actions", StringComparison.Ordinal))
            {
                foreach (var actionElement in child.Elements())
                {
                    yield return actionElement;
                }

                continue;
            }

            // Allow direct action children for convenience.
            if (string.Equals(child.Name.LocalName, nameof(SetValueAction), StringComparison.Ordinal) ||
                string.Equals(child.Name.LocalName, nameof(BeginStoryboard), StringComparison.Ordinal) ||
                string.Equals(child.Name.LocalName, nameof(StopStoryboard), StringComparison.Ordinal) ||
                string.Equals(child.Name.LocalName, nameof(PauseStoryboard), StringComparison.Ordinal) ||
                string.Equals(child.Name.LocalName, nameof(ResumeStoryboard), StringComparison.Ordinal) ||
                string.Equals(child.Name.LocalName, nameof(SeekStoryboard), StringComparison.Ordinal) ||
                string.Equals(child.Name.LocalName, nameof(RemoveStoryboard), StringComparison.Ordinal))
            {
                yield return child;
            }
        }
    }

    private static TriggerAction BuildTriggerAction(XElement actionElement, Type styleTargetType, FrameworkElement? resourceScope)
    {
        if (string.Equals(actionElement.Name.LocalName, nameof(SetValueAction), StringComparison.Ordinal))
        {
            var propertyText = GetRequiredAttributeValue(actionElement, nameof(Setter.Property));
            var dependencyProperty = ResolveSetterProperty(styleTargetType, propertyText);
            var valueText = GetRequiredAttributeValue(actionElement, nameof(Setter.Value));

            object convertedValue;
            if (TryParseDynamicResourceKey(valueText, out _))
            {
                throw CreateXamlException(
                    "DynamicResource is not supported in Setter/Trigger action values yet; use StaticResource or direct property assignment.",
                    actionElement);
            }

            if (TryParseStaticResourceKey(valueText, out var staticResourceKey))
            {
                var resolved = ResolveStaticResourceValue(staticResourceKey, resourceScope);
                convertedValue = CoerceResolvedResourceValue(
                    resolved,
                    dependencyProperty.PropertyType,
                    $"SetValueAction for {styleTargetType.Name}.{dependencyProperty.Name}");
            }
            else
            {
                convertedValue = ConvertValue(valueText, dependencyProperty.PropertyType);
            }

            return new SetValueAction(dependencyProperty, convertedValue);
        }

        if (string.Equals(actionElement.Name.LocalName, nameof(BeginStoryboard), StringComparison.Ordinal))
        {
            var action = new BeginStoryboard();
            var name = GetOptionalAttributeValue(actionElement, nameof(BeginStoryboard.Name));
            if (!string.IsNullOrWhiteSpace(name))
            {
                action.Name = name;
            }

            var handoff = GetOptionalAttributeValue(actionElement, nameof(BeginStoryboard.HandoffBehavior));
            if (!string.IsNullOrWhiteSpace(handoff))
            {
                action.HandoffBehavior = (HandoffBehavior)Enum.Parse(typeof(HandoffBehavior), handoff, ignoreCase: true);
            }

            var storyboardValue = GetOptionalAttributeValue(actionElement, nameof(BeginStoryboard.Storyboard));
            if (!string.IsNullOrWhiteSpace(storyboardValue))
            {
                if (TryParseStaticResourceKey(storyboardValue, out var storyboardResourceKey))
                {
                    var resolved = ResolveStaticResourceValue(storyboardResourceKey, resourceScope);
                    action.Storyboard = resolved as Storyboard
                        ?? throw CreateXamlException(
                            $"BeginStoryboard Storyboard resource '{storyboardResourceKey}' is not a Storyboard.",
                            actionElement);
                }
            }

            foreach (var child in actionElement.Elements())
            {
                if (string.Equals(child.Name.LocalName, nameof(Storyboard), StringComparison.Ordinal))
                {
                    action.Storyboard = (Storyboard)BuildObject(child, null, resourceScope);
                }
            }

            if (action.Storyboard == null)
            {
                throw CreateXamlException("BeginStoryboard requires a Storyboard.", actionElement);
            }

            return action;
        }

        if (string.Equals(actionElement.Name.LocalName, nameof(StopStoryboard), StringComparison.Ordinal))
        {
            return new StopStoryboard
            {
                BeginStoryboardName = GetRequiredAttributeValue(actionElement, nameof(StopStoryboard.BeginStoryboardName))
            };
        }

        if (string.Equals(actionElement.Name.LocalName, nameof(PauseStoryboard), StringComparison.Ordinal))
        {
            return new PauseStoryboard
            {
                BeginStoryboardName = GetRequiredAttributeValue(actionElement, nameof(PauseStoryboard.BeginStoryboardName))
            };
        }

        if (string.Equals(actionElement.Name.LocalName, nameof(ResumeStoryboard), StringComparison.Ordinal))
        {
            return new ResumeStoryboard
            {
                BeginStoryboardName = GetRequiredAttributeValue(actionElement, nameof(ResumeStoryboard.BeginStoryboardName))
            };
        }

        if (string.Equals(actionElement.Name.LocalName, nameof(RemoveStoryboard), StringComparison.Ordinal))
        {
            return new RemoveStoryboard
            {
                BeginStoryboardName = GetRequiredAttributeValue(actionElement, nameof(RemoveStoryboard.BeginStoryboardName))
            };
        }

        if (string.Equals(actionElement.Name.LocalName, nameof(SeekStoryboard), StringComparison.Ordinal))
        {
            var seek = new SeekStoryboard
            {
                BeginStoryboardName = GetRequiredAttributeValue(actionElement, nameof(SeekStoryboard.BeginStoryboardName))
            };

            var offset = GetOptionalAttributeValue(actionElement, nameof(SeekStoryboard.Offset));
            if (!string.IsNullOrWhiteSpace(offset))
            {
                seek.Offset = TimeSpan.Parse(offset, CultureInfo.InvariantCulture);
            }

            var origin = GetOptionalAttributeValue(actionElement, nameof(SeekStoryboard.Origin));
            if (!string.IsNullOrWhiteSpace(origin))
            {
                seek.Origin = (TimeSeekOrigin)Enum.Parse(typeof(TimeSeekOrigin), origin, ignoreCase: true);
            }

            return seek;
        }

        throw CreateXamlException($"Trigger action '{actionElement.Name.LocalName}' is not supported.", actionElement);
    }

    private static Setter BuildSetter(XElement element, Type styleTargetType, FrameworkElement? resourceScope)
    {
        var targetName = GetOptionalAttributeValue(element, "TargetName") ?? string.Empty;
        var propertyText = GetRequiredAttributeValue(element, nameof(Setter.Property));
        var dependencyProperty = ResolveSetterProperty(styleTargetType, propertyText);
        var valueText = GetRequiredAttributeValue(element, nameof(Setter.Value));
        object convertedValue;
        if (TryParseDynamicResourceKey(valueText, out _))
        {
            throw CreateXamlException(
                "DynamicResource is not supported in Setter/Trigger action values yet; use StaticResource or direct property assignment.",
                element);
        }

        if (TryParseStaticResourceKey(valueText, out var staticResourceKey))
        {
            var resolved = ResolveStaticResourceValue(staticResourceKey, resourceScope);
            convertedValue = CoerceResolvedResourceValue(
                resolved,
                dependencyProperty.PropertyType,
                $"Setter for {styleTargetType.Name}.{dependencyProperty.Name}");
        }
        else
        {
            convertedValue = ConvertValue(valueText, dependencyProperty.PropertyType);
        }

        return new Setter(targetName, dependencyProperty, convertedValue);
    }

    private static DependencyProperty ResolveSetterProperty(Type styleTargetType, string propertyText)
    {
        if (propertyText.Contains('.', StringComparison.Ordinal))
        {
            var separatorIndex = propertyText.IndexOf('.');
            var ownerTypeName = propertyText[..separatorIndex];
            var propertyName = propertyText[(separatorIndex + 1)..];
            var ownerType = ResolveElementType(ownerTypeName);
            return ResolveDependencyProperty(ownerType, propertyName)
                   ?? throw new InvalidOperationException(
                       $"Dependency property '{propertyText}' was not found.");
        }

        return ResolveDependencyProperty(styleTargetType, propertyText)
               ?? throw new InvalidOperationException(
                   $"Dependency property '{propertyText}' was not found on '{styleTargetType.Name}'.");
    }

    private static Binding ParseBindingMarkup(string rawMarkup, FrameworkElement? resourceScope)
    {
        var trimmed = rawMarkup.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
            !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            throw CreateXamlException($"Binding markup '{rawMarkup}' is invalid.");
        }

        var markupBody = trimmed[1..^1].Trim();
        if (!markupBody.StartsWith("Binding", StringComparison.OrdinalIgnoreCase))
        {
            throw CreateXamlException($"Binding markup '{rawMarkup}' is invalid.");
        }

        return ParseBinding(markupBody["Binding".Length..].Trim(), typeof(object), resourceScope);
    }

    private static Type ResolveStyleTargetType(string targetTypeText)
    {
        var trimmed = targetTypeText.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) &&
            trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            var body = trimmed[1..^1].Trim();
            if (body.StartsWith("x:Type", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = body["x:Type".Length..].Trim();
            }
        }

        return ResolveElementType(trimmed);
    }

    private static string GetRequiredAttributeValue(XElement element, string attributeName)
    {
        var value = GetOptionalAttributeValue(element, attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw CreateXamlException(
                $"Element '{element.Name.LocalName}' requires attribute '{attributeName}'.",
                element);
        }

        return value;
    }

    private static string? GetOptionalAttributeValue(XElement element, string attributeName)
    {
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || !string.IsNullOrEmpty(attribute.Name.NamespaceName))
            {
                continue;
            }

            if (string.Equals(attribute.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase))
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private static object ParseLooseValue(string rawValue)
    {
        if (bool.TryParse(rawValue, out var boolValue))
        {
            return boolValue;
        }

        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue;
        }

        if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
        {
            return floatValue;
        }

        return rawValue;
    }

    private static void AddResourceEntry(
        ResourceDictionary dictionary,
        XElement resourceElement,
        object? codeBehind,
        FrameworkElement? resourceScope)
    {
        var resourceValue = BuildObject(resourceElement, codeBehind, resourceScope);
        var resourceKey = GetResourceKey(resourceElement, resourceValue);
        dictionary[resourceKey] = resourceValue;
    }

    private static object GetResourceKey(XElement resourceElement, object resourceValue)
    {
        var keyAttribute = resourceElement.Attributes()
            .FirstOrDefault(attribute =>
                !attribute.IsNamespaceDeclaration &&
                attribute.Name.Namespace == XamlNamespace &&
                string.Equals(attribute.Name.LocalName, "Key", StringComparison.Ordinal));

        if (keyAttribute != null && !string.IsNullOrWhiteSpace(keyAttribute.Value))
        {
            return keyAttribute.Value;
        }

        if (resourceValue is Style style)
        {
            return style.TargetType;
        }

        if (resourceValue is DataTemplate dataTemplate && dataTemplate.DataType != null)
        {
            return dataTemplate.DataType;
        }

        throw new InvalidOperationException(
            $"Resource element '{resourceElement.Name.LocalName}' requires x:Key.");
    }

    private static bool TryParseStaticResourceKey(string valueText, out string key)
    {
        key = string.Empty;
        var trimmed = valueText.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
            !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        var markupBody = trimmed[1..^1].Trim();
        if (!markupBody.StartsWith("StaticResource", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyText = markupBody["StaticResource".Length..].Trim();
        if (string.IsNullOrWhiteSpace(keyText))
        {
            throw CreateXamlException("StaticResource markup requires a resource key.");
        }

        key = keyText;
        return true;
    }

    private static bool TryParseDynamicResourceKey(string valueText, out string key)
    {
        key = string.Empty;
        var trimmed = valueText.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
            !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        var markupBody = trimmed[1..^1].Trim();
        if (!markupBody.StartsWith("DynamicResource", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyText = markupBody["DynamicResource".Length..].Trim();
        if (string.IsNullOrWhiteSpace(keyText))
        {
            throw CreateXamlException("DynamicResource markup requires a resource key.");
        }

        key = keyText;
        return true;
    }

    private static object ResolveStaticResourceValue(string key, FrameworkElement? primaryScope, FrameworkElement? fallbackScope = null)
    {
        if (primaryScope != null &&
            ResourceResolver.TryFindResource(primaryScope, key, out var primaryResource, includeApplicationResources: false))
        {
            return primaryResource!;
        }

        if (fallbackScope != null &&
            !ReferenceEquals(fallbackScope, primaryScope) &&
            ResourceResolver.TryFindResource(fallbackScope, key, out var fallbackResource, includeApplicationResources: false))
        {
            return fallbackResource!;
        }

        if (TryFindResourceInConstructionScopes(key, primaryScope, fallbackScope, out var scopedResource))
        {
            return scopedResource!;
        }

        if (CurrentLoadRootScope != null &&
            !ReferenceEquals(CurrentLoadRootScope, primaryScope) &&
            !ReferenceEquals(CurrentLoadRootScope, fallbackScope) &&
            ResourceResolver.TryFindResource(CurrentLoadRootScope, key, out var rootResource, includeApplicationResources: false))
        {
            return rootResource!;
        }

        if (UiApplication.Current.Resources.TryGetValue(key, out var applicationResource))
        {
            return applicationResource;
        }

        throw CreateXamlException($"StaticResource key '{key}' was not found.");
    }

    private static void RunWithinConstructionScope(FrameworkElement? scope, Action action)
    {
        if (scope == null)
        {
            action();
            return;
        }

        CurrentConstructionScopes ??= new Stack<FrameworkElement>();
        CurrentConstructionScopes.Push(scope);
        try
        {
            action();
        }
        finally
        {
            CurrentConstructionScopes.Pop();
        }
    }

    private static bool TryFindResourceInConstructionScopes(
        object key,
        FrameworkElement? primaryScope,
        FrameworkElement? fallbackScope,
        out object? resource)
    {
        if (CurrentConstructionScopes == null || CurrentConstructionScopes.Count == 0)
        {
            resource = null;
            return false;
        }

        foreach (var scope in CurrentConstructionScopes)
        {
            if (ReferenceEquals(scope, primaryScope) || ReferenceEquals(scope, fallbackScope))
            {
                continue;
            }

            if (scope.Resources.TryGetValue(key, out var value))
            {
                resource = value;
                return true;
            }
        }

        resource = null;
        return false;
    }

    private static object CoerceResolvedResourceValue(object resolved, Type targetType, string location)
    {
        if (targetType == typeof(object))
        {
            return resolved;
        }

        if (resolved != null && targetType.IsInstanceOfType(resolved))
        {
            return resolved;
        }

        if (resolved is string text)
        {
            return ConvertValue(text, targetType);
        }

        throw new InvalidOperationException(
            $"Resource value for '{location}' is of type '{resolved?.GetType().Name ?? "null"}' and cannot be assigned to '{targetType.Name}'.");
    }

    private static void AssignName(object target, string name, object? codeBehind)
    {
        if (target is FrameworkElement frameworkElement)
        {
            frameworkElement.Name = name;
        }

        var nameScopeOwner = GetCurrentNameScopeOwner();
        if (nameScopeOwner != null)
        {
            nameScopeOwner.RegisterNameInLocalScope(name, target);
        }

        if (codeBehind == null)
        {
            return;
        }

        var codeBehindType = codeBehind.GetType();
        var field = codeBehindType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsInstanceOfType(target))
        {
            field.SetValue(codeBehind, target);
            return;
        }

        var property = codeBehindType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null &&
            property.CanWrite &&
            property.PropertyType.IsInstanceOfType(target))
        {
            property.SetValue(codeBehind, target);
        }
    }

    private static FrameworkElement? GetCurrentNameScopeOwner()
    {
        if (CurrentConstructionScopes != null && CurrentConstructionScopes.Count > 0)
        {
            return CurrentConstructionScopes.Last();
        }

        return CurrentLoadRootScope;
    }

    private static bool TryAttachEventHandler(object target, string eventName, string handlerName, object? codeBehind)
    {
        var eventInfo = target.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
        if (eventInfo == null)
        {
            return false;
        }

        if (codeBehind == null)
        {
            throw new InvalidOperationException(
                $"Event '{eventName}' requires a code-behind instance to resolve handler '{handlerName}'.");
        }

        var method = codeBehind.GetType().GetMethod(
            handlerName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (method == null)
        {
            throw new InvalidOperationException(
                $"Handler method '{handlerName}' was not found on code-behind type '{codeBehind.GetType().Name}'.");
        }

        var delegateInstance = Delegate.CreateDelegate(eventInfo.EventHandlerType!, codeBehind, method);
        eventInfo.AddEventHandler(target, delegateInstance);
        return true;
    }

    private static void ApplyProperty(object target, string propertyName, string valueText, FrameworkElement? resourceScope)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

        if (property == null || !property.CanWrite)
        {
            throw CreateXamlException(
                $"Property '{propertyName}' was not found on type '{target.GetType().Name}'.");
        }

        object converted;
        if (TryParseStaticResourceKey(valueText, out var staticResourceKey))
        {
            var scopedOwner = target as FrameworkElement;
            var resolved = ResolveStaticResourceValue(staticResourceKey, scopedOwner, resourceScope);
            converted = CoerceResolvedResourceValue(
                resolved,
                property.PropertyType,
                $"{target.GetType().Name}.{propertyName}");
        }
        else
        {
            converted = ConvertValue(valueText, property.PropertyType);
        }

        property.SetValue(target, converted);
    }

    private static bool TryApplyBindingExpression(object target, string propertyName, string valueText, FrameworkElement? resourceScope)
    {
        var trimmed = valueText.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
            !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        var markupBody = trimmed[1..^1].Trim();
        if (!markupBody.StartsWith("Binding", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (target is not DependencyObject dependencyObject)
        {
            throw CreateXamlException(
                $"Binding markup on '{target.GetType().Name}.{propertyName}' requires a DependencyObject target.");
        }

        var dependencyProperty = ResolveDependencyProperty(target.GetType(), propertyName);
        if (dependencyProperty == null)
        {
            throw CreateXamlException(
                $"Binding markup on '{target.GetType().Name}.{propertyName}' requires a dependency property named '{propertyName}Property'.");
        }

        var bindingBody = markupBody["Binding".Length..].Trim();
        var bindingScope = target as FrameworkElement ?? resourceScope;
        var binding = ParseBinding(bindingBody, dependencyProperty.PropertyType, bindingScope);
        BindingOperations.SetBinding(dependencyObject, dependencyProperty, binding);
        return true;
    }

    private static bool TryApplyDynamicResourceExpression(object target, string propertyName, string valueText)
    {
        if (!TryParseDynamicResourceKey(valueText, out var dynamicResourceKey))
        {
            return false;
        }

        if (target is not FrameworkElement frameworkElement)
        {
            throw CreateXamlException(
                $"DynamicResource markup on '{target.GetType().Name}.{propertyName}' requires a FrameworkElement target.");
        }

        var dependencyProperty = ResolveDependencyProperty(target.GetType(), propertyName);
        if (dependencyProperty == null)
        {
            throw CreateXamlException(
                $"DynamicResource markup on '{target.GetType().Name}.{propertyName}' requires a dependency property named '{propertyName}Property'.");
        }

        frameworkElement.SetResourceReference(dependencyProperty, dynamicResourceKey);
        return true;
    }

    private static Binding ParseBinding(string bindingBody, Type targetPropertyType, FrameworkElement? resourceScope)
    {
        var binding = new Binding();
        if (string.IsNullOrWhiteSpace(bindingBody))
        {
            return binding;
        }

        var segments = SplitBindingSegments(bindingBody);

        var index = 0;
        if (segments.Count > 0 && !segments[0].Contains('=', StringComparison.Ordinal))
        {
            binding.Path = segments[0];
            index = 1;
        }

        for (var i = index; i < segments.Count; i++)
        {
            var segment = segments[i];
            var equalsIndex = segment.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex == segment.Length - 1)
            {
                throw CreateXamlException($"Binding segment '{segment}' is invalid.");
            }

            var key = segment[..equalsIndex].Trim();
            var rawValue = segment[(equalsIndex + 1)..].Trim();

            if (TryApplyBindingOption(binding, key, rawValue, targetPropertyType, resourceScope))
            {
                continue;
            }

            throw CreateXamlException($"Binding key '{key}' is not supported.");
        }

        return binding;
    }

    private static bool TryApplyBindingOption(
        Binding binding,
        string key,
        string rawValue,
        Type targetPropertyType,
        FrameworkElement? resourceScope)
    {
        if (string.Equals(key, nameof(Binding.Path), StringComparison.OrdinalIgnoreCase))
        {
            binding.Path = rawValue;
            return true;
        }

        if (string.Equals(key, nameof(Binding.Mode), StringComparison.OrdinalIgnoreCase))
        {
            binding.Mode = (BindingMode)Enum.Parse(typeof(BindingMode), rawValue, ignoreCase: true);
            return true;
        }

        if (string.Equals(key, nameof(Binding.UpdateSourceTrigger), StringComparison.OrdinalIgnoreCase))
        {
            binding.UpdateSourceTrigger = (UpdateSourceTrigger)Enum.Parse(typeof(UpdateSourceTrigger), rawValue, ignoreCase: true);
            return true;
        }

        if (string.Equals(key, nameof(Binding.FallbackValue), StringComparison.OrdinalIgnoreCase))
        {
            binding.FallbackValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(Binding.TargetNullValue), StringComparison.OrdinalIgnoreCase))
        {
            binding.TargetNullValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(Binding.Source), StringComparison.OrdinalIgnoreCase))
        {
            binding.Source = ResolveBindingSourceValue(rawValue, resourceScope);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ElementName), StringComparison.OrdinalIgnoreCase))
        {
            binding.ElementName = rawValue;
            return true;
        }

        if (string.Equals(key, "RelativeSource", StringComparison.OrdinalIgnoreCase))
        {
            ApplyRelativeSource(binding, rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.Converter), StringComparison.OrdinalIgnoreCase))
        {
            binding.Converter = ResolveBindingResource<IValueConverter>(rawValue, resourceScope, nameof(Binding.Converter));
            return true;
        }

        if (string.Equals(key, nameof(Binding.ConverterParameter), StringComparison.OrdinalIgnoreCase))
        {
            binding.ConverterParameter = ResolveBindingSourceValue(rawValue, resourceScope);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ConverterCulture), StringComparison.OrdinalIgnoreCase))
        {
            binding.ConverterCulture = CultureInfo.GetCultureInfo(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ValidatesOnDataErrors), StringComparison.OrdinalIgnoreCase))
        {
            binding.ValidatesOnDataErrors = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ValidatesOnNotifyDataErrors), StringComparison.OrdinalIgnoreCase))
        {
            binding.ValidatesOnNotifyDataErrors = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.ValidatesOnExceptions), StringComparison.OrdinalIgnoreCase))
        {
            binding.ValidatesOnExceptions = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(Binding.BindingGroupName), StringComparison.OrdinalIgnoreCase))
        {
            binding.BindingGroupName = rawValue;
            return true;
        }

        if (string.Equals(key, nameof(Binding.UpdateSourceExceptionFilter), StringComparison.OrdinalIgnoreCase))
        {
            binding.UpdateSourceExceptionFilter = ResolveBindingResource<UpdateSourceExceptionFilterCallback>(
                rawValue,
                resourceScope,
                nameof(Binding.UpdateSourceExceptionFilter));
            return true;
        }

        return false;
    }

    private static bool TryApplyMultiBindingOption(
        MultiBinding multiBinding,
        string key,
        string rawValue,
        Type targetPropertyType,
        FrameworkElement? resourceScope)
    {
        if (string.Equals(key, nameof(MultiBinding.Mode), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.Mode = (BindingMode)Enum.Parse(typeof(BindingMode), rawValue, ignoreCase: true);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.UpdateSourceTrigger), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.UpdateSourceTrigger = (UpdateSourceTrigger)Enum.Parse(typeof(UpdateSourceTrigger), rawValue, ignoreCase: true);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.FallbackValue), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.FallbackValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.TargetNullValue), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.TargetNullValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.Converter), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.Converter = ResolveBindingResource<IMultiValueConverter>(rawValue, resourceScope, nameof(MultiBinding.Converter));
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ConverterParameter), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ConverterParameter = ResolveBindingSourceValue(rawValue, resourceScope);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ConverterCulture), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ConverterCulture = CultureInfo.GetCultureInfo(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ValidatesOnDataErrors), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ValidatesOnDataErrors = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ValidatesOnNotifyDataErrors), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ValidatesOnNotifyDataErrors = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.ValidatesOnExceptions), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.ValidatesOnExceptions = bool.Parse(rawValue);
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.BindingGroupName), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.BindingGroupName = rawValue;
            return true;
        }

        if (string.Equals(key, nameof(MultiBinding.UpdateSourceExceptionFilter), StringComparison.OrdinalIgnoreCase))
        {
            multiBinding.UpdateSourceExceptionFilter = ResolveBindingResource<UpdateSourceExceptionFilterCallback>(
                rawValue,
                resourceScope,
                nameof(MultiBinding.UpdateSourceExceptionFilter));
            return true;
        }

        return false;
    }

    private static bool TryApplyPriorityBindingOption(
        PriorityBinding priorityBinding,
        string key,
        string rawValue,
        Type targetPropertyType,
        FrameworkElement? resourceScope)
    {
        if (string.Equals(key, nameof(PriorityBinding.Mode), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.Mode = (BindingMode)Enum.Parse(typeof(BindingMode), rawValue, ignoreCase: true);
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.UpdateSourceTrigger), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.UpdateSourceTrigger = (UpdateSourceTrigger)Enum.Parse(typeof(UpdateSourceTrigger), rawValue, ignoreCase: true);
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.FallbackValue), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.FallbackValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.TargetNullValue), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.TargetNullValue = targetPropertyType == typeof(object)
                ? ParseLooseValue(rawValue)
                : ConvertValue(rawValue, targetPropertyType);
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.BindingGroupName), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.BindingGroupName = rawValue;
            return true;
        }

        if (string.Equals(key, nameof(PriorityBinding.UpdateSourceExceptionFilter), StringComparison.OrdinalIgnoreCase))
        {
            priorityBinding.UpdateSourceExceptionFilter = ResolveBindingResource<UpdateSourceExceptionFilterCallback>(
                rawValue,
                resourceScope,
                nameof(PriorityBinding.UpdateSourceExceptionFilter));
            return true;
        }

        return false;
    }

    private static List<string> SplitBindingSegments(string bindingBody)
    {
        var segments = new List<string>();
        var segmentStart = 0;
        var braceDepth = 0;

        for (var i = 0; i < bindingBody.Length; i++)
        {
            var ch = bindingBody[i];
            if (ch == '{')
            {
                braceDepth++;
                continue;
            }

            if (ch == '}')
            {
                braceDepth = Math.Max(0, braceDepth - 1);
                continue;
            }

            if (ch != ',' || braceDepth != 0)
            {
                continue;
            }

            var segment = bindingBody[segmentStart..i].Trim();
            if (segment.Length > 0)
            {
                segments.Add(segment);
            }

            segmentStart = i + 1;
        }

        var tail = bindingBody[segmentStart..].Trim();
        if (tail.Length > 0)
        {
            segments.Add(tail);
        }

        return segments;
    }

    private static void ApplyRelativeSource(Binding binding, string rawValue)
    {
        var trimmed = rawValue.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            binding.RelativeSourceMode = ParseRelativeSourceMode(trimmed);
            return;
        }

        if (!trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            throw CreateXamlException($"RelativeSource expression '{rawValue}' is invalid.");
        }

        var body = trimmed[1..^1].Trim();
        if (!body.StartsWith("RelativeSource", StringComparison.OrdinalIgnoreCase))
        {
            throw CreateXamlException($"RelativeSource expression '{rawValue}' is invalid.");
        }

        var args = body["RelativeSource".Length..].Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            throw CreateXamlException("RelativeSource requires a mode.");
        }

        var parts = SplitBindingSegments(args);
        if (parts.Count == 1 && !parts[0].Contains('=', StringComparison.Ordinal))
        {
            binding.RelativeSourceMode = ParseRelativeSourceMode(parts[0]);
            return;
        }

        foreach (var part in parts)
        {
            var equalsIndex = part.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex == part.Length - 1)
            {
                throw CreateXamlException($"RelativeSource segment '{part}' is invalid.");
            }

            var key = part[..equalsIndex].Trim();
            var value = part[(equalsIndex + 1)..].Trim();

            if (string.Equals(key, "Mode", StringComparison.OrdinalIgnoreCase))
            {
                binding.RelativeSourceMode = ParseRelativeSourceMode(value);
                continue;
            }

            if (string.Equals(key, "AncestorType", StringComparison.OrdinalIgnoreCase))
            {
                binding.RelativeSourceAncestorType = ResolveTypeReference(value);
                continue;
            }

            if (string.Equals(key, "AncestorLevel", StringComparison.OrdinalIgnoreCase))
            {
                binding.RelativeSourceAncestorLevel = int.Parse(value, CultureInfo.InvariantCulture);
                continue;
            }

            throw CreateXamlException($"RelativeSource key '{key}' is not supported.");
        }
    }

    private static RelativeSourceMode ParseRelativeSourceMode(string rawValue)
    {
        return (RelativeSourceMode)Enum.Parse(typeof(RelativeSourceMode), rawValue.Trim(), ignoreCase: true);
    }

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

    private static object ResolveBindingSourceValue(string rawValue, FrameworkElement? resourceScope)
    {
        if (TryParseStaticResourceKey(rawValue, out var staticResourceKey))
        {
            return ResolveStaticResourceValue(staticResourceKey, resourceScope);
        }

        return ParseLooseValue(rawValue);
    }

    private static T ResolveBindingResource<T>(string rawValue, FrameworkElement? resourceScope, string optionName)
        where T : class
    {
        object resolved;
        if (TryParseStaticResourceKey(rawValue, out var staticResourceKey))
        {
            resolved = ResolveStaticResourceValue(staticResourceKey, resourceScope);
        }
        else
        {
            resolved = ResolveStaticResourceValue(rawValue.Trim(), resourceScope);
        }

        if (resolved is T typed)
        {
            return typed;
        }

        throw CreateXamlException(
            $"{optionName} requires a resource assignable to '{typeof(T).Name}', but resolved '{resolved.GetType().Name}'.");
    }

    private static DependencyProperty? ResolveDependencyProperty(Type targetType, string propertyName)
    {
        var fieldName = propertyName + "Property";
        var current = targetType;
        while (current != null)
        {
            var field = current.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            if (field?.FieldType == typeof(DependencyProperty))
            {
                return (DependencyProperty?)field.GetValue(null);
            }

            current = current.BaseType;
        }

        return null;
    }

    private static void ApplyAttachedProperty(
        object target,
        string attachedPropertyName,
        string valueText,
        FrameworkElement? resourceScope)
    {
        var separatorIndex = attachedPropertyName.IndexOf('.');
        var ownerTypeName = attachedPropertyName[..separatorIndex];
        var propertyName = attachedPropertyName[(separatorIndex + 1)..];
        var ownerType = ResolveElementType(ownerTypeName);

        if (TryParseDynamicResourceKey(valueText, out var dynamicResourceKey))
        {
            var dependencyProperty = ResolveDependencyProperty(ownerType, propertyName);
            if (dependencyProperty == null)
            {
                throw CreateXamlException(
                    $"Attached property '{ownerType.Name}.{propertyName}' requires a dependency property named '{propertyName}Property' for DynamicResource usage.");
            }

            if (target is not FrameworkElement frameworkElement)
            {
                throw CreateXamlException(
                    $"DynamicResource markup on attached property '{ownerType.Name}.{propertyName}' requires a FrameworkElement target.");
            }

            frameworkElement.SetResourceReference(dependencyProperty, dynamicResourceKey);
            return;
        }

        var setter = ownerType.GetMethod(
            $"Set{propertyName}",
            BindingFlags.Public | BindingFlags.Static)
            ?? ownerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != $"Set{propertyName}")
                    {
                        return false;
                    }

                    var parameters = m.GetParameters();
                    return parameters.Length == 2 && parameters[0].ParameterType.IsAssignableFrom(target.GetType());
                });

        if (setter == null)
        {
            throw CreateXamlException(
                $"Attached property setter '{ownerType.Name}.Set{propertyName}(..., ...)' was not found.");
        }

        var firstParameter = setter.GetParameters()[0].ParameterType;
        if (!firstParameter.IsAssignableFrom(target.GetType()))
        {
            throw CreateXamlException(
                $"Attached property '{ownerType.Name}.{propertyName}' is not applicable to '{target.GetType().Name}'.");
        }

        var valueType = setter.GetParameters()[1].ParameterType;
        object converted;
        if (TryParseStaticResourceKey(valueText, out var staticResourceKey))
        {
            var resolved = ResolveStaticResourceValue(staticResourceKey, target as FrameworkElement, resourceScope);
            if (!valueType.IsInstanceOfType(resolved))
            {
                throw CreateXamlException(
                    $"Attached property '{ownerType.Name}.{propertyName}' requires a value assignable to '{valueType.Name}', but resource resolved to '{resolved.GetType().Name}'.");
            }

            converted = resolved;
        }
        else
        {
            converted = ConvertValue(valueText, valueType);
        }

        setter.Invoke(null, new[] { target, converted });
    }

    private static object ConvertValue(string rawValue, Type targetType)
    {
        var nullableUnderlyingType = Nullable.GetUnderlyingType(targetType);
        if (nullableUnderlyingType != null)
        {
            if (string.Equals(rawValue.Trim(), "null", StringComparison.OrdinalIgnoreCase))
            {
                return null!;
            }

            return ConvertValue(rawValue, nullableUnderlyingType);
        }

        if (targetType == typeof(string))
        {
            return rawValue;
        }

        if (targetType == typeof(object))
        {
            return rawValue;
        }

        if (targetType == typeof(Type))
        {
            return ResolveTypeReference(rawValue);
        }

        if (targetType == typeof(int))
        {
            return int.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(float))
        {
            return float.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(bool))
        {
            return bool.Parse(rawValue);
        }

        if (targetType == typeof(Color))
        {
            return ParseColor(rawValue);
        }

        if (targetType == typeof(Thickness))
        {
            return ParseThickness(rawValue);
        }

        if (targetType == typeof(GridLength))
        {
            return ParseGridLength(rawValue);
        }

        if (targetType == typeof(ImageSource))
        {
            return ImageSource.FromUri(rawValue);
        }

        if (targetType == typeof(Vector2))
        {
            return GeometryParsers.ParsePoint(rawValue);
        }

        if (targetType == typeof(Geometry))
        {
            return PathGeometry.Parse(rawValue);
        }

        if (targetType == typeof(PathGeometry))
        {
            return PathGeometry.Parse(rawValue);
        }

        if (targetType == typeof(Transform))
        {
            return ParseTransform(rawValue);
        }

        if (targetType == typeof(TimeSpan))
        {
            return TimeSpan.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(KeySpline))
        {
            return KeySpline.Parse(rawValue);
        }

        if (targetType == typeof(IEasingFunction))
        {
            return KeySpline.Parse(rawValue);
        }

        if (targetType == typeof(KeyTime))
        {
            var text = rawValue.Trim();
            if (string.Equals(text, "Uniform", StringComparison.OrdinalIgnoreCase))
            {
                return KeyTime.Uniform;
            }

            if (string.Equals(text, "Paced", StringComparison.OrdinalIgnoreCase))
            {
                return KeyTime.Paced;
            }

            return new KeyTime(TimeSpan.Parse(text, CultureInfo.InvariantCulture));
        }

        if (targetType == typeof(Duration))
        {
            var text = rawValue.Trim();
            if (string.Equals(text, "Automatic", StringComparison.OrdinalIgnoreCase))
            {
                return Duration.Automatic;
            }

            if (string.Equals(text, "Forever", StringComparison.OrdinalIgnoreCase))
            {
                return Duration.Forever;
            }

            return new Duration(TimeSpan.Parse(text, CultureInfo.InvariantCulture));
        }

        if (targetType == typeof(RepeatBehavior))
        {
            var text = rawValue.Trim();
            if (string.Equals(text, "Forever", StringComparison.OrdinalIgnoreCase))
            {
                return RepeatBehavior.Forever;
            }

            if (text.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                var count = double.Parse(text[..^1], CultureInfo.InvariantCulture);
                return new RepeatBehavior(count);
            }

            return new RepeatBehavior(TimeSpan.Parse(text, CultureInfo.InvariantCulture));
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, rawValue, ignoreCase: true);
        }

        throw CreateXamlException($"Cannot convert value '{rawValue}' to type '{targetType.Name}'.");
    }

    private static Color ParseColor(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('#'))
        {
            var hex = trimmed[1..];
            if (hex.Length == 6)
            {
                return new Color(
                    byte.Parse(hex[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }

            if (hex.Length == 8)
            {
                return new Color(
                    byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
        }

        var namedColor = typeof(Color).GetProperty(trimmed, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        if (namedColor?.PropertyType == typeof(Color))
        {
            return (Color)namedColor.GetValue(null)!;
        }

        throw CreateXamlException($"Color value '{value}' is not valid.");
    }

    private static Thickness ParseThickness(string value)
    {
        var parts = value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return new Thickness(float.Parse(parts[0], CultureInfo.InvariantCulture));
        }

        if (parts.Length == 4)
        {
            return new Thickness(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                float.Parse(parts[3], CultureInfo.InvariantCulture));
        }

        throw CreateXamlException("Thickness must be one value or four comma-separated values.");
    }

    private static GridLength ParseGridLength(string value)
    {
        var trimmed = value.Trim();
        if (string.Equals(trimmed, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return GridLength.Auto;
        }

        if (trimmed.EndsWith('*'))
        {
            var weightText = trimmed[..^1];
            var weight = string.IsNullOrWhiteSpace(weightText)
                ? 1f
                : float.Parse(weightText, CultureInfo.InvariantCulture);
            return new GridLength(weight, GridUnitType.Star);
        }

        return new GridLength(float.Parse(trimmed, CultureInfo.InvariantCulture), GridUnitType.Pixel);
    }

    private static object CreateObjectInstance(Type type, XObject? location)
    {
        try
        {
            var instance = Activator.CreateInstance(type);
            if (instance != null)
            {
                return instance;
            }
        }
        catch (MissingMethodException)
        {
            // Fall back to optional-parameter constructors below.
        }

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var allOptional = true;
            var arguments = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!parameters[i].IsOptional)
                {
                    allOptional = false;
                    break;
                }

                arguments[i] = Type.Missing;
            }

            if (!allOptional)
            {
                continue;
            }

            return constructor.Invoke(arguments);
        }

        throw CreateXamlException($"Could not create instance of '{type.Name}'.", location);
    }

    private static Type ResolveElementType(string elementName)
    {
        if (TypeByName.TryGetValue(elementName, out var type))
        {
            return type;
        }

        throw CreateXamlException($"XAML element '{elementName}' could not be resolved.");
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

        return map;
    }

    private static void ApplyObjectChildren(object target, XElement element, object? codeBehind, FrameworkElement? resourceScope)
    {
        if (target is Storyboard storyboard)
        {
            foreach (var child in element.Elements())
            {
                if (TryApplyPropertyElement(target, child, codeBehind, resourceScope))
                {
                    continue;
                }

                storyboard.Children.Add((Timeline)BuildObject(child, codeBehind, resourceScope));
            }

            return;
        }

        if (target is DoubleAnimationUsingKeyFrames doubleKeyFrameAnimation)
        {
            foreach (var child in element.Elements())
            {
                if (TryApplyPropertyElement(target, child, codeBehind, resourceScope))
                {
                    continue;
                }

                doubleKeyFrameAnimation.KeyFrames.Add((DoubleKeyFrame)BuildObject(child, codeBehind, resourceScope));
            }

            return;
        }

        if (target is ColorAnimationUsingKeyFrames colorKeyFrameAnimation)
        {
            foreach (var child in element.Elements())
            {
                if (TryApplyPropertyElement(target, child, codeBehind, resourceScope))
                {
                    continue;
                }

                colorKeyFrameAnimation.KeyFrames.Add((ColorKeyFrame)BuildObject(child, codeBehind, resourceScope));
            }

            return;
        }

        if (target is PointAnimationUsingKeyFrames pointKeyFrameAnimation)
        {
            foreach (var child in element.Elements())
            {
                if (TryApplyPropertyElement(target, child, codeBehind, resourceScope))
                {
                    continue;
                }

                pointKeyFrameAnimation.KeyFrames.Add((PointKeyFrame)BuildObject(child, codeBehind, resourceScope));
            }

            return;
        }

        if (target is ThicknessAnimationUsingKeyFrames thicknessKeyFrameAnimation)
        {
            foreach (var child in element.Elements())
            {
                if (TryApplyPropertyElement(target, child, codeBehind, resourceScope))
                {
                    continue;
                }

                thicknessKeyFrameAnimation.KeyFrames.Add((ThicknessKeyFrame)BuildObject(child, codeBehind, resourceScope));
            }

            return;
        }

        if (target is Int32AnimationUsingKeyFrames int32KeyFrameAnimation)
        {
            foreach (var child in element.Elements())
            {
                if (TryApplyPropertyElement(target, child, codeBehind, resourceScope))
                {
                    continue;
                }

                int32KeyFrameAnimation.KeyFrames.Add((Int32KeyFrame)BuildObject(child, codeBehind, resourceScope));
            }

            return;
        }

        if (target is ObjectAnimationUsingKeyFrames objectKeyFrameAnimation)
        {
            foreach (var child in element.Elements())
            {
                if (TryApplyPropertyElement(target, child, codeBehind, resourceScope))
                {
                    continue;
                }

                objectKeyFrameAnimation.KeyFrames.Add((ObjectKeyFrame)BuildObject(child, codeBehind, resourceScope));
            }

            return;
        }

        if (TryApplyRichTextObjectChildren(target, element, codeBehind, resourceScope))
        {
            return;
        }

        foreach (var child in element.Elements())
        {
            if (!TryApplyPropertyElement(target, child, codeBehind, resourceScope))
            {
                throw CreateXamlException(
                    $"Element '{element.Name.LocalName}' does not support nested child '{child.Name.LocalName}'.",
                    child);
            }
        }
    }

    private static bool TryApplyRichTextObjectChildren(object target, XElement element, object? codeBehind, FrameworkElement? resourceScope)
    {
        if (target is not FlowDocument &&
            target is not Section &&
            target is not Paragraph &&
            target is not Span &&
            target is not InkkSlinger.List &&
            target is not ListItem &&
            target is not Table &&
            target is not TableRowGroup &&
            target is not TableRow &&
            target is not TableCell &&
            target is not Run &&
            target is not LineBreak)
        {
            return false;
        }

        foreach (var child in element.Elements())
        {
            if (TryApplyPropertyElement(target, child, codeBehind, resourceScope))
            {
                continue;
            }

            if (target is FlowDocument flowDocument)
            {
                flowDocument.Blocks.Add(BuildRichTextDirectChild<Block>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is Section section)
            {
                section.Blocks.Add(BuildRichTextDirectChild<Block>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is Paragraph paragraph)
            {
                paragraph.Inlines.Add(BuildRichTextDirectChild<Inline>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is Span span)
            {
                span.Inlines.Add(BuildRichTextDirectChild<Inline>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is InkkSlinger.List list)
            {
                list.Items.Add(BuildRichTextDirectChild<ListItem>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is ListItem listItem)
            {
                listItem.Blocks.Add(BuildRichTextDirectChild<Block>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is Table table)
            {
                table.RowGroups.Add(BuildRichTextDirectChild<TableRowGroup>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is TableRowGroup rowGroup)
            {
                rowGroup.Rows.Add(BuildRichTextDirectChild<TableRow>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is TableRow row)
            {
                row.Cells.Add(BuildRichTextDirectChild<TableCell>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is TableCell cell)
            {
                cell.Blocks.Add(BuildRichTextDirectChild<Block>(target, child, codeBehind, resourceScope));
                continue;
            }

            if (target is Run)
            {
                throw CreateXamlException($"Element '{nameof(Run)}' cannot contain child elements.", child);
            }

            if (target is LineBreak)
            {
                throw CreateXamlException($"Element '{nameof(LineBreak)}' cannot contain child elements.", child);
            }
        }

        return true;
    }

    private static TExpected BuildRichTextDirectChild<TExpected>(
        object parent,
        XElement child,
        object? codeBehind,
        FrameworkElement? resourceScope)
        where TExpected : class
    {
        var built = BuildObject(child, codeBehind, resourceScope);
        if (built is TExpected typed)
        {
            return typed;
        }

        throw CreateXamlException(
            $"Element '{child.Name.LocalName}' is not valid inside '{parent.GetType().Name}'. Expected '{typeof(TExpected).Name}'.",
            child);
    }

    private static Transform ParseTransform(string rawValue)
    {
        var trimmed = rawValue.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new MatrixTransform(Matrix.Identity);
        }

        if (trimmed.StartsWith("translate(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["translate(".Length..^1]);
            return new TranslateTransform
            {
                X = args.Length > 0 ? args[0] : 0f,
                Y = args.Length > 1 ? args[1] : 0f
            };
        }

        if (trimmed.StartsWith("scale(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["scale(".Length..^1]);
            return new ScaleTransform
            {
                ScaleX = args.Length > 0 ? args[0] : 1f,
                ScaleY = args.Length > 1 ? args[1] : (args.Length > 0 ? args[0] : 1f),
                CenterX = args.Length > 2 ? args[2] : 0f,
                CenterY = args.Length > 3 ? args[3] : 0f
            };
        }

        if (trimmed.StartsWith("rotate(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["rotate(".Length..^1]);
            return new RotateTransform
            {
                Angle = args.Length > 0 ? args[0] : 0f,
                CenterX = args.Length > 1 ? args[1] : 0f,
                CenterY = args.Length > 2 ? args[2] : 0f
            };
        }

        if (trimmed.StartsWith("skew(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["skew(".Length..^1]);
            return new SkewTransform
            {
                AngleX = args.Length > 0 ? args[0] : 0f,
                AngleY = args.Length > 1 ? args[1] : 0f,
                CenterX = args.Length > 2 ? args[2] : 0f,
                CenterY = args.Length > 3 ? args[3] : 0f
            };
        }

        if (trimmed.StartsWith("matrix(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
        {
            var args = ParseFloatList(trimmed["matrix(".Length..^1]);
            if (args.Length != 6)
            {
                throw CreateXamlException("matrix(...) transform requires 6 values.");
            }

            var matrix = new Matrix(
                args[0], args[1], 0f, 0f,
                args[2], args[3], 0f, 0f,
                0f, 0f, 1f, 0f,
                args[4], args[5], 0f, 1f);
            return new MatrixTransform(matrix);
        }

        throw CreateXamlException($"Transform value '{rawValue}' is not valid.");
    }

    private static float[] ParseFloatList(string text)
    {
        var parts = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            result[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static InvalidOperationException CreateXamlException(string message, XObject? location = null, Exception? inner = null)
    {
        var fullMessage = message + FormatLineInfo(location);
        return inner == null
            ? new InvalidOperationException(fullMessage)
            : new InvalidOperationException(fullMessage, inner);
    }

    private static string FormatLineInfo(XObject? location)
    {
        if (location is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo())
        {
            return string.Empty;
        }

        return $" (Line {lineInfo.LineNumber}, Position {lineInfo.LinePosition})";
    }
}
