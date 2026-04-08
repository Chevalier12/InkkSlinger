using System;

namespace InkkSlinger;

public partial class ListBoxItemView : UserControl
{
    public ListBoxItemView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ListBoxItem");
        }
    }
}




