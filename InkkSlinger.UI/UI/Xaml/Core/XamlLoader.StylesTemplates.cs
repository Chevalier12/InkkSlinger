using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static Style BuildStyle(XElement element, FrameworkElement? resourceScope, object? codeBehind)
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
                    if (string.Equals(setterElement.Name.LocalName, nameof(Setter), StringComparison.Ordinal))
                    {
                        style.Setters.Add(BuildSetter(setterElement, targetType, resourceScope));
                        continue;
                    }

                    if (string.Equals(setterElement.Name.LocalName, nameof(EventSetter), StringComparison.Ordinal))
                    {
                        style.Setters.Add(BuildEventSetter(setterElement, targetType, codeBehind));
                        continue;
                    }

                    throw CreateXamlException("Style.Setters can only contain Setter or EventSetter elements.", setterElement);
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

            if (string.Equals(childName, nameof(EventSetter), StringComparison.Ordinal))
            {
                style.Setters.Add(BuildEventSetter(child, targetType, codeBehind));
                continue;
            }

            if (string.Equals(childName, nameof(Trigger), StringComparison.Ordinal) ||
                string.Equals(childName, nameof(MultiTrigger), StringComparison.Ordinal) ||
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
        var hasSourceSelector = false;
        var hasElementNameSelector = false;
        var hasRelativeSourceAttribute = false;
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

            if (string.Equals(localName, "RelativeSource", StringComparison.OrdinalIgnoreCase))
            {
                hasRelativeSourceAttribute = true;
            }
            else if (string.Equals(localName, nameof(Binding.Source), StringComparison.OrdinalIgnoreCase))
            {
                hasSourceSelector = true;
            }
            else if (string.Equals(localName, nameof(Binding.ElementName), StringComparison.OrdinalIgnoreCase))
            {
                hasElementNameSelector = true;
            }

            if (TryApplyBindingOption(binding, localName, rawValue, targetPropertyType, resourceScope))
            {
                continue;
            }

            throw CreateXamlException($"Binding attribute '{localName}' is not supported.", attribute);
        }

        var hasRelativeSourcePropertyElement = false;
        foreach (var child in element.Elements())
        {
            if (!string.Equals(child.Name.LocalName, "Binding.RelativeSource", StringComparison.Ordinal))
            {
                throw CreateXamlException("Binding can only contain Binding.RelativeSource child elements.", child);
            }

            if (hasRelativeSourcePropertyElement)
            {
                throw CreateXamlException("Binding.RelativeSource can only be specified once.", child);
            }

            if (hasRelativeSourceAttribute)
            {
                throw CreateXamlException(
                    "Binding cannot specify RelativeSource both as an attribute and as a Binding.RelativeSource property element.",
                    child);
            }

            ApplyBindingRelativeSourcePropertyElement(binding, child);
            hasRelativeSourcePropertyElement = true;
        }

        ValidateBindingSourceSelectorConflict(
            binding,
            element,
            hasSourceSelector,
            hasElementNameSelector,
            hasRelativeSourceAttribute || hasRelativeSourcePropertyElement);
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
        XElement? rootVisual = null;
        foreach (var child in element.Elements())
        {
            if (rootVisual != null)
            {
                throw CreateXamlException("DataTemplate can only contain a single root visual.", element);
            }

            rootVisual = new XElement(child);
        }

        var template = new DataTemplate((item, scope) =>
        {
            if (rootVisual == null)
            {
                return null;
            }

            var built = RunWithinIsolatedTemplateInstantiationScope(() =>
                BuildElement(rootVisual, null, scope ?? resourceScope));
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


    private static ItemsPanelTemplate BuildItemsPanelTemplate(XElement element, object? codeBehind, FrameworkElement? resourceScope)
    {
        XElement? rootPanel = null;
        foreach (var child in element.Elements())
        {
            if (rootPanel != null)
            {
                throw CreateXamlException("ItemsPanelTemplate can only contain a single root panel.", element);
            }

            var childType = ResolveElementType(child.Name.LocalName);
            if (!typeof(Panel).IsAssignableFrom(childType))
            {
                throw CreateXamlException("ItemsPanelTemplate root element must derive from Panel.", child);
            }

            rootPanel = new XElement(child);
        }

        if (rootPanel == null)
        {
            throw CreateXamlException("ItemsPanelTemplate requires a panel root element.", element);
        }

        var templatePanelRoot = rootPanel;
        return new ItemsPanelTemplate(owner =>
        {
            var built = RunWithinIsolatedTemplateInstantiationScope(() =>
                BuildElement(templatePanelRoot, codeBehind, owner ?? resourceScope));
            if (built is Panel panel)
            {
                return panel;
            }

            throw new InvalidOperationException("ItemsPanelTemplate must build a Panel instance.");
        });
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
                string.Equals(childName, nameof(MultiTrigger), StringComparison.Ordinal) ||
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

        var templateVisualRoot = new XElement(visualRoot);
        var visualStateGroupElements = ExtractTemplateVisualStateGroupElements(templateVisualRoot);
        var templateAnalysis = AnalyzeTemplateVisualRoot(templateVisualRoot, targetType, resourceScope);
        var rootElementType = ResolveElementType(templateVisualRoot.Name.LocalName);
        var template = new ControlTemplate(owner =>
        {
            var built = RunWithinIsolatedTemplateInstantiationScope(() =>
                BuildElement(templateVisualRoot, null, owner));
            if (built is FrameworkElement templateRoot && visualStateGroupElements.Count > 0)
            {
                VisualStateManager.SetVisualStateGroups(
                    templateRoot,
                    BuildVisualStateGroupCollection(
                        visualStateGroupElements,
                        rootElementType,
                        resourceScope,
                        targetName =>
                        {
                            return templateAnalysis.NamedElementTypes.TryGetValue(targetName, out var elementType)
                                ? elementType
                                : null;
                        }));
            }

            return built;
        })
        {
            TargetType = targetType
        };

        foreach (var templateBinding in templateAnalysis.Bindings)
        {
            template.BindTemplate(
                templateBinding.TargetName,
                templateBinding.TargetProperty,
                templateBinding.SourceProperty,
                templateBinding.FallbackValue,
                templateBinding.TargetNullValue);
        }

        foreach (var triggerElement in triggerDefinitions)
        {
            template.Triggers.Add(
                BuildTriggerBase(
                    triggerElement,
                    targetType,
                    resourceScope,
                    targetName =>
                    {
                        return templateAnalysis.NamedElementTypes.TryGetValue(targetName, out var elementType)
                            ? elementType
                            : null;
                    }));
        }

        return template;
    }


    private static List<XElement> ExtractTemplateVisualStateGroupElements(XElement templateVisualRoot)
    {
        var extracted = new List<XElement>();
        var visualStatePropertyElements = templateVisualRoot.Elements()
            .Where(static child => string.Equals(child.Name.LocalName, "VisualStateManager.VisualStateGroups", StringComparison.Ordinal))
            .ToList();

        foreach (var propertyElement in visualStatePropertyElements)
        {
            foreach (var child in propertyElement.Elements())
            {
                extracted.Add(new XElement(child));
            }

            propertyElement.Remove();
        }

        return extracted;
    }


    private static VisualStateGroupCollection BuildVisualStateGroupCollection(
        IEnumerable<XElement> groupElements,
        Type rootElementType,
        FrameworkElement? resourceScope,
        Func<string, Type?> setterTargetTypeResolver)
    {
        var groups = new VisualStateGroupCollection();
        foreach (var groupElement in groupElements)
        {
            groups.Add(BuildVisualStateGroup(groupElement, rootElementType, resourceScope, setterTargetTypeResolver));
        }

        return groups;
    }


    private static VisualStateGroup BuildVisualStateGroup(
        XElement element,
        Type rootElementType,
        FrameworkElement? resourceScope,
        Func<string, Type?> setterTargetTypeResolver)
    {
        if (!string.Equals(element.Name.LocalName, nameof(VisualStateGroup), StringComparison.Ordinal))
        {
            throw CreateXamlException(
                $"VisualStateManager.VisualStateGroups can only contain '{nameof(VisualStateGroup)}' elements.",
                element);
        }

        var name = ResolveVisualStateName(element, nameof(VisualStateGroup));
        var group = new VisualStateGroup(name);

        foreach (var child in element.Elements())
        {
            if (string.Equals(child.Name.LocalName, "VisualStateGroup.States", StringComparison.Ordinal))
            {
                foreach (var stateElement in child.Elements())
                {
                    group.States.Add(BuildVisualState(stateElement, rootElementType, resourceScope, setterTargetTypeResolver));
                }

                continue;
            }

            group.States.Add(BuildVisualState(child, rootElementType, resourceScope, setterTargetTypeResolver));
        }

        return group;
    }


    private static VisualState BuildVisualState(
        XElement element,
        Type rootElementType,
        FrameworkElement? resourceScope,
        Func<string, Type?> setterTargetTypeResolver)
    {
        if (!string.Equals(element.Name.LocalName, nameof(VisualState), StringComparison.Ordinal))
        {
            throw CreateXamlException(
                $"'{nameof(VisualStateGroup)}' can only contain '{nameof(VisualState)}' elements.",
                element);
        }

        var state = new VisualState(ResolveVisualStateName(element, nameof(VisualState)));

        foreach (var child in element.Elements())
        {
            if (string.Equals(child.Name.LocalName, nameof(Storyboard), StringComparison.Ordinal))
            {
                state.Storyboard = (Storyboard)BuildObject(child, null, resourceScope);
                continue;
            }

            if (string.Equals(child.Name.LocalName, "VisualState.Storyboard", StringComparison.Ordinal))
            {
                var storyboardElement = GetSingleChildElementOrThrow(
                    child,
                    "VisualState.Storyboard must contain exactly one Storyboard element.",
                    child);
                state.Storyboard = (Storyboard)BuildObject(storyboardElement, null, resourceScope);
                continue;
            }

            if (string.Equals(child.Name.LocalName, "VisualState.Setters", StringComparison.Ordinal))
            {
                foreach (var setterElement in child.Elements())
                {
                    state.Setters.Add(BuildSetter(setterElement, rootElementType, resourceScope, setterTargetTypeResolver));
                }

                continue;
            }

            throw CreateXamlException(
                $"Element '{nameof(VisualState)}' does not support nested child '{child.Name.LocalName}'.",
                child);
        }

        return state;
    }


    private static string ResolveVisualStateName(XElement element, string typeName)
    {
        var xName = element.Attribute(XName.Get("Name", XamlNamespace.NamespaceName))?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(xName))
        {
            return xName;
        }

        var name = element.Attribute(nameof(VisualState.Name))?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        throw CreateXamlException($"'{typeName}' requires x:Name or Name.", element);
    }


    private static TemplateAnalysis AnalyzeTemplateVisualRoot(
        XElement templateVisualRoot,
        Type templateTargetType,
        FrameworkElement? resourceScope)
    {
        var templateElements = new List<XElement>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        CollectTemplateElements(templateVisualRoot, templateElements, usedNames);

        var bindings = new List<TemplateBindingRegistration>();
        var namedElementTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        var autoNameCounter = 0;
        for (var i = 0; i < templateElements.Count; i++)
        {
            var templateElement = templateElements[i];
            var elementType = ResolveElementType(templateElement.Name.LocalName);
            if (TryGetTemplateElementName(templateElement, out var existingName))
            {
                namedElementTypes[existingName] = elementType;
            }

            var isRoot = i == 0;
            for (var attribute = templateElement.FirstAttribute; attribute != null;)
            {
                var nextAttribute = attribute.NextAttribute;
                if (!attribute.IsNamespaceDeclaration && string.IsNullOrEmpty(attribute.Name.NamespaceName))
                {
                    var targetPropertyName = attribute.Name.LocalName;
                    if (!targetPropertyName.Contains('.', StringComparison.Ordinal) &&
                        TryParseTemplateBindingMarkup(attribute.Value, attribute, out var markup))
                    {
                        var sourceProperty = ResolveDependencyProperty(templateTargetType, markup.SourcePropertyName);
                        if (sourceProperty == null)
                        {
                            throw CreateXamlException(
                                $"TemplateBinding source property '{markup.SourcePropertyName}' was not found on '{templateTargetType.Name}'.",
                                attribute);
                        }

                        var targetProperty = ResolveDependencyProperty(elementType, targetPropertyName);
                        if (targetProperty == null)
                        {
                            throw CreateXamlException(
                                $"TemplateBinding target property '{targetPropertyName}' was not found on '{elementType.Name}'.",
                                attribute);
                        }

                        var targetName = ResolveTemplateBindingTargetName(
                            templateElement,
                            elementType,
                            isRoot,
                            usedNames,
                            ref autoNameCounter,
                            attribute);
                        if (targetName.Length > 0)
                        {
                            namedElementTypes[targetName] = elementType;
                        }

                        object? fallbackValue = null;
                        if (markup.HasFallbackValue)
                        {
                            fallbackValue = ConvertTemplateBindingOptionValue(
                                markup.FallbackValueText,
                                sourceProperty.PropertyType,
                                resourceScope,
                                attribute,
                                nameof(Binding.FallbackValue));
                        }

                        object? targetNullValue = null;
                        if (markup.HasTargetNullValue)
                        {
                            targetNullValue = ConvertTemplateBindingOptionValue(
                                markup.TargetNullValueText,
                                sourceProperty.PropertyType,
                                resourceScope,
                                attribute,
                                nameof(Binding.TargetNullValue));
                        }

                        bindings.Add(
                            new TemplateBindingRegistration(
                                targetName,
                                targetProperty,
                                sourceProperty,
                                fallbackValue,
                                targetNullValue));

                        attribute.Remove();
                    }
                }

                attribute = nextAttribute;
            }
        }

        return new TemplateAnalysis(bindings, namedElementTypes);
    }


    private static void CollectTemplateElements(
        XElement element,
        ICollection<XElement> elements,
        ISet<string> usedNames)
    {
        if (!IsPropertyElementName(element.Name.LocalName))
        {
            elements.Add(element);
            if (TryGetTemplateElementName(element, out var existingName))
            {
                usedNames.Add(existingName);
            }
        }

        foreach (var child in element.Elements())
        {
            CollectTemplateElements(child, elements, usedNames);
        }
    }


    private static bool IsPropertyElementName(string localName)
    {
        var separatorIndex = localName.IndexOf('.');
        return separatorIndex > 0 && separatorIndex < localName.Length - 1;
    }


    private static bool TryGetTemplateElementName(XElement element, out string name)
    {
        name = string.Empty;

        var localName = element.Attribute(nameof(FrameworkElement.Name))?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(localName))
        {
            name = localName;
            return true;
        }

        var xName = element.Attribute(XName.Get("Name", XamlNamespace.NamespaceName))?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(xName))
        {
            name = xName;
            return true;
        }

        return false;
    }


    private static string ResolveTemplateBindingTargetName(
        XElement element,
        Type elementType,
        bool isRoot,
        ISet<string> usedNames,
        ref int autoNameCounter,
        XObject location)
    {
        if (isRoot)
        {
            return string.Empty;
        }

        if (TryGetTemplateElementName(element, out var existingName))
        {
            usedNames.Add(existingName);
            return existingName;
        }

        if (!typeof(FrameworkElement).IsAssignableFrom(elementType))
        {
            throw CreateXamlException(
                $"TemplateBinding target element '{elementType.Name}' must be a FrameworkElement or explicitly named root.",
                location);
        }

        string generatedName;
        do
        {
            generatedName = $"__templateBindingTarget{autoNameCounter.ToString(CultureInfo.InvariantCulture)}";
            autoNameCounter++;
        }
        while (usedNames.Contains(generatedName));

        usedNames.Add(generatedName);
        element.SetAttributeValue(nameof(FrameworkElement.Name), generatedName);
        return generatedName;
    }


    private static bool TryParseTemplateBindingMarkup(string rawValue, XObject location, out TemplateBindingMarkup markup)
    {
        markup = default;

        if (!TryParseMarkupExtensionExpression(rawValue, out var expression) ||
            !string.Equals(expression.Name, "TemplateBinding", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var arguments = expression.Body;
        if (string.IsNullOrWhiteSpace(arguments))
        {
            throw CreateXamlException("TemplateBinding markup requires a source property.", location);
        }

        var parts = SplitBindingSegments(arguments);
        if (parts.Count == 0)
        {
            throw CreateXamlException("TemplateBinding markup requires a source property.", location);
        }

        string? sourcePropertyName = null;
        string? fallbackValueText = null;
        var hasFallbackValue = false;
        string? targetNullValueText = null;
        var hasTargetNullValue = false;

        var index = 0;
        if (!parts[0].Contains('=', StringComparison.Ordinal))
        {
            sourcePropertyName = parts[0].Trim();
            index = 1;
        }

        for (var i = index; i < parts.Count; i++)
        {
            var part = parts[i];
            var equalsIndex = part.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex == part.Length - 1)
            {
                throw CreateXamlException($"TemplateBinding segment '{part}' is invalid.", location);
            }

            var key = part[..equalsIndex].Trim();
            var value = part[(equalsIndex + 1)..].Trim();
            if (string.Equals(key, "Property", StringComparison.OrdinalIgnoreCase))
            {
                sourcePropertyName = value;
                continue;
            }

            if (string.Equals(key, nameof(Binding.FallbackValue), StringComparison.OrdinalIgnoreCase))
            {
                fallbackValueText = value;
                hasFallbackValue = true;
                continue;
            }

            if (string.Equals(key, nameof(Binding.TargetNullValue), StringComparison.OrdinalIgnoreCase))
            {
                targetNullValueText = value;
                hasTargetNullValue = true;
                continue;
            }

            throw CreateXamlException($"TemplateBinding option '{key}' is not supported.", location);
        }

        if (string.IsNullOrWhiteSpace(sourcePropertyName))
        {
            throw CreateXamlException("TemplateBinding markup requires a source property.", location);
        }

        markup = new TemplateBindingMarkup(
            sourcePropertyName!,
            fallbackValueText,
            hasFallbackValue,
            targetNullValueText,
            hasTargetNullValue);
        return true;
    }


    private static object? ConvertTemplateBindingOptionValue(
        string? rawValueText,
        Type valueType,
        FrameworkElement? resourceScope,
        XObject location,
        string optionName)
    {
        if (rawValueText == null)
        {
            return null;
        }

        var trimmed = rawValueText.Trim();
        if (TryParseDynamicResourceKey(trimmed, out _))
        {
            return new DynamicResourceReferenceExpression(GetMarkupExtensionRequiredValue(trimmed, "DynamicResource", "resource key"));
        }

        if (TryParseStaticResourceKey(trimmed, out var staticResourceKey))
        {
            var resolved = ResolveStaticResourceValue(staticResourceKey, resourceScope);
            try
            {
                return CoerceResolvedResourceValue(resolved, valueType, $"TemplateBinding {optionName}");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
            {
                throw CreateXamlException($"Failed to parse TemplateBinding {optionName}: {ex.Message}", location, ex);
            }
        }

        if (TryResolveXNull(trimmed, out var isXNull) && isXNull)
        {
            if (valueType.IsValueType && Nullable.GetUnderlyingType(valueType) == null)
            {
                throw CreateXamlException($"Failed to parse TemplateBinding {optionName}: x:Null is not assignable to '{valueType.Name}'.", location);
            }

            return null;
        }

        if (TryResolveXType(trimmed, out var xType))
        {
            if (valueType != typeof(object) && valueType != typeof(Type))
            {
                throw CreateXamlException($"Failed to parse TemplateBinding {optionName}: x:Type is not assignable to '{valueType.Name}'.", location);
            }

            return xType;
        }

        if (TryResolveXReference(trimmed, resourceScope, out var xReferenceValue, location))
        {
            if (xReferenceValue is DeferredXReference deferredReference)
            {
                throw CreateXamlException(
                    $"TemplateBinding {optionName} could not resolve x:Reference target '{deferredReference.Name}'.",
                    location);
            }

            return xReferenceValue;
        }

        if (TryResolveXStatic(trimmed, out var xStaticValue, location))
        {
            return ResourceReferenceResolver.Coerce(xStaticValue, valueType, $"TemplateBinding {optionName}");
        }

        if (IsMarkupExtensionSyntax(trimmed))
        {
            ThrowUnsupportedMarkupExtension(trimmed, $"TemplateBinding {optionName}", location);
        }

        try
        {
            return ConvertValue(trimmed, valueType);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
        {
            throw CreateXamlException($"Failed to parse TemplateBinding {optionName}: {ex.Message}", location, ex);
        }
    }


    private readonly record struct TemplateBindingRegistration(
        string TargetName,
        DependencyProperty TargetProperty,
        DependencyProperty SourceProperty,
        object? FallbackValue,
        object? TargetNullValue);


    private readonly record struct TemplateAnalysis(
        IReadOnlyList<TemplateBindingRegistration> Bindings,
        IReadOnlyDictionary<string, Type> NamedElementTypes);


    private readonly record struct TemplateBindingMarkup(
        string SourcePropertyName,
        string? FallbackValueText,
        bool HasFallbackValue,
        string? TargetNullValueText,
        bool HasTargetNullValue);


    private static TriggerBase BuildTriggerBase(
        XElement element,
        Type styleTargetType,
        FrameworkElement? resourceScope,
        Func<string, Type?>? setterTargetTypeResolver = null)
    {
        if (string.Equals(element.Name.LocalName, nameof(Trigger), StringComparison.Ordinal))
        {
            return BuildPropertyTrigger(element, styleTargetType, resourceScope, setterTargetTypeResolver);
        }

        if (string.Equals(element.Name.LocalName, nameof(DataTrigger), StringComparison.Ordinal))
        {
            return BuildDataTrigger(element, styleTargetType, resourceScope, setterTargetTypeResolver);
        }

        if (string.Equals(element.Name.LocalName, nameof(MultiTrigger), StringComparison.Ordinal))
        {
            return BuildMultiTrigger(element, styleTargetType, resourceScope, setterTargetTypeResolver);
        }

        if (string.Equals(element.Name.LocalName, nameof(MultiDataTrigger), StringComparison.Ordinal))
        {
            return BuildMultiDataTrigger(element, styleTargetType, resourceScope, setterTargetTypeResolver);
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


    private static Trigger BuildPropertyTrigger(
        XElement element,
        Type styleTargetType,
        FrameworkElement? resourceScope,
        Func<string, Type?>? setterTargetTypeResolver)
    {
        var propertyText = GetRequiredAttributeValue(element, nameof(Trigger.Property));
        var dependencyProperty = ResolveSetterProperty(styleTargetType, propertyText);
        var rawValue = GetRequiredAttributeValue(element, nameof(Trigger.Value));
        var triggerValue = ConvertValue(rawValue, dependencyProperty.PropertyType);

        var trigger = new Trigger(dependencyProperty, triggerValue);
        foreach (var setterElement in EnumerateSetterElements(element, "Trigger.Setters"))
        {
            trigger.Setters.Add(BuildSetter(setterElement, styleTargetType, resourceScope, setterTargetTypeResolver));
        }

        BuildTriggerActions(element, styleTargetType, resourceScope, trigger, "Trigger.EnterActions", "Trigger.ExitActions");

        return trigger;
    }


    private static DataTrigger BuildDataTrigger(
        XElement element,
        Type styleTargetType,
        FrameworkElement? resourceScope,
        Func<string, Type?>? setterTargetTypeResolver)
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

            var bindingChild = GetSingleChildElementOrThrow(child, "DataTrigger.Binding must contain a single Binding element.", child);
            if (!string.Equals(bindingChild.Name.LocalName, nameof(Binding), StringComparison.Ordinal))
            {
                throw CreateXamlException("DataTrigger.Binding must contain a single Binding element.", child);
            }

            binding = BuildBindingElement(bindingChild, typeof(object), resourceScope);
        }

        if (binding == null)
        {
            throw CreateXamlException("DataTrigger requires a Binding.", element);
        }

        var valueText = GetRequiredAttributeValue(element, nameof(DataTrigger.Value));
        var dataTrigger = new DataTrigger(binding, ParseLooseValue(valueText));

        foreach (var setterElement in EnumerateSetterElements(element, "DataTrigger.Setters"))
        {
            dataTrigger.Setters.Add(BuildSetter(setterElement, styleTargetType, resourceScope, setterTargetTypeResolver));
        }

        BuildTriggerActions(element, styleTargetType, resourceScope, dataTrigger, "DataTrigger.EnterActions", "DataTrigger.ExitActions");

        return dataTrigger;
    }


    private static MultiDataTrigger BuildMultiDataTrigger(
        XElement element,
        Type styleTargetType,
        FrameworkElement? resourceScope,
        Func<string, Type?>? setterTargetTypeResolver)
    {
        var multiDataTrigger = new MultiDataTrigger();

        foreach (var conditionElement in EnumerateConditionElements(element, "MultiDataTrigger.Conditions"))
        {
            var condition = BuildCondition(conditionElement, styleTargetType, resourceScope);
            if (condition.Binding == null || condition.Property != null)
            {
                throw CreateXamlException("MultiDataTrigger condition requires Binding and forbids Property.", conditionElement);
            }

            multiDataTrigger.Conditions.Add(condition);
        }

        if (multiDataTrigger.Conditions.Count == 0)
        {
            throw CreateXamlException("MultiDataTrigger requires at least one Condition.", element);
        }

        foreach (var setterElement in EnumerateSetterElements(element, "MultiDataTrigger.Setters"))
        {
            multiDataTrigger.Setters.Add(BuildSetter(setterElement, styleTargetType, resourceScope, setterTargetTypeResolver));
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


    private static MultiTrigger BuildMultiTrigger(
        XElement element,
        Type styleTargetType,
        FrameworkElement? resourceScope,
        Func<string, Type?>? setterTargetTypeResolver)
    {
        var multiTrigger = new MultiTrigger();

        foreach (var conditionElement in EnumerateConditionElements(element, "MultiTrigger.Conditions"))
        {
            var condition = BuildCondition(conditionElement, styleTargetType, resourceScope);
            if (condition.Property == null || condition.Binding != null)
            {
                throw CreateXamlException("MultiTrigger condition requires Property and forbids Binding.", conditionElement);
            }

            multiTrigger.Conditions.Add(condition);
        }

        if (multiTrigger.Conditions.Count == 0)
        {
            throw CreateXamlException("MultiTrigger requires at least one Condition.", element);
        }

        foreach (var setterElement in EnumerateSetterElements(element, "MultiTrigger.Setters"))
        {
            multiTrigger.Setters.Add(BuildSetter(setterElement, styleTargetType, resourceScope, setterTargetTypeResolver));
        }

        BuildTriggerActions(
            element,
            styleTargetType,
            resourceScope,
            multiTrigger,
            "MultiTrigger.EnterActions",
            "MultiTrigger.ExitActions");

        return multiTrigger;
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


    private static Condition BuildCondition(XElement element, Type styleTargetType, FrameworkElement? resourceScope)
    {
        DependencyProperty? property = null;
        var propertyText = GetOptionalAttributeValue(element, nameof(Condition.Property));
        if (!string.IsNullOrWhiteSpace(propertyText))
        {
            property = ResolveSetterProperty(styleTargetType, propertyText);
        }

        Binding? binding = null;
        var bindingText = GetOptionalAttributeValue(element, nameof(Condition.Binding));
        if (!string.IsNullOrWhiteSpace(bindingText))
        {
            binding = ParseBindingMarkup(bindingText, resourceScope);
        }

        var rawValue = GetRequiredAttributeValue(element, nameof(Condition.Value));
        var value = property == null
            ? ParseLooseValue(rawValue)
            : ConvertValue(rawValue, property.PropertyType);

        foreach (var child in element.Elements())
        {
            if (!string.Equals(child.Name.LocalName, "Condition.Binding", StringComparison.Ordinal))
            {
                continue;
            }

            var bindingChild = GetSingleChildElementOrThrow(child, "Condition.Binding must contain a single Binding element.", child);
            if (!string.Equals(bindingChild.Name.LocalName, nameof(Binding), StringComparison.Ordinal))
            {
                throw CreateXamlException("Condition.Binding must contain a single Binding element.", child);
            }

            binding = BuildBindingElement(bindingChild, typeof(object), resourceScope);
        }

        return new Condition
        {
            Property = property,
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
                string.Equals(child.Name.LocalName, nameof(RemoveStoryboard), StringComparison.Ordinal) ||
                string.Equals(child.Name.LocalName, nameof(SetStoryboardSpeedRatio), StringComparison.Ordinal) ||
                string.Equals(child.Name.LocalName, nameof(SkipStoryboardToFill), StringComparison.Ordinal))
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
                convertedValue = new DynamicResourceReferenceExpression(
                    GetMarkupExtensionRequiredValue(valueText, "DynamicResource", "resource key"));
            }
            else if (TryParseStaticResourceKey(valueText, out var staticResourceKey))
            {
                var resolved = ResolveStaticResourceValue(staticResourceKey, resourceScope);
                convertedValue = CoerceResolvedResourceValue(
                    resolved,
                    dependencyProperty.PropertyType,
                    $"SetValueAction for {styleTargetType.Name}.{dependencyProperty.Name}");
            }
            else
            {
                convertedValue = ConvertAssignableTextValue(
                    valueText,
                    dependencyProperty.PropertyType,
                    resourceScope,
                    actionElement,
                    $"SetValueAction for {styleTargetType.Name}.{dependencyProperty.Name}");

                if (convertedValue is DeferredXReference deferredReference)
                {
                    throw CreateXamlException(
                        $"SetValueAction for {styleTargetType.Name}.{dependencyProperty.Name} could not resolve x:Reference target '{deferredReference.Name}'.",
                        actionElement);
                }
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
                action.HandoffBehavior = ParseEnumValue<HandoffBehavior>(handoff);
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
                else if (TryParseDynamicResourceKey(storyboardValue, out var dynamicStoryboardResourceKey))
                {
                    action.StoryboardResourceReference = new DynamicResourceReferenceExpression(dynamicStoryboardResourceKey);
                }
            }

            foreach (var child in actionElement.Elements())
            {
                if (string.Equals(child.Name.LocalName, nameof(Storyboard), StringComparison.Ordinal))
                {
                    action.Storyboard = (Storyboard)BuildObject(child, null, resourceScope);
                }
            }

            if (action.Storyboard == null && action.StoryboardResourceReference == null)
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
                seek.Origin = ParseEnumValue<TimeSeekOrigin>(origin);
            }

            return seek;
        }

        if (string.Equals(actionElement.Name.LocalName, nameof(SetStoryboardSpeedRatio), StringComparison.Ordinal))
        {
            var setSpeedRatio = new SetStoryboardSpeedRatio
            {
                BeginStoryboardName = GetRequiredAttributeValue(actionElement, nameof(SetStoryboardSpeedRatio.BeginStoryboardName))
            };

            var speedRatioText = GetOptionalAttributeValue(actionElement, nameof(SetStoryboardSpeedRatio.SpeedRatio));
            if (!string.IsNullOrWhiteSpace(speedRatioText))
            {
                if (!float.TryParse(speedRatioText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSpeedRatio))
                {
                    throw CreateXamlException(
                        $"SetStoryboardSpeedRatio SpeedRatio value '{speedRatioText}' is not valid. Use a finite positive number.",
                        actionElement);
                }

                if (!float.IsFinite(parsedSpeedRatio) || parsedSpeedRatio <= 0f)
                {
                    throw CreateXamlException(
                        $"SetStoryboardSpeedRatio SpeedRatio value '{speedRatioText}' must be finite and greater than 0.",
                        actionElement);
                }

                setSpeedRatio.SpeedRatio = parsedSpeedRatio;
            }

            return setSpeedRatio;
        }

        if (string.Equals(actionElement.Name.LocalName, nameof(SkipStoryboardToFill), StringComparison.Ordinal))
        {
            return new SkipStoryboardToFill
            {
                BeginStoryboardName = GetRequiredAttributeValue(actionElement, nameof(SkipStoryboardToFill.BeginStoryboardName))
            };
        }

        throw CreateXamlException(
            $"Trigger action '{actionElement.Name.LocalName}' is not supported.",
            actionElement,
            code: XamlDiagnosticCode.UnsupportedConstruct,
            hint: "Use BeginStoryboard or supported trigger actions only.");
    }


    private static Setter BuildSetter(
        XElement element,
        Type styleTargetType,
        FrameworkElement? resourceScope,
        Func<string, Type?>? setterTargetTypeResolver = null)
    {
        var targetName = GetOptionalAttributeValue(element, "TargetName") ?? string.Empty;
        var propertyText = GetRequiredAttributeValue(element, nameof(Setter.Property));
        var dependencyProperty = ResolveSetterProperty(styleTargetType, propertyText, targetName, setterTargetTypeResolver);
        var convertedValue = BuildSetterValue(element, styleTargetType, dependencyProperty, resourceScope);

        return new Setter(targetName, dependencyProperty, convertedValue);
    }


    private static EventSetter BuildEventSetter(XElement element, Type styleTargetType, object? codeBehind)
    {
        var eventName = GetRequiredAttributeValue(element, "Event");
        var handlerName = GetRequiredAttributeValue(element, "Handler");
        var handledEventsTooText = GetOptionalAttributeValue(element, "HandledEventsToo");
        var handledEventsToo = false;
        if (!string.IsNullOrWhiteSpace(handledEventsTooText) &&
            !bool.TryParse(handledEventsTooText, out handledEventsToo))
        {
            throw CreateXamlException(
                $"EventSetter HandledEventsToo value '{handledEventsTooText}' is not valid. Use 'True' or 'False'.",
                element);
        }

        if (codeBehind == null)
        {
            throw CreateXamlException(
                $"EventSetter for '{eventName}' requires a code-behind instance to resolve handler '{handlerName}'.",
                element);
        }

        var routedEvent = EventTrigger.ResolveRoutedEvent(styleTargetType, eventName);
        if (routedEvent == null)
        {
            throw CreateXamlException(
                $"RoutedEvent '{eventName}' could not be resolved on '{styleTargetType.Name}'.",
                element);
        }

        var method = ResolveCodeBehindHandlerMethod(codeBehind, handlerName);
        if (!IsSupportedEventSetterHandlerSignature(method, styleTargetType))
        {
            throw CreateXamlException(
                $"EventSetter handler '{handlerName}' is not supported. Use 0, 1, or 2 parameters compatible with sender/args.",
                element);
        }

        var parameters = XamlTypeResolver.GetMethodParameters(method);
        var delegateType = parameters.Length switch
        {
            0 => typeof(Action),
            1 => typeof(Action<>).MakeGenericType(parameters[0].ParameterType),
            2 => typeof(Action<,>).MakeGenericType(parameters[0].ParameterType, parameters[1].ParameterType),
            _ => throw CreateXamlException(
                $"EventSetter handler '{handlerName}' is not supported. Use 0, 1, or 2 parameters compatible with sender/args.",
                element)
        };

        Delegate handler;
        try
        {
            handler = Delegate.CreateDelegate(delegateType, codeBehind, method);
        }
        catch (ArgumentException ex)
        {
            throw CreateXamlException(
                $"Handler method '{handlerName}' on code-behind type '{codeBehind.GetType().Name}' could not be bound to a supported EventSetter delegate shape.",
                element,
                ex);
        }

        return new EventSetter(eventName, handler, handledEventsToo);
    }


    private static object BuildSetterValue(
        XElement setterElement,
        Type styleTargetType,
        DependencyProperty dependencyProperty,
        FrameworkElement? resourceScope)
    {
        var valueText = GetOptionalAttributeValue(setterElement, nameof(Setter.Value));
        var valueElements = setterElement.Elements()
            .Where(child => string.Equals(child.Name.LocalName, "Setter.Value", StringComparison.Ordinal))
            .ToList();

        if (!string.IsNullOrWhiteSpace(valueText) && valueElements.Count > 0)
        {
            throw CreateXamlException("Setter cannot define both Value attribute and Setter.Value property element.", setterElement);
        }

        if (valueElements.Count > 1)
        {
            throw CreateXamlException("Setter can contain at most one Setter.Value property element.", setterElement);
        }

        var unexpectedChild = setterElement.Elements()
            .FirstOrDefault(child => !string.Equals(child.Name.LocalName, "Setter.Value", StringComparison.Ordinal));
        if (unexpectedChild != null)
        {
            throw CreateXamlException("Setter only supports Setter.Value child elements.", unexpectedChild);
        }

        if (!string.IsNullOrWhiteSpace(valueText))
        {
            return ConvertSetterTextValue(
                valueText,
                styleTargetType,
                dependencyProperty,
                resourceScope,
                setterElement);
        }

        if (valueElements.Count == 1)
        {
            var valueElement = valueElements[0];
            var contentCardinality = GetChildElementCardinality(valueElement, out var contentElement);
            if (contentCardinality == ChildElementCardinality.Multiple)
            {
                throw CreateXamlException("Setter.Value must contain either one object element or text content.", valueElement);
            }

            if (contentCardinality == ChildElementCardinality.Single && contentElement != null)
            {
                var built = BuildObject(contentElement, codeBehind: null, resourceScope);
                return CoerceResolvedResourceValue(
                    built,
                    dependencyProperty.PropertyType,
                    $"Setter for {styleTargetType.Name}.{dependencyProperty.Name}");
            }

            var rawText = (valueElement.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                throw CreateXamlException("Setter.Value requires either text content or a single child object element.", valueElement);
            }

            return ConvertSetterTextValue(
                rawText,
                styleTargetType,
                dependencyProperty,
                resourceScope,
                valueElement);
        }

        throw CreateXamlException(
            $"Element '{setterElement.Name.LocalName}' requires attribute '{nameof(Setter.Value)}' or a Setter.Value property element.",
            setterElement);
    }


    private static object ConvertSetterTextValue(
        string rawValueText,
        Type styleTargetType,
        DependencyProperty dependencyProperty,
        FrameworkElement? resourceScope,
        XObject location)
    {
        if (TryParseDynamicResourceKey(rawValueText, out _))
        {
            return new DynamicResourceReferenceExpression(
                GetMarkupExtensionRequiredValue(rawValueText, "DynamicResource", "resource key"));
        }

        if (TryParseStaticResourceKey(rawValueText, out var staticResourceKey))
        {
            var resolved = ResolveStaticResourceValue(staticResourceKey, resourceScope);
            return CoerceResolvedResourceValue(
                resolved,
                dependencyProperty.PropertyType,
                $"Setter for {styleTargetType.Name}.{dependencyProperty.Name}");
        }

        var converted = ConvertAssignableTextValue(
            rawValueText,
            dependencyProperty.PropertyType,
            resourceScope,
            location,
            $"Setter for {styleTargetType.Name}.{dependencyProperty.Name}");
        if (converted is DeferredXReference deferredReference)
        {
            throw CreateXamlException(
                $"Setter for {styleTargetType.Name}.{dependencyProperty.Name} could not resolve x:Reference target '{deferredReference.Name}'.",
                location);
        }

        return converted;
    }


    private static DependencyProperty ResolveSetterProperty(
        Type styleTargetType,
        string propertyText,
        string? targetName = null,
        Func<string, Type?>? setterTargetTypeResolver = null)
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

        if (!string.IsNullOrWhiteSpace(targetName) && setterTargetTypeResolver != null)
        {
            var targetType = setterTargetTypeResolver(targetName);
            if (targetType == null)
            {
                throw new InvalidOperationException(
                    $"Template target '{targetName}' was not found while resolving dependency property '{propertyText}'.");
            }

            return ResolveDependencyProperty(targetType, propertyText)
                   ?? throw new InvalidOperationException(
                       $"Dependency property '{propertyText}' was not found on template target '{targetType.Name}'.");
        }

        return ResolveDependencyProperty(styleTargetType, propertyText)
               ?? throw new InvalidOperationException(
                   $"Dependency property '{propertyText}' was not found on '{styleTargetType.Name}'.");
    }


}
