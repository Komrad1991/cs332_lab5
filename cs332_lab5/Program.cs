using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using FractalApp.Parts;

namespace FractalApp;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }
}

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Фракталы: L-системы, midpoint displacement, кубические Безье";
        Width = 1280;
        Height = 860;
        MinWidth = 900;
        MinHeight = 600;

        var tabs = new TabControl();

        tabs.Items.Add(new TabItem
        {
            Header = "1a. L-система",
            Content = new Part1View()
        });

        tabs.Items.Add(new TabItem
        {
            Header = "1b. Фрактальное дерево",
            Content = new Part2TreeView()
        });

        tabs.Items.Add(new TabItem
        {
            Header = "2. Midpoint displacement",
            Content = new Part3TerrainView()
        });

        tabs.Items.Add(new TabItem
        {
            Header = "3. Кубические Безье",
            Content = new Part4BezierView()
        });

        Content = tabs;
    }
}