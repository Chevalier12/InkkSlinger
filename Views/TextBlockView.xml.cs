using System;

namespace InkkSlinger;

public partial class TextBlockView : UserControl
{
    public TextBlockView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("TextBlock");
        }
    }
}




