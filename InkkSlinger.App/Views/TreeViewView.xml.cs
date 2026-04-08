using System;

namespace InkkSlinger;

public partial class TreeViewView : UserControl
{
    public TreeViewView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("TreeView");
        }
    }
}




