using System;
using System.Numerics;

namespace InkkSlinger;

public sealed class InkkOopsScriptBuilder
{
    private readonly InkkOopsScript _script;

    public InkkOopsScriptBuilder(string name)
    {
        _script = new InkkOopsScript(name);
    }

    public InkkOopsScriptBuilder Add(IInkkOopsCommand command)
    {
        _script.Add(command);
        return this;
    }

    public InkkOopsScriptBuilder ResizeWindow(int width, int height)
    {
        return Add(new InkkOopsResizeWindowCommand(width, height));
    }

    public InkkOopsScriptBuilder MaximizeWindow()
    {
        return Add(new InkkOopsMaximizeWindowCommand());
    }

    public InkkOopsScriptBuilder WaitFrames(int frameCount)
    {
        return Add(new InkkOopsWaitFramesCommand(frameCount));
    }

    public InkkOopsScriptBuilder WaitForIdle()
    {
        return Add(new InkkOopsWaitForIdleCommand());
    }

    public InkkOopsScriptBuilder WaitForIdle(InkkOopsIdlePolicy policy)
    {
        return Add(new InkkOopsWaitForIdleCommand(policy));
    }

    public InkkOopsScriptBuilder WaitForElement(string targetName, int maxFrames = 120)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(targetName), maxFrames));
    }

    public InkkOopsScriptBuilder WaitForElement(InkkOopsTargetSelector selector, int maxFrames = 120)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(selector), maxFrames));
    }

    public InkkOopsScriptBuilder WaitForVisible(string targetName, int maxFrames = 120, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(targetName), maxFrames, InkkOopsWaitCondition.Visible, anchor));
    }

    public InkkOopsScriptBuilder WaitForVisible(InkkOopsTargetSelector selector, int maxFrames = 120, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(selector), maxFrames, InkkOopsWaitCondition.Visible, anchor));
    }

    public InkkOopsScriptBuilder WaitForEnabled(string targetName, int maxFrames = 120, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(targetName), maxFrames, InkkOopsWaitCondition.Enabled, anchor));
    }

    public InkkOopsScriptBuilder WaitForEnabled(InkkOopsTargetSelector selector, int maxFrames = 120, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(selector), maxFrames, InkkOopsWaitCondition.Enabled, anchor));
    }

    public InkkOopsScriptBuilder WaitForInViewport(string targetName, int maxFrames = 120, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(targetName), maxFrames, InkkOopsWaitCondition.InViewport, anchor));
    }

    public InkkOopsScriptBuilder WaitForInViewport(InkkOopsTargetSelector selector, int maxFrames = 120, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(selector), maxFrames, InkkOopsWaitCondition.InViewport, anchor));
    }

    public InkkOopsScriptBuilder WaitForInteractive(string targetName, int maxFrames = 120, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(targetName), maxFrames, InkkOopsWaitCondition.Interactive, anchor));
    }

    public InkkOopsScriptBuilder WaitForInteractive(InkkOopsTargetSelector selector, int maxFrames = 120, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsWaitForElementCommand(new InkkOopsTargetReference(selector), maxFrames, InkkOopsWaitCondition.Interactive, anchor));
    }

    public InkkOopsScriptBuilder Hover(string targetName)
    {
        return Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference(targetName)));
    }

    public InkkOopsScriptBuilder Hover(string targetName, InkkOopsPointerAnchor anchor)
    {
        return Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference(targetName), anchor));
    }

    public InkkOopsScriptBuilder Hover(InkkOopsTargetSelector selector, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsHoverTargetCommand(new InkkOopsTargetReference(selector), anchor));
    }

    public InkkOopsScriptBuilder Click(string targetName)
    {
        return Add(new InkkOopsClickTargetCommand(new InkkOopsTargetReference(targetName)));
    }

    public InkkOopsScriptBuilder Click(string targetName, InkkOopsPointerAnchor anchor)
    {
        return Add(new InkkOopsClickTargetCommand(new InkkOopsTargetReference(targetName), anchor));
    }

    public InkkOopsScriptBuilder Click(InkkOopsTargetSelector selector, InkkOopsPointerAnchor? anchor = null)
    {
        return Add(new InkkOopsClickTargetCommand(new InkkOopsTargetReference(selector), anchor));
    }

    public InkkOopsScriptBuilder MovePointer(Vector2 position)
    {
        return Add(new InkkOopsMovePointerCommand(position));
    }

    public InkkOopsScriptBuilder PointerDown(Vector2 position)
    {
        return Add(new InkkOopsPointerDownCommand(position));
    }

    public InkkOopsScriptBuilder PointerUp(Vector2 position)
    {
        return Add(new InkkOopsPointerUpCommand(position));
    }

    public InkkOopsScriptBuilder Drag(string targetName, float deltaX, float deltaY)
    {
        return Add(new InkkOopsDragTargetCommand(new InkkOopsTargetReference(targetName), deltaX, deltaY));
    }

    public InkkOopsScriptBuilder Drag(string targetName, float deltaX, float deltaY, InkkOopsPointerAnchor anchor)
    {
        return Add(new InkkOopsDragTargetCommand(new InkkOopsTargetReference(targetName), deltaX, deltaY, anchor));
    }

    public InkkOopsScriptBuilder Wheel(int delta)
    {
        return Add(new InkkOopsWheelCommand(delta));
    }

    public InkkOopsScriptBuilder Invoke(string targetName)
    {
        return Add(new InkkOopsInvokeTargetCommand(new InkkOopsTargetReference(targetName)));
    }

    public InkkOopsScriptBuilder Activate(string targetName)
    {
        return Add(new InkkOopsInvokeTargetCommand(new InkkOopsTargetReference(targetName)));
    }

    public InkkOopsScriptBuilder Activate(InkkOopsTargetSelector selector)
    {
        return Add(new InkkOopsInvokeTargetCommand(new InkkOopsTargetReference(selector)));
    }

    public InkkOopsScriptBuilder ScrollTo(string targetName, float horizontalPercent, float verticalPercent)
    {
        return Add(new InkkOopsScrollToCommand(new InkkOopsTargetReference(targetName), horizontalPercent, verticalPercent));
    }

    public InkkOopsScriptBuilder ScrollBy(string targetName, float horizontalPercentDelta, float verticalPercentDelta)
    {
        return Add(new InkkOopsScrollByCommand(new InkkOopsTargetReference(targetName), horizontalPercentDelta, verticalPercentDelta));
    }

    public InkkOopsScriptBuilder ScrollIntoView(string scrollViewerName, string targetName, float padding = 8f)
    {
        return Add(new InkkOopsScrollIntoViewCommand(
            new InkkOopsTargetReference(scrollViewerName),
            new InkkOopsTargetReference(targetName),
            padding));
    }

    public InkkOopsScriptBuilder ScrollIntoView(InkkOopsTargetSelector ownerSelector, InkkOopsTargetSelector targetSelector, float padding = 8f)
    {
        return Add(new InkkOopsScrollIntoViewCommand(
            new InkkOopsTargetReference(ownerSelector),
            new InkkOopsTargetReference(targetSelector),
            padding));
    }

    public InkkOopsScriptBuilder ScrollIntoViewItemText(string ownerName, string itemText, float padding = 8f)
    {
        return Add(new InkkOopsScrollIntoViewCommand(
            new InkkOopsTargetReference(ownerName),
            InkkOopsScrollLocator.ByItemText(itemText),
            padding));
    }

    public InkkOopsScriptBuilder ScrollIntoViewItemIndex(string ownerName, int itemIndex, float padding = 8f)
    {
        return Add(new InkkOopsScrollIntoViewCommand(
            new InkkOopsTargetReference(ownerName),
            InkkOopsScrollLocator.ByItemIndex(itemIndex),
            padding));
    }

    public InkkOopsScriptBuilder CaptureFrame(string artifactName)
    {
        return Add(new InkkOopsCaptureFrameCommand(artifactName));
    }

    public InkkOopsScriptBuilder DumpTelemetry(string artifactName)
    {
        return Add(new InkkOopsDumpTelemetryCommand(artifactName));
    }

    public InkkOopsScriptBuilder AssertExists(string targetName)
    {
        return Add(new InkkOopsAssertExistsCommand(new InkkOopsTargetReference(targetName)));
    }

    public InkkOopsScriptBuilder AssertNotExists(string targetName)
    {
        return Add(new InkkOopsAssertNotExistsCommand(new InkkOopsTargetReference(targetName)));
    }

    public InkkOopsScriptBuilder AssertProperty(string targetName, string propertyName, object? expectedValue)
    {
        return Add(new InkkOopsAssertPropertyCommand(new InkkOopsTargetReference(targetName), propertyName, expectedValue));
    }

    public InkkOopsScriptBuilder AssertAutomationEvent(
        AutomationEventType eventType,
        string targetName = "",
        string propertyName = "")
    {
        return Add(new InkkOopsAssertAutomationEventCommand(eventType, targetName, propertyName));
    }

    public InkkOopsScript Build()
    {
        return _script;
    }
}
