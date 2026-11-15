using System.Text;
using System.Text.Json;

namespace WowMouseMapper;

public record MouseBinding(string Button, string Signal, string Action);

public sealed class BindingStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string FilePath { get; } = path;

    public List<MouseBinding> Load() => File.Exists(FilePath) ? Read(FilePath) : [];

    public static List<MouseBinding> Read(string file)
    {
        if (new FileInfo(file).Length > 2_000_000) throw new InvalidDataException("This binding file is too large (maximum 2 MB).");
        var rows = JsonSerializer.Deserialize<List<MouseBinding>>(File.ReadAllText(file))
            ?? throw new InvalidDataException("The file does not contain a binding list.");
        if (rows.Count > 1000 || rows.Any(r => r is null || string.IsNullOrWhiteSpace(r.Signal) ||
            string.IsNullOrWhiteSpace(r.Button) || r.Action is null || r.Button.Length > 200 || r.Signal.Length > 200 || r.Action.Length > 500))
            throw new InvalidDataException("Invalid binding file: each entry needs a button, signal, and action; maximum 1,000 entries.");
        return rows;
    }

    public void Save(IReadOnlyList<MouseBinding> rows) => Write(FilePath, rows);

    public static void Write(string file, IReadOnlyList<MouseBinding> rows)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(file))!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".{Path.GetFileName(file)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(rows, Options), Encoding.UTF8);
            File.Move(temporary, file, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string Csv(IReadOnlyList<MouseBinding> rows)
    {
        // Prevent spreadsheet formula interpretation for user-entered labels.
        static string Cell(string value)
        {
            if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') ||
                value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return "Physical button,Detected input,WoW action\r\n" +
            string.Join("\r\n", rows.Select(r => string.Join(",", Cell(r.Button), Cell(r.Signal), Cell(r.Action)))) + "\r\n";
    }
}
