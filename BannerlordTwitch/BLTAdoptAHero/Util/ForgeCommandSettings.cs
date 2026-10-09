using System.ComponentModel;

namespace BLTAdoptAHero.Util
{
    public sealed class ForgeCommandSettings : ForgeSettings
    {
        [Category("Settings"), DisplayName("Use shared forging settings"),
         Description("True uses Weapon Forging in Global Configs. Set false to use the prices and weapon balance shown on this command.")]
        public bool UseGlobalSettings { get; set; } = true;

        // Retain the old configuration for reference; its old random power system is retired.
        [Browsable(false)]
        public object LegacySmithSettings { get; set; }

        public ForgeSettings Resolve() => UseGlobalSettings ? GlobalForgeConfig.Get() : this;
    }
}
