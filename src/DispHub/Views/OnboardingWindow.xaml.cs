using DispHub.ViewModels;

namespace DispHub.Views;

public partial class OnboardingWindow : Wpf.Ui.Controls.FluentWindow
{
    public OnboardingWindow(OnboardingViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        viewModel.Finished += (_, _) => Close();
        // Closing it early counts as seen: it isn't shown again.
        Closed += (_, _) => viewModel.Finish();
    }
}
