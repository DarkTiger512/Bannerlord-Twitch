using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Util;
using Microsoft.AspNet.SignalR;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero.UI
{
    public class MapHub : Hub
    {
        private static readonly object Sync = new();
        private static GeographyData geography;
        private static string geographyKey;
        private static int geographyRevision;

        public static MapSnapshot CurrentMapData { get; private set; }

        public sealed class MapSnapshot
        {
            public GeographyData Geography { get; set; }
            public DynamicMapData Dynamic { get; set; }
        }

        public sealed class GeographyData
        {
            public int Version { get; set; } = 2;
            public int Revision { get; set; }
            public ProjectionData Projection { get; set; }
            public MapSettingsData Settings { get; set; }
            public List<KingdomData> Kingdoms { get; set; } = new();
            public List<SettlementData> Settlements { get; set; } = new();
            public List<LandArea> Land { get; set; } = new();
            public List<CoastlineSegment> Coastline { get; set; } = new();
        }

        public sealed class DynamicMapData
        {
            public int Version { get; set; } = 2;
            public bool Visible { get; set; }
            public int GeographyRevision { get; set; }
            public List<SettlementOwnershipData> Ownership { get; set; } = new();
            public List<HeroMarkerData> Heroes { get; set; } = new();
        }

        public sealed class ProjectionData
        {
            public float MinX { get; set; }
            public float MaxX { get; set; }
            public float MinY { get; set; }
            public float MaxY { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
        }

        public sealed class MapSettingsData
        {
            public string Corner { get; set; }
            public float WidthPercent { get; set; }
            public float MaxHeightPercent { get; set; }
            public float BackgroundOpacity { get; set; }
            public float TownRadius { get; set; }
            public float CastleLength { get; set; }
            public float HeroRadius { get; set; }
            public string LabelDensity { get; set; }
            public bool SpectatorCamera { get; set; }
            public float SpectatorZoom { get; set; }
            public int SpectatorIntervalSeconds { get; set; }
        }

        public sealed class KingdomData { public string Id { get; set; } public string Name { get; set; } public string Color1 { get; set; } public string Color2 { get; set; } }
        public sealed class SettlementData { public string Id { get; set; } public string Name { get; set; } public string Type { get; set; } public float X { get; set; } public float Y { get; set; } }
        public sealed class SettlementOwnershipData { public string Id { get; set; } public string KingdomId { get; set; } }
        public sealed class CoastlineSegment { public float X1 { get; set; } public float Y1 { get; set; } public float X2 { get; set; } public float Y2 { get; set; } }
        public sealed class LandArea { public float X { get; set; } public float Y { get; set; } public float Width { get; set; } public float Height { get; set; } }

        private sealed class TerrainData
        {
            public List<LandArea> Land { get; } = new();
            public List<CoastlineSegment> Coastline { get; } = new();
        }

        public sealed class HeroMarkerData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public string Color { get; set; }
            public string Status { get; set; }
            public string ClusterId { get; set; }
        }

        public override Task OnConnected()
        {
            Refresh(0);
            return base.OnConnected();
        }

        public void Refresh(int knownGeographyRevision = 0)
        {
            if (!CanShowMap())
            {
                Clients.Caller.updateMapState(new DynamicMapData { Visible = false });
                return;
            }
            EnsureGeography();
            DynamicMapData dynamicData = BuildDynamicData();
            if (geography != null && knownGeographyRevision != geography.Revision)
                Clients.Caller.updateGeography(geography);
            Clients.Caller.updateMapState(dynamicData);
            CurrentMapData = new MapSnapshot { Geography = geography, Dynamic = dynamicData };
        }

        public static void UpdateMapData()
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<MapHub>();
            if (!CanShowMap())
            {
                context.Clients.All.updateMapState(new DynamicMapData { Visible = false });
                CurrentMapData = null;
                return;
            }
            int previousRevision = geography?.Revision ?? 0;
            EnsureGeography();
            DynamicMapData dynamicData = BuildDynamicData();
            if (geography != null && geography.Revision != previousRevision)
                context.Clients.All.updateGeography(geography);
            context.Clients.All.updateMapState(dynamicData);
            CurrentMapData = new MapSnapshot { Geography = geography, Dynamic = dynamicData };
        }

        private static bool CanShowMap() => BLTAdoptAHeroModule.CommonConfig?.ShowCampaignMapOverlay == true &&
                                            Mission.Current == null && Campaign.Current?.MapSceneWrapper != null;

        private static void EnsureGeography(string reason = null)
        {
            if (!CanShowMap()) return;
            lock (Sync)
            {
                IMapScene map = Campaign.Current.MapSceneWrapper;
                map.GetMapBorders(out Vec2 min, out Vec2 max, out _);
                var ids = Campaign.Current.Settlements.Where(s => s.IsTown || s.IsCastle)
                    .Select(s => s.StringId).OrderBy(id => id, StringComparer.Ordinal);
                MapSettingsData settings = BuildSettings();
                string settingsKey = $"{settings.Corner}:{settings.WidthPercent:F2}:{settings.MaxHeightPercent:F2}:{settings.BackgroundOpacity:F2}:" +
                                     $"{settings.TownRadius:F2}:{settings.CastleLength:F2}:{settings.HeroRadius:F2}:{settings.LabelDensity}:" +
                                     $"{settings.SpectatorCamera}:{settings.SpectatorZoom:F2}:{settings.SpectatorIntervalSeconds}";
                string key = $"{Campaign.Current.GetHashCode()}:{min.x:F2}:{min.y:F2}:{max.x:F2}:{max.y:F2}:{settingsKey}:" + string.Join(",", ids);
                if (geography != null && key == geographyKey && reason == null)
                {
                    geography.Settings = BuildSettings();
                    return;
                }

                reason ??= geography == null ? "initial campaign map" : "campaign or map bounds changed";
                var timer = Stopwatch.StartNew();
                var projection = new MapProjection(min.x, max.x, min.y, max.y);
                TerrainData terrain = BuildTerrain(map, projection, min, max);
                geography = new GeographyData
                {
                    Revision = ++geographyRevision,
                    Projection = new ProjectionData { MinX = min.x, MaxX = max.x, MinY = min.y, MaxY = max.y, Width = projection.DisplayWidth, Height = projection.DisplayHeight },
                    Settings = settings,
                    Kingdoms = BuildKingdoms(),
                    Settlements = BuildSettlements(projection),
                    Land = terrain.Land,
                    Coastline = terrain.Coastline
                };
                geographyKey = key;
                timer.Stop();
                Log.Info($"[MapHub] Geography revision {geography.Revision} built in {timer.ElapsedMilliseconds}ms ({reason}); " +
                         $"bounds=({min.x:F1},{min.y:F1})-({max.x:F1},{max.y:F1}), settlements={geography.Settlements.Count}, landRuns={geography.Land.Count}, coastline={geography.Coastline.Count}");
            }
        }

        private static MapSettingsData BuildSettings()
        {
            var config = GlobalCommonConfig.Get();
            return new MapSettingsData
            {
                // Anchor the map above the activity feed, independent of its growing layout.
                Corner = CampaignMapPanelCorner.TopLeft.ToString(), WidthPercent = Clamp(config.MapWidthPercent, 15, 100),
                MaxHeightPercent = Clamp(config.MapMaxHeightPercent, 15, 100), BackgroundOpacity = Clamp(config.MapBackgroundOpacity, 0, 1),
                TownRadius = Clamp(config.MapTownRadius, .25f, 8), CastleLength = Clamp(config.MapCastleLength, .25f, 8),
                HeroRadius = Clamp(config.MapHeroRadius, .25f, 8), LabelDensity = config.MapLabelDensity.ToString(),
                SpectatorCamera = config.MapSpectatorCamera, SpectatorZoom = Clamp(config.MapSpectatorZoom, 1, 6),
                SpectatorIntervalSeconds = (int)Clamp(config.MapSpectatorIntervalSeconds, 3, 60)
            };
        }

        private static List<KingdomData> BuildKingdoms() => Campaign.Current.Kingdoms
            .Where(k => !k.IsEliminated && !string.IsNullOrEmpty(k.StringId)).OrderBy(k => k.StringId, StringComparer.Ordinal)
            .Select(k => new KingdomData { Id = k.StringId, Name = k.Name?.ToString() ?? "Unknown", Color1 = KingdomColor(k, true), Color2 = KingdomColor(k, false) }).ToList();

        private static List<SettlementData> BuildSettlements(MapProjection projection) => Campaign.Current.Settlements
            .Where(s => s.IsTown || s.IsCastle).OrderBy(s => s.StringId, StringComparer.Ordinal).Select(s =>
            {
                MapPoint p = projection.Project(s.Position.X, s.Position.Y);
                return new SettlementData { Id = s.StringId ?? s.Name?.ToString() ?? "unknown", Name = s.Name?.ToString() ?? "Unknown", Type = s.IsTown ? "Town" : "Castle", X = p.X, Y = p.Y };
            }).ToList();

        private static TerrainData BuildTerrain(IMapScene map, MapProjection projection, Vec2 min, Vec2 max)
        {
            const int width = 128, height = 96;
            var land = new bool[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                float wx = min.x + (max.x - min.x) * x / (width - 1);
                float wy = min.y + (max.y - min.y) * y / (height - 1);
                land[x, y] = SampleLand(map, wx, wy);
            }
            var result = new TerrainData();
            float cellWorldWidth = (max.x - min.x) / (width - 1);
            float cellWorldHeight = (max.y - min.y) / (height - 1);
            for (int y = 0; y < height; y++)
            {
                int runStart = -1;
                for (int x = 0; x <= width; x++)
                {
                    bool isLand = x < width && land[x, y];
                    if (isLand && runStart < 0) runStart = x;
                    if ((!isLand || x == width) && runStart >= 0)
                    {
                        MapPoint topLeft = projection.Project(min.x + runStart * cellWorldWidth - cellWorldWidth / 2,
                            min.y + y * cellWorldHeight + cellWorldHeight / 2);
                        MapPoint bottomRight = projection.Project(min.x + (x - 1) * cellWorldWidth + cellWorldWidth / 2,
                            min.y + y * cellWorldHeight - cellWorldHeight / 2);
                        result.Land.Add(new LandArea { X = topLeft.X, Y = topLeft.Y, Width = bottomRight.X - topLeft.X, Height = bottomRight.Y - topLeft.Y });
                        runStart = -1;
                    }
                }
            }
            result.Coastline.AddRange(CampaignMapGeometry.TraceContours(land, projection, min.x, max.x, min.y, max.y)
                .Select(line => new CoastlineSegment { X1 = line.A.X, Y1 = line.A.Y, X2 = line.B.X, Y2 = line.B.Y }));
            return result;
        }

        private static bool SampleLand(IMapScene map, float x, float y)
        {
            foreach (bool onLand in new[] { true, false })
            {
                var p = new CampaignVec2(new Vec2(x, y), onLand);
                var face = map.GetFaceIndex(in p);
                if (!face.IsValid()) continue;
                TerrainType terrain = map.GetFaceTerrainType(face);
                return terrain != TerrainType.Water && terrain != TerrainType.OpenSea && terrain != TerrainType.CoastalSea && terrain != TerrainType.SeaRestriction;
            }
            return false;
        }

        private static DynamicMapData BuildDynamicData()
        {
            var result = new DynamicMapData { Visible = geography != null, GeographyRevision = geography?.Revision ?? 0 };
            if (geography == null || Campaign.Current == null) return result;
            result.Ownership = Campaign.Current.Settlements.Where(s => s.IsTown || s.IsCastle)
                .Select(s => new SettlementOwnershipData { Id = s.StringId ?? s.Name?.ToString() ?? "unknown", KingdomId = s.OwnerClan?.Kingdom?.StringId })
                .OrderBy(s => s.Id, StringComparer.Ordinal).ToList();

            var projection = new MapProjection(geography.Projection.MinX, geography.Projection.MaxX, geography.Projection.MinY, geography.Projection.MaxY, geography.Projection.Height);
            foreach (Hero hero in Hero.AllAliveHeroes.Where(h => h.IsAdopted()).OrderBy(h => h.StringId))
            {
                if (!TryGetHeroPosition(hero, out Vec2 position, out string status)) continue;
                MapPoint point = projection.Project(position.x, position.y);
                result.Heroes.Add(new HeroMarkerData
                {
                    Id = hero.StringId ?? hero.Name?.ToString() ?? "hero", Name = hero.Name?.ToString() ?? "Adopted Hero",
                    X = point.X, Y = point.Y, Color = ColorToHex((hero.Clan?.Color ?? 0xFFFFFFFF) | 0xFF000000), Status = status
                });
            }
            var clusters = CampaignMapGeometry.ClusterMarkers(result.Heroes.Select(h => new MapMarkerInput { Id = h.Id, X = h.X, Y = h.Y }), 2.2f);
            foreach (HeroMarkerData hero in result.Heroes) hero.ClusterId = clusters[hero.Id];
            return result;
        }

        private static bool TryGetHeroPosition(Hero hero, out Vec2 position, out string status)
        {
            if (hero.IsPrisoner && hero.PartyBelongedToAsPrisoner?.IsMobile == true)
            { position = hero.PartyBelongedToAsPrisoner.MobileParty.GetPosition2D; status = "Prisoner"; return true; }
            if (hero.IsPrisoner && hero.PartyBelongedToAsPrisoner?.IsSettlement == true)
            { position = hero.PartyBelongedToAsPrisoner.Settlement.Position.ToVec2(); status = "Prisoner"; return true; }
            if (hero.PartyBelongedTo != null)
            { position = hero.PartyBelongedTo.GetPosition2D; status = hero.PartyBelongedTo.Army != null ? "Army" : "Travelling"; return true; }
            if (hero.CurrentSettlement != null)
            { position = hero.CurrentSettlement.Position.ToVec2(); status = "Settlement"; return true; }
            position = default; status = null; return false;
        }

        private static string KingdomColor(Kingdom kingdom, bool primary)
        {
            uint color = primary ? kingdom.Color : kingdom.Color2;
            if ((color & 0x00FFFFFF) == 0) color = primary ? kingdom.RulingClan?.Color ?? 0xFF777777 : kingdom.RulingClan?.Color2 ?? 0xFFFFFFFF;
            return ColorToHex(color | 0xFF000000);
        }

        private static string ColorToHex(uint color) => $"#{(color >> 16) & 0xFF:X2}{(color >> 8) & 0xFF:X2}{color & 0xFF:X2}";
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));

        public static void Register() => BLTOverlay.BLTOverlay.Register("campaign-map", 0, GetContent("CampaignMap.css"), GetContent("CampaignMap.html"), GetContent("CampaignMap.js"));
        private static string GetContent(string fileName) => File.ReadAllText(Path.Combine(Path.GetDirectoryName(typeof(MapHub).Assembly.Location) ?? ".", "Overlay", "CampaignMap", fileName));
    }
}
