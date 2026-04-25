using System;
using System.IO;
using System.Text;

namespace InkkSlinger;

internal static class RichTextBoxClipboardCodec
{
    internal static void PublishRichClipboardPayloads(string richSlice, string selectedText)
    {
        TextClipboard.SetData(FlowDocumentSerializer.ClipboardFormat, richSlice);
        TextClipboard.SetData(RichTextBox.ClipboardXamlFormat, richSlice);
        TextClipboard.SetData(RichTextBox.ClipboardXamlPackageFormat, richSlice);
        TextClipboard.SetData(RichTextBox.ClipboardRtfFormat, BuildRtfFromPlainText(selectedText));
    }

    internal static bool TryGetRichClipboardPayload(TextClipboardReadSnapshot snapshot, out string payload, out string format)
    {
        if (TryGetNonEmptyClipboardData(snapshot, RichTextBox.ClipboardXamlPackageFormat, out payload))
        {
            format = RichTextBox.ClipboardXamlPackageFormat;
            return true;
        }

        if (TryGetNonEmptyClipboardData(snapshot, RichTextBox.ClipboardXamlFormat, out payload))
        {
            format = RichTextBox.ClipboardXamlFormat;
            return true;
        }

        if (TryGetNonEmptyClipboardData(snapshot, FlowDocumentSerializer.ClipboardFormat, out payload))
        {
            format = FlowDocumentSerializer.ClipboardFormat;
            return true;
        }

        if (TryGetNonEmptyClipboardData(snapshot, RichTextBox.ClipboardRtfFormat, out payload))
        {
            format = RichTextBox.ClipboardRtfFormat;
            return true;
        }

        payload = string.Empty;
        format = string.Empty;
        return false;
    }

    internal static string SerializeSelectionPayload(FlowDocument document, int selectionStart, int selectionLength, string dataFormat)
    {
        var selectionEnd = selectionStart + selectionLength;
        return NormalizeDataFormat(dataFormat) switch
        {
            var format when format == RichTextBox.ClipboardXamlFormat || format == RichTextBox.ClipboardXamlPackageFormat || format == FlowDocumentSerializer.ClipboardFormat =>
                FlowDocumentSerializer.SerializeRange(document, selectionStart, selectionEnd),
            var format when format == RichTextBox.ClipboardRtfFormat => BuildRtfFromPlainText(DocumentEditing.GetText(document).Substring(selectionStart, selectionLength)),
            var format when format == RichTextBox.ClipboardTextFormat || format == RichTextBox.ClipboardUnicodeTextFormat => NormalizePlainText(DocumentEditing.GetText(document).Substring(selectionStart, selectionLength)),
            _ => throw new NotSupportedException($"RichTextBox does not support saving format '{dataFormat}'.")
        };
    }

    internal static string SerializeDocumentPayload(FlowDocument document, string dataFormat)
    {
        return NormalizeDataFormat(dataFormat) switch
        {
            var format when format == RichTextBox.ClipboardXamlFormat || format == RichTextBox.ClipboardXamlPackageFormat || format == FlowDocumentSerializer.ClipboardFormat => FlowDocumentSerializer.Serialize(document),
            var format when format == RichTextBox.ClipboardRtfFormat => BuildRtfFromPlainText(DocumentEditing.GetText(document)),
            var format when format == RichTextBox.ClipboardTextFormat || format == RichTextBox.ClipboardUnicodeTextFormat => NormalizePlainText(DocumentEditing.GetText(document)),
            _ => throw new NotSupportedException($"RichTextBox does not support saving format '{dataFormat}'.")
        };
    }

    internal static FlowDocument DeserializeDocumentPayload(string payload, string dataFormat)
    {
        if (IsRichFragmentFormat(dataFormat))
        {
            return DeserializeFragmentPayload(payload, dataFormat);
        }

        var text = DeserializeTextPayload(payload, dataFormat);
        return CreateDocumentFromPlainText(text);
    }

    internal static FlowDocument DeserializeFragmentPayload(string payload, string dataFormat)
    {
        return NormalizeDataFormat(dataFormat) switch
        {
            var format when format == RichTextBox.ClipboardXamlFormat || format == RichTextBox.ClipboardXamlPackageFormat || format == FlowDocumentSerializer.ClipboardFormat => FlowDocumentSerializer.DeserializeFragment(payload),
            var format when format == RichTextBox.ClipboardRtfFormat => CreateDocumentFromPlainText(ParseRtfToPlainText(payload)),
            var format when format == RichTextBox.ClipboardTextFormat || format == RichTextBox.ClipboardUnicodeTextFormat => CreateDocumentFromPlainText(payload),
            _ => throw new NotSupportedException($"RichTextBox does not support loading format '{dataFormat}'.")
        };
    }

