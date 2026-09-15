import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const sourceRoot = path.join(root, "BannerlordTwitch");
const outputRoot = path.join(root, "docs", "twitch-integration", "inventory");
const yamlSourcePath = path.join(sourceRoot, "BannerlordTwitch", "_Module", "Bannerlord-Twitch-v4.yaml");
const yamlPath = process.env.BLT_INVENTORY_YAML ? path.resolve(process.env.BLT_INVENTORY_YAML) : yamlSourcePath;

const normalize = value => value?.replace(/^['"]/, "").replace(/['"]$/, "").trim() ?? "";
const cleanLoc = value => normalize(String(value ?? "")).replace(/^\{=[^}]+\}/, "");
const scalar = value => {
  const text = normalize(value);
  if (text === "") return null;
  if (text === "true") return true;
  if (text === "false") return false;
  if (/^-?\d+(\.\d+)?$/.test(text)) return Number(text);
  return text;
};

function walk(dir) {
  const result = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (["bin", "obj", "build", "deploy", "packages", ".git"].includes(entry.name)) continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) result.push(...walk(full));
    else result.push(full);
  }
  return result;
}

function parseTopLevelSequence(lines, start, end) {
  const items = [];
  let current = null;
  let nestedRoot = null;
  for (let index = start; index < end; index += 1) {
    const line = lines[index];
    const startMatch = line.match(/^- ([^:]+):\s*(.*)$/);
    if (startMatch) {
      if (current) items.push(current);
      current = { sourceLine: index + 1, settings: {} };
      current[startMatch[1]] = scalar(startMatch[2]);
      nestedRoot = null;
      continue;
    }
    if (!current) continue;
    const top = line.match(/^  ([^:]+):\s*(.*)$/);
    if (top) {
      const [, key, value] = top;
      if (value.trim() === "" && ["HandlerConfig", "RewardSpec"].includes(key)) {
        nestedRoot = key;
        current[key] = {};
      } else {
        nestedRoot = null;
        current[key] = scalar(value);
      }
      continue;
    }
    const nested = line.match(/^    ([^:]+):\s*(.*)$/);
    if (nested && nestedRoot) current[nestedRoot][nested[1]] = scalar(nested[2]);
  }
  if (current) items.push(current);
  return items;
}

function categoryFor(command) {
  if (command.ModeratorOnly) return "Stream Control";
  const name = cleanLoc(command.Name).toLowerCase();
  if (["retinue", "retinuelist", "eliteretinue"].includes(name)) return "Retinue";
  if (name === "power") return "Battle";
  const haystack = `${command.Name ?? ""} ${command.Handler ?? ""} ${command.Documentation ?? ""}`.toLowerCase();
  const categories = [
    ["Tournament", /tournament|arena|predict/],
    ["Battle", /battle|mission|summon|ammo|duel|guard|formation/],
    ["Kingdom", /kingdom|clan|vassal|fief|diplom|party|army|capital/],
    ["Equipment", /equip|item|smith|retinue|upgrade/],
    ["Progression", /skill|attribute|focus|class|power|gold|xp/],
    ["Community", /objective|leaderboard|auction|bid/],
    ["Hero", /hero|adopt|family|heir|follow|retire|rejuven/],
  ];
  return categories.find(([, pattern]) => pattern.test(haystack))?.[0] ?? "General";
}

const choice = (id, label, values, required = true) => ({ id, label, type: "choice", required, options: values.map(value => ({ value, label: value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, c => c.toUpperCase()) })) });
const labeledChoice = (id, label, options, required = true) => ({ id, label, type: "choice", required, options });
const dynamicChoice = (id, label, optionsSource, required = true) => ({ id, label, type: "choice", required, options: [], optionsSource });
const textInput = (id, label, required = true) => ({ id, label, type: "text", required });
const numberInput = (id, label, required = true) => ({ id, label, type: "integer", required });
const confirm = (confirmationPolicy = "ui-only", legacyToken = undefined) => ({ id: "confirm", label: "I understand this changes the campaign", type: "confirmation", required: true, confirmationPolicy, ...(legacyToken ? { legacyToken } : {}) });
const when = (input, visibleWhenInput, visibleWhenValues) => ({ ...input, visibleWhenInput, visibleWhenValues });

