using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace WowMouseMapper;

internal sealed class MainForm : Form, IMessageFilter
{
    private static readonly Color Background = Color.FromArgb(15, 21, 31);
    private static readonly Color Surface = Color.FromArgb(22, 29, 41);
    private static readonly Color Ink = Color.FromArgb(230, 237, 245);
    private static readonly Color Muted = Color.FromArgb(156, 174, 194);
    private static readonly Color Accent = Color.FromArgb(74, 222, 171);
    private readonly BindingStore store;
    private List<MouseBinding> bindings = [];
    private readonly CapturePad pad = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 18, 0) };
    private readonly Label signal = Label("Waiting for input", 22, Ink, true);
    private readonly Label detail = Label("Start a test to identify a button.", 10, Muted);
    private readonly Label status = Label("Ready · everything stays on this PC", 10, Muted);
    private readonly Label history = Label("Recent inputs will appear here.", 10, Muted);
    private readonly TextBox physical = TextField("e.g. Thumb button closest to me", 200);
    private readonly TextBox action = TextField("e.g. Interrupt / action bar 1, slot 6", 500);
    private readonly Button toggle = Button("Start testing", true);
    private readonly Button save = Button("Save binding", true);
    private readonly DataGridView grid = new();
    private readonly Queue<string> recent = new();
    private readonly HashSet<int> held = [];
    private DetectedInput? last;
    private bool listening;
    private bool inputAvailable;
    private bool storageHealthy = true;
    private int editingIndex = -1;

    public MainForm(BindingStore store)
    {
        SuspendLayout();
        this.store = store;
        Text = "WoW Mouse Mapper";
        BackColor = Background; ForeColor = Ink;
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(1120, 850);
        MinimumSize = new Size(1000, 820);
        StartPosition = FormStartPosition.CenterScreen;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 20, 26, 16), ColumnCount = 1, RowCount = 7 };
        foreach (var style in new[] { new RowStyle(SizeType.Absolute, 82), new RowStyle(SizeType.Absolute, 48),
            new RowStyle(SizeType.Absolute, 340), new RowStyle(SizeType.Absolute, 68),
            new RowStyle(SizeType.Absolute, 42), new RowStyle(SizeType.Percent, 100), new RowStyle(SizeType.Absolute, 36) }) root.RowStyles.Add(style);
        Controls.Add(root);

        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        heading.Controls.Add(Label("WoW Mouse Mapper", 25, Ink, true));
        heading.Controls.Add(Label("Know your buttons. Plan your bindings. Get back to the game.", 11, Muted));
        root.Controls.Add(heading, 0, 0);

        var toolbar = Flow();
        toggle.Click += (_, _) => SetListening(!listening);
        toolbar.Controls.Add(toggle);
        var help = Button("How to use"); help.Click += (_, _) => ShowHelp(); toolbar.Controls.Add(help);
        var support = new LinkLabel { Text = "Support this project ↗", AutoSize = true, LinkColor = Muted, ActiveLinkColor = Accent, Margin = new Padding(16, 10, 0, 0) };
        support.LinkClicked += (_, _) => OpenSupport(); toolbar.Controls.Add(support);
        root.Controls.Add(toolbar, 0, 1);

        var test = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        test.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); test.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        test.Controls.Add(pad, 0, 0);
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(16, 10, 16, 8), BackColor = Surface, Margin = Padding.Empty };
        foreach (int height in new[] { 24, 61, 40, 24, 34, 24, 34, 42 }) editor.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        editor.Controls.Add(Label("LAST DETECTED INPUT", 9, Accent, true));
        signal.AutoEllipsis = true; signal.AccessibleName = "Last detected input"; editor.Controls.Add(signal);
        editor.Controls.Add(detail);
        editor.Controls.Add(Label("Physical button / location", 10, Ink)); editor.Controls.Add(physical);
        editor.Controls.Add(Label("WoW action (optional)", 10, Ink)); editor.Controls.Add(action);
        var editorActions = Flow();
        save.Enabled = false; save.Click += (_, _) => SaveBinding(); editorActions.Controls.Add(save);
        var reset = Button("New binding"); reset.Click += (_, _) => ResetEditor(); editorActions.Controls.Add(reset);
        editor.Controls.Add(editorActions);
        test.Controls.Add(editor, 1, 0); root.Controls.Add(test, 0, 2);

        var note = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 8, 0, 0) };
        note.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        note.Controls.Add(history);
        note.Controls.Add(Label("Extra buttons may send keys (1, F13, etc.). DPI/profile buttons may send no detectable input.", 10, Muted));
        root.Controls.Add(note, 0, 3);

        var bindingTools = Flow();
        bindingTools.Controls.Add(new Label { Text = "YOUR BINDINGS", ForeColor = Accent, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 10, 18, 0) });
        AddTool(bindingTools, "Edit selected", EditSelected);
        AddTool(bindingTools, "Remove selected", RemoveSelected);
        AddTool(bindingTools, "Export…", Export);
        AddTool(bindingTools, "Import…", Import);
        root.Controls.Add(bindingTools, 0, 4);

        ConfigureGrid(); root.Controls.Add(grid, 0, 5);
        root.Controls.Add(status, 0, 6);
        Application.AddMessageFilter(this);
        Deactivate += (_, _) => { if (listening) SetListening(false); };
        FormClosed += (_, _) => Application.RemoveMessageFilter(this);
        Shown += (_, _) => LoadBindings();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ResumeLayout(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { NativeInput.RegisterMouse(Handle); inputAvailable = true; }
        catch (Win32Exception ex) { inputAvailable = false; toggle.Enabled = false; status.Text = "Mouse input could not start: " + ex.Message; }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x00FF && listening && ActiveForm == this && pad.RectangleToScreen(pad.ClientRectangle).Contains(Cursor.Position))
        {
            var mouse = NativeInput.ReadMouse(m.LParam);
            if (mouse is { } input)
                foreach (var detected in InputDecoder.Mouse(input.Flags, input.Wheel, Down(0x11), Down(0x10), Down(0x12))) Record(detected);
        }
        base.WndProc(ref m);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (!listening || ActiveForm != this || m.Msg is not (0x100 or 0x101 or 0x104 or 0x105 or 0x102 or 0x106)) return false;
        if (m.Msg is 0x102 or 0x106) return true;
        int key = m.WParam.ToInt32();
        if (m.Msg is 0x101 or 0x105) { held.Remove(key); return true; }
        if (key == 0x1B) { SetListening(false); return true; }
        if (InputDecoder.IsModifier(key) || !held.Add(key)) return true;
        var name = NativeInput.KeyName(key, m.LParam);
        Record(new(InputDecoder.Chord(name, Down(0x11), Down(0x10), Down(0x12)),
            $"Keyboard signal · VK 0x{key:X2} · mouse macro or keyboard"));
        return true;
    }

    private static bool Down(int key) => (NativeInput.GetKeyState(key) & 0x8000) != 0;

    private void Record(DetectedInput input)
    {
        last = input; signal.Text = input.Signal; detail.Text = input.Detail;
        pad.ActiveButton = input.MouseButton; pad.Invalidate();
        recent.Enqueue(input.Signal); while (recent.Count > 4) recent.Dequeue();
        history.Text = "Recent:  " + string.Join("   →   ", recent);
    }

    private void SetListening(bool value)
    {
        if (value && !inputAvailable) return;
        listening = value; held.Clear(); pad.Listening = value; pad.Invalidate();
        toggle.Text = value ? "Stop testing (Esc)" : "Start testing";
        physical.Enabled = action.Enabled = grid.Enabled = !value;
        save.Enabled = !value && last != null;
        status.Text = value ? "Listening · move into the test area · hold Ctrl / Shift / Alt to test combinations" :
            "Paused · label the detected input and save it, or start another test";
        if (value) pad.Focus();
    }

    private void SaveBinding()
    {
        SetListening(false);
        if (last == null) return;
        if (string.IsNullOrWhiteSpace(physical.Text)) { status.Text = "Give this button a name or location first."; physical.Focus(); return; }
        var row = new MouseBinding(physical.Text.Trim(), last.Signal, action.Text.Trim());
        var next = new List<MouseBinding>(bindings);
        if (editingIndex >= 0 && editingIndex < next.Count) next[editingIndex] = row;
        else
        {
            if (next.Count >= 1000) { status.Text = "Maximum 1,000 bindings. Remove a binding before adding another."; return; }
            next.Add(row);
        }
        if (!Persist(next)) return;
        bool conflict = next.Count(x => x.Signal == row.Signal) > 1;
        editingIndex = -1; save.Text = "Save binding";
        status.Text = conflict ? "Saved · another button uses this input; WoW will see the same binding." : "Saved locally · double-click a row to edit it.";
    }

    private bool Persist(List<MouseBinding> next)
    {
        try
        {
            if (!storageHealthy)
                throw new IOException("Your existing binding file could not be loaded. Back up and repair or rename the original file, then restart before saving.");
            store.Save(next); bindings = next; RefreshGrid(); return true;
        }
        catch (Exception ex) when (IsFileError(ex)) { ShowError("Could not save bindings", ex); return false; }
    }

    private void LoadBindings()
    {
        try { bindings = store.Load(); RefreshGrid(); }
        catch (Exception ex) when (IsFileError(ex))
        {
            storageHealthy = false;
            ShowError("Could not load bindings. The original file has been preserved.\n" + store.FilePath, ex);
        }
    }

    private void RefreshGrid()
    {
        grid.Rows.Clear();
        foreach (var binding in bindings) grid.Rows.Add(binding.Button, binding.Signal, binding.Action);
        grid.AccessibleDescription = $"{bindings.Count} saved bindings. Double-click a row to edit.";
    }

    private void ResetEditor()
    {
        SetListening(false); editingIndex = -1; last = null;
        signal.Text = "Waiting for input"; detail.Text = "Start a test to identify a button.";
        physical.Clear(); action.Clear(); save.Text = "Save binding"; save.Enabled = false;
        pad.ActiveButton = 0; pad.Invalidate();
    }

    private void EditSelected()
    {
        SetListening(false);
        if (grid.CurrentRow is not { } row) { status.Text = "Select a saved binding first."; return; }
        editingIndex = row.Index; var binding = bindings[editingIndex];
        last = new(binding.Signal, "Saved input · test again to replace this signal");
        signal.Text = last.Signal; detail.Text = last.Detail;
        physical.Text = binding.Button; action.Text = binding.Action;
        save.Text = "Update binding"; save.Enabled = true; physical.Focus();
    }

    private void RemoveSelected()
    {
        SetListening(false);
        if (grid.CurrentRow is not { } row) { status.Text = "Select a saved binding first."; return; }
        if (MessageBox.Show(this, $"Remove '{bindings[row.Index].Button}'?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var next = new List<MouseBinding>(bindings); next.RemoveAt(row.Index);
        if (Persist(next)) { ResetEditor(); status.Text = "Binding removed."; }
    }

    private void Export()
    {
        SetListening(false);
        using var dialog = new SaveFileDialog { Filter = "Binding backup (*.json)|*.json|Spreadsheet (*.csv)|*.csv", FileName = "wow-mouse-bindings", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (dialog.FilterIndex == 2) File.WriteAllText(dialog.FileName, BindingStore.Csv(bindings), new UTF8Encoding(true));
            else BindingStore.Write(dialog.FileName, bindings);
            status.Text = "Exported " + Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex) when (IsFileError(ex)) { ShowError("Could not export", ex); }
    }

    private void Import()
    {
        SetListening(false);
        using var dialog = new OpenFileDialog { Filter = "Binding backup (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var imported = BindingStore.Read(dialog.FileName);
            if (MessageBox.Show(this, $"Replace your current {bindings.Count} bindings with {imported.Count} imported bindings?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (Persist(imported)) { ResetEditor(); status.Text = $"Imported {imported.Count} bindings."; }
        }
        catch (Exception ex) when (IsFileError(ex)) { ShowError("Could not import", ex); }
    }

    private void ShowHelp()
    {
        SetListening(false);
        MessageBox.Show(this,
            "1. Click Start testing and move your pointer into the test area.\n" +
            "2. Press each mouse button or scroll. Try holding Ctrl, Shift, or Alt.\n" +
            "3. Press Esc or click Stop testing to keep the last detected input.\n" +
            "4. Name the physical button, add an action, and click Save binding.\n" +
            "5. In WoW's key bindings, select the action and press that same button.\n\n" +
            "BUTTON1 = left, BUTTON2 = right, BUTTON3 = wheel click, BUTTON4/5 = side inputs. The drawing is generic; physical positions vary.\n\n" +
            "MMO mice often send keyboard keys instead of extra mouse-button numbers. A key from your mouse looks the same as that key on a keyboard. The Recent line helps reveal multi-key macros. Only the last signal is saved.\n\n" +
            "No input? Check your mouse software: DPI and profile controls may not send a standard input. Assign a spare keyboard key there, then test again. Game-specific profiles may differ from desktop profiles. Horizontal scroll and some special keys may not be bindable in WoW.\n\n" +
            "Testing only runs while this window is active. Esc stops testing; system-reserved shortcuts may still act normally. This app identifies inputs and keeps a plan. Change bindings in WoW or in your mouse software.\n\n" +
            "Saved bindings: " + store.FilePath,
            "How to use WoW Mouse Mapper", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill; grid.Margin = new Padding(0, 5, 0, 4);
        grid.BackgroundColor = Surface; grid.BorderStyle = BorderStyle.None;
        grid.AllowUserToAddRows = grid.AllowUserToDeleteRows = grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true; grid.MultiSelect = false; grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(35, 46, 61), ForeColor = Ink, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(8) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Surface, ForeColor = Ink, SelectionBackColor = Color.FromArgb(35, 79, 77), SelectionForeColor = Ink, Padding = new Padding(8) };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(26, 35, 48);
        grid.GridColor = Color.FromArgb(43, 56, 72); grid.ColumnHeadersHeight = 40; grid.RowTemplate.Height = 38;
        grid.Columns.Add("physical", "Physical button / location"); grid.Columns.Add("signal", "Detected input"); grid.Columns.Add("action", "WoW action");
        foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        grid.AccessibleName = "Saved bindings";
    }

    private static Label Label(string text, float size, Color color, bool bold = false) => new()
    {
        Text = text, Dock = DockStyle.Fill, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
    };
    private static TextBox TextField(string placeholder, int maximum) => new()
    {
        Dock = DockStyle.Top, PlaceholderText = placeholder, MaxLength = maximum, BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.FromArgb(32, 43, 58), ForeColor = Ink, Margin = new Padding(0, 0, 0, 6)
    };
    private static FlowLayoutPanel Flow() => new() { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
    private static Button Button(string text, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 34, MinimumSize = new Size(100, 34),
            Padding = new Padding(12, 2, 12, 2), Margin = new Padding(0, 0, 8, 0), FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Color.FromArgb(35, 46, 61), ForeColor = primary ? Background : Ink,
            Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderSize = 0; return button;
    }
    private static void AddTool(FlowLayoutPanel panel, string text, Action click)
    { var button = Button(text); button.Click += (_, _) => click(); panel.Controls.Add(button); }
    private static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException or NotSupportedException;
    private void ShowError(string message, Exception ex)
    { status.Text = message.Split('\n')[0]; MessageBox.Show(this, message + "\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    private void OpenSupport()
    {
        SetListening(false);
        try { Process.Start(new ProcessStartInfo("https://buymeacoffee.com/darklodus") { UseShellExecute = true }); }
        catch (Win32Exception ex) { ShowError("Could not open your browser", ex); }
    }
}
