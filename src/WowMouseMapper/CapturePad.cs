using System.Drawing.Drawing2D;

namespace WowMouseMapper;

internal sealed class CapturePad : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Listening { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int ActiveButton { get; set; }

    public CapturePad()
    {
        DoubleBuffered = true;
        TabStop = true;
        AccessibleName = "Mouse test area";
        AccessibleDescription = "Start testing, then move the pointer here and press your mouse buttons.";
        BackColor = Color.FromArgb(22, 29, 41);
        ForeColor = Color.FromArgb(226, 232, 240);
        SetStyle(ControlStyles.Selectable, true);
    }

    protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var accent = Listening ? Color.FromArgb(74, 222, 171) : Color.FromArgb(108, 127, 151);
        using var border = new Pen(accent, 2);
        g.DrawRectangle(border, 1, 1, Width - 3, Height - 3);
        using var titleFont = new Font("Segoe UI", 12, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", 10);
        TextRenderer.DrawText(g, Listening ? "LISTENING  ·  TEST INSIDE THIS AREA" : "YOUR MOUSE, DECODED", titleFont,
            new Rectangle(8, 18, Width - 16, 28), accent, TextFormatFlags.HorizontalCenter);

        float scale = Math.Min((Width - 30) / 390f, (Height - 104) / 215f);
        scale = Math.Max(.2f, scale);
        var state = g.Save();
        g.TranslateTransform(Width / 2f - 195 * scale, 54);
        g.ScaleTransform(scale, scale);
        using var shell = new SolidBrush(Color.FromArgb(32, 43, 58));
        using var normal = new SolidBrush(Color.FromArgb(47, 62, 79));
        using var active = new SolidBrush(Color.FromArgb(74, 222, 171));
        using var outline = new Pen(Color.FromArgb(83, 103, 123), 2);
        using var shape = Rounded(new RectangleF(115, 0, 160, 210), 65);
        g.FillPath(shell, shape); g.DrawPath(outline, shape);
        void Button(int number, Rectangle rectangle, string label)
        {
            using var path = Rounded(rectangle, 12);
            g.FillPath(ActiveButton == number ? active : normal, path);
            using var font = new Font("Segoe UI", number == 3 ? 9 : 12, FontStyle.Bold);
            using var ink = new SolidBrush(ActiveButton == number ? Color.FromArgb(15, 31, 30) : ForeColor);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(label, font, ink, rectangle, format);
        }
        Button(1, new Rectangle(127, 16, 53, 74), "1");
        Button(2, new Rectangle(210, 16, 53, 74), "2");
        Button(3, new Rectangle(184, 27, 22, 54), "3");
        Button(5, new Rectangle(91, 91, 32, 39), "5");
        Button(4, new Rectangle(91, 136, 32, 39), "4");
        using var centerFont = new Font("Segoe UI", 9);
        using var muted = new SolidBrush(Color.FromArgb(147, 164, 185));
        g.DrawString("SIDE BUTTONS", centerFont, muted, 0, 65);
        g.DrawString("WHEEL", centerFont, muted, 286, 40);
        g.DrawLine(outline, 274, 51, 283, 51);
        g.DrawString("Generic layout", centerFont, muted, 151, 176);
        g.Restore(state);
        TextRenderer.DrawText(g, Listening ? "Click • scroll • press a side button   |   Esc to stop" : "Click Start testing, then move your pointer here.",
            smallFont, new Rectangle(8, Height - 38, Width - 16, 30), ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }

    private static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
