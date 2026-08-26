using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rovia.Desktop;

/// <summary>Lightweight modal dialog for capturing a single text value.</summary>
public sealed class InputDialog : Window
{
    private readonly TextBox _textBox;
    private string? _result;

    /// <summary>Initializes a new instance of the InputDialog class.</summary>
    public InputDialog(string title, string placeholder)
    {
        Title                  = title;
        Width                  = 520;
        Height                 = 200;
        CanResize              = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Grid grid = new() { Margin = new Thickness(16), RowDefinitions = new RowDefinitions("Auto,*,Auto") };

        TextBlock label = new()
        {
            Text        = title,
            FontWeight  = FontWeight.SemiBold,
            Margin      = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(label, 0);

        _textBox = new()
        {
            Watermark = placeholder,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _textBox.KeyDown += (_, keyEventArgs) =>
        {
            if (keyEventArgs.Key == Key.Enter)
                Confirm();
        };
        Grid.SetRow(_textBox, 1);

        StackPanel buttons = new()
        {
            Orientation           = Orientation.Horizontal,
            HorizontalAlignment   = HorizontalAlignment.Right,
            Spacing               = 8,
            Margin                = new Thickness(0, 12, 0, 0)
        };
        Button ok = new() { Content = "OK", IsDefault = true };
        ok.Click += (_, _) => Confirm();
        Button cancel = new() { Content = "Cancel", IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 2);

        grid.Children.Add(label);
        grid.Children.Add(_textBox);
        grid.Children.Add(buttons);
        Content = grid;
    }

    private void Confirm()
    {
        _result = _textBox.Text;
        Close();
    }

    /// <summary>Opens a modal dialog and returns the entered text, or null if cancelled.</summary>
    public static async Task<string?> ShowAsync(Window parent, string title, string placeholder)
    {
        InputDialog dialog = new(title, placeholder);
        await dialog.ShowDialog(parent);
        return dialog._result;
    }
}