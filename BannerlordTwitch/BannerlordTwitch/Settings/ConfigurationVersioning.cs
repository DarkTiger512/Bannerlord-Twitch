using System;
using YamlDotNet.Serialization;

namespace BannerlordTwitch
{
    public static class ConfigurationVersioning
    {
        public const int CurrentGeneration = 2;
        public sealed class Header
        {
            public int ConfigurationGeneration { get; set; }
        }

        // Read only the header before any obsolete handler or tagged setting is converted.
        public static string SelectYaml(string yaml, Func<string> readDefaults, out bool reset)
        {
            var parser = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            int Generation(string text) => string.IsNullOrWhiteSpace(text) ? 0
                : parser.Deserialize<Header>(text)?.ConfigurationGeneration ?? 0;
            int generation = Generation(yaml);
            if (generation > CurrentGeneration)
                throw new InvalidOperationException("This configuration requires a newer BLT version.");
            reset = generation < CurrentGeneration;
            string selected = reset ? readDefaults() : yaml;
            if (Generation(selected) != CurrentGeneration)
                throw new InvalidOperationException("The installed BLT default YAML is outdated. Reinstall this release's clean default configuration.");
            return selected;
        }
    }
}
