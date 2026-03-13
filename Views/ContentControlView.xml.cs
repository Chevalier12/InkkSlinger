using System;

namespace InkkSlinger;

public partial class ContentControlView : UserControl
{
    public ContentControlView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ContentControl");
        }
    }
}




