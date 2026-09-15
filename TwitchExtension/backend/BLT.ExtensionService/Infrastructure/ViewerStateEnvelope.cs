using System.Text.Json.Nodes;

namespace BLT.ExtensionService.Infrastructure;

// Game revisions restart with the game process. The reviewed viewer keeps its
// revision across reconnects, so give outgoing snapshots a server revision.
public static class ViewerStateEnvelope
{
    private static readonly object Sync = new();
    private static long revision;

    public static string Prepare(string message)
    {
        var envelope = JsonNode.Parse(message)!.AsObject();
        lock (Sync)
        {
            revision = Math.Max(revision + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            envelope["data"]!["mission"]!["revision"] = revision;
        }
        return envelope.ToJsonString();
    }
}
