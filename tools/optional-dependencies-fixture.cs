using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

class OptionalDependenciesTests
{
    static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch (ReflectionTypeLoadException e)
        {
            foreach (var error in e.LoaderExceptions) Console.Error.WriteLine(error);
            return 1;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    static void Run(string[] args)
    {
        var modules = new[] { "BannerlordTwitch", "BLTAdoptAHero", "BLTBuffet", "BLTConfigure" };
        var game = args[1];
        bool naval = args[3] == "present";
        var dirs = modules.Select(m => Path.Combine(args[0], m, "bin", "Win64_Shipping_Client"))
            .Concat(new[] { Path.Combine(game, "bin", "Win64_Shipping_Client"), Path.GetDirectoryName(args[2]) })
            .Concat(new[] { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle" }
                .Select(m => Path.Combine(game, "Modules", m, "bin", "Win64_Shipping_Client"))).ToList();
        if (naval) dirs.Add(Path.Combine(game, "Modules", "NavalDLC", "bin", "Win64_Shipping_Client"));
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            var name = new AssemblyName(e.Name).Name;
            if (!naval && name.StartsWith("NavalDLC")) throw new InvalidOperationException("Unexpected DLC resolution: " + name);
            var path = dirs.Select(d => Path.Combine(d, name + ".dll")).FirstOrDefault(File.Exists);
            return path == null ? null : Assembly.LoadFrom(path);
        };

        Assembly adopted = null;
        foreach (var module in modules)
        {
            var a = Assembly.LoadFrom(Path.Combine(args[0], module, "bin", "Win64_Shipping_Client", module + ".dll"));
            if (a.GetReferencedAssemblies().Any(r => r.Name.StartsWith("NavalDLC")))
                throw new Exception(module + " has a mandatory NavalDLC assembly reference");
            Console.WriteLine(module + ": loaded " + a.GetTypes().Length + " types without a NavalDLC assembly reference");
            if (module == "BLTAdoptAHero") adopted = a;
        }
        var api = adopted.GetType("BLTAdoptAHero.Actions.OptionalNavalApi", true);
        if (api.GetMethod("TryCreate").Invoke(null, new object[] { null }) != null)
            throw new Exception("Missing mission should have no naval API");

        if (naval)
        {
            var dlc = Assembly.LoadFrom(Path.Combine(dirs.Last(), "NavalDLC.dll"));
            var logic = FormatterServices.GetUninitializedObject(dlc.GetType("NavalDLC.Missions.MissionLogics.NavalAgentsLogic", true));
            // Resolves every member used by naval summoning against the real DLC,
            // including private reservation cleanup methods, without starting a game.
            Activator.CreateInstance(api, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { logic }, null);
            Console.WriteLine("Optional naval API: all bindings match installed War Sails");
        }
        else if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("NavalDLC")))
            throw new Exception("DLC was loaded in the absent-DLC test");
        Console.WriteLine("PASS: " + args[3]);
    }
}
