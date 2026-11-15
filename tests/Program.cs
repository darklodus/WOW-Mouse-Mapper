using WowMouseMapper;

int count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); count++;
}
for (int i = 0; i < 5; i++)
{
    var input = InputDecoder.Mouse((ushort)(1 << (i * 2)), 0, false, false, false).Single();
    Check(input.Signal == $"BUTTON{i + 1}" && input.MouseButton == i + 1, $"Mouse button {i + 1}");
}
Check(!InputDecoder.Mouse(0x2AA, 0, false, false, false).Any(), "Button releases are not new presses");
Check(!InputDecoder.Mouse(0, 0, false, false, false).Any(), "Movement is not a binding");
Check(InputDecoder.Mouse(0x400, 120, false, false, false).Single().Signal == "MOUSEWHEELUP", "Wheel up");
Check(InputDecoder.Mouse(0x400, -120, false, false, false).Single().Signal == "MOUSEWHEELDOWN", "Wheel down");
Check(InputDecoder.Mouse(0x800, 120, false, false, false).Single().Signal == "WHEELRIGHT", "Wheel right");
Check(InputDecoder.Mouse(0x800, -120, false, false, false).Single().Signal == "WHEELLEFT", "Wheel left");
Check(!InputDecoder.Mouse(0x400, 0, false, false, false).Any(), "Zero wheel delta ignored");
Check(InputDecoder.Mouse(0x40, 0, true, true, true).Single().Signal == "CTRL + SHIFT + ALT + BUTTON4", "Modified side button");
Check(InputDecoder.Mouse(5, 0, false, false, false).Count() == 2, "Simultaneous buttons");
Check(InputDecoder.KeyName(0x7C, false, "") == "F13", "Programmable F13 key");
Check(InputDecoder.KeyName(0x31, false, "") != InputDecoder.KeyName(0x61, false, ""), "Numpad distinct from number row");
Check(InputDecoder.KeyName(0x0D, true, "") == "NUMPAD ENTER", "Numpad Enter");
Check(InputDecoder.KeyName(0x0D, false, "") == "ENTER", "Main Enter");
Check(InputDecoder.KeyName(0xBA, false, "ö") == "Ö", "Keyboard layout fallback");
Check(InputDecoder.IsModifier(0xA3) && !InputDecoder.IsModifier(0x41), "Modifier recognition");
var folder = Path.Combine(Path.GetTempPath(), "wow-mapper-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    var store = new BindingStore(Path.Combine(folder, "bindings.json"));
    Check(store.Load().Count == 0, "First launch has no bindings");
    List<MouseBinding> rows = [new("Thumb, rear", "BUTTON4", "Interrupt \"target\""), new("前", "CTRL + F13", "")];
    store.Save(rows);
    Check(store.Load().SequenceEqual(rows), "Save/reload including Unicode");
    store.Save([rows[0]]);
    Check(store.Load().Count == 1 && Directory.GetFiles(folder).Length == 1, "Atomic replacement leaves no temporary files");
    Check(BindingStore.Csv(rows).Contains("\"Thumb, rear\",\"BUTTON4\",\"Interrupt \"\"target\"\"\""), "CSV escaping");
    Check(BindingStore.Csv([new("=1+1", "F1", "@sum")]).Contains("\"'=1+1\""), "Spreadsheet formula neutralization");
    foreach (var invalid in new[] { "null", "[null]", "[{\"Button\":\"Test\"}]", "not json" })
    {
        File.WriteAllText(store.FilePath, invalid);
        bool rejected = false;
        try { store.Load(); } catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { rejected = true; }
        Check(rejected && File.ReadAllText(store.FilePath) == invalid, "Invalid file rejected and preserved: " + invalid);
    }
}
finally { Directory.Delete(folder, true); }
Console.WriteLine($"All {count} checks passed.");
