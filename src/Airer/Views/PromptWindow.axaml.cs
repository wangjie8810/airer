using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Airer.Views;

public readonly record struct ChordPick(bool Accepted, HotkeyChord? Chord);

public partial class PromptWindow : Window
{
    private TextBlock TitleText = null!;
    private TextBlock BodyText = null!;
    private Button Primary = null!;
    private Button Secondary = null!;
    private readonly TaskCompletionSource<ChordPick> _chord = new();
    private readonly TaskCompletionSource _closed = new();

    public PromptWindow()
    {
        InitializeComponent();
        Title = Text.AppName;
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Closed += (_, _) =>
        {
            _chord.TrySetResult(new ChordPick(false, null));
            _closed.TrySetResult();
        };
    }

    public Task ShowWelcome()
    {
        TitleText.Text = Text.WelcomeTitle;
        BodyText.Text = Text.WelcomeBody;
        Primary.Content = Text.GotIt;
        Secondary.IsVisible = false;
        Primary.Click += (_, _) => Close();
        Show();
        Activate();
        return _closed.Task;
    }

    public Task ShowNote(string message, string? title = null)
    {
        TitleText.Text = title ?? Text.AppName;
        BodyText.Text = message;
        Primary.Content = Text.GotIt;
        Secondary.IsVisible = false;
        Primary.Click += (_, _) => Close();
        Show();
        Activate();
        return _closed.Task;
    }

    public Task<ChordPick> PickChord(string action)
    {
        Title = Text.Shortcuts;
        TitleText.Text = action;
        BodyText.Text = Text.PressShortcut + "\n" + Text.ShortcutHint;
        Primary.IsVisible = false;
        Secondary.Content = Text.Cancel;
        Secondary.Click += (_, _) => Close();
        Show();
        Activate();
        Focus();
        return _chord.Task;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (!Primary.IsVisible && Secondary.IsVisible)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key is Key.Delete or Key.Back)
            {
                _chord.TrySetResult(new ChordPick(true, new HotkeyChord { Key = "" }));
                Close();
            }
            else
            {
                var chord = HotkeyChord.TryCreate(
                    e.KeyModifiers.HasFlag(KeyModifiers.Control),
                    e.KeyModifiers.HasFlag(KeyModifiers.Alt),
                    e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                    e.KeyModifiers.HasFlag(KeyModifiers.Meta),
                    KeyName(e.Key));
                if (chord != null)
                {
                    _chord.TrySetResult(new ChordPick(true, chord));
                    Close();
                }
            }
        }
        e.Handled = true;
    }

    private static string? KeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
            return key.ToString();
        if (key is >= Key.D0 and <= Key.D9)
            return ((int)(key - Key.D0)).ToString();
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return ((int)(key - Key.NumPad0)).ToString();
        if (key is >= Key.F1 and <= Key.F12)
            return key.ToString();
        return key == Key.Space ? "Space" : null;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        TitleText = this.FindControl<TextBlock>("TitleText")!;
        BodyText = this.FindControl<TextBlock>("BodyText")!;
        Primary = this.FindControl<Button>("Primary")!;
        Secondary = this.FindControl<Button>("Secondary")!;
    }
}
