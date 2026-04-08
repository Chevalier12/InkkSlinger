using System;

namespace InkkSlinger;

public partial class DocumentViewerView : UserControl
{
    public DocumentViewerView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("DocumentViewer");
        }
    }
}




