using System;

namespace InkkSlinger;

public partial class ListViewItemView : UserControl
{
    public ListViewItemView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ListViewItem");
        }
    }
}




