using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using TaleWorlds.Library;
using YamlDotNet.Serialization;

#if DEBUG
using System.Runtime.CompilerServices;
#endif

// ReSharper disable MemberCanBePrivate.Global
#pragma warning disable 649

namespace BannerlordTwitch
{
    // Docs here https://dev.twitch.tv/docs/api/reference#create-custom-rewards

    public class Settings : IDocumentable
    {
        public const int CurrentConfigurationGeneration = ConfigurationVersioning.CurrentGeneration;
        [Browsable(false)]
        public int ConfigurationGeneration { get; set; }

        public static Settings ReadCurrentConfiguration(string yaml, Func<string> readDefaults, out bool reset)
            => YamlHelpers.Deserialize<Settings>(ConfigurationVersioning.SelectYaml(yaml, readDefaults, out reset));

        public ObservableCollection<Reward> Rewards { get; set; } = new();
        [YamlIgnore]
        public IEnumerable<Reward> EnabledRewards => Rewards.Where(r => r.Enabled);
        public ObservableCollection<Command> Commands { get; set; } = new();
        [YamlIgnore]
        public IEnumerable<Command> EnabledCommands => Commands.Where(r => r.Enabled);
        public ObservableCollection<GlobalConfig> GlobalConfigs { get; set; } = new();
        public SimTestingConfig SimTesting { get; set; }
        [YamlIgnore, Browsable(false)]
        public IEnumerable<ActionBase> AllActions => Rewards.Cast<ActionBase>().Concat(Commands);

        public bool DisableAutomaticFulfillment { get; set; }

        public Command GetCommand(string id) => EnabledCommands.FirstOrDefault(c =>
            string.Equals(c.Name.ToString(), id, StringComparison.CurrentCultureIgnoreCase));

        public T GetGlobalConfig<T>(string id) => (T)GlobalConfigs.First(c => c.Id == id).Config;

        private static string DefaultSettingsFileName
            => Path.Combine(Path.GetDirectoryName(typeof(Settings).Assembly.Location) ?? ".",
                "..", "..", "Bannerlord-Twitch-v4.yaml");

        public static Settings DefaultSettings { get; private set; }
        public static int ActiveProfile { get; set; } = 1;
        public static bool GameStarted { get; set; } = false;

#if DEBUG
        private static string ProjectRootDir([CallerFilePath]string file = "") => Path.Combine(Path.GetDirectoryName(file) ?? ".", "..");
        private static string SaveFilePath => Path.Combine(ProjectRootDir(), "_Module", "Bannerlord-Twitch-v4.yaml");
        public static Settings Load()
        {
            
            var settings = ReadCurrentConfiguration(File.ReadAllText(SaveFilePath),
                () => File.ReadAllText(DefaultSettingsFileName), out bool reset);
            SettingsPostLoad(settings);
            if (reset) Save(settings);
            
            return settings;
        }

        public static void Save(Settings settings)
        {
            SettingsPreSave(settings);
            File.WriteAllText(SaveFilePath, YamlHelpers.Serialize(settings));
        }
        public static void ChangeProfile(int Profile)
        {
            ActiveProfile = Profile;
        }
#else

        public static Settings Load()
        {
            var profilePath = FileSystem.GetConfigPath($"Bannerlord-Twitch-v4-p{ActiveProfile}.yaml");
            string yaml = FileSystem.FileExists(profilePath) ? FileSystem.GetFileContentString(profilePath) : null;
            var settings = ReadCurrentConfiguration(yaml, () => File.ReadAllText(DefaultSettingsFileName), out bool reset);
            SettingsPostLoad(settings);
            if (reset)
            {
                Save(settings);
                Log.Info($"Profile {ActiveProfile} replaced with clean configuration generation {CurrentConfigurationGeneration}. Reapply any custom settings.");
            }
            return settings;
        }

        public static void Save(Settings settings)
        {
            SettingsPreSave(settings);
            FileSystem.SaveFileString(FileSystem.GetConfigPath($"Bannerlord-Twitch-v4-p{ActiveProfile}.yaml"), YamlHelpers.Serialize(settings));
            Log.Info($"Settings Saved to Profile {ActiveProfile} at Bannerlord-Twitch-v4-p{ActiveProfile}.yaml");
        }
        public static void ChangeProfile(int Profile)
        {
            ActiveProfile = Profile;
        }
#endif

        private static void SettingsPostLoad(Settings settings)
        {
            settings.Commands ??= new();
            settings.Rewards ??= new();
            settings.GlobalConfigs ??= new();
            settings.SimTesting ??= new();

            ActionManager.ConvertSettings(settings.Commands);
            ActionManager.ConvertSettings(settings.Rewards);
            ActionManager.EnsureGlobalSettings(settings.GlobalConfigs);

            SettingsHelpers.CallInDepth<ILoaded>(settings, config => config.OnLoaded(settings));
        }

        private static void SettingsPreSave(Settings settings)
        {
            SettingsHelpers.CallInDepth<ISaving>(settings, config => config.OnSaving());
        }

        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.Div("commands", () =>
            {
                generator.H1("{=JlFpeaxe}Commands".Translate());
                generator.Table(() =>
                {
                    generator.TR(() => generator
                        .TH("{=15umM0Xo}Command".Translate())
                        .TH("{=J6daarYb}Description".Translate())
                        .TH("{=e2Fu7JYS}Settings".Translate()));
                    foreach (var d in Commands.Where(c => c.Enabled))
                    {
                        generator.TR(() =>
                        {
                            generator.TD(d.Name.ToString());
                            generator.TD(string.IsNullOrEmpty(d.Documentation.ToString())
                                ? d.Help.ToString()
                                : d.Documentation.ToString());
                            generator.TD(() =>
                            {
                                if (d.HandlerConfig is IDocumentable doc)
                                {
                                    doc.GenerateDocumentation(generator);
                                }
                                else if (d.HandlerConfig != null)
                                {
                                    DocumentationHelpers.AutoDocument(generator, d.HandlerConfig);
                                }
                            });
                        });
                    }
                });
            });
            generator.Br();
            generator.Div("rewards", () =>
            {
                generator.H1("{=u6xsREDY}Channel Point Rewards".Translate());
                generator.Table(() =>
                {
                    generator.TR(() => generator
                        .TH("{=15umM0Xo}Command".Translate())
                        .TH("{=J6daarYb}Description".Translate())
                        .TH("{=e2Fu7JYS}Settings".Translate()));
                    foreach (var r in Rewards.Where(r => r.Enabled))
                    {
                        generator.TR(() =>
                        {
                            generator.TD(r.RewardSpec.Title.ToString());
                            generator.TD(string.IsNullOrEmpty(r.Documentation.ToString())
                                ? r.RewardSpec.Prompt?.ToString() : r.Documentation.ToString());
                            generator.TD(() =>
                            {
                                if (r.HandlerConfig is IDocumentable doc)
                                {
                                    doc.GenerateDocumentation(generator);
                                }
                                else if (r.HandlerConfig != null)
                                {
                                    DocumentationHelpers.AutoDocument(generator, r.HandlerConfig);
                                }
                            });
                        });
                    }
                });
            });
            generator.Br();
            generator.Div("global-configs", () =>
            {
                foreach (var g in GlobalConfigs.Select(c => c.Config).OfType<IDocumentable>())
                {
                    g.GenerateDocumentation(generator);
                }
            });
        }

    }
}
