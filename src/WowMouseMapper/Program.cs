namespace WowMouseMapper;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // A separate data directory makes UI smoke tests independent of personal bindings.
        var dataDirectory = args.Length == 2 && args[0] == "--data-dir" ? args[1] :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WOW-Mouse-Mapper");
        Application.Run(new MainForm(new BindingStore(Path.Combine(dataDirectory, "bindings.json"))));
    }
}
