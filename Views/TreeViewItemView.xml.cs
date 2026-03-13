using System;

namespace InkkSlinger;

public partial class TreeViewItemView : UserControl
{
    public TreeViewItemView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("TreeViewItem");
        }
    }
}




