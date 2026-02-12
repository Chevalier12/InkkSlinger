using Xunit;

namespace InkkSlinger.Tests;

public class TextEditingBufferTests
{
    [Fact]
    public void Insert_Backspace_Delete_AndSelection_Work()
    {
        var buffer = new TextEditingBuffer();

        Assert.True(buffer.InsertText("hello"));
        Assert.Equal("hello", buffer.Text);
        Assert.Equal(5, buffer.CaretIndex);

        Assert.True(buffer.Backspace(byWord: false));
        Assert.Equal("hell", buffer.Text);

        buffer.SetCaret(1, extendSelection: false);
        Assert.True(buffer.Delete(byWord: false));
        Assert.Equal("hll", buffer.Text);

        buffer.SetCaret(1, extendSelection: false);
        buffer.SetCaret(3, extendSelection: true);
        Assert.Equal("ll", buffer.GetSelectedText());
        Assert.True(buffer.DeleteSelectionIfPresent());
        Assert.Equal("h", buffer.Text);
    }

    [Fact]
    public void WordNavigation_AndWordDeletion_Work()
    {
        var buffer = new TextEditingBuffer();
        buffer.SetText("ink slinger core", preserveCaret: false);

        Assert.True(buffer.MoveCaretLeft(extendSelection: false, byWord: true));
        Assert.Equal(12, buffer.CaretIndex);

        Assert.True(buffer.Backspace(byWord: true));
        Assert.Equal("ink core", buffer.Text);

        buffer.SetCaret(0, extendSelection: false);
        Assert.True(buffer.Delete(byWord: true));
        Assert.Equal(" core", buffer.Text);
    }

    [Fact]
    public void EditDelta_TracksLastMutation()
    {
        var buffer = new TextEditingBuffer();

        Assert.True(buffer.InsertText("abc"));
        var insert = buffer.ConsumeLastEditDelta();
        Assert.True(insert.IsValid);
        Assert.Equal(0, insert.Start);
        Assert.Equal(0, insert.OldLength);
        Assert.Equal(3, insert.NewLength);

        buffer.SetCaret(1, extendSelection: false);
        Assert.True(buffer.Delete(byWord: false));
        var delete = buffer.ConsumeLastEditDelta();
        Assert.True(delete.IsValid);
        Assert.Equal(1, delete.Start);
        Assert.Equal(1, delete.OldLength);
        Assert.Equal(0, delete.NewLength);

        var none = buffer.ConsumeLastEditDelta();
        Assert.False(none.IsValid);
    }
}
