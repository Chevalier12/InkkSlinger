using System;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class PasswordBoxView : UserControl
{
    private static readonly string[] MaskCharacters =
    {
        "*",
        "#",
        "o",
        ".",
    };

    private static readonly TextWrapping[] WrappingModes =
    {
        TextWrapping.NoWrap,
        TextWrapping.Wrap,
        TextWrapping.WrapWithOverflow,
    };

    private static readonly ScrollBarVisibility[] ScrollBarModes =
    {
        ScrollBarVisibility.Auto,
        ScrollBarVisibility.Disabled,
        ScrollBarVisibility.Visible,
        ScrollBarVisibility.Hidden,
    };

    private static readonly int[] MaxLengthModes =
    {
        0,
        8,
        16,
        32,
    };

    private int _stylePaletteIndex;
    private int _inspectorEventCount;
    private string _inspectorLastChange = "Last change: none";

    public PasswordBoxView()
    {
        InitializeComponent();

        SetupBasicSection();
        SetupPolicySection();
        SetupLayoutSection();
        SetupStyleSection();
        SetupGallerySection();
        SetupInspectorSection();
    }

    private void SetupBasicSection()
    {
        if (FindElement<PasswordBox>("BasicPasswordBox") is not { } box)
        {
            return;
        }

        box.Password = "Nimbus-42";
        box.PasswordChanged += OnBasicPasswordChanged;

        AttachClick("BasicSeedButton", OnBasicSeedClick);
        AttachClick("BasicPhraseButton", OnBasicPhraseClick);
        AttachClick("BasicClearButton", OnBasicClearClick);

        UpdateBasicStatus();
    }

    private void SetupPolicySection()
    {
        if (FindElement<PasswordBox>("PolicyPasswordBox") is not { } box)
        {
            return;
        }

        box.Password = "Delta-Vector-2026";
        box.MaxLength = 16;
        box.PasswordChanged += OnPolicyPasswordChanged;
        box.DependencyPropertyChanged += OnPolicyPropertyChanged;

        AttachClick("PolicyToggleRevealButton", OnPolicyToggleRevealClick);
        AttachClick("PolicyToggleReadOnlyButton", OnPolicyToggleReadOnlyClick);
        AttachClick("PolicyToggleClipboardButton", OnPolicyToggleClipboardClick);
        AttachClick("PolicyCycleMaxLengthButton", OnPolicyCycleMaxLengthClick);
        AttachClick("PolicyOverflowButton", OnPolicyOverflowClick);

        UpdatePolicyStatus();
    }

    private void SetupLayoutSection()
    {
        if (FindElement<PasswordBox>("LayoutPasswordBox") is not { } box)
        {
            return;
        }

        box.Password = BuildMultilineSample();
        box.TextWrapping = TextWrapping.Wrap;
        box.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        box.PasswordChar = MaskCharacters[0];
        box.PasswordChanged += OnLayoutPasswordChanged;
        box.DependencyPropertyChanged += OnLayoutPropertyChanged;

        AttachClick("LayoutLoadMultilineButton", OnLayoutLoadMultilineClick);
        AttachClick("LayoutLoadSingleLineButton", OnLayoutLoadSingleLineClick);
        AttachClick("LayoutCycleWrappingButton", OnLayoutCycleWrappingClick);
        AttachClick("LayoutCycleHorizontalButton", OnLayoutCycleHorizontalClick);
        AttachClick("LayoutCycleVerticalButton", OnLayoutCycleVerticalClick);
        AttachClick("LayoutCycleMaskButton", OnLayoutCycleMaskClick);

        UpdateLayoutStatus();
    }

    private void SetupStyleSection()
    {
        if (FindElement<PasswordBox>("StyledPasswordBox") is not { } box)
        {
            return;
        }

        box.Password = "Aurora vault";
        box.PasswordChanged += OnStylePasswordChanged;
        box.DependencyPropertyChanged += OnStylePropertyChanged;

        AttachClick("StyleWarmButton", OnStyleWarmClick);
        AttachClick("StyleCoolButton", OnStyleCoolClick);
        AttachClick("StyleSlateButton", OnStyleSlateClick);
        AttachClick("StyleThicknessButton", OnStyleThicknessClick);
        AttachClick("StyleRevealButton", OnStyleRevealClick);

        ApplyStylePalette(box, 0);
        UpdateStyleStatus();
    }

    private void SetupGallerySection()
    {
        if (FindElement<PasswordBox>("DisabledPasswordBox") is { } disabled)
        {
            disabled.Password = "Disabled sample";
        }

        if (FindElement<PasswordBox>("ReadOnlyPasswordBox") is { } readOnly)
        {
            readOnly.Password = "Readonly sample";
            readOnly.IsReadOnly = true;
            readOnly.AllowClipboardCopy = true;
        }

        if (FindElement<PasswordBox>("RevealPasswordBox") is { } reveal)
        {
            reveal.Password = "Visible credential sample";
            reveal.RevealPassword = true;
            reveal.AllowClipboardCopy = true;
        }

        SetText(
            "GalleryStatusLabel",
            "Disabled suppresses input, readonly preserves selection/navigation, and reveal mode swaps rendering without changing the stored password.");
    }

    private void SetupInspectorSection()
    {
        if (FindElement<PasswordBox>("InspectorPasswordBox") is not { } box)
        {
            return;
        }

        box.Password = "observer-seed";
        box.TextWrapping = TextWrapping.Wrap;
        box.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        box.AllowClipboardCopy = true;
        box.PasswordChanged += OnInspectorPasswordChanged;
        box.DependencyPropertyChanged += OnInspectorPropertyChanged;

        AttachClick("InspectorSeedButton", OnInspectorSeedClick);
        AttachClick("InspectorAppendButton", OnInspectorAppendClick);
        AttachClick("InspectorClearButton", OnInspectorClearClick);
        AttachClick("InspectorRefreshMetricsButton", OnInspectorRefreshMetricsClick);
        AttachClick("InspectorResetMetricsButton", OnInspectorResetMetricsClick);

        UpdateInspectorStatus();
    }

    private void OnBasicPasswordChanged(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateBasicStatus();
    }

    private void OnPolicyPasswordChanged(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdatePolicyStatus();
    }

    private void OnPolicyPropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property == PasswordBox.PasswordProperty ||
            e.Property == PasswordBox.RevealPasswordProperty ||
            e.Property == PasswordBox.IsReadOnlyProperty ||
            e.Property == PasswordBox.AllowClipboardCopyProperty ||
            e.Property == PasswordBox.MaxLengthProperty)
        {
            UpdatePolicyStatus();
        }
    }

    private void OnLayoutPasswordChanged(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateLayoutStatus();
    }

    private void OnLayoutPropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property == PasswordBox.PasswordProperty ||
            e.Property == PasswordBox.TextWrappingProperty ||
            e.Property == PasswordBox.HorizontalScrollBarVisibilityProperty ||
            e.Property == PasswordBox.VerticalScrollBarVisibilityProperty ||
            e.Property == PasswordBox.PasswordCharProperty)
        {
            UpdateLayoutStatus();
        }
    }

    private void OnStylePasswordChanged(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateStyleStatus();
    }

    private void OnStylePropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property == PasswordBox.PasswordProperty ||
            e.Property == PasswordBox.RevealPasswordProperty ||
            e.Property == PasswordBox.BorderThicknessProperty)
        {
            UpdateStyleStatus();
        }
    }

    private void OnInspectorPasswordChanged(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        _inspectorEventCount++;
        _inspectorLastChange = $"Last change: stored password is now {FormatPasswordPreview(GetInspectorPasswordValue())}";
        UpdateInspectorStatus();
    }

    private void OnInspectorPropertyChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property == PasswordBox.PasswordProperty ||
            e.Property == PasswordBox.TextWrappingProperty ||
            e.Property == PasswordBox.HorizontalScrollBarVisibilityProperty ||
            e.Property == PasswordBox.VerticalScrollBarVisibilityProperty ||
            e.Property == PasswordBox.RevealPasswordProperty ||
            e.Property == PasswordBox.IsReadOnlyProperty ||
            e.Property == PasswordBox.AllowClipboardCopyProperty)
        {
            UpdateInspectorStatus();
        }
    }

    private void OnBasicSeedClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("BasicPasswordBox") is { } box)
        {
            box.Password = "Nimbus-42";
        }
    }

    private void OnBasicPhraseClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("BasicPasswordBox") is { } box)
        {
            box.Password = "correct horse battery staple";
        }
    }

    private void OnBasicClearClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("BasicPasswordBox") is { } box)
        {
            box.Password = string.Empty;
        }
    }

    private void OnPolicyToggleRevealClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("PolicyPasswordBox") is { } box)
        {
            box.RevealPassword = !box.RevealPassword;
        }
    }

    private void OnPolicyToggleReadOnlyClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("PolicyPasswordBox") is { } box)
        {
            box.IsReadOnly = !box.IsReadOnly;
        }
    }

    private void OnPolicyToggleClipboardClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("PolicyPasswordBox") is { } box)
        {
            box.AllowClipboardCopy = !box.AllowClipboardCopy;
        }
    }

    private void OnPolicyCycleMaxLengthClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("PolicyPasswordBox") is { } box)
        {
            box.MaxLength = CycleValue(box.MaxLength, MaxLengthModes);
        }
    }

    private void OnPolicyOverflowClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("PolicyPasswordBox") is { } box)
        {
            box.Password = "overflow-example-password-2026";
        }
    }

    private void OnLayoutLoadMultilineClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("LayoutPasswordBox") is { } box)
        {
            box.Password = BuildMultilineSample();
        }
    }

    private void OnLayoutLoadSingleLineClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("LayoutPasswordBox") is { } box)
        {
            box.Password = "api_token_prod_2026_rotates_weekly_keep_scrolling_until_you_hit_the_end";
        }
    }

    private void OnLayoutCycleWrappingClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("LayoutPasswordBox") is { } box)
        {
            box.TextWrapping = CycleValue(box.TextWrapping, WrappingModes);
        }
    }

    private void OnLayoutCycleHorizontalClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("LayoutPasswordBox") is { } box)
        {
            box.HorizontalScrollBarVisibility = CycleValue(box.HorizontalScrollBarVisibility, ScrollBarModes);
        }
    }

    private void OnLayoutCycleVerticalClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("LayoutPasswordBox") is { } box)
        {
            box.VerticalScrollBarVisibility = CycleValue(box.VerticalScrollBarVisibility, ScrollBarModes);
        }
    }

    private void OnLayoutCycleMaskClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("LayoutPasswordBox") is { } box)
        {
            box.PasswordChar = CycleValue(box.PasswordChar, MaskCharacters);
        }
    }

    private void OnStyleWarmClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("StyledPasswordBox") is { } box)
        {
            ApplyStylePalette(box, 0);
            UpdateStyleStatus();
        }
    }

    private void OnStyleCoolClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("StyledPasswordBox") is { } box)
        {
            ApplyStylePalette(box, 1);
            UpdateStyleStatus();
        }
    }

    private void OnStyleSlateClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("StyledPasswordBox") is { } box)
        {
            ApplyStylePalette(box, 2);
            UpdateStyleStatus();
        }
    }

    private void OnStyleThicknessClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("StyledPasswordBox") is { } box)
        {
            box.BorderThickness = box.BorderThickness >= 3f
                ? 1f
                : box.BorderThickness + 1f;
            UpdateStyleStatus();
        }
    }

    private void OnStyleRevealClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("StyledPasswordBox") is { } box)
        {
            box.RevealPassword = !box.RevealPassword;
            UpdateStyleStatus();
        }
    }

    private void OnInspectorSeedClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("InspectorPasswordBox") is { } box)
        {
            box.Password = "observer-seed";
            _inspectorLastChange = "Last change: seeded the inspector sample";
            UpdateInspectorStatus();
        }
    }

    private void OnInspectorAppendClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("InspectorPasswordBox") is { } box)
        {
            box.Password += "-suffix";
            _inspectorLastChange = "Last change: appended '-suffix' programmatically";
            UpdateInspectorStatus();
        }
    }

    private void OnInspectorClearClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("InspectorPasswordBox") is { } box)
        {
            box.Password = string.Empty;
            _inspectorLastChange = "Last change: cleared the inspector buffer";
            UpdateInspectorStatus();
        }
    }

    private void OnInspectorRefreshMetricsClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        _inspectorLastChange = "Last change: refreshed performance counters";
        UpdateInspectorStatus();
    }

    private void OnInspectorResetMetricsClick(object? sender, RoutedSimpleEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FindElement<PasswordBox>("InspectorPasswordBox") is { } box)
        {
            box.ResetPerformanceSnapshot();
            _inspectorLastChange = "Last change: reset PasswordBox performance counters";
            UpdateInspectorStatus();
        }
    }

    private void UpdateBasicStatus()
    {
        if (FindElement<PasswordBox>("BasicPasswordBox") is not { } box)
        {
            return;
        }

        SetText(
            "BasicStatusLabel",
            $"Password: {FormatPasswordPreview(box.Password)} | TextLength: {box.TextLength} | Caret: {box.CaretIndex} | Lines: {box.LogicalLineCount}");
    }

    private void UpdatePolicyStatus()
    {
        if (FindElement<PasswordBox>("PolicyPasswordBox") is not { } box)
        {
            return;
        }

        SetText(
            "PolicyStatusLabel",
            $"Reveal: {box.RevealPassword} | Readonly: {box.IsReadOnly} | Clipboard: {box.AllowClipboardCopy} | MaxLength: {box.MaxLength}");
        SetText(
            "PolicyValueLabel",
            $"Stored password: {FormatPasswordPreview(box.Password)} | Effective length: {box.TextLength}");
    }

    private void UpdateLayoutStatus()
    {
        if (FindElement<PasswordBox>("LayoutPasswordBox") is not { } box)
        {
            return;
        }

        SetText(
            "LayoutStatusLabel",
            $"Wrapping: {box.TextWrapping} | Horizontal: {box.HorizontalScrollBarVisibility} | Vertical: {box.VerticalScrollBarVisibility} | Mask: {box.PasswordChar}");
        SetText(
            "LayoutMetricsLabel",
            $"TextLength: {box.TextLength} | Logical lines: {box.LogicalLineCount} | Caret: {box.CaretIndex} | Preview: {FormatPasswordPreview(box.Password)}");
    }

    private void UpdateStyleStatus()
    {
        if (FindElement<PasswordBox>("StyledPasswordBox") is not { } box)
        {
            return;
        }

        SetText(
            "StyleStatusLabel",
            $"Palette: {GetStylePaletteName(_stylePaletteIndex)} | BorderThickness: {box.BorderThickness:0} | Reveal: {box.RevealPassword} | Preview: {FormatPasswordPreview(box.Password)}");
    }

    private void UpdateInspectorStatus()
    {
        if (FindElement<PasswordBox>("InspectorPasswordBox") is not { } box)
        {
            return;
        }

        var snapshot = box.GetPerformanceSnapshot();
        SetText("InspectorValueLabel", $"Stored password: {FormatPasswordPreview(box.Password)}");
        SetText("InspectorStateLabel", $"TextLength: {box.TextLength} | Caret: {box.CaretIndex} | Lines: {box.LogicalLineCount}");
        SetText("InspectorPolicyLabel", $"Reveal: {box.RevealPassword} | Readonly: {box.IsReadOnly} | Clipboard: {box.AllowClipboardCopy}");
        SetText("InspectorLayoutLabel", $"Wrapping: {box.TextWrapping} | Horizontal: {box.HorizontalScrollBarVisibility} | Vertical: {box.VerticalScrollBarVisibility}");
        SetText("InspectorEventCountLabel", $"PasswordChanged fired: {_inspectorEventCount}");
        SetText("InspectorLastChangeLabel", _inspectorLastChange);
        SetText(
            "InspectorPerfSummaryLabel",
            $"Commits: {snapshot.CommitCount} | Immediate sync: {snapshot.ImmediateSyncCount} | Deferred sync: {snapshot.DeferredSyncScheduledCount}/{snapshot.DeferredSyncFlushCount}");
        SetText(
            "InspectorLayoutPerfLabel",
            $"Layout cache hit/miss: {snapshot.LayoutCacheHitCount}/{snapshot.LayoutCacheMissCount} | Viewport builds: {snapshot.ViewportLayoutBuildCount} | Full builds: {snapshot.FullLayoutBuildCount}");
        SetText(
            "InspectorBufferMetricsLabel",
            $"Buffer pieces: {snapshot.BufferMetrics.PieceCount} | Materializations: {snapshot.BufferMetrics.TextMaterializationCount} | Render avg/max ms: {snapshot.AverageRenderMilliseconds:F3}/{snapshot.MaxRenderMilliseconds:F3}");
    }

    private void ApplyStylePalette(PasswordBox box, int paletteIndex)
    {
        _stylePaletteIndex = paletteIndex;

        switch (paletteIndex)
        {
            case 0:
                box.Background = new Color(52, 32, 12);
                box.BorderBrush = new Color(200, 120, 40);
                box.Foreground = new Color(255, 208, 150);
                box.CaretBrush = new Color(255, 230, 180);
                box.SelectionBrush = new Color(210, 138, 61, 180);
                box.Padding = new Thickness(10f, 8f, 10f, 8f);
                break;
            case 1:
                box.Background = new Color(18, 35, 52);
                box.BorderBrush = new Color(88, 144, 197);
                box.Foreground = new Color(208, 235, 255);
                box.CaretBrush = new Color(162, 220, 255);
                box.SelectionBrush = new Color(74, 132, 191, 190);
                box.Padding = new Thickness(12f, 8f, 12f, 8f);
                break;
            default:
                box.Background = new Color(24, 28, 34);
                box.BorderBrush = new Color(110, 120, 136);
                box.Foreground = new Color(228, 232, 238);
                box.CaretBrush = new Color(255, 255, 255);
                box.SelectionBrush = new Color(96, 104, 118, 180);
                box.Padding = new Thickness(8f, 6f, 8f, 6f);
                break;
        }
    }

    private void AttachClick(string elementName, EventHandler<RoutedSimpleEventArgs> handler)
    {
        if (FindElement<Button>(elementName) is { } button)
        {
            button.Click += handler;
        }
    }

    private T? FindElement<T>(string name)
        where T : class
    {
        return this.FindName(name) as T;
    }

    private void SetText(string elementName, string text)
    {
        if (FindElement<TextBlock>(elementName) is { } textBlock)
        {
            textBlock.Text = text;
        }
    }

    private static T CycleValue<T>(T current, T[] values)
    {
        if (values.Length == 0)
        {
            return current;
        }

        var currentIndex = Array.IndexOf(values, current);
        if (currentIndex < 0)
        {
            return values[0];
        }

        return values[(currentIndex + 1) % values.Length];
    }

    private static string BuildMultilineSample()
    {
        return "launch-codes\narchive-phrase\nrecovery-seed-2026";
    }

    private string GetInspectorPasswordValue()
    {
        return FindElement<PasswordBox>("InspectorPasswordBox")?.Password ?? string.Empty;
    }

    private static string FormatPasswordPreview(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "empty";
        }

        var singleLine = password
            .Replace("\r\n", " \\n ")
            .Replace("\n", " \\n ")
            .Replace("\r", " \\n ");

        return singleLine.Length > 72
            ? $"{singleLine[..69]}..."
            : singleLine;
    }

    private static string GetStylePaletteName(int paletteIndex)
    {
        return paletteIndex switch
        {
            0 => "Warm",
            1 => "Cool",
            _ => "Slate",
        };
    }
}




