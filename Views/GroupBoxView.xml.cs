using System;

namespace InkkSlinger;

public partial class GroupBoxView : UserControl
{
    public GroupBoxView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("GroupBox");
        }
    }
}




