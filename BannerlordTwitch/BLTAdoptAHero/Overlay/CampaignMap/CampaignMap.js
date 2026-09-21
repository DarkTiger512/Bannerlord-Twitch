$(document).ready(function () {
    const ns = 'http://www.w3.org/2000/svg';
    const container = document.getElementById('campaign-map-container');
    // Keep the viewport-anchored map outside the growing activity stack.
    document.body.appendChild(container);
    const svg = document.getElementById('campaign-map-svg');
    const landLayer = document.getElementById('land-layer');
    const coastlineLayer = document.getElementById('coastline-layer');
    const settlementLayer = document.getElementById('settlement-markers');
    const settlementLabels = document.getElementById('settlement-labels');
    const heroLayer = document.getElementById('hero-markers');
    const status = document.getElementById('map-status');
    let geography = null, revision = 0, connected = false, currentHeroes = [];
    let cameraTargetId = null, lastCameraSwitch = 0, cameraFrame = null, currentView = null;
    let ownership = new Map(), kingdomColors = new Map();

    if (typeof $.connection.mapHub === 'undefined') { container.classList.add('hidden'); return; }
    const hub = $.connection.mapHub;
    hub.client.updateGeography = function (data) {
        if (!data || data.Version !== 2) return;
        geography = data; revision = data.Revision; renderGeography();
    };
    hub.client.updateMapState = function (data) {
        if (!data || !data.Visible) { container.classList.add('hidden'); container.setAttribute('aria-hidden', 'true'); return; }
        if (!geography || data.GeographyRevision !== revision) { hub.server.refresh(0); return; }
        container.classList.remove('hidden');
        container.setAttribute('aria-hidden', 'false');
        ownership = new Map((data.Ownership || []).map(item => [item.Id, item.KingdomId]));
        currentHeroes = data.Heroes || [];
        renderSettlements(currentHeroes); renderHeroes(currentHeroes);
        updateSpectatorCamera(currentHeroes);
    };
    $.connection.hub.start().done(function () {
        connected = true; hub.server.refresh(revision);
        setInterval(function () { if (connected) hub.server.refresh(revision); }, 2000);
    });
    $.connection.hub.disconnected(function () { connected = false; });
    $.connection.hub.reconnected(function () { connected = true; hub.server.refresh(revision); });

    function renderGeography() {
        const p = geography.Projection;
        currentView = { x: 0, y: 0, width: p.Width, height: p.Height };
        svg.setAttribute('viewBox', viewBoxText(currentView));
        applySettings(geography.Settings || {});
        kingdomColors = new Map((geography.Kingdoms || []).map(k => [k.Id, { fill: k.Color1, border: k.Color2 }]));
        landLayer.innerHTML = '';
        (geography.Land || []).forEach(area => {
            const rect = document.createElementNS(ns, 'rect');
            rect.setAttribute('x', area.X); rect.setAttribute('y', area.Y);
            rect.setAttribute('width', area.Width); rect.setAttribute('height', area.Height);
            landLayer.appendChild(rect);
        });
        coastlineLayer.innerHTML = '';
        let d = '';
        (geography.Coastline || []).forEach(seg => { d += `M${seg.X1.toFixed(2)},${seg.Y1.toFixed(2)}L${seg.X2.toFixed(2)},${seg.Y2.toFixed(2)}`; });
        const path = document.createElementNS(ns, 'path'); path.setAttribute('d', d); coastlineLayer.appendChild(path);
        renderSettlements(currentHeroes);
    }
    function applySettings(settings) {
        container.style.setProperty('--map-width', `${settings.WidthPercent || 42}vw`);
        container.style.setProperty('--map-max-height', `${settings.MaxHeightPercent || 38}vh`);
        container.style.setProperty('--map-opacity', Math.max(0, Math.min(1, settings.BackgroundOpacity ?? .9)));
        ['TopLeft', 'TopRight', 'BottomLeft', 'BottomRight'].forEach(c => container.classList.remove(`corner-${c}`));
        container.classList.add(`corner-${settings.Corner || 'TopLeft'}`);
    }
    function renderSettlements(heroes) {
        if (!geography) return;
        settlementLayer.innerHTML = ''; settlementLabels.innerHTML = '';
        const settings = geography.Settings || {}, density = settings.LabelDensity || 'Smart';
        const occupied = (heroes || []).map(h => ({ x: h.X - 1.5, y: h.Y - 2, w: Math.max(6, h.Name.length * .9 + 4), h: 4 }));
        const ordered = [...(geography.Settlements || [])].sort((a, b) =>
            (a.Type === 'Town' ? 0 : 1) - (b.Type === 'Town' ? 0 : 1) || a.Id.localeCompare(b.Id));
        ordered.forEach(s => {
            const colors = kingdomColors.get(ownership.get(s.Id)) || { fill: '#788087', border: '#e7e1d5' };
            const group = document.createElementNS(ns, 'g'); group.setAttribute('transform', `translate(${s.X},${s.Y})`);
            let shape;
            if (s.Type === 'Town') {
                shape = document.createElementNS(ns, 'circle'); shape.setAttribute('r', settings.TownRadius || 2.15);
            } else {
                const size = settings.CastleLength || 2.5; shape = document.createElementNS(ns, 'rect');
                shape.setAttribute('x', -size / 2); shape.setAttribute('y', -size / 2); shape.setAttribute('width', size); shape.setAttribute('height', size);
            }
            shape.setAttribute('class', 'settlement-shape'); shape.setAttribute('fill', colors.fill);
            shape.setAttribute('stroke', colors.border); shape.setAttribute('stroke-width', '.55');
            group.appendChild(shape); settlementLayer.appendChild(group);
            if (density === 'All' || (density === 'Smart' && s.Type === 'Town')) addSettlementLabel(s, occupied);
        });
    }
    function addSettlementLabel(s, occupied) {
        const box = { x: s.X + 1.8, y: s.Y - 2.2, w: Math.max(5, s.Name.length * .8), h: 2.2 };
        if (occupied.some(o => overlaps(o, box))) return;
        occupied.push(box);
        const text = document.createElementNS(ns, 'text'); text.setAttribute('x', box.x); text.setAttribute('y', s.Y - .6);
        text.setAttribute('class', `map-label ${s.Type.toLowerCase()}`); text.textContent = s.Name; settlementLabels.appendChild(text);
    }
    function renderHeroes(heroes) {
        heroLayer.innerHTML = ''; if (!geography) return;
        const radius = geography.Settings.HeroRadius || 1.8, groups = new Map();
        heroes.forEach(h => { if (!groups.has(h.ClusterId)) groups.set(h.ClusterId, []); groups.get(h.ClusterId).push(h); });
        [...groups.values()].forEach(cluster => cluster.sort((a, b) => a.Id.localeCompare(b.Id)).forEach((hero, index) => {
            const angle = cluster.length > 1 ? Math.PI * 2 * index / cluster.length : 0;
            const spread = cluster.length > 1 ? radius * 1.5 : 0;
            const x = hero.X + Math.cos(angle) * spread, y = hero.Y + Math.sin(angle) * spread;
            const group = document.createElementNS(ns, 'g'); group.setAttribute('class', 'hero-marker');
            const circle = document.createElementNS(ns, 'circle'); circle.setAttribute('cx', x); circle.setAttribute('cy', y);
            circle.setAttribute('r', radius); circle.setAttribute('fill', hero.Color || '#d8ad45'); group.appendChild(circle);
            const labelY = cluster.length > 1 ? hero.Y + (index - (cluster.length - 1) / 2) * 2.4 + .55 : y + .55;
            const label = document.createElementNS(ns, 'text'); label.setAttribute('x', x + radius + .7); label.setAttribute('y', labelY);
            label.setAttribute('class', 'hero-label'); label.textContent = hero.Name; group.appendChild(label); heroLayer.appendChild(group);
        }));
    }
    function updateSpectatorCamera(heroes) {
        if (!geography) return;
        const settings = geography.Settings || {}, ordered = [...heroes].sort((a, b) => a.Id.localeCompare(b.Id));
        if (!settings.SpectatorCamera || ordered.length === 0) {
            cameraTargetId = null; lastCameraSwitch = 0;
            animateCamera({ x: 0, y: 0, width: geography.Projection.Width, height: geography.Projection.Height });
            status.textContent = `${ordered.length} heroes`;
            return;
        }

        const now = Date.now(), interval = Math.max(3, settings.SpectatorIntervalSeconds || 10) * 1000;
        let index = ordered.findIndex(hero => hero.Id === cameraTargetId);
        if (index < 0) { index = 0; lastCameraSwitch = now; }
        else if (now - lastCameraSwitch >= interval) { index = (index + 1) % ordered.length; lastCameraSwitch = now; }
        const target = ordered[index]; cameraTargetId = target.Id;
        const zoom = Math.max(1, settings.SpectatorZoom || 3.5), map = geography.Projection;
        const width = map.Width / zoom, height = map.Height / zoom;
        animateCamera({
            x: clamp(target.X - width / 2, 0, map.Width - width),
            y: clamp(target.Y - height / 2, 0, map.Height - height), width, height
        });
        status.textContent = `Following ${target.Name}  •  ${index + 1}/${ordered.length}`;
    }
    function animateCamera(destination) {
        if (cameraFrame !== null) cancelAnimationFrame(cameraFrame);
        const start = currentView || destination, started = performance.now(), duration = 900;
        function step(now) {
            const progress = Math.min(1, (now - started) / duration), eased = 1 - Math.pow(1 - progress, 3);
            currentView = {
                x: start.x + (destination.x - start.x) * eased,
                y: start.y + (destination.y - start.y) * eased,
                width: start.width + (destination.width - start.width) * eased,
                height: start.height + (destination.height - start.height) * eased
            };
            svg.setAttribute('viewBox', viewBoxText(currentView));
            cameraFrame = progress < 1 ? requestAnimationFrame(step) : null;
        }
        cameraFrame = requestAnimationFrame(step);
    }
    function viewBoxText(view) { return `${view.x.toFixed(3)} ${view.y.toFixed(3)} ${view.width.toFixed(3)} ${view.height.toFixed(3)}`; }
    function clamp(value, min, max) { return Math.max(min, Math.min(max, value)); }
    function overlaps(a, b) { return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y; }
});
