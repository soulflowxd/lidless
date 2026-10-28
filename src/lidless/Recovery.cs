using System.Text.Json;

namespace Lidless;

internal sealed record Recovery(Guid Scheme, uint Ac, uint Dc);

internal static class RecoveryStore
{
    internal static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lidless");
    internal static readonly string FilePath = Path.Combine(DirectoryPath, "recovery.json");

    internal static Recovery? Read()
    {
        if (!File.Exists(FilePath)) return null;
        return JsonSerializer.Deserialize<Recovery>(File.ReadAllText(FilePath));
    }

    internal static void Save(Recovery state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state));
        File.Move(temporary, FilePath, true);
    }

    internal static void Restore()
    {
        using var mutex = new Mutex(false, "Local\\lidless-recovery");
        mutex.WaitOne();
        try
        {
            var saved = Read();
            if (saved is null) return;
            // Restore the saved plan without switching away from a newly selected plan.
            var active = PowerPolicy.ActiveScheme() == saved.Scheme;
            var current = PowerPolicy.Read(saved.Scheme);
            PowerPolicy.Write(saved.Scheme,
                current.ac == 0 ? saved.Ac : current.ac,
                current.dc == 0 ? saved.Dc : current.dc, active);
            File.Delete(FilePath);
        }
        finally { mutex.ReleaseMutex(); }
    }
}
