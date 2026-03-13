using System;

namespace InkkSlinger;

public partial class ContextMenuView : UserControl
{
    public ContextMenuView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ContextMenu");
        }
    }
}




