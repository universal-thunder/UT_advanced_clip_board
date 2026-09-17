namespace AdvancedClipboard;

internal static class Theme
{
    internal static readonly Color Background = Color.FromArgb(247, 248, 251);
    internal static readonly Color Surface = Color.White;
    internal static readonly Color Primary = Color.FromArgb(76, 91, 220);
    internal static readonly Color PrimaryLight = Color.FromArgb(235, 238, 255);
    internal static readonly Color Text = Color.FromArgb(31, 35, 48);
    internal static readonly Color Muted = Color.FromArgb(104, 111, 130);
    internal static readonly Color Border = Color.FromArgb(224, 227, 236);
    internal static readonly Color Danger = Color.FromArgb(205, 65, 70);

    internal static Button Button(string text, bool primary = false, bool danger = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 36,
            Padding = new Padding(14, 0, 14, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Primary : Surface,
            ForeColor = primary ? Color.White : danger ? Danger : Text,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular)
        };
        button.FlatAppearance.BorderColor = primary ? Primary : Border;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }
}
