using System.Text.Json.Nodes;
using BLT.ExtensionService.Infrastructure;

namespace BLT.ExtensionService.Tests;

public sealed class ViewerStateEnvelopeTests
{
    [Fact]
    public void ReconnectAndReplayAdvanceBeyondReviewedViewerDisconnectRevision()
    {
        const string snapshot = "{\"v\":1,\"kind\":\"state.snapshot\",\"channelId\":\"42\",\"data\":{\"gameStarted\":true,\"mission\":{\"revision\":5}}}";
        var first = JsonNode.Parse(ViewerStateEnvelope.Prepare(snapshot))!;
        var reconnect = JsonNode.Parse(ViewerStateEnvelope.Prepare(snapshot))!;
        var replay = JsonNode.Parse(ViewerStateEnvelope.Prepare(snapshot))!;
        long Revision(JsonNode value) => value["data"]!["mission"]!["revision"]!.GetValue<long>();
        Assert.True(Revision(reconnect) >= Revision(first) + 1);
        Assert.True(Revision(replay) > Revision(reconnect));
        Assert.True(reconnect["data"]!["gameStarted"]!.GetValue<bool>());
        Assert.Equal(5, JsonNode.Parse(snapshot)!["data"]!["mission"]!["revision"]!.GetValue<int>());
    }
}
