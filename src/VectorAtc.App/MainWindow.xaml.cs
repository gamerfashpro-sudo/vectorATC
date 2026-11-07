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

        // Удержание кнопки: пока нажата, микрофон слушает.
        TalkButton.PreviewMouseLeftButtonDown += (_, _) => (DataContext as MainViewModel)?.BeginTalk();
        TalkButton.PreviewMouseLeftButtonUp += (_, _) => (DataContext as MainViewModel)?.EndTalk();
        TalkButton.LostMouseCapture += (_, _) => (DataContext as MainViewModel)?.EndTalk();

        // Размер области карты сообщаем модели: по нему считаются положения значков.
        MapHost.SizeChanged += (_, e) => (DataContext as MainViewModel)?.SetMapViewport(e.NewSize.Width, e.NewSize.Height);
        Loaded += (_, _) => (DataContext as MainViewModel)?.SetMapViewport(MapHost.ActualWidth, MapHost.ActualHeight);

        // Таймер двигает наземные услуги раз в секунду.
        var last = DateTime.UtcNow;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            (DataContext as MainViewModel)?.Tick(now - last);
            last = now;
        };
        timer.Start();

        // Новая строка журнала прокручивает список вниз.
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.Lines.CollectionChanged += (_, _) => LogScroll.ScrollToEnd();
        };
    }
}
