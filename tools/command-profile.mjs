import { execFileSync } from "node:child_process";

export const profilePath = "BannerlordTwitch/BannerlordTwitch/_Module/Bannerlord-Twitch-v4.yaml";

const scalar = value => {
  const trimmed = value.trim().replace(/^['"]|['"]$/g, "");
  if (trimmed === "true") return true;
  if (trimmed === "false") return false;
  if (trimmed === "") return null;
  return trimmed;
};

export function readProfile(ref) {
  return execFileSync("git", ["show", `${ref}:${profilePath}`], { encoding: "utf8" });
}

export function parseCommands(yaml) {
  const lines = yaml.split(/\r?\n/);
  const start = lines.findIndex(line => line === "Commands:");
  const end = lines.findIndex((line, index) => index > start && /^[A-Za-z][^:]*:$/.test(line));
  const commands = [];
  let current;
  let nested;
  for (const line of lines.slice(start + 1, end < 0 ? undefined : end)) {
    const first = line.match(/^- ([^:]+):\s*(.*)$/);
    if (first) {
      if (current) commands.push(current);
      current = { HandlerConfig: {} };
      current[first[1]] = scalar(first[2]);
      nested = null;
      continue;
    }
    const property = line.match(/^  (\S[^:]*):\s*(.*)$/);
    if (property && current) {
      nested = property[1] === "HandlerConfig" && property[2].trim() === "" ? "HandlerConfig" : null;
      current[property[1]] = nested ? {} : scalar(property[2]);
      continue;
    }
    const child = line.match(/^    ([^:]+):\s*(.*)$/);
    if (child && current && nested) current[nested][child[1]] = scalar(child[2]);
  }
  if (current) commands.push(current);
  return commands;
}

export const cleanName = value => String(value ?? "").replace(/^\{=[^}]+\}/, "");
