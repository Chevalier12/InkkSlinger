using System;

namespace InkkSlinger;

public partial class ScrollViewerView : UserControl
{
    public ScrollViewerView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ScrollViewer");
        }
    }
}




