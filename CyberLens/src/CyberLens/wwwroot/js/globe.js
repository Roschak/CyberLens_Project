// CyberLens — 3D OSINT intelligence globe (Three.js).
// Imported by full URL (no bare specifiers) so it needs no importmap and won't
// collide with Blazor's own <ImportMap>. Custom drag/zoom instead of OrbitControls.
import * as THREE from 'https://unpkg.com/three@0.160.0/build/three.module.js';

const R = 1;                       // globe radius
const SENT = { positive: 0x2ecc71, neutral: 0xf1c40f, negative: 0xe74c3c };
const KIND = {                     // source-kind colors for the source layer
  News: 0x3987e5, SocialMedia: 0xe87ba4, Blog: 0x1baf7a,
  Forum: 0xc98500, Official: 0x9085e9, DarkWeb: 0xe74c3c
};

let S = null; // active scene state (single instance)

function latLonToVec3(lat, lon, radius) {
  const phi = (90 - lat) * Math.PI / 180;
  const theta = (lon + 180) * Math.PI / 180;
  return new THREE.Vector3(
    -radius * Math.sin(phi) * Math.cos(theta),
    radius * Math.cos(phi),
    radius * Math.sin(phi) * Math.sin(theta)
  );
}

function glowTexture() {
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d');
  const grd = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grd.addColorStop(0, 'rgba(255,255,255,1)');
  grd.addColorStop(0.25, 'rgba(255,255,255,0.7)');
  grd.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grd; g.fillRect(0, 0, 64, 64);
  return new THREE.CanvasTexture(c);
}

function tip() {
  let t = document.getElementById('globe-tip');
  if (!t) { t = document.createElement('div'); t.id = 'globe-tip'; t.className = 'd3-tip'; t.style.opacity = 0; document.body.appendChild(t); }
  return t;
}

window.clGlobeDispose = function () {
  if (!S) return;
  cancelAnimationFrame(S.raf);
  S.ro?.disconnect();
  S.renderer.dispose();
  S.el.innerHTML = '';
  S = null;
};

