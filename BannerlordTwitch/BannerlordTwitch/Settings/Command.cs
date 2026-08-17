using BannerlordTwitch.Localization;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch.Util;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using YamlDotNet.Serialization;

namespace BannerlordTwitch
{
    [LocDescription("{=8SbGSWFY}Bot command definition")]
    public class Command : ActionBase, ILoaded, ISaving
    {
        [LocDisplayName("{=D1lt1bSQ}Name"), LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=u1Ua0LXJ}The command itself, not including the !"),
         PropertyOrder(1), UsedImplicitly]
        public LocString Name { get; set; } = string.Empty;
        [LocDisplayName("{=BLT_Command_Aliases_Name}Aliases"), LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=BLT_Command_Aliases_Desc}Enter command aliases separated by commas"),
         PropertyOrder(2), UsedImplicitly]
        public string Aliases { get; set; } = string.Empty;
        [LocDisplayName("{=TpnV5Mpi}HideHelp"), LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=0AZVaBQN}Hides the command from the !help action"),
         PropertyOrder(3), UsedImplicitly]
        public bool HideHelp { get; set; }
        [LocDisplayName("{=JqNXbyIT}Help"), LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=9QV7UzFh}What to show in the !help command"),
         PropertyOrder(4), UsedImplicitly]
        public LocString Help { get; set; } = string.Empty;
        [LocDisplayName("{=Rh3cTAVq}Moderator only"), LocCategory("Permissions", "{=kJbOqjr1}Permissions"),
        LocDescription("{=6ggxMun9}Only moderators and broadcaster can use this command"),
        PropertyOrder(5), UsedImplicitly]
        public bool ModeratorOnly { get; set; }

        [YamlIgnore, Browsable(false)]
        public IReadOnlyList<string> AliasList => ParseAliases(Aliases);

        [ItemsSource(typeof(CommandHandlerItemsSource))]
        public override string Handler { get; set; }

        public bool HasName(string name) =>
            string.Equals(Name?.ToString(), name?.Trim(), StringComparison.CurrentCultureIgnoreCase);

        public bool HasAlias(string name) =>
            AliasList.Any(alias => string.Equals(alias, name?.Trim(), StringComparison.CurrentCultureIgnoreCase));

        private static List<string> ParseAliases(string aliases) =>
            (aliases ?? string.Empty)
                .Split(',')
                .Select(alias => alias.Trim())
                .Where(alias => !string.IsNullOrEmpty(alias))
                .ToList();

        private void NormalizeAliases() => Aliases = string.Join(", ", AliasList);

        public void OnLoaded(Settings settings) => NormalizeAliases();

        public void OnSaving() => NormalizeAliases();

        public override string ToString() => $"{Name} ({Handler})";
    }
}
