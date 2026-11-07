using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using VectorAtc.ViewModels;

namespace VectorAtc.App;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public MainWindow()
    {
        InitializeComponent();

        // Тёмная заголовочная полоса окна (Windows 10 20H1+ / 11). Если система не умеет, остаётся обычная.
        SourceInitialized += (_, _) =>
        {
            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                var on = 1;
                DwmSetWindowAttribute(handle, 20, ref on, sizeof(int));
            }
            catch (Exception)
            {
                // оформление необязательно
            }
        };

        // Новая строка журнала прокручивает список вниз.
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.Lines.CollectionChanged += (_, _) => LogScroll.ScrollToEnd();
        };
    }
}