    internal static string DeserializeTextPayload(string payload, string dataFormat)
    {
        return NormalizeDataFormat(dataFormat) switch
        {
            var format when format == RichTextBox.ClipboardRtfFormat => ParseRtfToPlainText(payload),
            var format when format == RichTextBox.ClipboardTextFormat || format == RichTextBox.ClipboardUnicodeTextFormat => payload,
            var format when format == RichTextBox.ClipboardXamlFormat || format == RichTextBox.ClipboardXamlPackageFormat || format == FlowDocumentSerializer.ClipboardFormat => DocumentEditing.GetText(FlowDocumentSerializer.DeserializeFragment(payload)),
            _ => throw new NotSupportedException($"RichTextBox does not support loading format '{dataFormat}'.")
        };
    }

    internal static string ReadStreamPayload(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }

    internal static void WriteStreamPayload(Stream stream, string payload)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
            stream.SetLength(0);
        }

        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(payload);
        writer.Flush();
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
    }

    internal static bool IsSupportedDataFormat(string? dataFormat)
    {
        var normalized = NormalizeDataFormat(dataFormat);
        return normalized == FlowDocumentSerializer.ClipboardFormat ||
               normalized == RichTextBox.ClipboardXamlFormat ||
               normalized == RichTextBox.ClipboardXamlPackageFormat ||
               normalized == RichTextBox.ClipboardRtfFormat ||
               normalized == RichTextBox.ClipboardTextFormat ||
               normalized == RichTextBox.ClipboardUnicodeTextFormat;
    }

    internal static bool IsRichFragmentFormat(string? dataFormat)
    {
        var normalized = NormalizeDataFormat(dataFormat);
        return normalized == FlowDocumentSerializer.ClipboardFormat ||
               normalized == RichTextBox.ClipboardXamlFormat ||
               normalized == RichTextBox.ClipboardXamlPackageFormat ||
               normalized == RichTextBox.ClipboardRtfFormat;
    }

    private static bool TryGetNonEmptyClipboardData(TextClipboardReadSnapshot snapshot, string format, out string value)
    {
        if (snapshot.TryGetData<string>(format, out var payload) &&
            !string.IsNullOrWhiteSpace(payload))
        {
            value = payload;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string BuildRtfFromPlainText(string text)
    {
        var normalized = NormalizePlainText(text);
        var builder = new StringBuilder();
        builder.Append(@"{\rtf1\ansi ");
        for (var i = 0; i < normalized.Length; i++)
        {
            var ch = normalized[i];
            switch (ch)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '{':
                    builder.Append(@"\{");
                    break;
                case '}':
                    builder.Append(@"\}");
                    break;
                case '\n':
                    builder.Append(@"\par ");
                    break;
                default:
                    if (ch <= 0x7f)
                    {
                        builder.Append(ch);
                    }
                    else
                    {
                        builder.Append(@"\u");
                        builder.Append((short)ch);
                        builder.Append('?');
                    }

                    break;
            }
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string NormalizeDataFormat(string? dataFormat)
    {
        return string.IsNullOrWhiteSpace(dataFormat) ? string.Empty : dataFormat.Trim();
    }

    private static FlowDocument CreateDocumentFromPlainText(string text)
    {
        var document = new FlowDocument();
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run(string.Empty));
        document.Blocks.Add(paragraph);
        DocumentEditing.ReplaceAllText(document, NormalizePlainText(text));
        return document;
    }

    private static string ParseRtfToPlainText(string rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(rtf.Length);
        for (var i = 0; i < rtf.Length; i++)
        {
            var ch = rtf[i];
            if (ch == '{' || ch == '}')
            {
                continue;
            }

            if (ch != '\\')
            {
                builder.Append(ch);
                continue;
            }

            if (i + 1 >= rtf.Length)
            {
                break;
            }

            var next = rtf[++i];
            if (next is '\\' or '{' or '}')
            {
                builder.Append(next);
                continue;
            }

            if (next == '\'')
            {
                if (i + 2 < rtf.Length)
                {
                    var hex = rtf.Substring(i + 1, 2);
                    if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var value))
                    {
                        builder.Append((char)value);
                    }

                    i += 2;
                }

                continue;
            }

            if (!char.IsLetter(next))
            {
                if (next == '~')
                {
                    builder.Append(' ');
                }

                continue;
            }

            var keywordStart = i;
            while (i < rtf.Length && char.IsLetter(rtf[i]))
            {
                i++;
            }

            var keyword = rtf.Substring(keywordStart, i - keywordStart);
            var negative = false;
            if (i < rtf.Length && rtf[i] == '-')
            {
                negative = true;
                i++;
            }

            var numberStart = i;
            while (i < rtf.Length && char.IsDigit(rtf[i]))
            {
                i++;
            }

            int? parameter = null;
            if (i > numberStart && int.TryParse(rtf.Substring(numberStart, i - numberStart), out var parsed))
            {
                parameter = negative ? -parsed : parsed;
            }

            if (i >= rtf.Length || rtf[i] != ' ')
            {
                i--;
            }

            switch (keyword)
            {
                case "par":
                case "line":
                    builder.Append('\n');
                    break;
                case "tab":
                    builder.Append('\t');
                    break;
                case "u" when parameter.HasValue:
                    builder.Append((char)(short)parameter.Value);
                    if (i + 1 < rtf.Length)
                    {
                        i++;
                    }

                    break;
            }
        }

        return NormalizePlainText(builder.ToString());
    }

    private static string NormalizePlainText(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}