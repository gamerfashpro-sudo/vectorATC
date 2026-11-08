using System.Reflection;
using System.Text.RegularExpressions;
using VectorAtc.ViewModels;
using Xunit;

namespace VectorAtc.Tests;

/// <summary>
/// Ошибка, из-за которой окно не открывалось: полоса прогресса привязана к свойству только для чтения, а у неё привязка по умолчанию
/// двусторонняя, и WPF бросает исключение при создании окна. Локально WPF не запускается, поэтому разметка проверяется по тексту.
/// </summary>
public class XamlBindingTests
{
    private static string Xaml()
    {
        var dir = new DirectoryInfo(Environment.GetEnvironmentVariable("VECTORATC_ROOT") ?? AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VectorAtc.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "VectorAtc.App", "MainWindow.xaml"));
    }

    [Fact]
    public void Progress_bars_bind_one_way()
    {
        var bars = Regex.Matches(Xaml(), @"<ProgressBar\b[^>]*>").Select(m => m.Value).ToList();
        Assert.True(bars.Count > 0);
        foreach (var bar in bars)
            Assert.Contains("Mode=OneWay", bar);
    }

    [Fact]
    public void Text_boxes_bind_only_to_writable_properties()
    {
        var names = Regex.Matches(Xaml(), @"<TextBox\b[^>]*?Text=""\{Binding\s+(\w+)").Select(m => m.Groups[1].Value).ToList();
        Assert.True(names.Count > 0);
        foreach (var name in names)
        {
            var property = typeof(MainViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            Assert.True(property!.CanWrite && property.SetMethod!.IsPublic);
        }
    }
}
