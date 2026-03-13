using System;

namespace InkkSlinger;

public partial class GroupItemView : UserControl
{
    public GroupItemView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("GroupItem");
        }
    }
}




