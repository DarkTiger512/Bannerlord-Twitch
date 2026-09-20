import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { cleanName, parseCommands, readProfile } from "./command-profile.mjs";

const args = process.argv.slice(2);
function option(name, fallback) {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  assert(args[index + 1] && !args[index + 1].startsWith("--"), `${name} requires a ref`);
  return args[index + 1];
}
export const classicRef = option("--classic-ref", "main");
export const integrationRef = option("--integration-ref", "BLT/twitch-integration");
export const readSource = (ref, file) => execFileSync("git", ["show", `${ref}:${file}`], { encoding: "utf8" }).replace(/^\uFEFF/, "").replace(/\r\n/g, "\n");
const main = parseCommands(readProfile(classicRef));
const integration = parseCommands(readProfile(integrationRef));
const canonicalName = name => cleanName(name) === "bltbet" ? "predict" : cleanName(name);
const compared = command => ({
  name: canonicalName(command.Name), handler: command.Handler === "TournamentBet" ? "TournamentPrediction" : command.Handler, enabled: command.Enabled,
  moderatorOnly: command.ModeratorOnly, hideHelp: command.HideHelp,
  handlerConfig: command.HandlerConfig ?? {}
});

assert.equal(main.length, 63, "main must contain the authoritative profile including prestige and balance");
assert.deepEqual(integration.map(compared), main.map(compared),
  "Integration command names, handlers, permissions, enabled/help state, or handler configuration diverged from main");

const matrix = readSource(integrationRef, "docs/twitch-integration/readiness/COMMAND-PARITY.md");
const rows = [...matrix.matchAll(/^\| (?:`command\.[^`]+` \| )?`!([^`]+)` \| `([^`]+)` \|/gm)];
assert.equal(rows.length, main.length, "The live parity matrix must contain exactly one row per main command");
assert.deepEqual(rows.map(row => row[1]), main.map(command => canonicalName(command.Name)), "The live parity matrix must follow the active main profile order");
console.log(`Command parity verified: ${main.length} commands match ${classicRef} and ${integrationRef}, including the live-test matrix.`);
