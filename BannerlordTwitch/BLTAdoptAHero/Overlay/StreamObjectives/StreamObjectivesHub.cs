using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BLTAdoptAHero.Behaviors;
using BLTAdoptAHero.Util;
using BannerlordTwitch.Util;
using Microsoft.AspNet.SignalR;

namespace BLTAdoptAHero.UI
{
    public sealed class StreamObjectivesHub : Hub
    {
        private static StreamObjectiveOverlayData current = Hidden();

        public StreamObjectiveOverlayData Refresh() => current;

        public static void Publish(StreamObjectiveState state)
        {
            current = Build(state);
            try { GlobalHost.ConnectionManager.GetHubContext<StreamObjectivesHub>().Clients.All.updateObjective(current); }
            catch (Exception ex) { Log.Exception("Stream objectives overlay update", ex, noRethrow: true); }
        }

        private static StreamObjectiveOverlayData Build(StreamObjectiveState state)
        {
            var config = GlobalEventConfig.Get();
            if (state == null || config?.StreamObjectivesOverlayEnabled != true) return Hidden();
            int target = state.Kind == StreamObjectiveKind.Survive ? state.RequiredHeroes : state.Target;
            return new StreamObjectiveOverlayData
            {
                Visible = true, Kind = state.Kind.ToString(), Description = StreamObjectivePolicy.Describe(state),
                Progress = state.Progress, Target = target, Gold = state.GoldReward, XP = state.XPReward,
                Status = state.Status.ToString(), Opacity = config.StreamObjectivesOverlayOpacity,
                WidthPercent = config.StreamObjectivesOverlayWidthPercent,
                Contributors = state.Contributors.Values.OrderByDescending(c => c.Amount)
                    .ThenBy(c => c.Owner, StringComparer.OrdinalIgnoreCase)
                    .Take(Math.Max(0, config.StreamObjectivesContributorCount))
                    .Select(c => new StreamObjectiveContributorData
                    { Name = c.Owner, Amount = c.Amount, Detail = state.Kind == StreamObjectiveKind.Survive ? $"{c.SurvivalStreak}/{state.RequiredBattles}" : c.Amount.ToString() }).ToList()
            };
        }

        private static StreamObjectiveOverlayData Hidden() => new() { Version = 1, Visible = false };

        public static void Register() => BLTOverlay.BLTOverlay.Register("stream-objectives", 10,
            Content("StreamObjectives.css"), Content("StreamObjectives.html"), Content("StreamObjectives.js"));
        private static string Content(string name) => File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(typeof(StreamObjectivesHub).Assembly.Location) ?? ".", "Overlay", "StreamObjectives", name));
    }

    public sealed class StreamObjectiveOverlayData
    {
        public int Version { get; set; } = 1;
        public bool Visible { get; set; }
        public string Kind { get; set; }
        public string Description { get; set; }
        public int Progress { get; set; }
        public int Target { get; set; }
        public int Gold { get; set; }
        public int XP { get; set; }
        public string Status { get; set; }
        public float Opacity { get; set; }
        public float WidthPercent { get; set; }
        public List<StreamObjectiveContributorData> Contributors { get; set; } = new();
    }

    public sealed class StreamObjectiveContributorData
    {
        public string Name { get; set; }
        public int Amount { get; set; }
        public string Detail { get; set; }
    }
}
