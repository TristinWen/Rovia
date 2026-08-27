using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rovia.Desktop;

/// <summary>Displays a modal confirmation before a destructive desktop action.</summary>
public sealed class ConfirmationDialog : Window
{
    private bool _confirmed;

    /// <summary>Initializes a new confirmation dialog.</summary>
    public ConfirmationDialog(string title, string message, string confirmText)
    {
        Title                  = title;
        Width                  = 440;
        Height                 = 180;
        CanResize              = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Grid grid = new() { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("*,Auto") };
        TextBlock prompt = new()
        {
            Text         = message,
            TextWrapping = TextWrapping.Wrap
        };
        StackPanel buttons = new()
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing             = 8
        };
        Button cancel = new() { Content = "Cancel", IsCancel = true };
        Button confirm = new() { Content = confirmText, IsDefault = true };
        confirm.Click += (_, _) =>
        {
            _confirmed = true;
            Close();
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        Grid.SetRow(buttons, 1);
        grid.Children.Add(prompt);
        grid.Children.Add(buttons);
        Content = grid;
    }

    /// <summary>Shows the dialog and returns whether the destructive action was confirmed.</summary>
    public static async Task<bool> ShowAsync(Window parent, string title, string message, string confirmText)
    {
        ConfirmationDialog dialog = new(title, message, confirmText);
        await dialog.ShowDialog(parent);
        return dialog._confirmed;
    }
}
