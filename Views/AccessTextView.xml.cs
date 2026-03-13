using System;

namespace InkkSlinger;

public partial class AccessTextView : UserControl
{
    public AccessTextView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("AccessText");
        }
    }
}




