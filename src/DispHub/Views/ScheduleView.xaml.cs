using System.Windows;
using System.Windows.Controls;
using DispHub.ViewModels;

namespace DispHub.Views;

public partial class ScheduleView : UserControl
{
    public ScheduleView()
    {
        InitializeComponent();
        Timeline.SlotMoved += (_, e) => (DataContext as ScheduleViewModel)?.MoveSlot(e.SlotId, e.Minutes);
    }

    private void OnTemplatesClick(object sender, RoutedEventArgs e)
    {
        if (TemplatesButton.ContextMenu is { } menu)
        {
            menu.PlacementTarget = TemplatesButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
