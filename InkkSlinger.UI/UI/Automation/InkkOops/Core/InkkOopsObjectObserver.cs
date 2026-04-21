using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace InkkSlinger;

public abstract class InkkOopsObjectObserver
{
    protected InkkOopsObjectObserver(string targetName, string? dumpFileName = null)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            throw new ArgumentException("Target name is required.", nameof(targetName));
        }

        TargetName = targetName;
        DumpFileName = string.IsNullOrWhiteSpace(dumpFileName)
            ? SanitizeFileStem(targetName) + "ObserverDump.txt"
            : dumpFileName;
    }

    public string TargetName { get; }

    public string DumpFileName { get; }

    public virtual int Order => 0;

    internal string CaptureLine(InkkOopsObjectObserverContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new InkkOopsObjectObserverDumpBuilder();
        var report = InkkOopsTargetResolver.Resolve(context.Session.Host, new InkkOopsTargetReference(TargetName));
        if (report.Status != InkkOopsTargetResolutionStatus.Resolved || report.Element == null)
        {
            builder.Add("status", report.Status.ToString().ToLowerInvariant());
            return builder.BuildLine(context.ActionIndex, TargetName, context.ActionDescription);
        }

        builder.Add("status", "resolved");
        Observe(context, report.Element, builder);
        return builder.BuildLine(context.ActionIndex, TargetName, context.ActionDescription);
    }

    protected abstract void Observe(InkkOopsObjectObserverContext context, UIElement element, InkkOopsObjectObserverDumpBuilder builder);

    private static string SanitizeFileStem(string targetName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(targetName.Length);
        for (var i = 0; i < targetName.Length; i++)
        {
            var character = targetName[i];
            builder.Append(Array.IndexOf(invalidCharacters, character) >= 0 ? '_' : character);
        }

        return builder.Length == 0 ? "ObservedObject" : builder.ToString();
    }
}

public sealed class InkkOopsObjectObserverContext
{
    public required InkkOopsSession Session { get; init; }

    public required int ActionIndex { get; init; }

    public required string ActionDescription { get; init; }
}

public sealed class InkkOopsObjectObserverDumpBuilder
{
    private readonly List<KeyValuePair<string, string>> _facts = new();

    public void Add(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Fact name is required.", nameof(name));
        }

        _facts.Add(new KeyValuePair<string, string>(name, Quote(value ?? string.Empty)));
    }

    public void Add(string name, bool value)
    {
        AddRaw(name, value ? "true" : "false");
    }

    public void Add(string name, int value)
    {
        AddRaw(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public void Add(string name, float value)
    {
        if (float.IsNaN(value))
        {
            AddRaw(name, "NaN");
            return;
        }

        if (float.IsPositiveInfinity(value))
        {
            AddRaw(name, "+Infinity");
            return;
        }

        if (float.IsNegativeInfinity(value))
        {
            AddRaw(name, "-Infinity");
            return;
        }

        AddRaw(name, value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    public void Add(string name, double value)
    {
        if (double.IsNaN(value))
        {
            AddRaw(name, "NaN");
            return;
        }

        if (double.IsPositiveInfinity(value))
        {
            AddRaw(name, "+Infinity");
            return;
        }

        if (double.IsNegativeInfinity(value))
        {
            AddRaw(name, "-Infinity");
            return;
        }

        AddRaw(name, value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    internal string BuildLine(int actionIndex, string targetName, string actionDescription)
    {
        var builder = new StringBuilder();
        builder.Append("action[")
            .Append(actionIndex)
            .Append("] ")
            .Append(targetName);
        if (!string.IsNullOrWhiteSpace(actionDescription))
        {
            builder.Append(" action_description=")
                .Append(Quote(actionDescription));
        }

        for (var i = 0; i < _facts.Count; i++)
        {
            var fact = _facts[i];
            builder.Append(' ')
                .Append(fact.Key)
                .Append('=')
                .Append(fact.Value);
        }

        return builder.ToString();
    }

    private void AddRaw(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Fact name is required.", nameof(name));
        }

        _facts.Add(new KeyValuePair<string, string>(name, value));
    }

    private static string Quote(string value)
    {
        return string.Concat('"', value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal), '"');
    }
}