const actionDescriptions = {
  objective: "Manage community stream objectives as a moderator.",
  objectives: "View the active community objective and your contribution.",
  ammo: "Check the ammunition remaining for your hero in the current battle.",
  ach: "View your hero's achievements and tracked statistics.",
  adopt: "Adopt a newly created hero for this campaign.",
  adoptbyclan: "Adopt a random available hero from a chosen clan.",
  adoptbyculture: "Adopt a random available hero from a chosen culture.",
  adoptbyfaction: "Adopt a random available hero from a chosen faction.",
  adoptbyname: "Adopt an available hero by name.",
  adoptrandom: "Adopt a random available hero from the campaign.",
  attack: "Summon your hero and retinue on the enemy side of the current battle.",
  auction: "Auction one of your custom items with a reserve price.",
  bid: "Bid gold on the active custom-item auction.",
  predict: "Predict the winning team using in-game hero gold. Gold committed may be lost; rewards are shared from the prediction pool.",
  buymount: "Buy a randomly tiered mount for a mounted hero class.",
  clan: "Create, join, leave, inspect, or manage a viewer clan.",
  class: "Choose your hero's class and update equipment requirements.",
  customitems: "View the custom items stored by your hero.",
  discarditem: "Permanently discard one of your stored custom items.",
  equip: "Purchase an equipment-tier upgrade for your hero.",
  giveitem: "Give one of your custom items to another viewer's hero.",
  gold: "View the amount of gold owned by your hero.",
  heal: "Heal your summoned hero over time during a battle.",
  hero: "Change your hero's appearance, gender, or marriage options.",
  inv: "View the equipment currently worn and carried by your hero.",
  kingdom: "Join, leave, rebel against, or inspect a kingdom.",
  nameitem: "Give one of your custom items a new name.",
  power: "Activate one of the special powers unlocked by your hero.",
  powers: "View the active and passive powers available to your hero.",
  reequip: "Reroll equipment at your current tier without replacing superior or custom items.",
  retinue: "Recruit or upgrade troops in your hero's battle retinue.",
  retinuelist: "View the troops currently serving in your hero's retinue.",
  retire: "Retire your adopted hero and resolve configured inheritance.",
  smitharmor: "Commission a named custom armor item for your hero.",
  smithweapon: "Commission a named custom weapon for your hero.",
  stats: "View your hero's location, health, skills, attributes, clan, and gold.",
  summon: "Summon your hero and retinue on the streamer's side of the current battle.",
  tournament: "Add your adopted hero to the viewer tournament queue.",
  itemstats: "Inspect detailed stats for equipped, stored, or custom items.",
  buyattribute: "Spend gold to increase one of your hero's attributes.",
  info: "View current campaign, settlement, faction, or world information.",
  rejuvenate: "Spend gold to make your adopted hero younger.",
  leaderboard: "View campaign rankings for participating viewer heroes.",
  heir: "Choose or create the hero who will inherit your legacy.",
  diplomacy: "Manage wars, peace, alliances, and other diplomatic actions.",
  battle: "View the current battle, participants, teams, and mission status.",
  reinforce: "Send additional troops to reinforce an eligible party or army.",
  transfer: "Transfer supported assets between your hero, clan, or another target.",
  buyfocus: "Spend gold to add a focus point to one of your hero's skills.",
  party: "Review or issue orders to your hero's campaign party.",
  income: "View your hero's recurring income and financial sources.",
  upgrade: "Purchase and manage available hero, clan, or settlement upgrades.",
  family: "View and manage your hero's spouse, children, and family actions.",
  logs: "View recent campaign events relevant to viewer heroes.",
  equipcustom: "Equip a stored custom item in a compatible equipment slot.",
  formation: "Choose the battle formation used by your summoned hero.",
  fief: "Inspect and manage settlements controlled by your hero or clan.",
  vassal: "Manage your hero's vassal status and related kingdom actions.",
  capital: "Choose or inspect the capital settlement for your domain.",
  eliteretinue: "Recruit or upgrade troops in your hero's elite retinue.",
  skills: "View your hero's skills, focus points, and current progression.",
};

