using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using VectorAtc.ViewModels;

namespace VectorAtc.App;

/// <summary>
/// Окно при запуске: одно поле для ID или имени SimBrief. Закрывается с true, когда план загружен, с false по «Пропустить».
/// Запрос плана уходит только по нажатию кнопки или Enter.
/// </summary>
public partial class StartWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public StartWindow()
    {
        InitializeComponent();

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

        SkipButton.Click += (_, _) => DialogResult = false;

        Loaded += (_, _) =>
        {
            IdBox.Focus();
            IdBox.SelectAll();
            if (DataContext is MainViewModel vm)
            {
                PropertyChangedEventHandler? handler = null;
                handler = (_, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.HasPlan) && vm.HasPlan)
                    {
                        vm.PropertyChanged -= handler;
                        DialogResult = true;
                    }
                };
                vm.PropertyChanged += handler;
            }
        };
    }
}
