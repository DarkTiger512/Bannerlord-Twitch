# Instant adult heirs

`!adoptheir` creates one new biological child with your hero's spouse, grows them to the active game model's adulthood age, and reserves them as your future BLT heir. Your current hero stays active. After death or retirement, use `!heir` to adopt the child through the existing gold/custom-item inheritance system.

The command keeps an existing valid heir, uses native names/gender/appearance/culture, and obeys the configured `FamilyManagement.MakeKidsLimit` (default 3 living children in your clan). `AdoptHeir.GoldCost` defaults to 0. Configure it on the command in BLT settings. The default command is added only when its ID, handler and name do not conflict with administrator configuration; existing renamed/disabled commands are retained.

Use it on the campaign map with living adult opposite-sex spouses who are free and outside battle. An existing pregnancy is left untouched and blocks new creation. Mixed-race parents are rejected because the installed native offspring method requires matching races. Missing native templates or aging support fail without charging. Native skills, equipment, education and placement use the installed game's models; no TOAM culture or race names are hardcoded.

## Recovery and persistence

Existing `HeirData` remains unchanged. Optional versioned `PendingOffspringV1` records retain the child, parents, original price and lifecycle stages if processing fails. Missing equipment or settlement can be repaired and the same command retried; completed events are not repeated. The child and original price survive save/reload.

Native event listeners are not transactional. If a listener throws during creation, birth or adulthood, BLT retains the child/reference when available and blocks replay rather than risking duplicate children or duplicate mod effects. Inspect the campaign log and resolve the offending mod; these interrupted-event records require case-specific recovery, not blind retries. No gold is charged on these failures. Normal successful completion reserves the child and charges once. Reserved/pending children cannot be taken by normal adoption.

## Automated validation

Run `dotnet run --project tools/adopt-heir-tests/AdoptHeirTests.csproj`. The harness compiles the production service, native adapter, reservation behavior and actual BLT JSON persistence against engine stubs. It covers both parent genders, modded adulthood ages, two adopted parents, preflight failures, costs, interrupted events, replay prevention, recovery, save identity and non-destructive command defaults. It does not simulate the complete Bannerlord lifecycle.

## Required in-game checks (not automated)

On both Classic and TOAM, load an existing save and test male/female adopted parents, noble/wanderer and player-clan parents, supported modded races/cultures, an existing heir, pregnancy and child limits. Confirm family links/name/appearance, adulthood age, inherited skills, battle/civilian equipment, activation, placement and completed education without lingering prompts. Save/reload before and after creation; retire/die, use `!heir`, and verify clan leadership, names, gold, custom items and reservation cleanup. Test recoverable missing-equipment failure after restoring its assets and reloading. Test incompatible races and verify no child/gold change. These checks require a running game and are not established by a successful build or stub test.