window.clGlobeInit = function (containerId, points, clusters) {
  window.clGlobeDispose();
  const el = document.getElementById(containerId);
  if (!el) return;
  const w = el.clientWidth || 800, h = el.clientHeight || 560;

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(45, w / h, 0.01, 100);
  camera.position.set(0, 0.4, 3);
  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  renderer.setSize(w, h); renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  el.appendChild(renderer.domElement);

  scene.add(new THREE.AmbientLight(0xffffff, 0.6));
  const key = new THREE.DirectionalLight(0xffffff, 1.1); key.position.set(3, 2, 4); scene.add(key);
  const rim = new THREE.DirectionalLight(0xff6a2a, 0.5); rim.position.set(-4, -1, -3); scene.add(rim);

  const world = new THREE.Group(); scene.add(world);

  // Globe: dark sphere, upgraded with an equirectangular texture when it loads.
  const globeMat = new THREE.MeshStandardMaterial({ color: 0x16324a, roughness: 1, metalness: 0, emissive: 0x0a1622, emissiveIntensity: 0.6 });
  const globe = new THREE.Mesh(new THREE.SphereGeometry(R, 64, 48), globeMat);
  world.add(globe);
  new THREE.TextureLoader().load(
    'https://unpkg.com/three-globe@2.31.0/example/img/earth-dark.jpg',
    tex => { tex.colorSpace = THREE.SRGBColorSpace; globeMat.map = tex; globeMat.color.set(0xffffff); globeMat.emissive.set(0x0b1a2a); globeMat.needsUpdate = true; },
    undefined, () => {/* keep plain globe on failure */ });

  // Graticule
  const grat = new THREE.LineSegments(
    graticuleGeometry(),
    new THREE.LineBasicMaterial({ color: 0x2f6f9f, transparent: true, opacity: 0.22 }));
  world.add(grat);

  // Atmosphere glow (backside shell)
  const atm = new THREE.Mesh(
    new THREE.SphereGeometry(R * 1.14, 48, 32),
    new THREE.MeshBasicMaterial({ color: 0x2b6fff, transparent: true, opacity: 0.10, side: THREE.BackSide }));
  world.add(atm);

  // Starfield (stays in scene root, doesn't spin with the world)
  scene.add(starfield());

  const gtex = glowTexture();
  const layers = {
    sentiment: new THREE.Group(),
    source: new THREE.Group(),
    events: new THREE.Group(),
    threat: new THREE.Group(),
  };
  Object.values(layers).forEach(g => world.add(g));
  const pickable = [];

  // ---- Sentiment heatmap (glow sprites) ----
  for (const p of points) {
    const pos = latLonToVec3(p.lat, p.lon, R * 1.006);
    const mat = new THREE.SpriteMaterial({ map: gtex, color: SENT[p.sentimentLabel] || SENT.neutral, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true, opacity: 0.85 });
    const s = new THREE.Sprite(mat);
    s.position.copy(pos);
    const sc = 0.05 + p.intensity * 0.012;
    s.scale.set(sc, sc, sc);
    s.userData = { time: p.timeMs, tip: `${p.location} · ${p.sourceName}<br>${p.category} · sentimen ${p.sentiment}` };
    layers.sentiment.add(s); pickable.push(s);
  }

  // ---- Source geolocation markers (cones pointing outward) ----
  for (const p of points) {
    const pos = latLonToVec3(p.lat, p.lon, R * 1.01);
    const cone = new THREE.Mesh(
      new THREE.ConeGeometry(0.012, 0.05, 8),
      new THREE.MeshStandardMaterial({ color: KIND[p.sourceKind] || 0x888888, emissive: KIND[p.sourceKind] || 0x444444, emissiveIntensity: 0.4 }));
    cone.position.copy(pos);
    cone.lookAt(0, 0, 0); cone.rotateX(Math.PI / 2); // point away from center
    cone.userData = { time: p.timeMs, tip: `${p.sourceName}<br>${p.sourceKind} · ${p.location}` };
    layers.source.add(cone); pickable.push(cone);
  }
  layers.source.visible = false;

  // ---- Event clustering bubbles (size = count) ----
  const maxCount = Math.max(1, ...clusters.map(c => c.count));
  for (const c of clusters) {
    const pos = latLonToVec3(c.lat, c.lon, R * 1.02);
    const rad = 0.03 + Math.sqrt(c.count / maxCount) * 0.12;
    const col = c.avgSentiment > 0.15 ? SENT.positive : c.avgSentiment < -0.15 ? SENT.negative : SENT.neutral;
    const bubble = new THREE.Mesh(
      new THREE.SphereGeometry(rad, 20, 16),
      new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: 0.28 }));
    bubble.position.copy(pos);
    bubble.userData = { time: 0, tip: `${c.location}<br><b>${c.count}</b> peristiwa · ${c.topCategory}` };
    layers.events.add(bubble); pickable.push(bubble);
  }
  layers.events.visible = false;

  // ---- Threat intelligence layer (pulsing red bars) ----
  const threatBars = [];
  for (const p of points.filter(x => x.threat)) {
    const dir = latLonToVec3(p.lat, p.lon, 1).normalize();
    const height = 0.08 + p.intensity * 0.02;
    const bar = new THREE.Mesh(
      new THREE.CylinderGeometry(0.006, 0.006, height, 6),
      new THREE.MeshBasicMaterial({ color: 0xff3020 }));
    bar.position.copy(dir.clone().multiplyScalar(R + height / 2));
    bar.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
    bar.userData = { time: p.timeMs, tip: `⚠ ANCAMAN<br>${p.sourceName} · ${p.location}` };
    layers.threat.add(bar); threatBars.push(bar); pickable.push(bar);
  }
  layers.threat.visible = false;

  // ---- Interaction: drag to rotate, wheel to zoom ----
  let dragging = false, px = 0, py = 0;
  let rotY = 0, tiltX = 0.15, targetRotY = 0, targetTiltX = 0.15, dist = 3, targetDist = 3;
  let autoRotate = true;

  renderer.domElement.style.cursor = 'grab';
  renderer.domElement.addEventListener('pointerdown', e => { dragging = true; px = e.clientX; py = e.clientY; renderer.domElement.style.cursor = 'grabbing'; });
  addEventListener('pointerup', () => { dragging = false; if (S) renderer.domElement.style.cursor = 'grab'; });
  addEventListener('pointermove', e => {
    if (dragging) {
      targetRotY += (e.clientX - px) * 0.006;
      targetTiltX = Math.max(-1.2, Math.min(1.2, targetTiltX + (e.clientY - py) * 0.006));
      px = e.clientX; py = e.clientY;
    }
  });
  renderer.domElement.addEventListener('wheel', e => {
    e.preventDefault();
    targetDist = Math.max(1.5, Math.min(6, targetDist + e.deltaY * 0.0016));
  }, { passive: false });

  // Hover tooltip via raycaster
  const ray = new THREE.Raycaster(); const mouse = new THREE.Vector2();
  renderer.domElement.addEventListener('pointermove', e => {
    const r = renderer.domElement.getBoundingClientRect();
    mouse.x = ((e.clientX - r.left) / r.width) * 2 - 1;
    mouse.y = -((e.clientY - r.top) / r.height) * 2 + 1;
    ray.setFromCamera(mouse, camera);
    const hit = ray.intersectObjects(pickable.filter(o => o.parent.visible && o.visible), false)[0];
    const t = tip();
    if (hit && hit.object.userData.tip) {
      t.innerHTML = hit.object.userData.tip; t.style.opacity = 1;
      t.style.left = (e.clientX + 14) + 'px'; t.style.top = (e.clientY - 10) + 'px';
    } else { t.style.opacity = 0; }
  });
  renderer.domElement.addEventListener('pointerleave', () => { tip().style.opacity = 0; });

  const ro = new ResizeObserver(() => {
    const nw = el.clientWidth, nh = el.clientHeight;
    if (nw && nh) { camera.aspect = nw / nh; camera.updateProjectionMatrix(); renderer.setSize(nw, nh); }
  });
  ro.observe(el);

  let t0 = performance.now();
  function animate() {
    const now = performance.now(); const dt = (now - t0) / 1000; t0 = now;
    if (autoRotate && !dragging) targetRotY += dt * 0.06;
    rotY += (targetRotY - rotY) * 0.12;
    tiltX += (targetTiltX - tiltX) * 0.12;
    dist += (targetDist - dist) * 0.1;
    world.rotation.y = rotY; world.rotation.x = tiltX;
    camera.position.setLength(dist);
    // pulse threat bars
    const pulse = 1 + Math.sin(now * 0.006) * 0.3;
    threatBars.forEach(b => { b.scale.y = pulse; });
    renderer.render(scene, camera);
    S.raf = requestAnimationFrame(animate);
  }

  S = {
    el, renderer, scene, camera, world, layers, ro, raf: 0, pickable,
    setAuto: v => { autoRotate = v; },
  };
  animate();
};

