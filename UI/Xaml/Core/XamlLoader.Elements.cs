using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public static partial class XamlLoader
{
    private static UIElement BuildElement(XElement element, object? codeBehind, FrameworkElement? resourceScope)
    {
        var type = ResolveElementType(element.Name.LocalName);
        if (XamlObjectFactory.CreateInstance(type) is not UIElement uiElement)
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


    private enum ChildElementCardinality
    {
        None,
        Single,
        Multiple
    }


    private static ChildElementCardinality GetChildElementCardinality(XContainer container, out XElement? firstChild)
    {
        using var enumerator = container.Elements().GetEnumerator();
        if (!enumerator.MoveNext())
        {
            firstChild = null;
            return ChildElementCardinality.None;
        }

        firstChild = enumerator.Current;
        return enumerator.MoveNext()
            ? ChildElementCardinality.Multiple
            : ChildElementCardinality.Single;
    }


    private static XElement GetSingleChildElementOrThrow(XContainer container, string errorMessage, XObject location)
    {
        var cardinality = GetChildElementCardinality(container, out var child);
        if (cardinality != ChildElementCardinality.Single || child == null)
        {
            throw CreateXamlException(errorMessage, location);
        }

        return child;
    }


    private static bool HasChildElements(XContainer container)
    {
        using var enumerator = container.Elements().GetEnumerator();
        return enumerator.MoveNext();
    }


    private static string? GetSignificantTextContent(XContainer container)
    {
        List<string>? segments = null;
        foreach (var node in container.Nodes())
        {
            if (node is not XText textNode || string.IsNullOrWhiteSpace(textNode.Value))
            {
                continue;
            }

            segments ??= new List<string>();
            segments.Add(textNode.Value);
        }

        return segments == null ? null : string.Concat(segments);
    }


    private static void ApplyChildren(UIElement parent, XElement parentElement, object? codeBehind, FrameworkElement? resourceScope)
    {
        XElement? firstVisualChild = null;
        List<XElement>? additionalVisualChildren = null;
        var textContent = GetSignificantTextContent(parentElement);
        foreach (var node in parentElement.Nodes())
        {
            if (node is not XElement childElement)
            {
                continue;
            }

            if (TryApplyPropertyElement(parent, childElement, codeBehind, resourceScope))
            {
                continue;
            }

            if (firstVisualChild == null)
            {
                firstVisualChild = childElement;
            }
            else
            {
                additionalVisualChildren ??= new List<XElement>();
                additionalVisualChildren.Add(childElement);
            }
        }

        if (firstVisualChild == null)
        {
            if (textContent != null && parent is ContentControl contentTextHost)
            {
                contentTextHost.Content = textContent;
                return;
            }

            return;
        }

        if (parent is Panel panel)
        {
            if (textContent != null)
            {
                throw CreateXamlException(
                    $"Type '{parent.GetType().Name}' cannot host text content in XAML.",
                    parentElement);
            }

            panel.AddChild(BuildElement(firstVisualChild, codeBehind, panel));
            if (additionalVisualChildren == null)
            {
                return;
            }

            foreach (var childElement in additionalVisualChildren)
            {
                panel.AddChild(BuildElement(childElement, codeBehind, panel));
            }

            return;
        }

        if (parent is Border border)
        {
            if (textContent != null)
            {
                throw CreateXamlException("Border does not support implicit text content.", parentElement);
            }

            if (additionalVisualChildren != null)
            {
                throw CreateXamlException("Border supports a single child element.", parentElement);
            }

            border.Child = BuildElement(firstVisualChild, codeBehind, border);
            return;
        }

        if (parent is Decorator decorator)
        {
            if (textContent != null)
            {
                throw CreateXamlException("Decorator does not support implicit text content.", parentElement);
            }

            if (additionalVisualChildren != null)
            {
                throw CreateXamlException("Decorator supports a single child element.", parentElement);
            }

            decorator.Child = BuildElement(firstVisualChild, codeBehind, decorator);
            return;
        }

        if (parent is ContentControl contentControl)
        {
            if (additionalVisualChildren != null)
            {
                throw CreateXamlException("ContentControl supports a single content child.", parentElement);
            }

            if (textContent != null)
            {
                throw CreateXamlException("ContentControl cannot mix implicit text content with child elements.", parentElement);
            }

            contentControl.Content = BuildElement(firstVisualChild, codeBehind, contentControl);
            return;
        }

        if (textContent != null && parent is ContentControl textContentHost)
        {
            textContentHost.Content = textContent;
            return;
        }

        if (parent is ItemsControl itemsControl)
        {
            if (textContent != null)
            {
                throw CreateXamlException(
                    $"Type '{parent.GetType().Name}' cannot host implicit text items in XAML.",
                    parentElement);
            }

            itemsControl.Items.Add(BuildElement(firstVisualChild, codeBehind, itemsControl));
            if (additionalVisualChildren == null)
            {
                return;
            }

            foreach (var childElement in additionalVisualChildren)
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

        var property = XamlTypeResolver.GetWritableProperty(targetType, propertyName);

        if (property == null)
        {
            if (TryApplyAttachedPropertyElement(target, ownerType, propertyName, propertyElement, codeBehind, resourceScope))
            {
                return true;
            }

            throw CreateXamlException(
                $"Property element '{propertyElementName}' could not be resolved on '{targetType.Name}'.",
                propertyElement,
                code: XamlDiagnosticCode.UnknownProperty,
                propertyName: propertyName,
                elementName: targetType.Name,
                hint: $"Check whether '{propertyName}' exists on '{targetType.Name}'.");
        }

        if (TryApplyRichTextPropertyElement(target, propertyName, propertyElementName, propertyElement, codeBehind, resourceScope))
        {
            return true;
        }

        if (target is ResourceDictionary dictionary &&
            string.Equals(propertyName, nameof(ResourceDictionary.MergedDictionaries), StringComparison.Ordinal))
        {
            var listItemScope = target as FrameworkElement ?? resourceScope;
            foreach (var itemElement in propertyElement.Elements())
            {
                var item = BuildObject(itemElement, codeBehind, listItemScope);
                if (item is not ResourceDictionary mergedDictionary)
                {
                    throw CreateXamlException(
                        $"Property element '{propertyElementName}' can only contain ResourceDictionary elements.",
                        itemElement);
                }

                try
                {
                    dictionary.AddMergedDictionary(mergedDictionary);
                }
                catch (InvalidOperationException ex)
                {
                    throw CreateXamlException(
                        $"Failed to merge resource dictionary: {ex.Message}",
                        itemElement,
                        ex);
                }
            }

            return true;
        }

        var propertyValue = property.GetValue(target);
        if (propertyValue is ResourceDictionary resourceDictionary)
        {
            var resourceScopeForEntries = target as FrameworkElement ?? resourceScope;
            var resourceChildCardinality = GetChildElementCardinality(propertyElement, out var resourceChild);
            if (resourceChildCardinality == ChildElementCardinality.Single &&
                resourceChild != null &&
                string.Equals(resourceChild.Name.LocalName, nameof(ResourceDictionary), StringComparison.Ordinal) &&
                !HasExplicitXamlKey(resourceChild))
            {
                var nestedDictionary = BuildObject(resourceChild, codeBehind, resourceScopeForEntries) as ResourceDictionary;
                if (nestedDictionary == null)
                {
                    throw CreateXamlException(
                        $"Property element '{propertyElementName}' expected a ResourceDictionary value.",
                        resourceChild);
                }

                MergeResourceDictionaryContents(resourceDictionary, nestedDictionary);
                return true;
            }

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

        var childElement = GetSingleChildElementOrThrow(
            propertyElement,
            $"Property element '{propertyElementName}' must contain exactly one child element.",
            propertyElement);

        if (target is DependencyObject dependencyObject)
        {
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

                if (TryQueueDeferredBindingReferenceResolution(
                        dependencyObject,
                        dependencyProperty,
                        bindingBase,
                        bindingScope))
                {
                    return true;
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

        var converted = BuildObject(childElement, codeBehind, target as FrameworkElement ?? resourceScope);
        property.SetValue(target, converted);
        return true;
    }


    private static object BuildObject(XElement element, object? codeBehind, FrameworkElement? resourceScope)
    {
        if (string.Equals(element.Name.LocalName, nameof(Style), StringComparison.Ordinal))
        {
            return BuildStyle(element, resourceScope, codeBehind);
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

        if (string.Equals(element.Name.LocalName, nameof(Color), StringComparison.Ordinal))
        {
            return BuildColorObject(element);
        }

        if (string.Equals(element.Name.LocalName, nameof(ResourceDictionary), StringComparison.Ordinal))
        {
            var dictionary = new ResourceDictionary();
            ApplyAttributes(dictionary, element, codeBehind, resourceScope);

            foreach (var childElement in element.Elements())
            {
                if (TryApplyPropertyElement(dictionary, childElement, codeBehind, resourceScope))
                {
                    continue;
                }

                AddResourceEntry(dictionary, childElement, codeBehind, resourceScope);
            }

            return dictionary;
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


}
