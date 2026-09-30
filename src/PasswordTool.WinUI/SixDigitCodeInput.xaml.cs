using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace PasswordTool_WinUI;

public sealed partial class SixDigitCodeInput : UserControl
{
    private bool updating;
    private TextBox[] digits = [];
    public event EventHandler? CodeChanged;

    public SixDigitCodeInput()
    {
        InitializeComponent();
        digits = DigitPanel.Children.OfType<TextBox>().ToArray();
    }

    public string Code => string.Concat(digits.Select(box => box.Text));

    public void FocusFirst() => digits[0].Focus(FocusState.Programmatic);

    public void Clear()
    {
        foreach (var digit in digits) digit.Text = string.Empty;
    }

    private void CodeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (updating || sender is not TextBox box) return;
        var index = Array.IndexOf(digits, box);
        var value = new string(box.Text.Where(char.IsAsciiDigit).ToArray());
        if (value.Length == 0)
        {
            box.Text = string.Empty;
            CodeChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        updating = true;
        try
        {
            for (var i = index; i < digits.Length; i++) digits[i].Text = string.Empty;
            for (var i = 0; i < value.Length && index + i < digits.Length; i++) digits[index + i].Text = value[i].ToString();
        }
        finally { updating = false; }

        digits[Math.Min(index + value.Length, digits.Length - 1)].Focus(FocusState.Programmatic);
        CodeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CodeBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var index = Array.IndexOf(digits, box);
        if (e.Key == VirtualKey.Back && box.Text.Length == 0 && index > 0)
        {
            digits[index - 1].Focus(FocusState.Programmatic);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Left && index > 0)
        {
            digits[index - 1].Focus(FocusState.Programmatic);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right && index < digits.Length - 1)
        {
            digits[index + 1].Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }
}
