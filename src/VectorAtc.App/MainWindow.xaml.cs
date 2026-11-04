using System.Windows;
using VectorAtc.ViewModels;

namespace VectorAtc.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Новая строка журнала прокручивает список вниз.
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.Lines.CollectionChanged += (_, _) => LogScroll.ScrollToEnd();
        };
    }
}
