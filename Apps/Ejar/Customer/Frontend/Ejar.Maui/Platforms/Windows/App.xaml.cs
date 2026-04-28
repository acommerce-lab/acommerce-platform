using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace Ejar.Maui.WinUI;

/// <summary>
/// تطبيق WinUI الذي يستضيف MAUI app على Windows. يبني MauiApp الموحَّد
/// عبر MauiProgram.CreateMauiApp ليتطابق سلوك Android/iOS.
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => Ejar.Maui.MauiProgram.CreateMauiApp();
}
