using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class VerifyCleanConfig
{
    static string modules;
    static string game;
    static object Property(object obj, string name) { return obj.GetType().GetProperty(name).GetValue(obj); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        foreach (string directory in Directory.GetDirectories(modules).Select(m => Path.Combine(m, "bin", "Win64_Shipping_Client")).Concat(new[] { game }).Concat(Directory.GetDirectories(Path.Combine(game, "..", "..", "Modules")).Select(m => Path.Combine(m, "bin", "Win64_Shipping_Client"))))
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path)) return Assembly.LoadFrom(path);
        }
        return null;
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            modules = Path.GetFullPath(args[0]);
            game = Path.GetFullPath(args[1]);
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            var assemblies = new[] { "BannerlordTwitch", "BLTAdoptAHero", "BLTBuffet", "BLTConfigure" }
                .Select(m => Assembly.LoadFrom(Path.Combine(modules, m, "bin", "Win64_Shipping_Client", m + ".dll"))).ToArray();
            var core = assemblies[0];
            var settingsType = core.GetType("BannerlordTwitch.Settings", true);
            var yamlType = core.GetType("BannerlordTwitch.Util.YamlHelpers", true);
            var convert = yamlType.GetMethod("ConvertObject", new[] { typeof(object), typeof(Type) });
            var serialize = yamlType.GetMethod("Serialize", new[] { typeof(object) });
            string yaml = File.ReadAllText(Path.Combine(modules, "BannerlordTwitch", "Bannerlord-Twitch-v4.yaml"));
            object[] readArgs = { "Commands: [!ObsoleteHandler {Name: old}]", new Func<string>(() => yaml), false };
            var read = settingsType.GetMethod("ReadCurrentConfiguration");
            object settings = read.Invoke(null, readArgs);
            Check((bool)readArgs[2], "Old profile was not reset.");
            var commands = ((IEnumerable)Property(settings, "Commands")).Cast<object>().ToList();
            Check(commands.Count == 65, "Unexpected default command count.");
            Check(commands.Select(c => Property(c, "ID")).Distinct().Count() == commands.Count, "Duplicate command IDs.");
            var handlerInterface = core.GetType("BannerlordTwitch.Rewards.ICommandHandler", true);
            var handlers = assemblies.SelectMany(a => a.GetTypes()).Where(t => handlerInterface.IsAssignableFrom(t) && !t.IsAbstract).ToDictionary(t => t.Name);
            Check(handlers.ContainsKey("TournamentPrediction") && !handlers.ContainsKey("TournamentBet"), "Prediction registration mismatch.");
            foreach (var command in commands)
            {
                string handlerName = (string)Property(command, "Handler");
                Check(handlers.ContainsKey(handlerName), "Missing handler: " + handlerName);
                object handler = Activator.CreateInstance(handlers[handlerName]);
                Type configType = (Type)handlerInterface.GetProperty("HandlerConfigType").GetValue(handler);
                object config = Property(command, "HandlerConfig");
                if (configType != null)
                    command.GetType().GetProperty("HandlerConfig").SetValue(command,
                        config == null ? Activator.CreateInstance(configType) : convert.Invoke(null, new[] { config, configType }));
            }
            foreach (string handlerName in new[] { "EnchantWeapon", "BalanceCommand", "CheckAmmo", "PrestigeCommand", "TournamentPrediction" })
                Check(commands.Count(c => (string)Property(c, "Handler") == handlerName && (bool)Property(c, "Enabled")) == 1, "Missing/duplicate enabled default: " + handlerName);
            var simGold = commands.Single(c => (string)Property(c, "Handler") == "SimGold");
            Check(!(bool)Property(simGold, "Enabled") && (bool)Property(simGold, "ModeratorOnly"), "Unsafe SimGold defaults.");
            var prestige = commands.Single(c => (string)Property(c, "Handler") == "PrestigeCommand");
            var prestigeConfig = Property(prestige, "HandlerConfig");
            Check((int)Property(prestigeConfig, "BaseKills") == 500 && (int)Property(prestigeConfig, "BaseGold") == 5000000, "Prestige defaults missing.");
            Check(TypeDescriptor.GetProperties(prestigeConfig)["BaseKills"].DisplayName == "Base Kills", "Prestige field is not readable.");
            var alias = Activator.CreateInstance(prestige.GetType());
            alias.GetType().GetProperty("Handler").SetValue(alias, "PrestigeCommand");
            alias.GetType().GetProperty("HandlerConfig").SetValue(alias, Activator.CreateInstance(prestigeConfig.GetType()));
            ((IList)Property(settings, "Commands")).Add(alias);
            prestigeConfig.GetType().GetMethod("OnLoaded").Invoke(prestigeConfig, new[] { settings });
            Check(ReferenceEquals(Property(alias, "HandlerConfig"), prestigeConfig), "Prestige aliases diverge.");
            ((IList)Property(settings, "Commands")).Remove(alias);
            var globals = ((IEnumerable)Property(settings, "GlobalConfigs")).Cast<object>().ToList();
            var classGlobal = globals.Single(c => (string)Property(c, "Id") == "Adopt A Hero - Class Config");
            var classType = assemblies[1].GetType("BLTAdoptAHero.GlobalHeroClassConfig", true);
            var classes = convert.Invoke(null, new[] { Property(classGlobal, "Config"), classType });
            var infantry = ((IEnumerable)Property(classes, "ClassDefs")).Cast<object>()
                .Single(c => (Guid)Property(c, "ID") == new Guid("9fa426af-af3e-41f6-9b48-0ffef6dc3553"));
            Check((string)Property(infantry, "Formation") == "Infantry", "Default Infantry class is not infantry.");
            foreach (string name in new[] { "Retinue", "Retinue2" })
            {
                var command = commands.Single(c => (string)Property(c, "Handler") == name);
                var config = Property(Property(command, "HandlerConfig"), name);
                Check((bool)Property(config, "HireByHeroClass"), "Class guidance is disabled for " + name);
            }
            // Exercise the actual Retinue settings resolver without connecting Twitch.
            var retinueType = assemblies[1].GetType("BLTAdoptAHero.Retinue", true);
            var currentRetinue = retinueType.GetProperty("CurrentSettings", BindingFlags.Static | BindingFlags.NonPublic);
            var primary = commands.Single(c => (string)Property(c, "Handler") == "Retinue");
            var primaryConfig = Property(Property(primary, "HandlerConfig"), "Retinue");
            var fallbackConfig = currentRetinue.GetValue(null);
            foreach (var property in primaryConfig.GetType().GetProperties().Where(p => p.CanRead && p.CanWrite))
                Check(Equals(property.GetValue(primaryConfig), property.GetValue(fallbackConfig)), "Retinue fallback differs from shipped default: " + property.Name);
            var serviceType = core.GetType("BannerlordTwitch.Twitch.TwitchService", false)
                ?? core.GetTypes().Single(t => t.Name == "TwitchService");
            var service = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(serviceType);
            GC.SuppressFinalize(service);
            serviceType.GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(service, settings);
            var serviceProperty = core.GetType("BannerlordTwitch.BLTModule", true).GetProperty("TwitchService", BindingFlags.Static | BindingFlags.Public);
            var commandList = (IList)Property(settings, "Commands");
            var retinueAlias = Activator.CreateInstance(primary.GetType());
            retinueAlias.GetType().GetProperty("Handler").SetValue(retinueAlias, "Retinue");
            retinueAlias.GetType().GetProperty("ID").SetValue(retinueAlias, Guid.Empty);
            retinueAlias.GetType().GetProperty("HandlerConfig").SetValue(retinueAlias, Activator.CreateInstance(Property(primary, "HandlerConfig").GetType()));
            commandList.Add(retinueAlias);
            serviceProperty.GetSetMethod(true).Invoke(null, new[] { service });
            try
            {
                var secondary = commands.Single(c => (string)Property(c, "Handler") == "Retinue2");
                var secondaryConfig = Property(Property(secondary, "HandlerConfig"), "Retinue2");
                var guidance = assemblies[1].GetType("BLTAdoptAHero.Retinue2", true).GetProperty("ClassGuidanceEnabled", BindingFlags.Static | BindingFlags.NonPublic);
                secondaryConfig.GetType().GetProperty("HireByHeroClass").SetValue(secondaryConfig, false);
                Check(!(bool)guidance.GetValue(null), "Secondary class changes ignore disabled guidance.");
                secondaryConfig.GetType().GetProperty("HireByHeroClass").SetValue(secondaryConfig, true);
                Check((bool)guidance.GetValue(null), "Secondary guidance does not re-enable.");
                Check(ReferenceEquals(currentRetinue.GetValue(null), primaryConfig), "Retinue alias displaced primary settings.");
                commandList.Remove(primary);
                Check(ReferenceEquals(currentRetinue.GetValue(null), Property(Property(retinueAlias, "HandlerConfig"), "Retinue")), "Retinue alias fallback failed.");
                commandList.Remove(retinueAlias);
                Check(!(bool)Property(currentRetinue.GetValue(null), "UseEliteTroops"), "Missing command did not use shipped retinue defaults.");
            }
            finally
            {
                serviceProperty.GetSetMethod(true).Invoke(null, new object[] { null });
                commandList.Remove(retinueAlias);
                if (!commandList.Contains(primary)) commandList.Add(primary);
            }
            var eventConfig = globals.Single(c => (string)Property(c, "Id") == "Adopt A Hero - Events");
            Type eventType = assemblies[1].GetType("BLTAdoptAHero.GlobalEventConfig", true);
            object events = convert.Invoke(null, new[] { Property(eventConfig, "Config"), eventType });
            Check((int)Property(events, "CursedArtifactRequiredWins") == 5 && (bool)Property(events, "StreamObjectivesEnabled"), "Event defaults missing.");
            Type commonType = assemblies[1].GetType("BLTAdoptAHero.GlobalCommonConfig", true);
            var commonGlobal = globals.Single(c => (string)Property(c, "Id") == "Adopt A Hero - General Config");
            var common = convert.Invoke(null, new[] { Property(commonGlobal, "Config"), commonType });
            Check(!(bool)Property(common, "ShowCampaignMapOverlay") && !(bool)Property(Activator.CreateInstance(commonType), "ShowCampaignMapOverlay"), "Campaign map must default off in YAML and code.");
            Check(commonType.GetProperty("Prestige") == null && commonType.GetProperty("RandomEventsEnabled") == null, "Settings remain in Common Config.");
            Check(core.GetType("BannerlordTwitch.ActionBase", true).GetProperty("RespondInExtension") == null, "Obsolete response option remains.");
            // Persist a customization and load it twice through the production selector/serializer.
            prestigeConfig.GetType().GetProperty("BaseKills").SetValue(prestigeConfig, 777);
            for (int i = 0; i < 2; i++)
            {
                string saved = (string)serialize.Invoke(null, new[] { settings });
                Check(!saved.Contains("RespondInExtension"), "Obsolete response option serialized.");
                object[] reload = { saved, new Func<string>(() => { throw new Exception("Defaults should not reload."); }), false };
                settings = read.Invoke(null, reload);
                Check(!(bool)reload[2], "Current profile reset on reload.");
                object c = ((IEnumerable)Property(settings, "Commands")).Cast<object>().Single(x => (string)Property(x, "Handler") == "PrestigeCommand");
                var pc = convert.Invoke(null, new[] { Property(c, "HandlerConfig"), prestigeConfig.GetType() });
                Check((int)Property(pc, "BaseKills") == 777, "Customization lost.");
            }
            Console.WriteLine("PASS: production YAML reset/round-trip, 65 registered commands, defaults, Prestige aliases/labels, Events, removed response option.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            for (Exception e = ex; e != null; e = e.InnerException)
                if (e is ReflectionTypeLoadException)
                    foreach (var loader in ((ReflectionTypeLoadException)e).LoaderExceptions) Console.Error.WriteLine(loader.Message);
            return 1;
        }
    }
}
