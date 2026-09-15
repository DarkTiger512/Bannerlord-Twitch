import { useEffect, useState } from "react";
import type { CommandActivity, GameState, InventorySnapshot, RetinueSnapshot, ViewerIdentity } from "../types";
import { isLiveLocalIntegration, isLocalHost } from "../environment";

const initialState: GameState = {
  connected: true, gameStarted: true, unavailable: {}, cooldowns: {}, selectors: { cultures: ["Vlandia", "Calradic Empire", "Realm of Thrones"], heroes: [], clans: [], kingdoms: [], settlements: [], skills: [] },
  commands: [], viewer: { adopted: true, heroId: "rowan", heroName: "FNC_Chair [BLT]", gold: 50000 },
  mission: {
    active: true, kind: "battle", revision: 12, deploymentFinished: true,
    actionAvailability: { "command.summon": null, "command.attack": null, "command.heal": null, "command.power": null, "command.formation": null },
    combatants: [
      { id: "rowan", name: "Rowan", hp: 86, maxHp: 112, state: "active", isPlayerSide: true, tournamentTeam: -1, cooldownFractionRemaining: .18, cooldownSecondsRemaining: 9, activePowerName: "War Cry", activePowerActive: true, activePowerFractionRemaining: .42, kills: 6, retinue: 4, deadRetinue: 1, eliteRetinue: 2, deadEliteRetinue: 0, retinueKills: 11, goldEarned: 1840, xpEarned: 920, ammoCurrent: 18, ammoMaximum: 32 },
      { id: "shieldmaiden", name: "Shieldmaiden", hp: 54, maxHp: 100, state: "active", isPlayerSide: true, tournamentTeam: -1, cooldownFractionRemaining: 0, cooldownSecondsRemaining: 0, activePowerFractionRemaining: 0, kills: 3, retinue: 2, deadRetinue: 2, eliteRetinue: 0, deadEliteRetinue: 0, retinueKills: 4, goldEarned: 760, xpEarned: 380, ammoCurrent: 0, ammoMaximum: 0 },
      { id: "blackwolf", name: "BlackWolf", hp: 31, maxHp: 105, state: "active", isPlayerSide: false, tournamentTeam: -1, cooldownFractionRemaining: .6, cooldownSecondsRemaining: 28, activePowerFractionRemaining: 0, kills: 4, retinue: 1, deadRetinue: 4, eliteRetinue: 1, deadEliteRetinue: 1, retinueKills: 7, goldEarned: 1030, xpEarned: 515, ammoCurrent: 6, ammoMaximum: 24 },
      { id: "ironstag", name: "IronStag", hp: 72, maxHp: 98, state: "active", isPlayerSide: true, tournamentTeam: -1, cooldownFractionRemaining: 0, cooldownSecondsRemaining: 0, activePowerFractionRemaining: .2, kills: 2, retinue: 3, deadRetinue: 0, eliteRetinue: 0, deadEliteRetinue: 0, retinueKills: 3, goldEarned: 510, xpEarned: 260, ammoCurrent: 12, ammoMaximum: 20 },
      { id: "ravenna", name: "Ravenna", hp: 44, maxHp: 91, state: "active", isPlayerSide: true, tournamentTeam: -1, cooldownFractionRemaining: 0, cooldownSecondsRemaining: 0, activePowerFractionRemaining: 0, kills: 1, retinue: 1, deadRetinue: 1, eliteRetinue: 0, deadEliteRetinue: 0, retinueKills: 2, goldEarned: 290, xpEarned: 145, ammoCurrent: 0, ammoMaximum: 0 },
      { id: "oakheart", name: "Oakheart", hp: 80, maxHp: 104, state: "active", isPlayerSide: true, tournamentTeam: -1, cooldownFractionRemaining: 0, cooldownSecondsRemaining: 0, activePowerFractionRemaining: 0, kills: 3, retinue: 2, deadRetinue: 0, eliteRetinue: 1, deadEliteRetinue: 0, retinueKills: 5, goldEarned: 870, xpEarned: 435, ammoCurrent: 8, ammoMaximum: 18 },
      { id: "grimhollow", name: "GrimHollow", hp: 67, maxHp: 110, state: "active", isPlayerSide: false, tournamentTeam: -1, cooldownFractionRemaining: 0, cooldownSecondsRemaining: 0, activePowerFractionRemaining: .65, kills: 5, retinue: 4, deadRetinue: 2, eliteRetinue: 0, deadEliteRetinue: 0, retinueKills: 6, goldEarned: 1320, xpEarned: 660, ammoCurrent: 0, ammoMaximum: 0 },
      { id: "silverwolf", name: "SilverWolf", hp: 0, maxHp: 103, state: "unconscious", isPlayerSide: false, tournamentTeam: -1, cooldownFractionRemaining: 0, cooldownSecondsRemaining: 0, activePowerFractionRemaining: 0, kills: 2, retinue: 0, deadRetinue: 3, eliteRetinue: 0, deadEliteRetinue: 1, retinueKills: 4, goldEarned: 640, xpEarned: 320, ammoCurrent: 0, ammoMaximum: 0 },
      { id: "viper", name: "Viper", hp: 59, maxHp: 94, state: "active", isPlayerSide: false, tournamentTeam: -1, cooldownFractionRemaining: .15, cooldownSecondsRemaining: 7, activePowerFractionRemaining: 0, kills: 3, retinue: 2, deadRetinue: 1, eliteRetinue: 0, deadEliteRetinue: 0, retinueKills: 3, goldEarned: 720, xpEarned: 360, ammoCurrent: 14, ammoMaximum: 26 },
    ],
  },
};

