import type { ViewerIdentity } from "./types";
import { developmentIdentityConfig, isLocalHost } from "./environment";

function developmentIdentity(): ViewerIdentity {
  const config = developmentIdentityConfig();
  const page = window.location.pathname.split("/").pop()?.toLowerCase();
  const role = page === "config.html" || page === "live-config.html" ? "broadcaster" : config.role;
  return { token: "development-token", channelId: config.channelId, userId: config.userId,
    displayName: config.displayName, roles: [role], linked: true, locale: new URLSearchParams(window.location.search).get("locale") ?? navigator.language };
}

export function authorizeViewer(): Promise<ViewerIdentity> {
  if (isLocalHost() || !window.Twitch?.ext) return Promise.resolve(developmentIdentity());
  return new Promise(resolve => {
    window.Twitch?.ext.onAuthorized(auth => {
      const viewer = window.Twitch?.ext.viewer;
      const role = viewer?.role ?? "viewer";
      resolve({
        token: auth.token,
        channelId: auth.channelId,
        userId: viewer?.id ?? null,
        displayName: "Viewer",
        roles: [role],
        linked: Boolean(viewer?.id && viewer?.isLinked),
        locale: new URLSearchParams(window.location.search).get("locale") ?? navigator.language,
      });
    });
  });
}

export function requestIdentity() {
  window.Twitch?.ext.actions.requestIdShare();
}
