using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class TextBoxTests
{
    public TextBoxTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
        TextClipboard.ResetForTests();
    }

    [Fact]
    public void TextInput_AndBackspace_UpdateText_AndRaiseTextChanged()
    {
        var textBox = new TestTextBox();
        var changedCount = 0;
        textBox.TextChanged += (_, _) => changedCount++;

        textBox.Focus();
        textBox.FireTextInput('a');
        textBox.FireTextInput('b');
        textBox.FireTextInput('c');

        Assert.Equal("abc", textBox.Text);

        textBox.FireKeyDown(Keys.Back);
        Assert.Equal("ab", textBox.Text);
        Assert.Equal(4, changedCount);
    }

    [Fact]
    public void ShiftSelection_AndDelete_RemovesSelection()
    {
        var textBox = new TestTextBox
        {
            Text = "slinger"
        };

        textBox.Focus();
        textBox.FireKeyDown(Keys.End);
        textBox.FireKeyDown(Keys.Left, ModifierKeys.Shift);
        textBox.FireKeyDown(Keys.Left, ModifierKeys.Shift);
        textBox.FireKeyDown(Keys.Delete);

        Assert.Equal("sling", textBox.Text);
        Assert.Equal(5, textBox.CaretIndex);
    }

    [Fact]
    public void Clipboard_Copy_Cut_Paste_AndSelectAll_Work()
    {
        var textBox = new TestTextBox
        {
            Text = "ink core"
        };

        textBox.Focus();
        textBox.FireKeyDown(Keys.End);
        textBox.FireKeyDown(Keys.A, ModifierKeys.Control);
        textBox.FireKeyDown(Keys.C, ModifierKeys.Control);

        Assert.True(TextClipboard.TryGetText(out var copied));
        Assert.Equal("ink core", copied);

        textBox.FireKeyDown(Keys.X, ModifierKeys.Control);
        Assert.Equal(string.Empty, textBox.Text);

        textBox.FireKeyDown(Keys.V, ModifierKeys.Control);
        Assert.Equal("ink core", textBox.Text);
    }

    [Fact]
    public void Enter_InsertsNewLine()
    {
        var textBox = new TestTextBox
        {
            Text = "ab"
        };

        textBox.Focus();
        textBox.FireKeyDown(Keys.End);
        textBox.FireKeyDown(Keys.Enter);
        textBox.FireTextInput('c');

        Assert.Equal("ab\nc", textBox.Text);
    }

    [Fact]
    public void Enter_KeyRepeat_DoesNotInsertExtraNewLine()
    {
        var textBox = new TestTextBox
        {
            Text = "ab"
        };

        textBox.Focus();
        textBox.FireKeyDown(Keys.End);
        textBox.FireKeyDown(Keys.Enter, isRepeat: false);
        textBox.FireKeyDown(Keys.Enter, isRepeat: true);

        Assert.Equal("ab\n", textBox.Text);
    }

    [Fact]
    public void MouseWheel_ScrollsMultilineTextBoxViewport()
    {
        var textBox = new TestTextBox
        {
            Width = 240f,
            Height = 60f,
            TextWrapping = TextWrapping.NoWrap
        };

        var source = new System.Text.StringBuilder();
        for (var i = 0; i < 30; i++)
        {
            source.Append("Line ");
            source.Append(i);
            source.Append('\n');
        }

        textBox.Text = source.ToString();
        textBox.Measure(new Vector2(240f, 60f));
        textBox.Arrange(new LayoutRect(0f, 0f, 240f, 60f));
        textBox.Focus();

        var before = textBox.VerticalOffset;
        textBox.FireMouseWheel(new Vector2(8f, 8f), -120);
        var after = textBox.VerticalOffset;

        Assert.True(after > before);
    }

    [Fact]
    public void ScrollBarVisibility_DefaultsToAuto_AndCanBeSet()
    {
        var textBox = new TestTextBox();

        Assert.Equal(ScrollBarVisibility.Auto, textBox.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, textBox.VerticalScrollBarVisibility);

        textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
        textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

        Assert.Equal(ScrollBarVisibility.Visible, textBox.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, textBox.VerticalScrollBarVisibility);
    }

    [Fact]
    public void XamlLoader_BindsTextBox_TextChanged_Handler()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <TextBox x:Name="Editor"
                                       Text="seed"
                                       TextWrapping="Wrap"
                                       MaxLength="5"
                                       TextChanged="OnEditorChanged" />
                            </UserControl>
                            """;

        var codeBehind = new TextBoxCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        var editor = Assert.IsType<TextBox>(view.Content);
        Assert.Same(editor, codeBehind.Editor);

        editor.Text = "bigger-than-max";
        Assert.Equal("bigge", editor.Text);
        Assert.Equal(TextWrapping.Wrap, editor.TextWrapping);
        Assert.Equal(1, codeBehind.TextChangedCount);
    }

    private sealed class TestTextBox : TextBox
    {
        public float VerticalOffset => VerticalOffsetForTesting;

        public void FireKeyDown(Keys key, ModifierKeys modifiers = ModifierKeys.None, bool isRepeat = false)
        {
            RaisePreviewKeyDown(key, isRepeat, modifiers);
            RaiseKeyDown(key, isRepeat, modifiers);
        }

        public void FireTextInput(char character)
        {
            RaisePreviewTextInput(character);
            RaiseTextInput(character);
        }

        public void FireMouseWheel(Vector2 position, int delta)
        {
            RaisePreviewMouseWheel(position, delta, ModifierKeys.None);
            RaiseMouseWheel(position, delta, ModifierKeys.None);
        }

    }

    private sealed class TextBoxCodeBehind
    {
        public TextBox? Editor { get; set; }

        public int TextChangedCount { get; private set; }

        public void OnEditorChanged(object? sender, RoutedSimpleEventArgs args)
        {
            TextChangedCount++;
        }
    }
}
