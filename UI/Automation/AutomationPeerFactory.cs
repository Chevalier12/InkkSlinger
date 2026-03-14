namespace InkkSlinger;

public static class AutomationPeerFactory
{
    public static AutomationPeer CreatePeer(AutomationManager manager, UIElement element)
    {
        if (element is Control control)
        {
            return new GenericAutomationPeer(manager, control, MapControlType(element));
        }

        if (element is FrameworkElement frameworkElement)
        {
            return new FrameworkElementAutomationPeer(manager, frameworkElement);
        }

        return new ElementAutomationPeer(manager, element);
    }

    private static AutomationControlType MapControlType(UIElement element)
    {
        return element switch
        {
            CheckBox => AutomationControlType.CheckBox,
            RadioButton => AutomationControlType.RadioButton,
            RepeatButton => AutomationControlType.Button,
            Button => AutomationControlType.Button,
            ComboBox => AutomationControlType.ComboBox,
            ComboBoxItem => AutomationControlType.ListItem,
            TextBox => AutomationControlType.Edit,
            PasswordBox => AutomationControlType.Password,
            ListView => AutomationControlType.List,
            ListViewItem => AutomationControlType.ListItem,
            ListBox => AutomationControlType.List,
            ListBoxItem => AutomationControlType.ListItem,
            TreeView => AutomationControlType.Tree,
            TreeViewItem => AutomationControlType.TreeItem,
            Menu => AutomationControlType.MenuBar,
            MenuItem => AutomationControlType.MenuItem,
            ContextMenu => AutomationControlType.Menu,
            TabControl => AutomationControlType.Tab,
            TabItem => AutomationControlType.TabItem,
            ScrollViewer => AutomationControlType.Pane,
            ScrollBar => AutomationControlType.ScrollBar,
            Slider => AutomationControlType.Slider,
            ProgressBar => AutomationControlType.ProgressBar,
            Calendar => AutomationControlType.Calendar,
            DatePicker => AutomationControlType.ComboBox,
            DataGrid => AutomationControlType.DataGrid,
            DataGridRow => AutomationControlType.ListItem,
            DataGridCell => AutomationControlType.Custom,
            RichTextBox => AutomationControlType.Document,
            DocumentViewer => AutomationControlType.Document,
            Frame => AutomationControlType.Pane,
            Page => AutomationControlType.Pane,
            Expander => AutomationControlType.Group,
            GroupBox => AutomationControlType.Group,
            Separator => AutomationControlType.Separator,
            ToolBar => AutomationControlType.ToolBar,
            ToolTip => AutomationControlType.ToolTip,
            Label => AutomationControlType.Text,
            TextBlock => AutomationControlType.Text,
            Image => AutomationControlType.Image,
            Border => AutomationControlType.Pane,
            Panel => AutomationControlType.Pane,
            _ => AutomationControlType.Custom
        };
    }
}
