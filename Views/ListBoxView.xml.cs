using System;

namespace InkkSlinger;

public partial class ListBoxView : UserControl
{
    public ListBoxView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ListBox");
        }
    }
}




