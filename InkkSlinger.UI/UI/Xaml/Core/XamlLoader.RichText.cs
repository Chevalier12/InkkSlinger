using System;
using System.Globalization;
using System.Xml.Linq;

namespace InkkSlinger;

public static partial class XamlLoader
{
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

        if (target is Paragraph paragraphWithTabs &&
            string.Equals(propertyName, nameof(Paragraph.Tabs), StringComparison.Ordinal))
        {
            ApplyTypedCollectionPropertyElement<TextTabProperties>(
                propertyElementName,
                propertyElement,
                codeBehind,
                resourceScope,
                item => paragraphWithTabs.Tabs.Add(item));
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
            var contentElement = GetSingleChildElementOrThrow(
                propertyElement,
                $"Property element '{propertyElementName}' must contain exactly one child element.",
                propertyElement);
            var built = BuildObject(contentElement, codeBehind, target as FrameworkElement ?? resourceScope);
            if (built is not FlowDocument document)
            {
                throw CreateXamlException(
                    $"Element '{contentElement.Name.LocalName}' is not valid inside property element '{propertyElementName}'. Expected '{nameof(FlowDocument)}'.",
                    contentElement);
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


}
