using Function.Models;
using System;
using System.Collections.ObjectModel;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Function.ViewModels.Pages
{
    public partial class DashboardViewModel(ISnackbarService snackbarService) : ObservableObject
    {
        [ObservableProperty]
        private int _counter = 0;
        [ObservableProperty]
        private ObservableCollection<GenApp> _basicListViewItems = GeneratePersons();

        private static ObservableCollection<GenApp> GeneratePersons()
        {
            var persons = new ObservableCollection<GenApp>();
            for (int i = 1; i <= 1; i++)
            {
                persons.Add(new GenApp
                {
                    AppName = $"应用程序 {i}",
                    AppVersion = $"版本 {i}.0",
                    AppDescription = $"这是应用程序 {i} 的描述。"
                });
            }
            return persons;
        }

        [RelayCommand]
        private void OnCounterIncrement()
        {
            Counter++;
        }

        [RelayCommand]
        private void OnShowIncrement()
        {
            snackbarService.Show(
                "操作成功",
                "数据已成功保存到数据库",
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(3)
            );
        }
    }
}
