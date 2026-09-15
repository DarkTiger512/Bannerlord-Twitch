import { act, renderHook } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import { useIntegrationState } from "./useIntegrationState";
import type { ViewerIdentity } from "../types";

class TestSocket {
  static instances: TestSocket[] = [];
  listeners = new Map<string, Array<(event: Event) => void>>();
  constructor(public url: string | URL, public protocols?: string | string[]) { TestSocket.instances.push(this); }
  addEventListener(kind: string, listener: (event: Event) => void) { this.listeners.set(kind, [...(this.listeners.get(kind) ?? []), listener]); }
  emit(kind: string, event = new Event(kind)) { for (const listener of this.listeners.get(kind) ?? []) listener(event); }
  close() { }
}

const identity: ViewerIdentity = { token: "development-token", channelId: "42", userId: "9", displayName: "TestHero", roles: ["viewer"], linked: true };

afterEach(() => { vi.useRealTimers(); vi.unstubAllEnvs(); vi.unstubAllGlobals(); TestSocket.instances = []; });

test("live local viewer socket starts without demo state and reconnects after interruption", () => {
  vi.useFakeTimers();
  vi.stubEnv("VITE_BLT_LIVE_INTEGRATION", "true");
  vi.stubGlobal("WebSocket", TestSocket);
  const { result, unmount } = renderHook(() => useIntegrationState(identity));
  expect(result.current.connected).toBe(false);
  expect(result.current.mission.active).toBe(false);
  expect(TestSocket.instances).toHaveLength(1);
  expect(TestSocket.instances[0].protocols).toBe("blt.viewer.v1");
  act(() => TestSocket.instances[0].emit("open"));
  expect(result.current.connected).toBe(true);
  act(() => TestSocket.instances[0].emit("close"));
  expect(result.current.connected).toBe(false);
  act(() => vi.advanceTimersByTime(1000));
  expect(TestSocket.instances).toHaveLength(2);
  unmount();
});

test("authoritative campaign snapshot reveals the live overlay state", () => {
  vi.stubEnv("VITE_BLT_LIVE_INTEGRATION", "true");
  vi.stubGlobal("WebSocket", TestSocket);
  const { result, unmount } = renderHook(() => useIntegrationState(identity));
  const socket = TestSocket.instances[0];
  act(() => socket.emit("message", new MessageEvent("message", { data: JSON.stringify({
    v: 1,
    channelId: "42",
    kind: "connection.status",
    data: { connected: true, gameStarted: false },
  }) })));
  expect(result.current.gameStarted).toBe(false);
  act(() => socket.emit("message", new MessageEvent("message", { data: JSON.stringify({
    v: 1,
    channelId: "42",
    kind: "state.snapshot",
    data: { connected: true, gameStarted: true, mission: { active: false, kind: "inactive", revision: 1, deploymentFinished: false, combatants: [], actionAvailability: {} } },
  }) })));
  expect(result.current.gameStarted).toBe(true);
  unmount();
});

test("campaign disconnect clears hero ownership before a new campaign arrives", () => {
  vi.stubEnv("VITE_BLT_LIVE_INTEGRATION", "true");
  vi.stubGlobal("WebSocket", TestSocket);
  const { result, unmount } = renderHook(() => useIntegrationState(identity));
  const socket = TestSocket.instances[0];
  const send = (kind: string, data: object) => act(() => socket.emit("message", new MessageEvent("message", { data: JSON.stringify({ v: 1, channelId: "42", kind, data }) })));
  expect(String(socket.url)).toContain("hud=hero-id-v1");
  send("viewer.state", { adopted: true, heroId: "hero-a", heroName: "Alice" });
  expect(result.current.viewer.heroId).toBe("hero-a");
  send("connection.status", { connected: false, gameStarted: false });
  expect(result.current.viewer.heroId).toBeUndefined();
  expect(result.current.mission.active).toBe(false);
  send("viewer.state", { adopted: true, heroId: "hero-b", heroName: "Bob" });
  expect(result.current.viewer.heroId).toBe("hero-b");
  unmount();
});
