using System;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal static class CanvasThumbInvestigationLog
{
    private const string EnableEnvironmentVariable = "INKKSLINGER_CANVAS_THUMB_DIAGNOSTICS";
    private const string LogRelativePath = "artifacts\\diagnostics\\canvas-thumb-catalog-investigation.log";
    private static readonly object Sync = new();
    private static bool? _isEnabled;
    private static bool _isInitialized;

    internal static bool IsEnabled => _isEnabled ??= IsEnabledFromEnvironment();

    internal static string LogPath => Path.Combine(Environment.CurrentDirectory, LogRelativePath);

    internal static void ResetForTests()
    {
        lock (Sync)
        {
            _isEnabled = null;
            _isInitialized = false;
            if (File.Exists(LogPath))
            {
                File.Delete(LogPath);
            }
        }
    }

    internal static bool ShouldTrace(UIElement? visualRoot, Vector2 pointerPosition, params UIElement?[] elements)
    {
        if (!IsEnabled)
        {
            return false;
        }

        for (var i = 0; i < elements.Length; i++)
        {
            if (IsRelevantElement(elements[i]))
            {
                return true;
            }
        }

        if (TryFindNamedElement<Thumb>(visualRoot, "CanvasSceneDragThumb", out var thumb) &&
            thumb != null &&
            thumb.TryGetRenderBoundsInRootSpace(out var bounds) &&
            ContainsExpanded(bounds, pointerPosition, 24f))
        {
            return true;
        }

        return false;
    }

    internal static void Write(string category, string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Sync)
        {
            EnsureInitialized();
            using var writer = new StreamWriter(LogPath, append: true, Encoding.UTF8);
            writer.Write(DateTime.UtcNow.ToString("O"));
            writer.Write(" | ");
            writer.Write(category);
            writer.Write(" | ");
            writer.WriteLine(message);
        }
    }

    internal static string DescribeElement(UIElement? element)
    {
        if (element == null)
        {
            return "null";
        }

        var typeName = element.GetType().Name;
        var name = element is FrameworkElement frameworkElement && !string.IsNullOrWhiteSpace(frameworkElement.Name)
            ? frameworkElement.Name
            : string.Empty;
        var slot = element.TryGetRenderBoundsInRootSpace(out var bounds)
            ? $"bounds=({bounds.X:0.##},{bounds.Y:0.##},{bounds.Width:0.##},{bounds.Height:0.##})"
            : "bounds=unavailable";
        var zIndex = element is FrameworkElement ? $" z={Panel.GetZIndex(element)}" : string.Empty;
        return string.IsNullOrEmpty(name)
            ? $"{typeName} {slot}{zIndex}"
            : $"{typeName}#{name} {slot}{zIndex}";
    }

    internal static string DescribePointer(Vector2 pointerPosition)
    {
        return $"({pointerPosition.X:0.##},{pointerPosition.Y:0.##})";
    }

    internal static string DescribeHitTestMetrics(HitTestMetrics? metrics)
    {
        if (metrics is not HitTestMetrics value)
        {
            return "none";
        }

        return $"nodes={value.NodesVisited}; depth={value.MaxDepth}; ms={value.TotalMilliseconds:0.###}; hottestType={value.HottestTypeSummary}; hottestNode={value.HottestNodeSummary}; traversal={value.TraversalSummary}; rejects={value.RejectSummary}";
    }

    private static bool IsEnabledFromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(EnableEnvironmentVariable);
        return string.Equals(value, "1", StringComparison.Ordinal) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            LogPath,
            $"scenario=Controls Catalog Canvas Thumb directional-entry investigation{Environment.NewLine}" +
            $"timestamp_utc={DateTime.UtcNow:O}{Environment.NewLine}" +
            $"process_id={Environment.ProcessId}{Environment.NewLine}" +
            $"working_directory={Environment.CurrentDirectory}{Environment.NewLine}" +
            $"repro_steps=Open Controls Catalog -> select Canvas -> approach CanvasSceneDragThumb from left/top/right/bottom -> compare hover resolution and click-to-drag routing{Environment.NewLine}" +
            $"log_begin{Environment.NewLine}",
            Encoding.UTF8);

        _isInitialized = true;
    }

    private static bool IsRelevantElement(UIElement? element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is CanvasView)
            {
                return true;
            }

            if (current is FrameworkElement frameworkElement)
            {
                if (string.Equals(frameworkElement.Name, "CanvasSceneDragThumb", StringComparison.Ordinal) ||
                    string.Equals(frameworkElement.Name, "CanvasWorkbench", StringComparison.Ordinal) ||
                    string.Equals(frameworkElement.Name, "CanvasSceneRootCard", StringComparison.Ordinal) ||
                    string.Equals(frameworkElement.Name, "CanvasSceneBadge", StringComparison.Ordinal) ||
                    string.Equals(frameworkElement.Name, "PreviewHost", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindNamedElement<TElement>(UIElement? visualRoot, string name, out TElement? element)
        where TElement : UIElement
    {
        if (visualRoot is FrameworkElement frameworkRoot && frameworkRoot.FindName(name) is TElement found)
        {
            element = found;
            return true;
        }

        element = null;
        return false;
    }

    private static bool ContainsExpanded(LayoutRect rect, Vector2 point, float padding)
    {
        return point.X >= rect.X - padding &&
               point.X <= rect.X + rect.Width + padding &&
               point.Y >= rect.Y - padding &&
               point.Y <= rect.Y + rect.Height + padding;
    }
}