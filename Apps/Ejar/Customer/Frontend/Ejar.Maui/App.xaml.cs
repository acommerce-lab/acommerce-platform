using ACommerce.L10n.Blazor;
using Microsoft.Maui.Controls;

namespace Ejar.Maui;

public partial class App : Application
{
    private readonly L _l;

    // ‹L› يُحقَن عَبر MauiProgram.UseMauiApp<App>() — DI يَحُلّ App من الـ
    // service container، فالحَقن يَعمل دون شيفرة تَسجيل إضافيَّة.
    public App(L l)
    {
        _l = l;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(new MainPage()) { Title = _l["app.name"] };
}
