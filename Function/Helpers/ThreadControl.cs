using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Function.Helpers
{
    public static class ThreadControl
    {
        unsafe public static void setPromat(ISnackbarService* snackbarService)
        {
            (*snackbarService).Show(
                "Processing",
                "Please wait while the operation completes.",
                ControlAppearance.Info,
                null,
                TimeSpan.FromSeconds(5)
            );
        }
    }
}
