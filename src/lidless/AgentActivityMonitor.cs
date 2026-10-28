using System.Text.Json;

namespace Lidless;

/// <summary>
/// Observes local coding-agent session journals. An open app by itself is not an
/// active agent; a started turn without its matching completion is.
/// </summary>
internal sealed class AgentActivityMonitor
{
    private sealed record Snapshot(long Length, bool Active, DateTimeOffset LastEvent);
    private readonly Dictionary<string, Snapshot> cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    private readonly string codexRoot;
    private readonly string claudeRoot;

    internal AgentActivityMonitor(string? codexRoot = null, string? claudeRoot = null)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        this.codexRoot = codexRoot ?? Path.Combine(home, ".codex", "sessions");
        this.claudeRoot = claudeRoot ?? Path.Combine(home, ".claude", "projects");
    }

    internal string? Detect()
    {
        var codex = HasActiveSession(codexRoot, "codex");
        var claude = HasActiveSession(claudeRoot, "claude");
        return (codex, claude) switch
        {
            (true, true) => "Codex + Claude",
            (true, false) => "Codex",
            (false, true) => "Claude",
            _ => null
        };
    }

    private bool HasActiveSession(string root, string provider)
    {
        if (!Directory.Exists(root)) return false;
        try
        {
            return Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc > file.CreationTimeUtc
                    ? file.LastWriteTimeUtc : file.CreationTimeUtc)
                .Take(24)
                .Any(file => IsActive(file, provider));
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private bool IsActive(FileInfo file, string provider)
    {
        if (!cache.TryGetValue(file.FullName, out var snapshot) || snapshot.Length != file.Length)
        {
            try
            {
                var active = false;
                var lastEvent = DateTimeOffset.MinValue;
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                while (reader.ReadLine() is { } line)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var record = doc.RootElement;
                        if (record.TryGetProperty("timestamp", out var time) &&
                            DateTimeOffset.TryParse(time.GetString(), out var parsed) && parsed > lastEvent)
                            lastEvent = parsed;
                        if (!record.TryGetProperty("type", out var type)) continue;
                        if (provider == "codex" && type.GetString() == "event_msg" &&
                            record.TryGetProperty("payload", out var payload) &&
                            payload.TryGetProperty("type", out var eventType))
                        {
                            if (eventType.GetString() == "task_started") active = true;
                            if (eventType.GetString() is "task_complete" or "turn_aborted") active = false;
                        }
                        else if (provider == "claude")
                        {
                            if (type.GetString() == "user" && IsPrompt(record)) active = true;
                            else if (type.GetString() == "assistant" &&
                                record.TryGetProperty("message", out var message) &&
                                message.TryGetProperty("stop_reason", out var reason))
                            {
                                if (reason.GetString() == "end_turn") active = false;
                                else active = true;
                            }
                        }
                    }
                    catch (JsonException) { /* A partially written final line is retried next scan. */ }
                }
                snapshot = new Snapshot(file.Length, active, lastEvent);
                cache[file.FullName] = snapshot;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        return snapshot.Active && DateTimeOffset.UtcNow - snapshot.LastEvent < StaleAfter;
    }

    private static bool IsPrompt(JsonElement record)
    {
        if (!record.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content)) return false;
        if (content.ValueKind == JsonValueKind.String) return true;
        return content.ValueKind == JsonValueKind.Array && content.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.Object && item.TryGetProperty("type", out var type) &&
            type.GetString() != "tool_result");
    }
}
