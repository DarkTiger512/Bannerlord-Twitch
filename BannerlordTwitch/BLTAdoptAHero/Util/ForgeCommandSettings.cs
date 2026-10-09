using System.ComponentModel;

namespace BLTAdoptAHero.Util
{
    public sealed class ForgeCommandSettings : ForgeSettings
    {
        // Retain the old configuration for reference; its old random power system is retired.
        [Browsable(false)]
        public object LegacySmithSettings { get; set; }
    }
}
