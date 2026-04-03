using Function.Services;
using Function.ViewModels.Pages;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;


namespace Function.Views.Pages;

public partial class GoSettingsPage : INavigableView<GoSettingsViewModel>
{
    public GoSettingsViewModel ViewModel { get; }

    public GoSettingsPage(GoSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