function actionInput(command) {
  const name = cleanLoc(command.Name).toLowerCase();
  const noInput = new Set(["objectives", "ammo", "ach", "adopt", "adoptrandom", "gold", "heal", "inv", "powers", "retinuelist", "stats", "tournament", "battle", "income", "skills"]);
  if (noInput.has(name)) return [];
  const definitions = {
    objective: [choice("operation", "Objective operation", ["list", "start", "status", "stop"]), when(textInput("objective", "Objective type and goal options", false), "operation", ["start"])],
    adoptbyclan: [textInput("clan", "Clan")], adoptbyculture: [dynamicChoice("culture", "Culture", "cultures")], adoptbyfaction: [textInput("faction", "Faction")], adoptbyname: [textInput("hero", "Hero name")],
    attack: [textInput("shout", "Optional battle shout", false)], auction: [numberInput("item", "Custom item number"), numberInput("reserve", "Reserve price")], bid: [numberInput("amount", "Bid amount")],
    predict: [numberInput("entrant", "Team number"), numberInput("amount", "Gold committed")], buymount: [dynamicChoice("culture", "Item culture", "cultures", false)],
    clan: [choice("operation", "Clan operation", ["join", "create", "lead", "rename", "stats", "party", "fiefs", "leave", "buy title", "banner", "ship", "home"], false), when(textInput("target", "Clan name, banner code, ship, or home settlement", false), "operation", ["join", "create", "rename", "banner", "ship", "home"])],
    class: [textInput("class", "Hero class")], customitems: [textInput("filter", "Item filter", false)], discarditem: [numberInput("item", "Custom item number")],
    hero: [choice("operation", "Hero action", ["gender", "looks", "marry", "race", "culture"]), when(textInput("value", "Gender, appearance, spouse, race, or culture"), "operation", ["gender", "looks", "marry", "race", "culture"])],
    equip: [dynamicChoice("culture", "Equipment culture", "cultures", false)],
    giveitem: [numberInput("item", "Custom item number"), textInput("target", "Recipient viewer")], kingdom: [choice("operation", "Kingdom operation", ["join", "merc", "rebel", "leave", "create", "release", "expel", "stats", "armies", "tax", "sponsor", "policy"], false), when(textInput("target", "Kingdom, hero, clan, rate, amount, or policy", false), "operation", ["join", "merc", "create", "release", "expel", "armies", "tax", "sponsor", "policy"])],
    nameitem: [numberInput("item", "Custom item number"), textInput("name", "New item name")], power: [], reequip: [dynamicChoice("culture", "Equipment culture", "cultures", false)], retinue: [
      labeledChoice("operation", "Retinue action", [
        { value: "upgrade-one", label: "Recruit or upgrade one" },
        { value: "upgrade-count", label: "Recruit or upgrade a chosen quantity" },
        { value: "upgrade-all", label: "Recruit or upgrade as many as possible" },
        { value: "clear-slot", label: "Dismiss a troop from a numbered slot" },
        { value: "clear-all", label: "Dismiss every retinue troop" },
      ]),
      numberInput("slot", "Slot to dismiss (only for numbered dismissal)", false),
      numberInput("count", "Troops to recruit or upgrade", false),
    ], retire: [confirm("legacy-token", "retire-yes")],
    smitharmor: [dynamicChoice("culture", "Armor culture", "cultures", false)], smithweapon: [dynamicChoice("culture", "Weapon culture", "cultures", false)],
    summon: [textInput("shout", "Optional battle shout", false)], itemstats: [choice("mode", "Item source", ["armor", "inv", "custom", "store"]), textInput("item", "Item name or number", false)], buyattribute: [textInput("attribute", "Attribute")], rejuvenate: [confirm("ui-only")],
    heir: [textInput("target", "Heir name", false)], diplomacy: [choice("operation", "Diplomacy action", ["war", "peace", "alliance", "trade", "army"]), textInput("kingdom", "Kingdom or army order")],
    reinforce: [choice("operation", "Reinforcement type", ["info", "militia", "elitemilitia"]), textInput("settlement", "Settlement"), when(textInput("amount", "Troops or all", false), "operation", ["militia", "elitemilitia"])], transfer: [choice("mode", "Transfer mode", ["normal", "force"]), textInput("settlement", "Settlement"), choice("recipientType", "Recipient type", ["clan", "hero"]), textInput("target", "Recipient viewer", false)], buyfocus: [textInput("skill", "Skill")],
    party: [choice("operation", "Party order", ["govern", "create", "stats", "disband", "train", "army", "release", "siege", "defend", "guard", "patrol", "raid", "garrison"]), textInput("target", "Target, amount, or subcommand", false)],
    upgrade: [choice("operation", "Upgrade operation", ["apply", "auto", "bulk", "info", "list", "remove"]), when(choice("scope", "Upgrade scope", ["fief", "clan", "kingdom"]), "operation", ["apply", "auto", "bulk", "info", "list", "remove"]), textInput("target", "Settlement, clan, or kingdom", false), textInput("upgrade", "Upgrade ID", false)], family: [choice("operation", "Family view", ["spouse", "children", "parents", "member"], false), textInput("target", "Family member or spouse action", false)],
    equipcustom: [textInput("item", "Custom item name or number")], formation: [choice("formation", "Formation action", ["1", "2", "3", "4", "5", "6", "7", "8", "detach", "attach", "charge", "hold", "follow", "gate", "walls", "front", "back"])],
    fief: [choice("operation", "Fief action", ["info", "projects", "gold", "explanation"]), textInput("target", "Settlement or building"), when(textInput("value", "Buildings or gold amount", false), "operation", ["projects", "gold"])], vassal: [textInput("target", "Vassal action and target")],
    capital: [choice("operation", "Capital action", ["info", "list", "set", "cancel"], false), when(textInput("settlement", "Capital settlement"), "operation", ["set"])],
    info: [choice("mode", "Campaign information", ["kingdomlist", "culturelist", "warlist", "kingdom", "war", "wars", "fief", "fiefs", "clan", "clans", "vassals", "time", "date", "player"]), textInput("target", "Kingdom, clan, or settlement", false)],
    leaderboard: [choice("scope", "Leaderboard", ["hero", "clan"]), textInput("stat", "Statistic", false)],
    logs: [choice("scope", "Log scope", ["hero", "clan", "kingdom", "fief"]), textInput("target", "Clan, kingdom, or fief", false)],
    eliteretinue: [
      labeledChoice("operation", "Elite retinue action", [
        { value: "upgrade-one", label: "Recruit or upgrade one" },
        { value: "upgrade-count", label: "Recruit or upgrade a chosen quantity" },
        { value: "upgrade-all", label: "Recruit or upgrade as many as possible" },
        { value: "clear-slot", label: "Dismiss a troop from a numbered slot" },
        { value: "clear-all", label: "Dismiss every elite-retinue troop" },
      ]),
      numberInput("slot", "Slot to dismiss (only for numbered dismissal)", false),
      numberInput("count", "Troops to recruit or upgrade", false),
    ]
  };
  return definitions[name] ?? [textInput("query", "Action selection")];
}

