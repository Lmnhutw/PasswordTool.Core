namespace PasswordTool.WinForms;

internal sealed class ClipboardShortcutBlocker : IMessageFilter
{
    private const int WmCopy = 0x0301;
    private const int WmCut = 0x0300;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int VkC = 0x43;
    private const int VkX = 0x58;
    private const int VkInsert = 0x2D;
    private const int VkDelete = 0x2E;

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg is WmCopy or WmCut)
        {
            return true;
        }

        if (message.Msg is not (WmKeyDown or WmSysKeyDown))
        {
            return false;
        }

        var key = message.WParam.ToInt32();
        var controlPressed = (Control.ModifierKeys & Keys.Control) == Keys.Control;
        var shiftPressed = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

        return (controlPressed && key is VkC or VkX or VkInsert)
            || (shiftPressed && key is VkInsert or VkDelete);
    }
}
