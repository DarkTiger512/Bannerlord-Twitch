export type Role = "viewer" | "moderator" | "broadcaster";
export type InputType = "text" | "integer" | "number" | "boolean" | "choice" | "hero" | "item" | "confirmation";

export interface ActionInput {
  id: string;
  type: InputType;
  required: boolean;
  label?: string;
  labelKey?: string;
  description?: string;
  descriptionKey?: string;
  options?: Array<{ value: string; label: string; labelKey?: string }>;
  optionsSource?: keyof GameSelectors;
  minimum?: number;
  maximum?: number;
  confirmationPolicy?: "legacy-token" | "ui-only";
  legacyToken?: string;
  visibleWhenInput?: string;
  visibleWhenValues?: string[];
}

export interface ManifestAction {
  id: string;
  legacyName: string;
  handler: string;
  category: string;
  description: string;
  nameKey?: string;
  descriptionKey?: string;
  categoryKey?: string;
  enabledByDefault: boolean;
  hiddenFromHelp: boolean;
  permissions: Role[];
  availability: string[];
  mutatesCampaign: boolean;
  inputs: ActionInput[];
  settings?: StreamerSetting[];
}
export interface StreamerSetting { id: string; label: string; type: "boolean" | "number" | "string"; defaultValue: boolean | number | string }

export interface ActionManifest { protocolVersion: number; manifestVersion?: number; actions: ManifestAction[] }

export interface RuntimeCommand { name: string; handler: string; help: string; helpKey?: string; moderatorOnly: boolean; hideHelp: boolean }
export interface ViewerState { adopted: boolean; heroId?: string; heroName?: string; gold?: number }

export interface ViewerIdentity {
  token: string;
  channelId: string;
  userId: string | null;
  displayName: string;
  roles: Role[];
  linked: boolean;
  locale?: string;
}

export interface GameSelectors {
  cultures: string[];
  heroes: string[];
  clans: string[];
  kingdoms: string[];
  settlements: string[];
  skills: string[];
}

export interface GameState {
  connected: boolean;
  gameStarted: boolean;
  unavailable: Record<string, string>;
  cooldowns: Record<string, number>;
  selectors: GameSelectors;
  commands: RuntimeCommand[];
  viewer: ViewerState;
  mission: MissionState;
}

export type MissionKind = "inactive" | "battle" | "tournament";
export interface MissionCombatant {
  id: string;
  name: string;
  hp: number;
  maxHp: number;
  state: string;
  isPlayerSide: boolean;
  tournamentTeam: number;
  cooldownFractionRemaining: number;
  cooldownSecondsRemaining: number;
  activePowerFractionRemaining: number;
  activePowerName?: string;
  activePowerActive?: boolean;
  kills: number;
  retinue: number;
  deadRetinue: number;
  eliteRetinue: number;
  deadEliteRetinue: number;
  retinueKills: number;
  goldEarned: number;
  xpEarned: number;
  ammoCurrent: number;
  ammoMaximum: number;
}
export interface MissionState {
  active: boolean;
  kind: MissionKind;
  revision: number;
  deploymentFinished: boolean;
  combatants: MissionCombatant[];
  actionAvailability: Record<string, string | null>;
}

export interface InventoryItem { index: number; name: string; type: string; equipped: boolean }
export interface EquipmentSlot { id: string; label: string; accepts: string; itemName?: string; customItemIndex?: number }
export interface InventorySnapshot { heroName: string; limit: number; items: InventoryItem[]; slots: EquipmentSlot[]; updatedAt?: string }
export interface RetinueTroop { slot: number; name: string; tier: number; culture?: string }
export interface RetinueSnapshot { heroName: string; retinue: RetinueTroop[]; eliteRetinue: RetinueTroop[]; updatedAt?: string }
export type CommandActivityStatus = "pending" | "succeeded" | "failed";
export interface CommandActivity { requestId: string; actionId: string; actionName: string; status: CommandActivityStatus; submittedAt: string; completedAt?: string; messages: string[] }
export interface CommandPreference { actionId: string; enabled: boolean; settings?: Record<string, boolean | number | string> }
export interface ConfigurationProfile { profileId: number; extensionEnabled: boolean; commands: CommandPreference[] }
export interface ChannelConfiguration { schemaVersion: number; extensionEnabled: boolean; commands: CommandPreference[]; profiles: ConfigurationProfile[]; activeProfile: number; revision: number; updatedAt: string }
export interface InstallationSummary { installationId: string; createdAt: string; lastSeenAt?: string; revokedAt?: string }
export type PairingRequestState = "pending" | "approved" | "denied" | "expired" | "cancelled";
export interface PairingRequestSummary { requestId: string; modVersion: string; platformLabel: string; fingerprint: string; createdAt: string; expiresAt: string; status: PairingRequestState }
export interface PairingDecision { requestId: string; decision: "approved" | "denied" }
export interface ConfigurationContext { configuration: ChannelConfiguration; gameConnected: boolean; lastStateAt?: string; installations: InstallationSummary[]; pairingRequests: PairingRequestSummary[]; runtimeCommands: RuntimeCommand[] }

declare global {
  interface Window {
    Twitch?: {
      ext: {
        onAuthorized(callback: (auth: { token: string; channelId: string; userId?: string }) => void): void;
        onContext(callback: (context: { mode?: string }) => void): void;
        actions: { requestIdShare(): void };
        viewer: { id?: string | null; role?: Role; isLinked?: boolean };
      };
    };
  }
}
