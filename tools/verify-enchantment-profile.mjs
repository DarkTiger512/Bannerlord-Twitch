import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { cleanName, parseCommands, profilePath } from "./command-profile.mjs";

// Exercise the working profile, without requiring a commit or an integration port.
const commands = parseCommands(readFileSync(profilePath, "utf8"));
assert.equal(commands.length, 65);
const matches = commands.filter(command => cleanName(command.Name) === "enchant");
assert.equal(matches.length, 1);
const command = matches[0];
assert.equal(command.Handler, "EnchantWeapon");
assert.equal(command.Enabled, true);
assert.equal(command.ModeratorOnly, false);
assert.equal(command.HideHelp, false);
assert.equal(command.RespondInTwitch, true);
assert.equal(new Set(commands.map(command => command.ID)).size, commands.length);
assert.deepEqual(command.HandlerConfig, {
  DamageGain: "5", SpeedGain: "2", MissileSpeedGain: "5",
  Level1GoldCost: "100000", Level2GoldCost: "200000", Level3GoldCost: "300000",
  Level4GoldCost: "400000", Level5GoldCost: "500000",
  Level2FailurePercent: "15", Level3FailurePercent: "20",
  Level4FailurePercent: "25", Level5FailurePercent: "30",
});
// Nested settings must not be mistaken for command-level properties.
assert.equal(command.DamageGain, undefined);
console.log("Enchantment profile verified: 65 commands and all 12 configured defaults.");