export function useIntegrationState(identity: ViewerIdentity | null) {
  const [state, setState] = useState<GameState>(() => (!isLocalHost() || isLiveLocalIntegration())
    ? { connected: false, gameStarted: false, unavailable: {}, cooldowns: {}, selectors: { cultures: [], heroes: [], clans: [], kingdoms: [], settlements: [], skills: [] }, commands: [], viewer: { adopted: false }, mission: { active: false, kind: "inactive", revision: 0, deploymentFinished: false, combatants: [], actionAvailability: {} } }
    : new URLSearchParams(window.location.search).get("mission") === "inactive"
    ? { ...initialState, mission: { active: false, kind: "inactive", revision: 0, deploymentFinished: false, combatants: [], actionAvailability: {} } }
    : initialState);
  const [inventory, setInventory] = useState<InventorySnapshot>();
  const [inventoryError, setInventoryError] = useState<string>();
  const [retinue, setRetinue] = useState<RetinueSnapshot>();
  const [retinueError, setRetinueError] = useState<string>();
  const [commandActivity, setCommandActivity] = useState<CommandActivity[]>([]);
  useEffect(() => {
    if (!identity || (identity.token === "development-token" && !isLiveLocalIntegration())) return;
    let disposed = false;
    let socket: WebSocket | undefined;
    let reconnectTimer: number | undefined;
    let reconnectDelay = 1000;
    const apiBase = import.meta.env.VITE_BLT_API_URL ?? window.location.origin;
    const url = new URL(`/ws/viewer/${encodeURIComponent(identity.channelId)}`, apiBase);
    url.protocol = url.protocol === "https:" ? "wss:" : "ws:";
    url.searchParams.set("token", identity.token);
    url.searchParams.set("hud", "hero-id-v1");
    const handleMessage = (event: MessageEvent) => {
      let envelope: { v?: number; id?: string; channelId?: string; kind?: string; timestamp?: string; data?: Record<string, any> };
      try { envelope = JSON.parse(String(event.data)); } catch { return; }
      if (envelope.v !== 1 || envelope.channelId !== identity.channelId || !envelope.data) return;
      const data = envelope.data;
      if (envelope.kind === "connection.status") {
        setState(value => ({ ...value, ...data, viewer: data.connected && data.gameStarted !== false ? value.viewer : { adopted: false }, mission: data.connected && data.gameStarted !== false ? value.mission : { active: false, kind: "inactive", revision: value.mission.revision + 1, deploymentFinished: false, combatants: [], actionAvailability: {} } }));
      } else if (envelope.kind === "state.snapshot" || envelope.kind === "state.patch") {
        setState(value => {
          const nextMission = data.mission;
          if (nextMission && nextMission.revision < value.mission.revision) return value;
          return { ...value, ...data, mission: nextMission ? { ...value.mission, ...nextMission } : value.mission };
        });
      } else if (envelope.kind === "viewer.state") {
        setState(value => ({ ...value, viewer: { adopted: Boolean(data.adopted), heroId: typeof data.heroId === "string" ? data.heroId : undefined, heroName: data.heroName, gold: typeof data.gold === "number" ? data.gold : undefined } }));
      } else if (envelope.kind === "inventory.snapshot") {
        const rawItems = Array.isArray(data.items) ? data.items : Array.isArray(data.Items) ? data.Items : [];
        const rawSlots = Array.isArray(data.slots) ? data.slots : Array.isArray(data.Slots) ? data.Slots : [];
        setInventory({
          heroName: String(data.heroName ?? data.HeroName ?? ""),
          limit: Number(data.limit ?? data.Limit ?? 0),
          items: rawItems.map((item: Record<string, unknown>) => ({ index: Number(item.index ?? item.Index ?? 0), name: String(item.name ?? item.Name ?? ""), type: String(item.type ?? item.Type ?? ""), equipped: Boolean(item.equipped ?? item.Equipped) })),
          slots: rawSlots.map((slot: Record<string, unknown>) => ({ id: String(slot.id ?? slot.Id ?? ""), label: String(slot.label ?? slot.Label ?? ""), accepts: String(slot.accepts ?? slot.Accepts ?? "item"), itemName: slot.itemName ?? slot.ItemName, customItemIndex: slot.customItemIndex ?? slot.CustomItemIndex })),
          updatedAt: envelope.timestamp,
        } as InventorySnapshot);
        setInventoryError(undefined);
      } else if (envelope.kind === "inventory.error") {
        setInventoryError(data.error);
      } else if (envelope.kind === "retinue.snapshot") {
        const rawRetinue = Array.isArray(data.retinue) ? data.retinue : Array.isArray(data.Retinue) ? data.Retinue : [];
        const rawEliteRetinue = Array.isArray(data.eliteRetinue) ? data.eliteRetinue : Array.isArray(data.EliteRetinue) ? data.EliteRetinue : [];
        const troop = (item: Record<string, unknown>) => ({ slot: Number(item.slot ?? item.Slot ?? 0), name: String(item.name ?? item.Name ?? ""), tier: Number(item.tier ?? item.Tier ?? 0), culture: String(item.culture ?? item.Culture ?? "") });
        setRetinue({
          heroName: String(data.heroName ?? data.HeroName ?? ""),
          retinue: rawRetinue.map(troop),
          eliteRetinue: rawEliteRetinue.map(troop),
          updatedAt: envelope.timestamp,
        } as RetinueSnapshot);
        setRetinueError(undefined);
      } else if (envelope.kind === "retinue.error") {
        setRetinueError(data.error);
      } else if (envelope.kind === "action.result" || envelope.kind === "action.error") {
        const requestId = String(data.requestId ?? envelope.id);
        const succeeded = envelope.kind === "action.result";
        const messages = succeeded ? (data.messages ?? []) : [data.error ?? "The command failed."];
        setCommandActivity(entries => entries.map(entry => entry.requestId === requestId ? { ...entry, status: succeeded ? "succeeded" : "failed", messages, completedAt: envelope.timestamp } : entry));
      }
    };
    const connect = () => {
      if (disposed) return;
      socket = new WebSocket(url, "blt.viewer.v1");
      socket.addEventListener("open", () => { reconnectDelay = 1000; setState(value => ({ ...value, connected: true })); });
      socket.addEventListener("message", handleMessage);
      socket.addEventListener("close", () => {
        if (disposed) return;
        setState(value => ({ ...value, connected: false, gameStarted: false, viewer: { adopted: false }, mission: { active: false, kind: "inactive", revision: value.mission.revision + 1, deploymentFinished: false, combatants: [], actionAvailability: {} } }));
        reconnectTimer = window.setTimeout(connect, reconnectDelay);
        reconnectDelay = Math.min(reconnectDelay * 2, 30000);
      });
    };
    connect();
    return () => { disposed = true; if (reconnectTimer) window.clearTimeout(reconnectTimer); socket?.close(); };
  }, [identity]);
  function recordCommand(entry: Omit<CommandActivity, "submittedAt" | "messages">) {
    setCommandActivity(entries => [{ ...entry, submittedAt: new Date().toISOString(), messages: [] }, ...entries.filter(item => item.requestId !== entry.requestId)].slice(0, 100));
    window.setTimeout(() => setCommandActivity(entries => entries.map(item => item.requestId === entry.requestId && item.status === "pending"
      ? { ...item, status: "failed", messages: ["Bannerlord did not return a result within 30 seconds."], completedAt: new Date().toISOString() }
      : item)), 30_000);
  }
  function completeDevelopmentCommand(requestId: string, message: string) {
    setCommandActivity(entries => entries.map(entry => entry.requestId === requestId ? { ...entry, status: "succeeded", messages: [message], completedAt: new Date().toISOString() } : entry));
  }
  function failCommand(requestId: string, message: string) {
    setCommandActivity(entries => entries.map(entry => entry.requestId === requestId ? { ...entry, status: "failed", messages: [message], completedAt: new Date().toISOString() } : entry));
  }
  return { ...state, inventory, inventoryError, retinue, retinueError, commandActivity, setInventory, setInventoryError, setRetinue, setRetinueError, recordCommand, completeDevelopmentCommand, failCommand, clearCommandActivity: () => setCommandActivity([]) };
}
