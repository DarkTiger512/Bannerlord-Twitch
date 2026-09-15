import { Crosshair, Shield, Swords, Users, Zap } from "lucide-react";
import { memo, useMemo } from "react";
import type { CSSProperties } from "react";
import type { GameState, ManifestAction, MissionCombatant, ViewerIdentity } from "../types";
import { BattleCommandStrip } from "./BattleCommandStrip";
import { useI18n } from "../i18n";

interface Props {
  mission: GameState["mission"];
  heroId?: string;
  actions: ManifestAction[];
  identity: ViewerIdentity;
  cooldowns: Record<string, number>;
  selectors: GameState["selectors"];
  busy: boolean;
  error?: string;
  onRequestIdentity(): void;
  onSubmit(action: ManifestAction, args: Record<string, unknown>): void;
}

const healthPercent = (hero: MissionCombatant) => Math.max(0, Math.min(100, hero.maxHp ? hero.hp * 100 / hero.maxHp : 0));
const powerPercent = (hero: MissionCombatant) => Math.round(Math.max(0, Math.min(1, hero.activePowerFractionRemaining ?? 0)) * 100);
const sideKey = (hero: MissionCombatant, tournament: boolean) => tournament ? "team" : hero.isPlayerSide ? "streamer" : "opposing";

const RosterTile = memo(function RosterTile({ hero, tournament }: { hero: MissionCombatant; tournament: boolean }) {
  const { t } = useI18n();
  const percent = healthPercent(hero);
  return <article className={`battle-roster-tile ${hero.isPlayerSide ? "player-side" : "enemy-side"} state-${hero.state}`} title={`${hero.name} · ${Math.round(hero.hp)} / ${Math.round(hero.maxHp)} HP · ${hero.state}`}>
    <div><strong>{hero.name}</strong><i aria-label={hero.state} /></div>
    <div className="roster-health" aria-label={t("battle.health", { name: hero.name, current: Math.round(hero.hp), maximum: Math.round(hero.maxHp) })}><span style={{ width: `${percent}%` }} /><b>{Math.round(hero.hp)} / {Math.round(hero.maxHp)}</b></div>
    {tournament ? <small>{t("battle.team", { number: hero.tournamentTeam + 1 })}</small> : null}
  </article>;
});

function HeroHud({ hero, tournament }: { hero: MissionCombatant; tournament: boolean }) {
  const { t, number } = useI18n();
  const power = powerPercent(hero);
  const powerActive = hero.activePowerActive ?? power > 0;
  return <article className={`hero-hud ${hero.state !== "active" ? "inactive" : ""}`}>
    <div className="hero-hud-heading"><div><small>{t(sideKey(hero, tournament) === "team" ? "battle.team" : sideKey(hero, tournament) === "streamer" ? "battle.streamerSide" : "battle.opposingSide", { number: hero.tournamentTeam + 1 })}</small><strong>{hero.name}</strong></div><span className={`combatant-state ${hero.state}`}>{hero.state}</span></div>
    <div className="hero-primary-bars">
      <div><div className="health-track hero-health" aria-label={t("battle.health", { name: hero.name, current: Math.round(hero.hp), maximum: Math.round(hero.maxHp) })}><i style={{ width: `${healthPercent(hero)}%` }} /><b>{Math.round(hero.hp)} / {Math.round(hero.maxHp)} HP</b></div></div>
      <div className="power-status"><div className="power-label"><span><Zap />{hero.activePowerName || t("battle.power")}</span><b>{powerActive ? `${power}%` : t("common.ready")}</b></div><div className="power-track" aria-label={t(powerActive ? "battle.powerRemaining" : "battle.powerReady", { name: hero.activePowerName || t("battle.power"), percent: power })}><i style={{ width: `${powerActive ? power : 0}%` }} /></div></div>
    </div>
    <div className="hero-metrics"><span>{t("battle.cooldown")} <b>{Math.ceil(hero.cooldownSecondsRemaining)}s</b></span><span><Crosshair /> <b>{hero.ammoCurrent}/{hero.ammoMaximum}</b></span><span><Swords /> <b>{hero.kills}</b></span><span><Users /> <b>{hero.retinue + hero.eliteRetinue}</b></span><span>{t("battle.gold")} <b>{number(hero.goldEarned)}</b></span><span>{t("battle.xp")} <b>{number(hero.xpEarned)}</b></span></div>
  </article>;
}

export function BattleWorkspace({ mission, heroId, actions, identity, cooldowns, busy, onRequestIdentity, onSubmit }: Props) {
  const { t } = useI18n();
  const ownIndex = heroId ? mission.combatants.findIndex(hero => hero.id === heroId) : -1;
  const ownHero = ownIndex >= 0 ? mission.combatants[ownIndex] : undefined;
  const groups = useMemo(() => mission.combatants.reduce((result, hero) => {

    const key = sideKey(hero, mission.kind === "tournament") === "team" ? t("battle.team", { number: hero.tournamentTeam + 1 }) : hero.isPlayerSide ? t("battle.streamerSide") : t("battle.opposingSide");
    const group = result.get(key);
    if (group) group.push(hero); else result.set(key, [hero]);
    return result;
  }, new Map<string, MissionCombatant[]>()), [mission.combatants, mission.kind, t]);

  return <section className="battle-workspace" aria-label={t("battle.live")}>
    <div className="battle-stage">
      <section className="personal-battle-hud" aria-label={t("battle.yourHero")}>
        <h2>{t("battle.yourHero")}</h2>
        {ownHero ? <HeroHud hero={ownHero} tournament={mission.kind === "tournament"} /> : <div className="viewer-absent"><Shield /><div><strong>{t("battle.notDeployed")}</strong><span>{t("battle.notDeployedHint")}</span></div></div>}
        <BattleCommandStrip actions={actions} identity={identity} mission={mission} cooldowns={cooldowns} busy={busy} onRequestIdentity={onRequestIdentity} onSubmit={onSubmit} />
      </section>
      <section className="minimal-battle-roster" aria-label={t("battle.roster")}>
        <h2>{t("battle.roster")} <span>{mission.combatants.length}</span></h2>
        <div className="minimal-team-groups">{Array.from(groups, ([name, heroes]) => <div className="minimal-team-group" key={name}><h3>{name}</h3><div style={{ "--roster-columns": Math.min(heroes.length, 8) } as CSSProperties}>{heroes.map(hero => <RosterTile key={hero.id} hero={hero} tournament={mission.kind === "tournament"} />)}</div></div>)}</div>
      </section>
    </div>
  </section>;
}
