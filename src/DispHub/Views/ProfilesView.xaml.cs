using System.Windows;
using System.Windows.Controls;

namespace DispHub.Views;

public partial class ProfilesView : UserControl
{
    public ProfilesView() => InitializeComponent();

    private void OnTemplatesClick(object sender, RoutedEventArgs e)
    {
        if (TemplatesButton.ContextMenu is { } menu)
        {
            menu.DataContext = DataContext;
            menu.PlacementTarget = TemplatesButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
