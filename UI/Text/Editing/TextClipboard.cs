using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace InkkSlinger;

public static class TextClipboard
{
    private static string _text = string.Empty;
    private static readonly Dictionary<string, object?> DataByFormat = new(StringComparer.Ordinal);
    private static long _lastExternalSyncTicks;
    private static readonly long ExternalSyncIntervalTicks = Stopwatch.Frequency / 5;
    private static long _syncCallCount;
    private static long _syncThrottleSkipCount;
    private static long _syncExternalReadCount;
    private static double _syncExternalReadMsTotal;
    private static double _lastSyncMs;
    private static string _lastSyncSource = "None";
    private static bool _lastSyncChanged;
    private static bool _lastSyncThrottled;

    public static System.Func<string?>? GetTextOverride { get; set; }

    public static System.Action<string>? SetTextOverride { get; set; }

    public static bool TryGetText(out string text)
    {
        SyncFromExternalClipboard();
        text = _text;
        return text.Length > 0;
    }

    public static void SetText(string text)
    {
        text ??= string.Empty;
        _text = text;
        DataByFormat.Clear();

        if (SetTextOverride != null)
        {
            SetTextOverride(text);
        }
        else
        {
            TryWriteSystemClipboardText(text);
        }
    }

    public static void SetData(string format, object? value)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException("Clipboard format is required.", nameof(format));
        }

        DataByFormat[format] = value;
    }

    public static bool TryGetData<T>(string format, out T? value)
    {
        value = default;
        SyncFromExternalClipboard();
        if (string.IsNullOrWhiteSpace(format) || !DataByFormat.TryGetValue(format, out var boxed))
        {
            return false;
        }

        if (boxed is T typed)
        {
            value = typed;
            return true;
        }

        return false;
    }

    public static void ResetForTests()
    {
        _text = string.Empty;
        DataByFormat.Clear();
        GetTextOverride = null;
        SetTextOverride = null;
        _lastExternalSyncTicks = 0;
        _syncCallCount = 0;
        _syncThrottleSkipCount = 0;
        _syncExternalReadCount = 0;
        _syncExternalReadMsTotal = 0d;
        _lastSyncMs = 0d;
        _lastSyncSource = "None";
        _lastSyncChanged = false;
        _lastSyncThrottled = false;
    }

    public static TextClipboardDiagnosticsSnapshot GetDiagnosticsSnapshot()
    {
        return new TextClipboardDiagnosticsSnapshot(
            _syncCallCount,
            _syncThrottleSkipCount,
            _syncExternalReadCount,
            _syncExternalReadMsTotal,
            _lastSyncMs,
            _lastSyncSource,
            _lastSyncChanged,
            _lastSyncThrottled);
    }

    public static TextClipboardReadSnapshot CaptureSnapshot()
    {
        SyncFromExternalClipboard();
        return new TextClipboardReadSnapshot(_text, new Dictionary<string, object?>(DataByFormat, StringComparer.Ordinal));
    }

    private static void SyncFromExternalClipboard()
    {
        _syncCallCount++;
        _lastSyncChanged = false;
        _lastSyncThrottled = false;
        var now = Stopwatch.GetTimestamp();
        if (now - _lastExternalSyncTicks < ExternalSyncIntervalTicks)
        {
            _syncThrottleSkipCount++;
            _lastSyncThrottled = true;
            _lastSyncSource = "ThrottleSkip";
            _lastSyncMs = 0d;
            return;
        }

        _lastExternalSyncTicks = now;
        if (!TryReadExternalClipboardText(out var externalText, out var source, out var elapsedMs))
        {
            _lastSyncSource = source;
            _lastSyncMs = elapsedMs;
            return;
        }

        _syncExternalReadCount++;
        _syncExternalReadMsTotal += elapsedMs;
        _lastSyncSource = source;
        _lastSyncMs = elapsedMs;
        externalText ??= string.Empty;
        if (string.Equals(_text, externalText, StringComparison.Ordinal))
        {
            return;
        }

        _text = externalText;
        DataByFormat.Clear();
        _lastSyncChanged = true;
    }

    private static bool TryReadExternalClipboardText(out string? text, out string source, out double elapsedMs)
    {
        var readStart = Stopwatch.GetTimestamp();
        text = null;
        source = "None";
        elapsedMs = 0d;
        if (GetTextOverride != null)
        {
            text = GetTextOverride();
            source = "Override";
            elapsedMs = Stopwatch.GetElapsedTime(readStart).TotalMilliseconds;
            return true;
        }

        if (!OperatingSystem.IsWindows() || IsTestHostProcess())
        {
            source = "Unsupported";
            elapsedMs = Stopwatch.GetElapsedTime(readStart).TotalMilliseconds;
            return false;
        }

        if (TryReadSystemClipboardTextFast(out text))
        {
            source = "SystemNative";
            elapsedMs = Stopwatch.GetElapsedTime(readStart).TotalMilliseconds;
            return true;
        }

        source = "SystemPowerShellFallback";
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -NonInteractive -Command \"try { $v = Get-Clipboard -Raw -TextFormatType Text -ErrorAction Stop; if ($null -ne $v) { [Console]::Write($v) } } catch { }\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            if (!process.Start())
            {
                source = "SystemStartFailed";
                elapsedMs = Stopwatch.GetElapsedTime(readStart).TotalMilliseconds;
                return false;
            }

            text = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1500);
            source = "SystemPowerShell";
            elapsedMs = Stopwatch.GetElapsedTime(readStart).TotalMilliseconds;
            return process.ExitCode == 0;
        }
        catch
        {
            source = "SystemPowerShellException";
            elapsedMs = Stopwatch.GetElapsedTime(readStart).TotalMilliseconds;
            return false;
        }
    }

    private static bool TryReadSystemClipboardTextFast(out string? text)
    {
        text = null;
        const uint cfUnicodeText = 13;
        if (!IsClipboardFormatAvailable(cfUnicodeText))
        {
            return false;
        }

        // Clipboard can be temporarily locked by another process; retry briefly.
        var opened = false;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                opened = true;
                break;
            }

            System.Threading.Thread.Sleep(2);
        }

        if (!opened)
        {
            return false;
        }

        try
        {
            var handle = GetClipboardData(cfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                text = Marshal.PtrToStringUni(ptr);
                return true;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void TryWriteSystemClipboardText(string text)
    {
        if (!OperatingSystem.IsWindows() || IsTestHostProcess())
        {
            return;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "clip.exe",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            if (!process.Start())
            {
                return;
            }

            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit(1500);
        }
        catch
        {
            // Best effort only; app-local clipboard still works.
        }
    }

    private static bool IsTestHostProcess()
    {
        try
        {
            var name = Process.GetCurrentProcess().ProcessName;
            return name.Contains("testhost", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}

public readonly record struct TextClipboardDiagnosticsSnapshot(
    long SyncCallCount,
    long SyncThrottleSkipCount,
    long SyncExternalReadCount,
    double SyncExternalReadMsTotal,
    double LastSyncMs,
    string LastSyncSource,
    bool LastSyncChanged,
    bool LastSyncThrottled);

public readonly record struct TextClipboardReadSnapshot(
    string Text,
    IReadOnlyDictionary<string, object?> DataByFormat)
{
    public bool TryGetText(out string text)
    {
        text = Text ?? string.Empty;
        return text.Length > 0;
    }

    public bool TryGetData<T>(string format, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(format) || !DataByFormat.TryGetValue(format, out var boxed))
        {
            return false;
        }

        if (boxed is T typed)
        {
            value = typed;
            return true;
        }

        return false;
    }
}
