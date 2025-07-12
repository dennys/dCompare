using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace dCompare.Controls
{
    public class HotKeyTextBox : TextBox
    {
        public HotKeyTextBox()
        {
            // We don't want the user to type freely, so we handle the input ourselves.
            IsReadOnly = true;
            Focusable = true;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // Prevent the key from being processed further by other controls.
            e.Handled = true;

            // Get the pressed key. Ignore system/modifier keys on their own.
            var key = (e.Key == Key.System) ? e.SystemKey : e.Key;
            if (key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            // Handle Backspace to clear the hotkey
            if (key == Key.Back || key == Key.Delete)
            {
                Text = string.Empty;
                return;
            }

            // Build the hotkey string from modifiers and the pressed key.
            var sb = new StringBuilder();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                sb.Append("Ctrl+");
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                sb.Append("Shift+");
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                sb.Append("Alt+");
            }

            sb.Append(key.ToString());
            Text = sb.ToString();
        }
    }
}