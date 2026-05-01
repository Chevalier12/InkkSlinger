using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace InkkSlinger;

internal static class InkkOopsActionLogFormatter
{
    public static IReadOnlyList<InkkOopsActionLogEntry> CreatePlannedEntries(IInkkOopsCommand command, int index, string commandDescription, string displayedFps)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(commandDescription);

        if (IsRuntimeLogged(command))
        {
            return [];
        }

        return [CreatePlannedEntry(command, index, commandDescription, displayedFps)];
    }

    public static IReadOnlyList<InkkOopsActionLogEntry> CreatePointerMoveEntries(
        int index,
        Vector2 position,
        UIElement? hoveredBefore,
        UIElement? hoveredAfter,
        string displayedFps,
        string pointerTelemetry)
    {
        var entries = new List<InkkOopsActionLogEntry>();
        var metadata = FormatPointerMoveMetadata(position, pointerTelemetry);
        if (!ReferenceEquals(hoveredBefore, hoveredAfter) && hoveredBefore != null)
        {
            entries.Add(new InkkOopsActionLogEntry(
                DescribeElementSubject(hoveredBefore, "viewport"),
                FormatAction(index, "pointer exit", displayedFps, metadata)));
        }

        if (!ReferenceEquals(hoveredBefore, hoveredAfter) && hoveredAfter != null)
        {
            entries.Add(new InkkOopsActionLogEntry(
                DescribeElementSubject(hoveredAfter, "viewport"),
                FormatAction(index, "pointer enter", displayedFps, metadata)));
        }

        entries.Add(new InkkOopsActionLogEntry(
            DescribeElementSubject(hoveredAfter, "viewport"),
            FormatAction(index, "pointer over", displayedFps, metadata)));

        return entries;
    }

    public static InkkOopsActionLogEntry CreatePointerDownEntry(
        int index,
        Vector2 position,
        UIElement? hoveredBefore,
        UIElement? capturedBefore,
        UIElement? hoveredAfter,
        UIElement? capturedAfter,
        UIElement? clickDown,
        string displayedFps)
    {
        var subject = clickDown ?? capturedAfter ?? hoveredAfter ?? capturedBefore ?? hoveredBefore;
        return new InkkOopsActionLogEntry(
            DescribeElementSubject(subject, "viewport"),
            FormatAction(index, "pointer down", displayedFps, $"at={FormatPoint(position)}"));
    }

    public static InkkOopsActionLogEntry CreatePointerUpEntry(
        int index,
        Vector2 position,
        UIElement? hoveredBefore,
        UIElement? capturedBefore,
        UIElement? hoveredAfter,
        UIElement? clickUp,
        string displayedFps)
    {
        var subject = clickUp ?? capturedBefore ?? hoveredAfter ?? hoveredBefore;
        return new InkkOopsActionLogEntry(
            DescribeElementSubject(subject, "viewport"),
            FormatAction(index, "pointer up", displayedFps, $"at={FormatPoint(position)}"));
    }

    public static InkkOopsActionLogEntry CreateWheelEntry(
        int index,
        int delta,
        UIElement? hoveredBefore,
        UIElement? capturedBefore,
        UIElement? hoveredAfter,
        UIElement? capturedAfter,
        string displayedFps)
    {
        var subject = capturedAfter ?? capturedBefore ?? hoveredAfter ?? hoveredBefore;
        var action = delta switch
        {
            > 0 => "wheel up",
            < 0 => "wheel down",
            _ => "wheel"
        };

        return new InkkOopsActionLogEntry(
            DescribeElementSubject(subject, "viewport"),
            FormatAction(index, action, displayedFps, $"delta={delta}"));
    }

    private static bool IsRuntimeLogged(IInkkOopsCommand command)
    {
        return command is InkkOopsHoverTargetCommand or
               InkkOopsMovePointerCommand or
               InkkOopsClickTargetCommand or
               InkkOopsPointerDownCommand or
               InkkOopsPointerUpCommand or
               InkkOopsWheelCommand or
               InkkOopsDragTargetCommand;
    }

    private static InkkOopsActionLogEntry CreatePlannedEntry(IInkkOopsCommand command, int index, string commandDescription, string displayedFps)
    {
        return command switch
        {
            InkkOopsInvokeTargetCommand invoke => new InkkOopsActionLogEntry(invoke.Target.ToString(), FormatAction(index, "invoke", displayedFps)),
            InkkOopsScrollByCommand scrollBy => new InkkOopsActionLogEntry(scrollBy.Target.ToString(), FormatAction(index, "scroll by", displayedFps, $"horizontal={FormatNumber(scrollBy.HorizontalPercentDelta)} vertical={FormatNumber(scrollBy.VerticalPercentDelta)}")),
            InkkOopsScrollToCommand scrollTo => new InkkOopsActionLogEntry(scrollTo.Target.ToString(), FormatAction(index, "scroll to", displayedFps, $"horizontal={FormatNumber(scrollTo.HorizontalPercent)} vertical={FormatNumber(scrollTo.VerticalPercent)}")),
            InkkOopsScrollIntoViewCommand scrollIntoView => new InkkOopsActionLogEntry(scrollIntoView.OwnerTarget.ToString(), FormatAction(index, "scroll into view", displayedFps, $"locator={scrollIntoView.Locator} padding={FormatNumber(scrollIntoView.Padding)}")),
            InkkOopsResizeWindowCommand resize => new InkkOopsActionLogEntry("window", FormatAction(index, "resize", displayedFps, $"width={resize.Width} height={resize.Height}")),
            InkkOopsMaximizeWindowCommand => new InkkOopsActionLogEntry("window", FormatAction(index, "maximize", displayedFps)),
            InkkOopsWaitFramesCommand waitFrames => new InkkOopsActionLogEntry("system", FormatAction(index, "wait frames", displayedFps, $"count={waitFrames.FrameCount}")),
            InkkOopsWaitForIdleCommand waitForIdle => new InkkOopsActionLogEntry("system", FormatAction(index, "wait for idle", displayedFps, $"policy={waitForIdle.Policy}")),
            InkkOopsWaitForElementCommand waitForElement => new InkkOopsActionLogEntry(waitForElement.Target.ToString(), FormatAction(index, $"wait for {waitForElement.Condition.ToString().ToLowerInvariant()}", displayedFps, $"maxFrames={waitForElement.MaxFrames} anchor={waitForElement.Anchor}")),
            InkkOopsSetClipboardTextCommand setClipboardText => new InkkOopsActionLogEntry("clipboard", FormatAction(index, "set clipboard text", displayedFps, $"length={setClipboardText.Text.Length}")),
            InkkOopsCaptureFrameCommand capture => new InkkOopsActionLogEntry("system", FormatAction(index, "capture frame", displayedFps, $"artifact={capture.ArtifactName}")),
            InkkOopsDumpTelemetryCommand dump => new InkkOopsActionLogEntry("system", FormatAction(index, "dump telemetry", displayedFps, $"artifact={dump.ArtifactName}")),
            InkkOopsAssertExistsCommand exists => new InkkOopsActionLogEntry(exists.Target.ToString(), FormatAction(index, "assert exists", displayedFps)),
            InkkOopsAssertNotExistsCommand notExists => new InkkOopsActionLogEntry(notExists.Target.ToString(), FormatAction(index, "assert not exists", displayedFps)),
            InkkOopsAssertPropertyCommand property => new InkkOopsActionLogEntry(property.Target.ToString(), FormatAction(index, "assert property", displayedFps, $"name={property.PropertyName} expected={InkkOopsCommandUtilities.FormatObject(property.ExpectedValue)}")),
            InkkOopsAssertAutomationEventCommand automationEvent => new InkkOopsActionLogEntry("automation", FormatAction(index, "assert automation event", displayedFps, $"event={automationEvent.EventType} target={automationEvent.TargetName} property={automationEvent.PropertyName}")),
            _ => new InkkOopsActionLogEntry("system", FormatAction(index, commandDescription, displayedFps))
        };
    }

    private static string DescribeElementSubject(UIElement? element, string fallback)
    {
        if (element == null)
        {
            return fallback;
        }

        if (element is FrameworkElement { Name.Length: > 0 } frameworkElement)
        {
            return $"{element.GetType().Name}#{frameworkElement.Name}";
        }

        var text = TryGetElementText(element);
        return string.IsNullOrWhiteSpace(text)
            ? element.GetType().Name
            : $"{element.GetType().Name}[{text}]";
    }

    private static string TryGetElementText(UIElement element)
    {
        var text = element switch
        {
            TextBlock textBlock => textBlock.Text,
            TextBox textBox => textBox.Text,
            DatePicker datePicker => datePicker.Text,
            ContentControl contentControl => Label.ExtractAutomationText(contentControl.Content),
            _ => string.Empty
        };

        return NormalizeSubjectText(text);
    }

    private static string NormalizeSubjectText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.Length <= 48
            ? normalized
            : normalized[..45] + "...";
    }

    private static string FormatAction(int index, string action, string displayedFps, string? metadata = null)
    {
        var fpsMetadata = $"fps={displayedFps}";
        return string.IsNullOrWhiteSpace(metadata)
            ? $"action[{index}] {action} {fpsMetadata}"
            : $"action[{index}] {action} {fpsMetadata} {metadata}";
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatPoint(Vector2 value)
    {
        return $"({FormatNumber(value.X)},{FormatNumber(value.Y)})";
    }

    private static string FormatPointerMoveMetadata(Vector2 position, string telemetry)
    {
        return string.IsNullOrWhiteSpace(telemetry)
            ? $"at={FormatPoint(position)} motion=none"
            : $"at={FormatPoint(position)} {telemetry}";
    }
}

internal readonly record struct InkkOopsActionLogEntry(string Subject, string Details);