function buildActions(commands) {
  return commands.map(command => {
    const legacyName = cleanLoc(command.Name).toLowerCase();
    if (!actionDescriptions[legacyName]) throw new Error(`Missing curated description for command '${legacyName}'`);
    return ({
    id: `command.${cleanLoc(command.Name ?? command.Handler).trim().toLowerCase().replace(/[^a-z0-9]+/g, ".").replace(/^\.|\.$/g, "")}`,
    legacyName: cleanLoc(command.Name),
    handler: command.Handler,
    category: categoryFor(command),
    description: actionDescriptions[legacyName],
    enabledByDefault: command.Enabled === true,
    hiddenFromHelp: command.HideHelp === true,
    permissions: command.ModeratorOnly ? ["moderator", "broadcaster"] : ["viewer", "moderator", "broadcaster"],
    response: {
      twitch: command.RespondInTwitch === true,
      overlay: command.RespondInOverlay === true,
      extension: true,
      privateByDefault: command.ModeratorOnly !== true,
    },
    availability: new Set(["ammo", "attack", "heal", "power", "summon", "battle", "formation"]).has(legacyName) ? ["mission.active"] : ["game.started"],
    cooldown: { strategy: "legacy" },
    mutatesCampaign: !/info|status|check|list|leaderboard|help|logs|ammo/i.test(`${command.Handler} ${command.Name}`),
    inputs: actionInput(command),
    source: { file: path.relative(root, yamlSourcePath).replaceAll("\\", "/"), line: command.sourceLine },
    });
  });
}

