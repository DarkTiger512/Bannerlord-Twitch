import { useEffect, useState } from "react";
import { requestInventory, requestRetinue, submitAction, submitCommand } from "./api";
import { isLiveLocalIntegration } from "./environment";
import { BattleWorkspace } from "./components/BattleWorkspace";
import { CommandWorkspace } from "./components/CommandWorkspace";
import { CommandFeedView } from "./components/CommandFeedView";
import { InventoryView } from "./components/InventoryView";
import { IntegrationDiagnostics } from "./components/IntegrationDiagnostics";
import { RetinueView } from "./components/RetinueView";
import { PrestigeView } from "./components/PrestigeView";
import { useIntegrationState } from "./hooks/useIntegrationState";
import { authorizeViewer, requestIdentity } from "./twitch";
import type { ActionManifest, ManifestAction, ViewerIdentity } from "./types";
import bltLogo from "./assets/blt-logo-v2.png";
import { useI18n } from "./i18n";

export function App() {
  const { t } = useI18n();
  const [manifest, setManifest] = useState<ActionManifest | null>(null);
  const [identity, setIdentity] = useState<ViewerIdentity | null>(null);
  const [workspace, setWorkspace] = useState<"home" | "inventory" | "retinue" | "prestige">("home");
  const [inventoryFilter, setInventoryFilter] = useState("");
  const [open, setOpen] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [feedExpanded, setFeedExpanded] = useState(false);
  const [inventoryLoading, setInventoryLoading] = useState(false);
  const [retinueLoading, setRetinueLoading] = useState(false);
  const state = useIntegrationState(identity);

  useEffect(() => {
    Promise.all([
      fetch("./action-manifest.json").then(response => response.json() as Promise<ActionManifest>),
      authorizeViewer(),
    ]).then(([loadedManifest, viewer]) => {
      setManifest(loadedManifest);
      setIdentity(viewer);
    }).catch(reason => setError(String(reason)));
  }, []);

  if (!identity || !manifest) return null;

  async function handleActionSubmit(action: ManifestAction, args: Record<string, unknown>) {
    setBusy(true); setError(undefined);
    const requestId = crypto.randomUUID();
    state.recordCommand({ requestId, actionId: action.id, actionName: action.legacyName, status: "pending" });
    try {
      const response = await submitAction(identity!, action, args, requestId);
      if (identity!.token === "development-token" && !isLiveLocalIntegration()) state.completeDevelopmentCommand(response.requestId, `${action.legacyName} completed successfully.`);
    } catch (reason) { const message = reason instanceof Error ? reason.message : t("error.action"); state.failCommand(requestId, message); setError(message); }
    finally { setBusy(false); }
  }
  async function handleCommand(commandLine: string) {
    if (!identity!.linked) { requestIdentity(); return; }
    const normalized = commandLine.trim().replace(/^!/, "");
    const separator = normalized.indexOf(" ");
    const name = (separator < 0 ? normalized : normalized.slice(0, separator)).toLowerCase();
    const args = separator < 0 ? "" : normalized.slice(separator + 1).trim();
    if (["inv", "slots", "customitems"].includes(name)) { setInventoryFilter(name === "customitems" ? args : ""); openInventory(); return; }
    if (name === "retinuelist") { openRetinue(); return; }
    if (name === "prestige" && !args) { setWorkspace("prestige"); return; }
    const requestId = crypto.randomUUID();
    state.recordCommand({ requestId, actionId: `command.${name}`, actionName: name, status: "pending" });
    setBusy(true); setError(undefined);
    try {
      const response = await submitCommand(identity!, normalized, requestId);
      if (identity!.token === "development-token" && !isLiveLocalIntegration()) state.completeDevelopmentCommand(response.requestId, `${name} completed successfully.`);
      return response.requestId;
    } catch (reason) { const message = reason instanceof Error ? reason.message : t("error.command"); state.failCommand(requestId, message); setError(message); }
    finally { setBusy(false); }
  }

  async function loadInventory() {
    setInventoryLoading(true); state.setInventoryError(undefined);
    try { const snapshot = await requestInventory(identity!); if (snapshot) state.setInventory(snapshot); }
    catch (reason) { state.setInventoryError(reason instanceof Error ? reason.message : t("error.inventory")); }
    finally { setInventoryLoading(false); }
  }

  function openInventory() { setWorkspace("inventory"); if (identity!.linked && !state.inventory && !inventoryLoading) void loadInventory(); }

  async function loadRetinue() {
    setRetinueLoading(true); state.setRetinueError(undefined);
    try { const snapshot = await requestRetinue(identity!); if (snapshot) state.setRetinue(snapshot); }
    catch (reason) { state.setRetinueError(reason instanceof Error ? reason.message : t("error.retinue")); }
    finally { setRetinueLoading(false); }
  }

  function openRetinue() { setWorkspace("retinue"); if (identity!.linked && !state.retinue && !retinueLoading) void loadRetinue(); }

  async function manageRetinue(actionId: "command.retinue" | "command.eliteretinue", operation: string, value?: number) {
    const action = manifest!.actions.find(candidate => candidate.id === actionId);
    if (!action) { state.setRetinueError("Retinue controls are unavailable."); return; }
    setBusy(true); state.setRetinueError(undefined);
    const requestId = crypto.randomUUID();
    state.recordCommand({ requestId, actionId: action.id, actionName: action.legacyName, status: "pending" });
    try {
      const args = operation === "upgrade-count" ? { operation, count: value } : operation === "clear-slot" ? { operation, slot: value } : { operation };
      const response = await submitAction(identity!, action, args, requestId);
      if (identity!.token === "development-token" && !isLiveLocalIntegration()) state.completeDevelopmentCommand(response.requestId, `${action.legacyName} completed successfully.`);
      window.setTimeout(() => void loadRetinue(), identity!.token === "development-token" && !isLiveLocalIntegration() ? 350 : 700);
    } catch (reason) { const message = reason instanceof Error ? reason.message : "That retinue order could not be completed."; state.failCommand(requestId, message); state.setRetinueError(message); }
    finally { setBusy(false); }
  }

  async function equipInventoryItem(itemIndex: number, slotId: string) {
    const equipAction = manifest!.actions.find(action => action.id === "command.equipcustom");
    if (!equipAction) { state.setInventoryError("Equipment controls are unavailable."); return; }
    setInventoryLoading(true); state.setInventoryError(undefined);
    const requestId = crypto.randomUUID();
    state.recordCommand({ requestId, actionId: equipAction.id, actionName: "Equip custom item", status: "pending" });
    try {
      const response = await submitAction(identity!, equipAction, { item: `${itemIndex} @${slotId}` }, requestId);
      if (identity!.token === "development-token" && !isLiveLocalIntegration()) state.completeDevelopmentCommand(response.requestId, "Equipment updated successfully.");
      if (!isLiveLocalIntegration()) state.setInventory(current => current ? {
        ...current,
        items: current.items.map(item => ({ ...item, equipped: item.index === itemIndex || current.slots.some(slot => slot.customItemIndex === item.index && slot.id !== slotId) })),
        slots: current.slots.map(slot => slot.id === slotId ? { ...slot, itemName: current.items.find(item => item.index === itemIndex)?.name, customItemIndex: itemIndex } : slot),
        updatedAt: new Date().toISOString(),
      } : current);
      if (identity!.token !== "development-token" || isLiveLocalIntegration()) window.setTimeout(() => void loadInventory(), 700);
    } catch (reason) { const message = reason instanceof Error ? reason.message : "That item could not be equipped."; state.failCommand(requestId, message); state.setInventoryError(message); }
    finally { setInventoryLoading(false); }
  }

  const redundantBattleActions = new Set(["command.battle", "command.stats", "command.ammo"]);
  const battleActions = manifest.actions.filter(action => Object.hasOwn(state.mission.actionAvailability, action.id) && !redundantBattleActions.has(action.id));
  const battleActive = state.mission.active && (state.mission.kind === "battle" || state.mission.kind === "tournament");

  // The shell remains available while waiting for a campaign or connection.

  return <main className={open ? "overlay-shell open" : "overlay-shell collapsed"}>
    {!open ? <button className="floating-overlay-toggle" onClick={() => setOpen(true)} aria-label={t("app.open")} title={t("app.open")}><img src={bltLogo} alt="" /><span>BLT</span></button> : null}
    {open ? <div className="overlay-window">
      <IntegrationDiagnostics identity={identity} />
      <button className="floating-overlay-toggle" onClick={() => setOpen(false)} aria-label={t("app.collapse")} title={t("app.collapse")}><img src={bltLogo} alt="" /><span>BLT</span></button>
      <div className={`overlay-content ${battleActive ? "battle-active" : ""}`}>
        <div className="workspace-stack">
          <div className="workspace-main">
            {battleActive ? <BattleWorkspace heroId={state.viewer.heroId} viewer={state.viewer} connected={state.connected} mission={state.mission} actions={battleActions} identity={identity} cooldowns={state.cooldowns} selectors={state.selectors} busy={busy} error={error} onRequestIdentity={requestIdentity} onSubmit={handleActionSubmit} /> : workspace === "prestige" ? <PrestigeView key={`${state.viewer.heroName}:${state.viewer.prestige?.count}`} viewer={state.viewer} connected={state.connected} linked={identity.linked} busy={busy} activity={state.commandActivity} onBack={() => setWorkspace("home")} onCommand={handleCommand} /> : workspace === "inventory" ? <InventoryView inventory={state.inventory} loading={inventoryLoading} error={state.inventoryError} linked={identity.linked} initialFilter={inventoryFilter} onBack={() => setWorkspace("home")} onRefresh={loadInventory} onEquip={equipInventoryItem} onRequestIdentity={requestIdentity} /> : workspace === "retinue" ? <RetinueView retinue={state.retinue} loading={retinueLoading} error={state.retinueError} linked={identity.linked} busy={busy} onBack={() => setWorkspace("home")} onRefresh={loadRetinue} onManage={manageRetinue} onRequestIdentity={requestIdentity} /> : <CommandWorkspace actions={manifest.actions} commands={state.commands} identity={identity} state={state} busy={busy} onExecute={handleCommand} onInventory={openInventory} onRetinue={openRetinue} onPrestige={() => setWorkspace("prestige")} />}
          </div>
          <CommandFeedView entries={state.commandActivity} expanded={feedExpanded} onToggle={() => setFeedExpanded(value => !value)} onClear={state.clearCommandActivity} />
        </div>
      </div>
    </div> : null}
  </main>;
}
