using Xunit;

namespace InkkSlinger.Tests;

public sealed class AutomationEventRegressionTests
{
    [Fact]
    public void CoalescedValueChangedEvent_UsesLatestNewValue()
    {
        var host = new Canvas();
        var textBox = new TextBox { Text = "seed" };
        host.AddChild(textBox);
        var uiRoot = new UiRoot(host);

        uiRoot.Automation.BeginFrame();
        textBox.Text = "first";
        textBox.Text = "second";
        uiRoot.Automation.EndFrameAndFlush();

        var events = uiRoot.Automation.GetAndClearEventLogForTests();
        var valueChanged = Assert.Single(events, e =>
            e.EventType == AutomationEventType.ValueChanged &&
            e.PropertyName == "Text");
        Assert.Equal("seed", valueChanged.OldValue);
        Assert.Equal("second", valueChanged.NewValue);

        uiRoot.Shutdown();
    }

    [Fact]
    public void PasswordChangedEvents_DoNotExposePlaintextPayload()
    {
        var host = new Canvas();
        var passwordBox = new PasswordBox();
        host.AddChild(passwordBox);
        var uiRoot = new UiRoot(host);

        uiRoot.Automation.BeginFrame();
        passwordBox.Password = "TopSecret1";
        passwordBox.Password = "TopSecret2";
        uiRoot.Automation.EndFrameAndFlush();

        var events = uiRoot.Automation.GetAndClearEventLogForTests();
        Assert.Contains(events, e =>
            e.EventType == AutomationEventType.ValueChanged &&
            e.PropertyName == "Password");
        Assert.DoesNotContain(events, e =>
            Equals(e.OldValue, "TopSecret1") ||
            Equals(e.NewValue, "TopSecret1") ||
            Equals(e.OldValue, "TopSecret2") ||
            Equals(e.NewValue, "TopSecret2"));

        uiRoot.Shutdown();
    }

    [Fact]
    public void RemovingElement_QueuesStructureChangedEvent()
    {
        var host = new Canvas();
        var button = new Button();
        host.AddChild(button);
        var uiRoot = new UiRoot(host);
        Assert.NotNull(uiRoot.Automation.GetPeer(button));

        uiRoot.Automation.BeginFrame();
        host.RemoveChild(button);
        uiRoot.Automation.EndFrameAndFlush();

        var events = uiRoot.Automation.GetAndClearEventLogForTests();
        Assert.Contains(events, e => e.EventType == AutomationEventType.StructureChanged);

        uiRoot.Shutdown();
    }
}