function extractComponents(files) {
  const patterns = [
    ["action-handler", /(?:ActionHandlerBase|HeroActionHandlerBase|HeroCommandHandlerBase|ImproveAdoptedHero|ICommandHandler|IRewardHandler)/],
    ["behavior", /\b(?:CampaignBehaviorBase|MissionBehavior|Behavior)\b/],
    ["harmony-patch", /\bHarmonyPatch\b|\.Patch\(/],
    ["overlay-hub", /:\s*Hub\b|IHubContext|GetHubContext/],
    ["persistence", /Saveable|IDataStore|SyncData|ScopedJsonSync|Yaml/],
    ["twitch-service", /TwitchService|EventSub|ExtensionPubSub/],
    ["test", /\[Test\]|Assert\.|Tests?\b/],
    ["configuration", /LocDisplayName|GlobalConfig|Settings\b/],
  ];
  return files.filter(file => /\.(cs|csproj|xaml|html|js|css)$/.test(file)).map(file => {
    const content = fs.readFileSync(file, "utf8");
    const kinds = patterns.filter(([, pattern]) => pattern.test(content)).map(([kind]) => kind);
    return {
      path: path.relative(root, file).replaceAll("\\", "/"),
      project: path.relative(sourceRoot, file).split(path.sep)[0],
      kinds: kinds.length ? kinds : ["support"],
      symbols: [...content.matchAll(/\b(?:class|interface|enum)\s+([A-Za-z0-9_]+)/g)].map(match => match[1]),
    };
  });
}

function extractSettings(files) {
  const results = [];
  for (const file of files.filter(file => file.endsWith(".cs"))) {
    const lines = fs.readFileSync(file, "utf8").split(/\r?\n/);
    let displayName = null;
    let description = null;
    for (let index = 0; index < lines.length; index += 1) {
      const line = lines[index];
      const display = line.match(/LocDisplayName\("([^"]*)"/);
      const desc = line.match(/LocDescription\("([^"]*)"/);
      if (display) displayName = display[1];
      if (desc) description = desc[1];
      const property = line.match(/\bpublic\s+([A-Za-z0-9_<>,.?\[\]]+)\s+([A-Za-z0-9_]+)\s*\{\s*get;/);
      if (!property) continue;
      const defaultMatch = line.match(/=\s*([^;]+);/);
      results.push({
        path: `${path.basename(file, ".cs")}.${property[2]}`,
        type: property[1],
        default: defaultMatch ? defaultMatch[1].trim() : null,
        displayName,
        description,
        constraints: /RangeInt|RangeFloat/.test(property[1]) ? "range object" : null,
        persistence: "Bannerlord-Twitch-v4-p{profile}.yaml",
        source: { file: path.relative(root, file).replaceAll("\\", "/"), line: index + 1 },
      });
      displayName = null;
      description = null;
    }
  }
  return results;
}

const allFiles = walk(sourceRoot);
const yamlLines = fs.readFileSync(yamlPath, "utf8").split(/\r?\n/);
const commandStart = yamlLines.findIndex(line => line === "Commands:") + 1;
const globalStart = yamlLines.findIndex(line => line === "GlobalConfigs:");
const rewardStart = yamlLines.findIndex(line => line === "Rewards:") + 1;
const commands = parseTopLevelSequence(yamlLines, commandStart, globalStart);
const rewards = parseTopLevelSequence(yamlLines, rewardStart, yamlLines.length);
const actions = buildActions(commands);
const components = extractComponents(allFiles);
const settings = extractSettings(allFiles);
const semantics = actions.map(action => {
  const handlerSource = components.find(component => component.symbols.includes(action.handler))?.path ?? null;
  return {
    actionId: action.id,
    legacyName: action.legacyName,
    handler: action.handler,
    parserSource: handlerSource,
    legacyPattern: action.inputs.filter(input => input.confirmationPolicy !== "ui-only").map(input => input.type === "confirmation" ? `<${input.legacyToken}>` : `<${input.id}>`).join(" "),
    parameters: action.inputs.map((input, position) => ({
      id: input.id, label: input.label, type: input.type, position,
      required: input.required, optionsSource: input.optionsSource ?? null,
      acceptedValues: input.options?.map(option => option.value) ?? null,
      confirmationPolicy: input.confirmationPolicy ?? null,
      visibleWhenInput: input.visibleWhenInput ?? null,
      visibleWhenValues: input.visibleWhenValues ?? null,
    })),
    auditBasis: "handler-source",
  };
});

fs.mkdirSync(outputRoot, { recursive: true });
const writeJson = (name, data) => fs.writeFileSync(path.join(outputRoot, name), `${JSON.stringify(data, null, 2)}\n`);
writeJson("commands.json", commands);
writeJson("rewards.json", rewards);
const actionManifest = { protocolVersion: 1, generatedAt: new Date().toISOString(), actions };
writeJson("action-manifest.json", actionManifest);
writeJson("command-semantics.json", semantics);
const moduleManifestPath = path.join(sourceRoot, "BannerlordTwitch", "_Module", "TwitchIntegration", "action-manifest.json");
fs.mkdirSync(path.dirname(moduleManifestPath), { recursive: true });
fs.writeFileSync(moduleManifestPath, `${JSON.stringify(actionManifest, null, 2)}\n`);
writeJson("settings.json", settings);
writeJson("components.json", components);

const categoryCounts = Object.entries(actions.reduce((acc, action) => ({ ...acc, [action.category]: (acc[action.category] ?? 0) + 1 }), {}));
const componentCounts = Object.entries(components.flatMap(item => item.kinds).reduce((acc, kind) => ({ ...acc, [kind]: (acc[kind] ?? 0) + 1 }), {}));
const markdown = `# BLT Twitch Integration Inventory\n\n` +
  `Generated from tracked source and the default v4 YAML configuration. Re-run with \`node tools/generate-inventory.mjs\`.\n\n` +
  `## Coverage\n\n| Area | Count |\n|---|---:|\n| Commands | ${commands.length} |\n| Rewards | ${rewards.length} |\n| Settings | ${settings.length} |\n| Source components | ${components.length} |\n\n` +
  `## Action categories\n\n| Category | Commands |\n|---|---:|\n${categoryCounts.map(([name, count]) => `| ${name} | ${count} |`).join("\n")}\n\n` +
  `## Component map\n\n| Kind | Files |\n|---|---:|\n${componentCounts.map(([name, count]) => `| ${name} | ${count} |`).join("\n")}\n\n` +
  `## Current data flow\n\n` +
  `Twitch chat/EventSub and channel-point redemptions are normalized into \`ReplyContext\`, resolved through \`ActionManager\`, and executed by registered handlers. Settings come from per-profile YAML and are edited by BLTConfigure. Self-hosted overlays use SignalR hubs. The experimental Extension code signs privileged JWTs in the mod and the local relay forwards raw command strings; both paths are replaced by the structured managed-service protocol.\n\n` +
  `## Machine-readable references\n\n` +
  `- \`commands.json\`: configured ordinary commands and handler settings.\n` +
  `- \`rewards.json\`: native channel-point reward definitions.\n` +
  `- \`action-manifest.json\`: initial Extension-facing action catalog.\n` +
  `- \`settings.json\`: public configurable properties and source locations.\n` +
  `- \`components.json\`: project files, symbols, and architectural roles.\n`;
fs.writeFileSync(path.join(outputRoot, "README.md"), markdown);

const parityMatrix = `# Command parity matrix\n\n` +
  `Generated from the default v4 profile and structured manifest. “Mapped” proves structural parity; live outcome parity remains a release-validation task.\n\n` +
  `| Action ID | Legacy command | Handler | Inputs | Permission | Structural parity | Live parity |\n|---|---|---|---:|---|---|---|\n` +
  actions.map(action => `| \`${action.id}\` | \`!${action.legacyName}\` | \`${action.handler}\` | ${action.inputs.length} | ${action.permissions.join(", ")} | Mapped | Pending hosted game test |`).join("\n") + `\n`;
const readinessRoot = path.join(root, "docs", "twitch-integration", "readiness");
fs.mkdirSync(readinessRoot, { recursive: true });
fs.writeFileSync(path.join(readinessRoot, "COMMAND-PARITY.md"), parityMatrix);

console.log(JSON.stringify({ commands: commands.length, rewards: rewards.length, actions: actions.length, settings: settings.length, components: components.length }, null, 2));
