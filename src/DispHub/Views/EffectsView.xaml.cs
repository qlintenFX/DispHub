using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DispHub.ViewModels;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace DispHub.Views;

public partial class EffectsView : UserControl
{
    /// <summary>Below this width the look's name and actions go under the preview.</summary>
    private const double NarrowWidth = 640;

    public EffectsView() => InitializeComponent();

    private void OnPreviewCardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = e.NewSize.Width < NarrowWidth;
        Grid.SetColumn(InfoPanel, narrow ? 0 : 1);
        Grid.SetRow(InfoPanel, narrow ? 1 : 0);
        InfoPanel.Margin = narrow ? new Thickness(0, 14, 0, 0) : new Thickness(20, 0, 0, 0);
        PictureColumn.Width = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(3, GridUnitType.Star);
        InfoColumn.Width = narrow ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
        InfoColumn.MinWidth = narrow ? 0 : 220;
    }

    /// <summary>The effects that can be added, grouped under headings, as a menu under the button.</summary>
    private void OnAddPartClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EffectsViewModel viewModel)
            return;
        var menu = new ContextMenu { PlacementTarget = AddPartButton, Placement = PlacementMode.Bottom, MaxHeight = 480 };
        foreach (AddPartGroup group in viewModel.AddableParts)
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = group.Name, IsEnabled = false, FontWeight = FontWeights.SemiBold });
            foreach (AddPartChoice part in group.Parts)
                menu.Items.Add(new MenuItem { Header = part.Name, ToolTip = part.Hint, Command = part.AddCommand, Icon = new SymbolIcon { Symbol = SymbolRegular.Add16 } });
        }
        menu.IsOpen = true;
    }
}
