using System;

namespace InkkSlinger;

public partial class StatusBarItemView : UserControl
{
    public StatusBarItemView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("StatusBarItem");
        }
    }
}




