using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using QuantumUpdater.Services;

namespace QuantumUpdater.Views;

public sealed class SignInPage : StackPanel
{
    private readonly AppServices _services;
    private readonly TextBox _email;
    private readonly PasswordBox _password;
    private readonly TextBlock _status;
    private readonly Button _signIn;
    private readonly Button _create;
    private readonly Button _notNow;

    public event Action? SignedIn;
    public event Action? Dismissed;

    public SignInPage(AppServices services)
    {
        _services = services;
        Spacing = 8;
        MaxWidth = 420;
        HorizontalAlignment = HorizontalAlignment.Left;
        var logo = BrandAssets.Emblem(64);
        logo.HorizontalAlignment = HorizontalAlignment.Left;
        logo.Margin = new Thickness(0, 0, 0, 8);
        AutomationProperties.SetName(logo, "DeskVerve");
        Children.Add(logo);
        Children.Add(Chrome.Title("Sign in to DeskVerve"));
        Children.Add(Chrome.Line("Sign in is optional. DeskVerve works without an account."));
        _email = new TextBox
        {
            PlaceholderText = "Email",
            Background = Theme.Card,
            Foreground = Theme.Text,
            BorderBrush = Theme.Stroke,
        };
        _password = new PasswordBox
        {
            PlaceholderText = "Password",
            Background = Theme.Card,
            Foreground = Theme.Text,
            BorderBrush = Theme.Stroke,
        };
        Children.Add(_email);
        Children.Add(_password);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _signIn = Chrome.Action("Sign in");
        _create = Chrome.Action("Create account");
        _notNow = Chrome.Action("Not now");
        _signIn.Click += async (_, _) => await SubmitAsync(create: false).ConfigureAwait(true);
        _create.Click += async (_, _) => await SubmitAsync(create: true).ConfigureAwait(true);
        _notNow.Click += (_, _) => Dismissed?.Invoke();
        row.Children.Add(_signIn);
        row.Children.Add(_create);
        row.Children.Add(_notNow);
        Children.Add(row);
        _status = Chrome.Line("Your account stays on this PC.");
        Children.Add(_status);
    }

    private async Task SubmitAsync(bool create)
    {
        _signIn.IsEnabled = false;
        _create.IsEnabled = false;
        _notNow.IsEnabled = false;
        _status.Text = create ? "Creating the account…" : "Signing in…";
        _status.Foreground = Theme.Muted;
        try
        {
            var result = create
                ? await _services.Account.RegisterAsync(_email.Text, _password.Password).ConfigureAwait(true)
                : await _services.Account.SignInAsync(_email.Text, _password.Password).ConfigureAwait(true);
            if (!result.Ok || string.IsNullOrEmpty(result.Token))
            {
                _status.Text = result.Message;
                _status.Foreground = Theme.Critical;
                return;
            }

            _services.Session.Save(result.Token, _email.Text.Trim());
            SignedIn?.Invoke();
        }
        finally
        {
            _signIn.IsEnabled = true;
            _create.IsEnabled = true;
            _notNow.IsEnabled = true;
        }
    }
}
