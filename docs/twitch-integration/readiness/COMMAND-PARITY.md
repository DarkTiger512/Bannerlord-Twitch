# Command parity matrix

Generated from the default v4 profile and structured manifest. “Mapped” proves structural parity; live outcome parity remains a release-validation task.

| Action ID | Legacy command | Handler | Inputs | Permission | Structural parity | Live parity |
|---|---|---|---:|---|---|---|
| `command.objective` | `!objective` | `StreamObjectiveAdminCommand` | 2 | moderator, broadcaster | Mapped | Pending hosted game test |
| `command.objectives` | `!objectives` | `StreamObjectivesStatusCommand` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.ammo` | `!ammo` | `CheckAmmo` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.ach` | `!ach` | `HeroInfoCommand` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.adopt` | `!adopt` | `AdoptAHero` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.adoptbyclan` | `!adoptByClan` | `AdoptAHero` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.adoptbyculture` | `!adoptByCulture` | `AdoptAHero` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.adoptbyfaction` | `!adoptByFaction` | `AdoptAHero` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.adoptbyname` | `!adoptByName` | `AdoptAHero` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.adoptrandom` | `!adoptRandom` | `AdoptAHero` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.attack` | `!attack` | `SummonHero` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.auction` | `!auction` | `AuctionItem` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.bid` | `!bid` | `BidOnItem` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.predict` | `!predict` | `TournamentPrediction` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.buymount` | `!buymount` | `SmithItem` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.clan` | `!clan` | `ClanManagement` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.class` | `!class` | `SetHeroClass` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.customitems` | `!customitems` | `HeroInfoCommand` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.discarditem` | `!discarditem` | `DiscardItem` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.equip` | `!equip` | `EquipHero` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.giveitem` | `!giveitem` | `GiveItem` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.gold` | `!gold` | `HeroInfoCommand` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.heal` | `!heal` | `CharacterEffect` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.hero` | `!hero` | `HeroFeatures` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.inv` | `!inv` | `HeroInfoCommand` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.kingdom` | `!kingdom` | `KingdomManagement` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.nameitem` | `!nameitem` | `NameItem` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.power` | `!power` | `UsePower` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.powers` | `!powers` | `HeroInfoCommand` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.reequip` | `!reequip` | `EquipHero` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.retinue` | `!retinue` | `Retinue` | 3 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.retinuelist` | `!retinuelist` | `HeroInfoCommand` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.retire` | `!retire` | `RetireMyHero` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.smitharmor` | `!smitharmor` | `SmithItem` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.smithweapon` | `!smithweapon` | `SmithItem` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.stats` | `!stats` | `HeroInfoCommand` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.summon` | `!summon` | `SummonHero` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.tournament` | `!tournament` | `JoinTournament` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.itemstats` | `!itemstats` | `ItemStats` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.buyattribute` | `!buyattribute` | `AttributePoints` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.info` | `!info` | `CampaignInfo` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.rejuvenate` | `!rejuvenate` | `Rejuvenate` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.leaderboard` | `!leaderboard` | `Leaderboard` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.heir` | `!heir` | `HeirCommand` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.diplomacy` | `!diplomacy` | `Diplomacy` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.battle` | `!battle` | `BattleInfo` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.reinforce` | `!reinforce` | `ReinforceAction` | 3 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.transfer` | `!transfer` | `TransferAction` | 4 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.buyfocus` | `!buyfocus` | `FocusPoints` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.party` | `!party` | `PartyManagement` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.income` | `!income` | `GoldIncomeAction` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.upgrade` | `!upgrade` | `UpgradeAction` | 4 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.family` | `!family` | `FamilyManagement` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.logs` | `!logs` | `CampaignLogs` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.equipcustom` | `!equipcustom` | `EquipCustomItemAction` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.formation` | `!formation` | `FormationCommand` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.fief` | `!fief` | `ManageFief` | 3 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.vassal` | `!vassal` | `VassalManagement` | 1 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.capital` | `!capital` | `CapitalAction` | 2 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.eliteretinue` | `!eliteretinue` | `Retinue2` | 3 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
| `command.skills` | `!skills` | `HeroInfoCommand` | 0 | viewer, moderator, broadcaster | Mapped | Pending hosted game test |
