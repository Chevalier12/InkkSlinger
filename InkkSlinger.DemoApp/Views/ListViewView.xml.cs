using System;

namespace InkkSlinger;

public partial class ListViewView : UserControl
{
    public ListViewView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ListView");
        }
    }
}




