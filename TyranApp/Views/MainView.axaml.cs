using Avalonia.Controls;
using TyranApp.ViewModels;
using Avalonia.Threading;

namespace TyranApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;

        viewModel.Logs.CollectionChanged += (s, e) =>
        {
            if (viewModel.AutoScroll)
            {
                // Ensure UI thread execution
                Dispatcher.UIThread.Post(() =>
                {
                    LogsScrollViewer.ScrollToEnd();
                });
            }
        };
    }
}
