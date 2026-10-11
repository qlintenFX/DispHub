using System.Windows;
using System.Windows.Controls;
using DispHub.ViewModels;

namespace DispHub.Views;

public partial class AutomationView : UserControl
{
    public AutomationView() => InitializeComponent();

    private void RunningAppsMenu_Opened(object sender, RoutedEventArgs e)
    {
        RunningAppsMenu.Items.Clear();
        if (DataContext is not AutomationViewModel viewModel)
            return;

        foreach (string app in AutomationViewModel.RunningApps())
        {
            var item = new MenuItem { Header = app };
            item.Click += (_, _) => viewModel.AddAppRuleCommand.Execute(app);
            RunningAppsMenu.Items.Add(item);
        }
        if (RunningAppsMenu.Items.Count == 0)
            RunningAppsMenu.Items.Add(new MenuItem { Header = Localization.Loc.Get("Automation_NoRunningApps"), IsEnabled = false });
    }
}