window.clGlobeToggleLayer = function (name, on) {
  if (S && S.layers[name]) S.layers[name].visible = on;
};

window.clGlobeSetMaxTime = function (ms) {
  if (!S) return;
  for (const o of S.pickable) {
    const t = o.userData.time || 0;
    o.visible = (t === 0) || (t <= ms); // clusters (time 0) always shown
  }
};

window.clGlobeAutoRotate = function (on) { if (S) S.setAuto(on); };

function graticuleGeometry() {
  const pts = [];
  const step = 15, seg = 64;
  for (let lat = -75; lat <= 75; lat += step) {
    for (let i = 0; i < seg; i++) {
      const l1 = -180 + (360 * i) / seg, l2 = -180 + (360 * (i + 1)) / seg;
      pts.push(latLonToVec3(lat, l1, R * 1.001), latLonToVec3(lat, l2, R * 1.001));
    }
  }
  for (let lon = -180; lon < 180; lon += step) {
    for (let i = 0; i < seg; i++) {
      const a1 = -90 + (180 * i) / seg, a2 = -90 + (180 * (i + 1)) / seg;
      pts.push(latLonToVec3(a1, lon, R * 1.001), latLonToVec3(a2, lon, R * 1.001));
    }
  }
  return new THREE.BufferGeometry().setFromPoints(pts);
}

function starfield() {
  const n = 1200, pos = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) {
    const v = new THREE.Vector3().randomDirection().multiplyScalar(20 + Math.random() * 30);
    pos[i * 3] = v.x; pos[i * 3 + 1] = v.y; pos[i * 3 + 2] = v.z;
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  return new THREE.Points(g, new THREE.PointsMaterial({ color: 0x8899aa, size: 0.12, sizeAttenuation: true }));
}
