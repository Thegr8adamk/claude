/* =====================================================================
   3D WORLD — Tech Town (three.js + Blender-made glTF assets)
   ===================================================================== */
const STREETS = [0, -36, -72, -108];
// Building placement. ext = how far porches/steps stick out in front of the wall. dz = door trigger distance from wall.
const BLD = {
  home: { x: -26, row: 0, W: 10, D: 8, ext: 2.5, dz: 1.4 },
  mouse: { x: 0, row: 0, W: 14, D: 10, ext: 2.2, dz: 1.3 },
  keys: { x: 26, row: 0, W: 12, D: 10, ext: 1.3, dz: 1.6 },
  office: { x: 26, row: 1, W: 12, D: 11, ext: 2.4, dz: 1.4 },
  web: { x: 0, row: 1, W: 14, D: 11, ext: 5.0, dz: 2.0 },
  mail: { x: -26, row: 1, W: 12, D: 10, ext: 2.6, dz: 1.3 },
  android: { x: -26, row: 2, W: 11, D: 9, ext: 1.2, dz: 1.3 },
  security: { x: 0, row: 2, W: 13, D: 10, ext: 1.4, dz: 1.3 },
  final: { x: 0, row: 3, W: 20, D: 14, ext: 5.2, dz: 2.0 },
  iphone: { x: 26, row: 2, W: 11, D: 9, ext: 1.3, dz: 1.3 },
};
const SIGNS = {
  home: { bg: '#5b3a1e', fg: '#ffe9c7', t: 'HOME SWEET HOME' }, mouse: { bg: '#ffffff', fg: '#e07b39', t: 'COMMUNITY CENTER' },
  keys: { bg: '#1f3b5a', fg: '#ffffff', t: 'TYPING SCHOOL' }, office: { bg: '#1f2d3d', fg: '#9fd0ff', t: 'THE OFFICE' },
  web: { bg: '#e6dcc6', fg: '#3a2a0a', t: 'PUBLIC LIBRARY' }, mail: { bg: '#2b4c7e', fg: '#ffffff', t: 'POST OFFICE' },
  android: { bg: '#0f5132', fg: '#3ddc84', t: 'GREEN PHONE SHOP' }, iphone: { bg: '#f7f7f9', fg: '#222222', t: 'SILVER PHONE SHOP' },
  security: { bg: '#16324f', fg: '#6fe3ff', t: 'CYBER SAFETY HQ' },
  final: { bg: '#ece4d2', fg: '#3a2a0a', t: 'CITY HALL' },
};

const World = {
  ready: false, paused: true, mode: 'title', keys: {}, pad: {}, t: 0, textures: [], markers: {}, near: null,
  colliders: [], boxes: [], ramps: [], npcs: [], cars: [], clouds: [], waters: [],
  onAction: null, onMove: null,

  init(glbBase64, done) {
    if (!window.THREE) return done(false);
    try {
      this.renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    } catch (e) { console.warn('WebGL unavailable', e); return done(false); }
    const r = this.renderer;
    r.setSize(innerWidth, innerHeight); r.setPixelRatio(Math.min(devicePixelRatio || 1, 2));
    r.shadowMap.enabled = true; r.shadowMap.type = THREE.PCFSoftShadowMap;
    r.outputEncoding = THREE.sRGBEncoding; r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = 1.05;
    $('#scene').appendChild(r.domElement);
    this.scene = new THREE.Scene();
    this.scene.fog = new THREE.Fog(0xcde9fb, 75, 190);
    this.camera = new THREE.PerspectiveCamera(50, innerWidth / innerHeight, 0.3, 500);
    this.camera.position.set(0, 40, 60);
    addEventListener('resize', () => this.resize());
    this.lights(); this.sky();
    // Decode embedded Blender assets
    let buf;
    try {
      const bin = atob(glbBase64); const u8 = new Uint8Array(bin.length);
      for (let i = 0; i < bin.length; i++) u8[i] = bin.charCodeAt(i);
      buf = u8.buffer;
    } catch (e) { console.warn('asset decode failed', e); return done(false); }
    new THREE.GLTFLoader().parse(buf, '', gltf => {
      try {
        this.assets = {};
        gltf.scene.children.forEach(o => { this.assets[o.name] = o; });
        this.prepareMaterials(gltf.scene);
        Object.keys(this.assets).forEach(k => { if (k !== 'C_person') this.toonify(this.assets[k]); });
        Object.keys(this.assets).forEach(k => { if (k !== 'C_person') this.outline(this.assets[k], k.startsWith('B_') ? .06 : .035); });
        this.buildTown();
        this.ready = true; this.refreshMarkers(); this.setRetro(S.settings.graphics === 'retro');
        this.bindInput(); this.loop(); done(true);
      } catch (e) { console.error('town build failed', e); done(false); }
    }, err => { console.error('glTF parse failed', err); done(false); });
  },

  resize() {
    if (!this.renderer) return;
    this.camera.aspect = innerWidth / innerHeight; this.camera.updateProjectionMatrix();
    this.renderer.setSize(innerWidth, innerHeight);
  },
  setRetro(on) {
    if (!this.renderer) return;
    this.retro = on;
    this.renderer.setPixelRatio(on ? 0.34 : Math.min(devicePixelRatio || 1, 2));
    this.renderer.setSize(innerWidth, innerHeight);
    this.textures.forEach(t => {
      t.magFilter = on ? THREE.NearestFilter : THREE.LinearFilter;
      t.minFilter = on ? THREE.NearestFilter : THREE.LinearMipmapLinearFilter; t.needsUpdate = true;
    });
  },

  /* ---------------- lights & sky ---------------- */
  lights() {
    const hemi = new THREE.HemisphereLight(0xeaf6ff, 0x7aa04a, 0.95); this.scene.add(hemi);
    const sun = new THREE.DirectionalLight(0xfff0d8, 1.15);
    sun.castShadow = true; sun.shadow.mapSize.set(2048, 2048);
    const c = sun.shadow.camera; c.left = -40; c.right = 40; c.top = 40; c.bottom = -40; c.near = 1; c.far = 160;
    sun.shadow.bias = -0.0004; sun.shadow.normalBias = 0.02;
    this.scene.add(sun); this.scene.add(sun.target); this.sun = sun;
    this.scene.add(new THREE.AmbientLight(0xffffff, 0.12));
  },
  sky() {
    const cv = document.createElement('canvas'); cv.width = 16; cv.height = 256; const g = cv.getContext('2d');
    const gr = g.createLinearGradient(0, 0, 0, 256);
    gr.addColorStop(0, '#1f6fe0'); gr.addColorStop(0.42, '#62b4ff'); gr.addColorStop(0.62, '#c8ecff'); gr.addColorStop(1, '#f2fbff');
    g.fillStyle = gr; g.fillRect(0, 0, 16, 256);
    const tx = new THREE.CanvasTexture(cv); tx.encoding = THREE.sRGBEncoding;
    const sky = new THREE.Mesh(new THREE.SphereGeometry(420, 24, 16), new THREE.MeshBasicMaterial({ map: tx, side: THREE.BackSide, fog: false, depthWrite: false }));
    this.scene.add(sky); this.skyMesh = sky;
    // sun disc
    const sd = new THREE.Mesh(new THREE.SphereGeometry(10, 16, 12), new THREE.MeshBasicMaterial({ color: 0xfff6d0, fog: false }));
    sd.position.set(160, 230, -300); this.scene.add(sd);
  },

  /* ---------------- procedural textures ---------------- */
  canvasTex(w, h, draw, { repeat = true, srgb = true, gltf = false } = {}) {
    const cv = document.createElement('canvas'); cv.width = w; cv.height = h; const g = cv.getContext('2d'); draw(g, w, h);
    const t = new THREE.CanvasTexture(cv);
    if (repeat) { t.wrapS = t.wrapT = THREE.RepeatWrapping; }
    if (srgb) t.encoding = THREE.sRGBEncoding;
    if (gltf || !repeat) t.flipY = false; // glTF UVs have their origin at the top-left
    t.anisotropy = 4; this.textures.push(t); return t;
  },
  shade(hex, amt) {
    const c = new THREE.Color(hex); const hsl = {}; c.getHSL(hsl);
    c.setHSL(hsl.h, hsl.s, Math.max(0, Math.min(1, hsl.l + amt))); return '#' + c.getHexString();
  },
  noise(g, w, h, base, n = 900, amt = 0.08, size = 2) {
    g.fillStyle = base; g.fillRect(0, 0, w, h);
    for (let i = 0; i < n; i++) { g.fillStyle = this.shade(base, (Math.random() - 0.5) * 2 * amt); g.fillRect(Math.random() * w, Math.random() * h, size, size); }
  },
  texFor(kind, hex) {
    const key = kind + hex; this._tc = this._tc || {}; if (this._tc[key]) return this._tc[key];
    const S_ = 256; let t;
    const self = this;
    if (kind === 'Brick') t = this.canvasTex(S_, S_, g => {
      g.fillStyle = '#d9d2c5'; g.fillRect(0, 0, S_, S_);
      const bw = 32, bh = 10.66;
      for (let r = 0; r < 24; r++) for (let c = -1; c < 9; c++) {
        const x = c * bw + (r % 2 ? bw / 2 : 0), y = r * bh;
        g.fillStyle = self.shade(hex, (Math.random() - 0.5) * 0.12); g.fillRect(x + 1.5, y + 1.5, bw - 3, bh - 3);
      }
    });
    else if (kind === 'Siding') t = this.canvasTex(S_, S_, g => {
      for (let r = 0; r < 16; r++) {
        g.fillStyle = self.shade(hex, (Math.random() - 0.5) * 0.04); g.fillRect(0, r * 16, S_, 16);
        g.fillStyle = self.shade(hex, -0.14); g.fillRect(0, r * 16 + 14, S_, 2);
        g.fillStyle = self.shade(hex, 0.08); g.fillRect(0, r * 16, S_, 2);
      }
    });
    else if (kind === 'Roof') t = this.canvasTex(S_, S_, g => {
      g.fillStyle = self.shade(hex, -0.12); g.fillRect(0, 0, S_, S_);
      for (let r = 0; r < 16; r++) for (let c = -1; c < 9; c++) {
        const x = c * 32 + (r % 2 ? 16 : 0), y = r * 16;
        g.fillStyle = self.shade(hex, (Math.random() - 0.5) * 0.12); g.fillRect(x + 1, y + 1, 30, 13);
        g.fillStyle = self.shade(hex, -0.2); g.fillRect(x + 1, y + 12, 30, 2);
      }
    });
    else if (kind === 'Stone') t = this.canvasTex(S_, S_, g => {
      self.noise(g, S_, S_, hex, 1200, 0.05);
      g.strokeStyle = self.shade(hex, -0.15); g.lineWidth = 2;
      for (let r = 0; r < 4; r++) { g.beginPath(); g.moveTo(0, r * 64); g.lineTo(S_, r * 64); g.stroke();
        for (let c = 0; c < 3; c++) { const x = c * 96 + (r % 2 ? 48 : 0); g.beginPath(); g.moveTo(x, r * 64); g.lineTo(x, r * 64 + 64); g.stroke(); } }
    });
    else if (kind === 'Stucco' || kind === 'Concrete') t = this.canvasTex(S_, S_, g => {
      self.noise(g, S_, S_, hex, 2500, kind === 'Stucco' ? 0.05 : 0.07, 2);
      if (kind === 'Concrete') { g.strokeStyle = self.shade(hex, -0.1); g.lineWidth = 1.5; g.strokeRect(0, 0, S_, S_); }
    });
    else if (kind === 'Leaf') t = this.canvasTex(128, 128, g => { self.noise(g, 128, 128, hex, 700, 0.16, 4); });
    else if (kind === 'Bark') t = this.canvasTex(64, 128, g => {
      g.fillStyle = hex; g.fillRect(0, 0, 64, 128);
      for (let i = 0; i < 26; i++) { g.fillStyle = self.shade(hex, (Math.random() - 0.5) * 0.2); g.fillRect(Math.random() * 64, 0, 2 + Math.random() * 3, 128); }
    });
    else if (kind === 'Glass') t = this.canvasTex(S_, S_, g => {
      const gr = g.createLinearGradient(0, 0, S_, S_); gr.addColorStop(0, '#9cc9ec'); gr.addColorStop(0.5, '#4f7fa8'); gr.addColorStop(1, '#6d9fca');
      g.fillStyle = gr; g.fillRect(0, 0, S_, S_);
      g.fillStyle = 'rgba(255,255,255,.18)'; g.beginPath(); g.moveTo(40, 0); g.lineTo(110, 0); g.lineTo(20, S_); g.lineTo(-50, S_); g.fill();
      g.strokeStyle = '#2a3a4a'; g.lineWidth = 4; g.strokeRect(0, 0, S_, S_); g.beginPath(); g.moveTo(S_ / 2, 0); g.lineTo(S_ / 2, S_); g.stroke();
    });
    this._tc[key] = t; return t;
  },
  windowTex() {
    return this._win || (this._win = this.canvasTex(128, 128, g => {
      const gr = g.createLinearGradient(0, 0, 0, 128); gr.addColorStop(0, '#cfe9fb'); gr.addColorStop(1, '#5e8db5');
      g.fillStyle = gr; g.fillRect(0, 0, 128, 128);
      g.fillStyle = 'rgba(255,255,255,.35)'; g.beginPath(); g.moveTo(20, 0); g.lineTo(55, 0); g.lineTo(10, 128); g.lineTo(-25, 128); g.fill();
      g.fillStyle = '#f3e7c9'; g.fillRect(4, 4, 22, 120); g.fillRect(102, 4, 22, 120); // curtains
      g.fillStyle = '#ffffff'; g.fillRect(60, 0, 8, 128); g.fillRect(0, 60, 128, 8);
      g.strokeStyle = '#ffffff'; g.lineWidth = 8; g.strokeRect(0, 0, 128, 128);
    }, { repeat: false }));
  },
  doorTex(hex) {
    this._dt = this._dt || {}; if (this._dt[hex]) return this._dt[hex];
    return this._dt[hex] = this.canvasTex(128, 256, g => {
      g.fillStyle = hex; g.fillRect(0, 0, 128, 256);
      g.strokeStyle = this.shade(hex, -0.15); g.lineWidth = 6;
      [[16, 20, 96, 90], [16, 130, 42, 110], [70, 130, 42, 110]].forEach(r => g.strokeRect(...r));
      g.fillStyle = 'rgba(190,225,255,.9)'; g.fillRect(24, 28, 80, 74);
      g.fillStyle = '#e8c35a'; g.beginPath(); g.arc(104, 140, 8, 0, 7); g.fill();
    }, { repeat: false });
  },
  signTex(id) {
    if (id === 'clock') return this.canvasTex(256, 256, g => {
      g.fillStyle = '#fffdf3'; g.beginPath(); g.arc(128, 128, 124, 0, 7); g.fill(); g.lineWidth = 10; g.strokeStyle = '#3a2a0a'; g.stroke();
      g.fillStyle = '#3a2a0a'; g.font = 'bold 34px Georgia'; g.textAlign = 'center'; g.textBaseline = 'middle';
      for (let i = 1; i <= 12; i++) { const a = i / 12 * Math.PI * 2; g.fillText(String(i), 128 + Math.sin(a) * 92, 128 - Math.cos(a) * 92); }
      g.lineCap = 'round'; g.lineWidth = 10; g.beginPath(); g.moveTo(128, 128); g.lineTo(128, 62); g.stroke();
      g.lineWidth = 7; g.beginPath(); g.moveTo(128, 128); g.lineTo(178, 150); g.stroke();
    }, { repeat: false });
    if (id === 'stop') return this.canvasTex(128, 128, g => {
      g.fillStyle = '#d63a3a'; g.beginPath();
      for (let i = 0; i < 8; i++) { const a = (i + 0.5) / 8 * Math.PI * 2; g.lineTo(64 + Math.cos(a) * 62, 64 + Math.sin(a) * 62); }
      g.fill(); g.fillStyle = '#fff'; g.font = 'bold 34px Arial'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText('STOP', 64, 66);
    }, { repeat: false });
    const s = SIGNS[id] || { bg: '#333', fg: '#fff', t: id };
    const ch = CHAPTERS.find(c => c.id === id);
    return this.canvasTex(1024, 160, g => {
      g.fillStyle = s.bg; g.fillRect(0, 0, 1024, 160);
      g.strokeStyle = s.fg; g.globalAlpha = .35; g.lineWidth = 6; g.strokeRect(10, 10, 1004, 140); g.globalAlpha = 1;
      g.fillStyle = s.fg; g.textAlign = 'center'; g.textBaseline = 'middle';
      let fs = 96; g.font = `900 ${fs}px Nunito, Arial Black, Arial, sans-serif`;
      while (g.measureText(s.t).width > 900 && fs > 30) { fs -= 4; g.font = `900 ${fs}px Nunito, Arial Black, Arial, sans-serif`; }
      g.fillText(s.t, 512, ch ? 70 : 82);
      if (ch) { g.font = '800 34px Nunito, Arial, sans-serif'; g.globalAlpha = .85; g.fillText('CHAPTER ' + ch.n, 512, 134); g.globalAlpha = 1; }
    }, { repeat: false });
  },
  faceTex() {
    return this._face || (this._face = this.canvasTex(128, 106, g => {
      g.clearRect(0, 0, 128, 106);
      g.fillStyle = '#1f1a17';
      g.beginPath(); g.ellipse(38, 46, 9, 12, 0, 0, 7); g.fill(); g.beginPath(); g.ellipse(90, 46, 9, 12, 0, 0, 7); g.fill();
      g.fillStyle = '#fff'; g.beginPath(); g.arc(41, 42, 3.5, 0, 7); g.fill(); g.beginPath(); g.arc(93, 42, 3.5, 0, 7); g.fill();
      g.strokeStyle = '#3b2416'; g.lineWidth = 5; g.lineCap = 'round';
      g.beginPath(); g.moveTo(26, 24); g.lineTo(48, 21); g.stroke(); g.beginPath(); g.moveTo(80, 21); g.lineTo(102, 24); g.stroke();
      g.strokeStyle = '#8a3324'; g.lineWidth = 5; g.beginPath(); g.arc(64, 66, 17, 0.25 * Math.PI, 0.75 * Math.PI); g.stroke();
      g.fillStyle = 'rgba(233,120,110,.45)'; g.beginPath(); g.arc(20, 70, 10, 0, 7); g.fill(); g.beginPath(); g.arc(108, 70, 10, 0, 7); g.fill();
    }, { repeat: false }));
  },

  worldUV(geo, scale) {
    const p = geo.attributes.position, n = geo.attributes.normal; if (!p || !n) return;
    const uv = new Float32Array(p.count * 2);
    for (let i = 0; i < p.count; i++) {
      const x = p.getX(i), y = p.getY(i), z = p.getZ(i), ax = Math.abs(n.getX(i)), ay = Math.abs(n.getY(i)), az = Math.abs(n.getZ(i));
      let u, v; if (ay >= ax && ay >= az) { u = x; v = z; } else if (ax >= az) { u = z; v = y; } else { u = x; v = y; }
      uv[i * 2] = u * scale; uv[i * 2 + 1] = v * scale;
    }
    geo.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
  },

  prepareMaterials(root) {
    const cache = {};
    const scaleFor = { Brick: .5, Siding: .5, Roof: .45, Stone: .3, Stucco: .25, Concrete: .25, Leaf: .6, Bark: .6, Glass: .35 };
    const make = (name) => {
      if (cache[name]) return cache[name];
      const [kind, hexRaw] = name.split('_'); const hex = hexRaw && /^[0-9a-f]{6}$/i.test(hexRaw) ? '#' + hexRaw : null;
      let m;
      if (['Brick', 'Siding', 'Roof', 'Stone', 'Stucco', 'Concrete', 'Leaf'].includes(kind)) m = new THREE.MeshStandardMaterial({ map: this.texFor(kind, hex), roughness: .9, flatShading: kind === 'Leaf' });
      else if (kind === 'Bark') m = new THREE.MeshStandardMaterial({ map: this.texFor('Bark', '#6b4a2e'), roughness: 1 });
      else if (kind === 'Glass') m = new THREE.MeshStandardMaterial({ map: this.texFor('Glass', '#5f8fb8'), roughness: .15, metalness: .35 });
      else if (kind === 'Window') m = new THREE.MeshStandardMaterial({ map: this.windowTex(), roughness: .25, metalness: .1, emissive: 0x223344, emissiveIntensity: .2 });
      else if (kind === 'Door') m = new THREE.MeshStandardMaterial({ map: this.doorTex(hex), roughness: .7 });
      else if (kind === 'Sign') m = new THREE.MeshStandardMaterial({ map: this.signTex(hexRaw), roughness: .6, emissive: 0xffffff, emissiveMap: this.signTex(hexRaw), emissiveIntensity: .25 });
      else if (kind === 'Face') m = new THREE.MeshBasicMaterial({ map: this.faceTex(), transparent: true, depthWrite: false });
      else if (kind === 'Water') { const t = this.canvasTex(128, 128, g => { this.noise(g, 128, 128, '#3f9ad8', 300, .12, 6); g.strokeStyle = 'rgba(255,255,255,.45)'; g.lineWidth = 2; for (let i = 0; i < 9; i++) { g.beginPath(); g.arc(Math.random() * 128, Math.random() * 128, 6 + Math.random() * 14, 0, 3); g.stroke(); } });
        m = new THREE.MeshStandardMaterial({ map: t, roughness: .1, metalness: .2, emissive: 0x0d3350, emissiveIntensity: .4 }); this.waters.push(t); }
      else if (kind === 'Glow') m = new THREE.MeshBasicMaterial({ color: hex || '#fff2b0' });
      else {
        const src = root.getObjectByProperty ? null : null; // placeholder (keep original color)
        m = null;
      }
      cache[name] = m; return m;
    };
    root.traverse(o => {
      if (!o.isMesh) return;
      o.castShadow = true; o.receiveShadow = true;
      const nm = o.material && o.material.name || '';
      const kind = nm.split('_')[0];
      const m = make(nm);
      if (m) {
        o.material = m;
        if ({ Brick: 1, Siding: 1, Roof: 1, Stone: 1, Stucco: 1, Concrete: 1, Leaf: 1, Bark: 1, Glass: 1 }[kind]) this.worldUV(o.geometry, ({ Brick: .5, Siding: .5, Roof: .45, Stone: .3, Stucco: .25, Concrete: .25, Leaf: .6, Bark: .6, Glass: .35 })[kind]);
        if (kind === 'Face' || kind === 'Glow') o.castShadow = false;
      } else if (o.material) {
        o.material.roughness = Math.max(o.material.roughness ?? .8, .35);
      }
    });
    void scaleFor;
  },

  /* ---------------- town construction ---------------- */
  add(name, x, z, rotY = 0, scale = 1) {
    const src = this.assets[name]; if (!src) return null;
    const o = src.clone(true); o.position.set(x, 0, z); o.rotation.y = rotY; o.scale.setScalar(scale);
    this.scene.add(o); (this.statics = this.statics || []).push(o); return o;
  },
  circle(x, z, r) { this.colliders.push({ x, z, r }); },
  block(x0, x1, z0, z1) { this.boxes.push({ x0: Math.min(x0, x1), x1: Math.max(x0, x1), z0: Math.min(z0, z1), z1: Math.max(z0, z1) }); },
  bldCenterZ(id) { const b = BLD[id]; return STREETS[b.row] - 5.5 - b.ext - b.D / 2; },
  // Convert a Blender-space offset (bx, by) of building id into world x,z
  bw(id, bx, by) { return { x: BLD[id].x + bx, z: this.bldCenterZ(id) - by }; },

  buildTown() {
    const sc = this.scene;
    // ground
    const grass = this.canvasTex(256, 256, g => { this.noise(g, 256, 256, '#78c850', 5000, .1, 2); for (let i = 0; i < 400; i++) { g.fillStyle = 'rgba(40,90,30,.35)'; g.fillRect(Math.random() * 256, Math.random() * 256, 1, 4); } });
    grass.repeat.set(90, 90);
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(420, 420), new THREE.MeshStandardMaterial({ map: grass, roughness: 1 }));
    ground.rotation.x = -Math.PI / 2; ground.receiveShadow = true; sc.add(ground);

    // roads
    const roadTex = (rot) => this.canvasTex(256, 256, g => {
      this.noise(g, 256, 256, '#4a4f57', 4000, .08, 2);
      g.save(); if (rot) { g.translate(128, 128); g.rotate(Math.PI / 2); g.translate(-128, -128); }
      g.fillStyle = '#f2c230'; g.fillRect(0, 124, 140, 8);
      g.fillStyle = '#e8e8e8'; g.fillRect(0, 6, 256, 5); g.fillRect(0, 245, 256, 5); g.restore();
    });
    const rt = roadTex(false); rt.repeat.set(92 / 6, 1);
    const at = roadTex(true); at.repeat.set(1, 120 / 6);
    const plainAsph = this.canvasTex(128, 128, g => this.noise(g, 128, 128, '#4a4f57', 1500, .08, 2)); plainAsph.repeat.set(1.5, 1.5);
    const walkTex = this.canvasTex(256, 256, g => { this.noise(g, 256, 256, '#cfc9bd', 2500, .05, 2); g.strokeStyle = '#a9a396'; g.lineWidth = 3; for (let i = 0; i <= 4; i++) { g.beginPath(); g.moveTo(i * 64, 0); g.lineTo(i * 64, 256); g.stroke(); g.beginPath(); g.moveTo(0, i * 64); g.lineTo(256, i * 64); g.stroke(); } });
    const flat = (w, d, x, z, y, mat) => { const m = new THREE.Mesh(new THREE.PlaneGeometry(w, d), mat); m.rotation.x = -Math.PI / 2; m.position.set(x, y, z); m.receiveShadow = true; sc.add(m); return m; };
    const slab = (w, d, x, z, h, mat) => { const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat); m.position.set(x, h / 2, z); m.receiveShadow = true; sc.add(m); this.worldUV(m.geometry, .25); return m; };
    const walkMat = new THREE.MeshStandardMaterial({ map: walkTex, roughness: .95 });
    STREETS.forEach(s => {
      flat(92, 6, 0, s, .02, new THREE.MeshStandardMaterial({ map: rt, roughness: .95 }));
      slab(92, 2.4, 0, s - 4.2, .16, walkMat); slab(92, 2.4, 0, s + 4.2, .16, walkMat);
    });
    [-13, 13].forEach(x => {
      flat(6, 120, x, -56, .03, new THREE.MeshStandardMaterial({ map: at, roughness: .95 }));
      STREETS.forEach(s => {
        flat(6.2, 6.2, x, s, .04, new THREE.MeshStandardMaterial({ map: plainAsph, roughness: .95 }));
        for (let k = 0; k < 5; k++) { const cw = new THREE.Mesh(new THREE.PlaneGeometry(.55, 2.4), new THREE.MeshStandardMaterial({ color: 0xeeeeee, roughness: .9 })); cw.rotation.x = -Math.PI / 2; cw.position.set(x - 2.2 + k * 1.1, .05, s - 4.2); sc.add(cw); const cw2 = cw.clone(); cw2.position.z = s + 4.2; sc.add(cw2); }
      });
    });
    // park plaza
    const plaza = new THREE.Mesh(new THREE.CircleGeometry(9, 40), new THREE.MeshStandardMaterial({ map: walkTex, roughness: .95 }));
    plaza.rotation.x = -Math.PI / 2; plaza.position.set(0, .03, 14); plaza.receiveShadow = true; sc.add(plaza);
    this.worldUV(plaza.geometry, .25);
    flat(3, 6, 0, 8, .025, walkMat);

    // buildings
    Object.keys(BLD).forEach(id => {
      const b = BLD[id], zc = this.bldCenterZ(id);
      const o = this.add('B_' + id, b.x, zc); if (!o) return;
      if (id === 'final') { this.block(b.x - 4, b.x + 4, zc - b.D / 2, zc + b.D / 2); this.block(b.x - 10, b.x + 10, zc - b.D / 2, zc + b.D / 2 - 1); }
      else this.block(b.x - b.W / 2, b.x + b.W / 2, zc - b.D / 2, zc + b.D / 2);
    });
    // detail colliders (columns, posts, planters, flagpoles) in Blender offsets
    const C = (id, bx, by, r) => { const p = this.bw(id, bx, by); this.circle(p.x, p.z, r); };
    [[-1.8, -5.6], [1.8, -5.6]].forEach(([x, y]) => C('home', x, y, .25));
    C('mouse', -2.2, -7, .2); C('mouse', 2.2, -7, .2); C('mouse', -8.2, -6, .2);
    C('office', -2.8, -7.7, .2); C('office', 2.8, -7.7, .2);
    [-4.2, -1.5, 1.5, 4.2].forEach(x => C('web', x, -8.1, .5)); C('web', -6.3, -6.5, .7); C('web', 6.3, -6.5, .7);
    C('mail', 3.55, -7.3, .5); C('mail', -7, -6, .2);
    C('android', -4.2, -5.2, .6); C('android', 4.2, -5.2, .6); C('iphone', -4.2, -5.3, .6); C('iphone', 4.2, -5.3, .6);
    [-3.8, -2.3, -.8, .8, 2.3, 3.8].forEach(x => C('final', x, -9.7, .45)); C('final', -9, -9, .2); C('final', 9, -9, .2);
    // platforms / ramps (porticos and steps) so the player walks up them
    const R = (id, bx0, bx1, by0, by1, h0, h1) => { const a = this.bw(id, bx0, by0), b = this.bw(id, bx1, by1); this.ramps.push({ x0: Math.min(a.x, b.x), x1: Math.max(a.x, b.x), z0: Math.min(a.z, b.z), z1: Math.max(a.z, b.z), zh0: a.z, zh1: b.z, h0, h1 }); };
    R('web', -5, 5, -5.5, -8.7, .9, .9); R('web', -5, 5, -8.7, -10.5, .9, 0);
    R('final', -4.6, 4.6, -7, -10.4, 1.0, 1.0); R('final', -4.6, 4.6, -10.4, -12.4, 1.0, 0);
    R('home', -2, 2, -4, -5.8, .3, .3); R('home', -.8, .8, -5.8, -6.5, .3, 0);
    R('keys', -1.6, 1.6, -5, -6.2, .54, 0);

    // back yards are hedged off so the camera never ends up inside a building
    const rows = [[-30.5, -18.5], [-66.5, -58.5], [-102.5, -91]];
    rows.forEach(([a, b]) => { this.block(-60, -16.2, a, b); this.block(-9.8, 9.8, a, b); this.block(16.2, 60, a, b); });
    this.block(-60, 60, -180, -134.5);
    rows.forEach(([a, b]) => {
      for (let x = -44; x <= 44; x += 5.5) { if (Math.abs(Math.abs(x) - 13) < 4) continue;
        this.add(Math.random() < .5 ? 'P_tree_round' : 'P_tree_pine', x + (Math.random() - .5) * 2, (a + b) / 2 + (Math.random() - .5) * 5, Math.random() * 6, .9 + Math.random() * .4);
        this.add('P_bush', x + 2, a + .6, Math.random() * 6, 1); this.add('P_bush', x + 2, b - .6, Math.random() * 6, 1); }
    });
    for (let x = -44; x <= 44; x += 6) { this.add('P_tree_pine', x, -137 - Math.random() * 6, 0, 1 + Math.random() * .5); this.add('P_tree_round', x + 3, -142 - Math.random() * 6, 0, 1.2); }
    // City Hall garden on the last street
    [[-30, -118], [-22, -126], [-36, -128], [30, -118], [22, -126], [36, -128], [-16, -130], [16, -130]].forEach(([x, z], i) => { this.add(i % 2 ? 'P_tree_round' : 'P_tree_pine', x, z, i, 1.2); this.circle(x, z, .6); });
    [[-18, -116], [18, -116], [-28, -122], [28, -122]].forEach(([x, z]) => { this.add('P_planter', x, z); this.circle(x, z, .7); });
    // outer tree border (sides and south)
    for (let z = 22; z >= -136; z -= 7) { [-50, 50].forEach(x => this.add(Math.random() < .5 ? 'P_tree_pine' : 'P_tree_round', x + (Math.random() - .5) * 4, z, Math.random() * 6, 1 + Math.random() * .5)); }
    for (let x = -48; x <= 48; x += 6) this.add(Math.random() < .5 ? 'P_tree_pine' : 'P_tree_round', x, 29 + Math.random() * 4, Math.random() * 6, 1.1 + Math.random() * .4);
    // lawn trees in front strips between buildings
    [[-39, 0], [-39, 1], [39, 0], [39, 1], [-39, 2], [39, 2], [-17.8, 2], [17.8, 2]].forEach(([x, r]) => { const z = STREETS[r] - 9; this.add('P_tree_round', x, z, 0, 1); this.circle(x, z, .5); });

    // park
    this.add('P_fountain', 0, 14); this.circle(0, 14, 3.2);
    [[-6.5, 18, .6], [6.5, 18, -.6], [-6.5, 10, 2.5], [6.5, 10, -2.5]].forEach(([x, z, r]) => { this.add('P_bench', x, z, r + Math.PI); this.circle(x, z, 1.0); });
    [[-10, 20], [10, 20], [-14, 12], [14, 12], [-16, 21], [16, 21], [-22, 16], [22, 16], [-30, 12], [30, 12], [-36, 19], [36, 19]].forEach(([x, z], i) => { this.add(i % 3 ? 'P_tree_round' : 'P_tree_pine', x, z, i, 1 + (i % 3) * .15); this.circle(x, z, .5); });
    [[-4, 7.2], [4, 7.2]].forEach(([x, z]) => { this.add('P_planter', x, z); this.circle(x, z, .7); });
    this.add('P_trash', 3, 20); this.circle(3, 20, .4);
    // street furniture: lamps, hydrants, stop signs
    STREETS.forEach((s, ri) => {
      for (let x = -40; x <= 40; x += 13) {
        if ([-26, 0, 26].some(dx => Math.abs(x - dx) < 5) || Math.abs(Math.abs(x) - 13) < 4) continue;
        const l = this.add('P_lamp', x, s - 5.4, Math.PI); this.circle(x, s - 5.4, .25);
        const l2 = this.add('P_lamp', x + 6, s + 5.4, 0); this.circle(x + 6, s + 5.4, .25);
        void l; void l2;
      }
      this.add('P_hydrant', -18.5, s + 5.6); this.circle(-18.5, s + 5.6, .3);
      this.add('P_sign', 16.4, s + 5.7, 0); this.circle(16.4, s + 5.7, .15); this.add('P_sign', -9.6, s - 5.7, Math.PI); this.circle(-9.6, s - 5.7, .15);
      void ri;
    });
    this.hills(); this.makeClouds();
    // characters
    this.player = this.makePerson({ shirt: S.shirt, hair: S.hair, skin: S.skin, pants: '#34405a', long: S.hairStyle === 'long' });
    sc.add(this.player.root);
    this.maya = this.makePerson({ shirt: '#7b4fd6', hair: '#2b1a12', skin: '#8d5a3b', pants: '#2f3b52', maya: true });
    this.maya.root.position.set(-3.6, 0, 10.5); this.maya.root.rotation.y = .5; sc.add(this.maya.root);
    this.maya.label = this.label('Maya · Tech Guide', '#7b4fd6'); this.maya.label.position.set(0, 2.2, 0); this.maya.root.add(this.maya.label);
    this.playerLabel = this.label(S.name || 'You', '#2d7ff9'); this.playerLabel.position.set(0, 2.2, 0); this.player.root.add(this.playerLabel);
    this.spawnNPCs(); this.spawnCars(); this.makeMarkers(); this.makeGuideArrow(); this.makeFoliage(); this.makeFairy(); this.spawnEnemies(); this.spawnItems();
    this.toonify(this.scene);
    this.mergeStatics();
    const p = S.pos; this.player.root.position.set(p ? p.x : -2, 0, p ? p.z : 8.5);
    this.player.root.rotation.y = Math.PI;
    this.camTarget = new THREE.Vector3();
  },

  hills() {
    const mat = new THREE.MeshStandardMaterial({ color: 0x6a9a58, flatShading: true, roughness: 1 });
    const mat2 = new THREE.MeshStandardMaterial({ color: 0x8aa4b8, flatShading: true, roughness: 1 });
    for (let i = 0; i < 26; i++) {
      const a = i / 26 * Math.PI * 2, r = 150 + Math.random() * 40;
      const far = Math.random() < .45;
      const h = new THREE.Mesh(new THREE.ConeGeometry(30 + Math.random() * 25, far ? 55 + Math.random() * 40 : 18 + Math.random() * 16, 7), far ? mat2 : mat);
      h.position.set(Math.cos(a) * r, (far ? 20 : 6), -55 + Math.sin(a) * r); this.scene.add(h);
    }
  },
  makeClouds() {
    const m = new THREE.MeshStandardMaterial({ color: 0xffffff, flatShading: true, roughness: 1, emissive: 0xffffff, emissiveIntensity: .25 });
    for (let i = 0; i < 14; i++) {
      const g = new THREE.Group();
      for (let k = 0; k < 5; k++) { const s = new THREE.Mesh(new THREE.IcosahedronGeometry(3 + Math.random() * 3, 1), m); s.position.set(k * 3.5 - 7, Math.random() * 2, Math.random() * 3); s.scale.y = .6; g.add(s); }
      g.position.set(-150 + Math.random() * 300, 45 + Math.random() * 25, -180 + Math.random() * 200); g.userData.v = .6 + Math.random() * .8;
      this.scene.add(g); this.clouds.push(g);
    }
  },

  label(text, color) {
    const cv = document.createElement('canvas'); cv.width = 512; cv.height = 96; const g = cv.getContext('2d');
    g.font = '900 44px Nunito, Arial, sans-serif'; const w = Math.min(500, g.measureText(text).width + 50);
    g.fillStyle = 'rgba(255,253,247,.95)'; const x = (512 - w) / 2;
    g.beginPath(); g.moveTo(x + 30, 8); g.arcTo(x + w, 8, x + w, 88, 30); g.arcTo(x + w, 88, x, 88, 30); g.arcTo(x, 88, x, 8, 30); g.arcTo(x, 8, x + w, 8, 30); g.fill();
    g.fillStyle = color; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(text, 256, 50);
    const t = new THREE.CanvasTexture(cv); t.encoding = THREE.sRGBEncoding;
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, depthTest: false, transparent: true })); s.scale.set(2.8, .52, 1); s.renderOrder = 10;
    return s;
  },

  /* ---------------- cel shading ---------------- */
  grad() {
    if (this._grad) return this._grad;
    const d = new Uint8Array([90, 90, 90, 255, 175, 175, 175, 255, 255, 255, 255, 255]);
    const t = new THREE.DataTexture(d, 3, 1, THREE.RGBAFormat); t.minFilter = t.magFilter = THREE.NearestFilter; t.generateMipmaps = false; t.needsUpdate = true;
    return this._grad = t;
  },
  toon(o) { const m = new THREE.MeshToonMaterial({ gradientMap: this.grad() }); ['map', 'color', 'emissive', 'emissiveMap', 'emissiveIntensity', 'transparent', 'opacity'].forEach(k => { if (o[k] !== undefined && o[k] !== null) { if (m[k] && m[k].copy && o[k].isColor) m[k].copy(o[k]); else m[k] = o[k]; } }); m.name = o.name || ''; return m; },
  toonify(root) {
    const cache = new Map();
    root.traverse(o => {
      if (!o.isMesh || !o.material || !o.material.isMeshStandardMaterial || o.material.userData.real) return;
      if (!cache.has(o.material)) { const src = o.material; const m = this.toon(src); if (src.color && !src.map) { const hsl = {}; m.color.getHSL(hsl); m.color.setHSL(hsl.h, Math.min(1, hsl.s * 1.15), hsl.l); } cache.set(src, m); }
      o.material = cache.get(o.material);
    });
  },
  outlineMat(th) {
    this._om = this._om || {}; if (this._om[th]) return this._om[th];
    const m = new THREE.MeshBasicMaterial({ color: 0x1b1a2b, side: THREE.BackSide });
    m.onBeforeCompile = sh => { sh.vertexShader = sh.vertexShader.replace('#include <begin_vertex>', 'vec3 transformed = position + normal * ' + th.toFixed(3) + ';'); };
    return this._om[th] = m;
  },
  outline(root, th = .045) {
    const skip = /^(Window|Sign|Face|Glow|Door|Water)/;
    const list = []; root.traverse(o => { if (o.isMesh && !o.userData.isOutline && !skip.test(o.material.name || '')) list.push(o); });
    list.forEach(o => { const l = new THREE.Mesh(o.geometry, this.outlineMat(th)); l.userData.isOutline = true; l.castShadow = false; l.receiveShadow = false; o.add(l); });
  },
  makePerson({ shirt, hair, skin, pants, maya = false, long = false, bun = false }) {
    const root = this.assets['C_person'].clone(true);
    const parts = {}; ['Torso', 'Head', 'ArmL', 'ArmR', 'LegL', 'LegR', 'Glasses', 'HairBun', 'HairLong'].forEach(n => parts[n] = root.getObjectByName(n));
    const recolor = { Shirt: shirt, Hair: hair, Skin: skin, Pants: pants };
    const mats = {};
    root.traverse(o => {
      if (!o.isMesh) return; o.castShadow = true;
      const nm = o.material.name.split('_')[0];
      if (recolor[nm]) { mats[nm] = mats[nm] || (() => { const m = new THREE.MeshStandardMaterial({ color: new THREE.Color(recolor[nm]).convertSRGBToLinear(), roughness: nm === 'Skin' ? .55 : nm === 'Hair' ? .5 : .85, metalness: 0 }); m.name = o.material.name; return m; })(); o.material = mats[nm]; }
      o.receiveShadow = true; o.material.userData.real = true;
    });
    if (parts.Glasses) parts.Glasses.visible = maya; if (parts.HairBun) parts.HairBun.visible = maya || bun; if (parts.HairLong) parts.HairLong.visible = long;
    root.scale.setScalar(1.0);
    return { root, parts, mats, phase: Math.random() * 6, speed: 0, pose: null };
  },
  recolorPlayer() {
    if (!this.player) return;
    const m = this.player.mats;
    if (m.Shirt) m.Shirt.color = new THREE.Color(S.shirt).convertSRGBToLinear();
    if (m.Hair) m.Hair.color = new THREE.Color(S.hair).convertSRGBToLinear();
    if (m.Skin) m.Skin.color = new THREE.Color(S.skin).convertSRGBToLinear();
    const hl = this.player.parts.HairLong; if (hl) hl.visible = S.hairStyle === 'long';
    this.player.root.remove(this.playerLabel);
    this.playerLabel = this.label(S.name || 'You', '#2d7ff9'); this.playerLabel.position.set(0, 2.2, 0); this.player.root.add(this.playerLabel);
  },
  animatePerson(p, dt, moving) {
    p.phase += dt * (moving ? 9 : 2);
    const sw = moving ? Math.sin(p.phase) * .7 : 0;
    const k = 1 - Math.exp(-12 * dt);
    const lerp = (o, v) => { if (o) o.rotation.x += (v - o.rotation.x) * k; };
    if (p.pose === 'sit') { lerp(p.parts.LegL, -1.5); lerp(p.parts.LegR, -1.5); lerp(p.parts.ArmL, -.3); lerp(p.parts.ArmR, -.3); }
    else if (p.pose === 'attack') { lerp(p.parts.ArmR, -2.3); lerp(p.parts.ArmL, .4); }
    else if (p.pose === 'cheer') { lerp(p.parts.ArmL, -3.0); lerp(p.parts.ArmR, -3.0); lerp(p.parts.LegL, 0); lerp(p.parts.LegR, 0); }
    else { lerp(p.parts.ArmL, sw); lerp(p.parts.ArmR, -sw); lerp(p.parts.LegL, -sw * .9); lerp(p.parts.LegR, sw * .9); }
    if (p.parts.Torso) p.parts.Torso.position.y = p.parts.Torso.userData.y0 ?? (p.parts.Torso.userData.y0 = p.parts.Torso.position.y);
    const bob = p.pose === 'sit' ? 0 : moving ? Math.abs(Math.sin(p.phase)) * .06 : Math.sin(p.phase) * .012;
    p.root.children.forEach(c => { if (c.userData.y0 === undefined) c.userData.y0 = c.position.y; c.position.y = c.userData.y0 + bob; });
  },

  spawnNPCs() {
    const looks = NPCS.map(n => [n.shirt, n.hair, n.skin]);
    const paths = [[[-40, 4.4], [40, 4.4]], [[40, -4.4], [-40, -4.4]], [[-40, -31.6], [40, -31.6]], [[40, -40.4], [-40, -40.4]], [[-40, -103.6], [40, -103.6]]];
    looks.forEach((l, i) => {
      const p = this.makePerson({ shirt: l[0], hair: l[1], skin: l[2], pants: '#3a3f4a', long: !!NPCS[i].long, bun: !!NPCS[i].bun });
      p.path = paths[i]; p.t = Math.random(); p.dir = 1; p.root.position.set(0, .16, 0); this.scene.add(p.root); this.npcs.push(p);
    });
  },
  spawnCars() {
    const cols = ['#e5484d', '#2d7ff9', '#f5b700', '#23a55a', '#ffffff', '#7b4fd6'];
    cols.forEach((c, i) => {
      const car = this.assets['P_car'].clone(true);
      const m = new THREE.MeshToonMaterial({ color: new THREE.Color(c).convertSRGBToLinear(), gradientMap: this.grad() });
      car.traverse(o => { if (o.isMesh && o.material.name === 'Paint') o.material = m; });
      const row = i % 4, dir = i % 2 ? -1 : 1;
      car.userData = { row, dir, x: -45 + Math.random() * 90, v: 7 + Math.random() * 3, cur: 0 };
      car.position.set(car.userData.x, 0, STREETS[row] + (dir > 0 ? 1.5 : -1.5));
      car.rotation.y = dir > 0 ? Math.PI / 2 : -Math.PI / 2; // model front is +Z: face the driving direction
      this.scene.add(car); this.cars.push(car);
    });
  },

  makeMarkers() {
    CHAPTERS.forEach(ch => {
      const d = this.doorPos(ch.id);
      const ring = new THREE.Mesh(new THREE.RingGeometry(1.05, 1.4, 40), new THREE.MeshBasicMaterial({ color: 0xffc400, transparent: true, opacity: .85, side: THREE.DoubleSide, depthWrite: false }));
      ring.rotation.x = -Math.PI / 2; ring.position.set(d.x, d.y + .06, d.z); this.scene.add(ring);
      const beam = new THREE.Mesh(new THREE.CylinderGeometry(1.1, 1.3, 40, 20, 1, true), new THREE.MeshBasicMaterial({ color: 0xffd24a, transparent: true, opacity: .22, side: THREE.DoubleSide, depthWrite: false, blending: THREE.AdditiveBlending }));
      beam.position.set(d.x, d.y + 20, d.z); this.scene.add(beam);
      const arrow = new THREE.Mesh(new THREE.ConeGeometry(.7, 1.3, 4), new THREE.MeshStandardMaterial({ color: 0xffc400, emissive: 0xffa000, emissiveIntensity: .6, flatShading: true }));
      arrow.rotation.x = Math.PI; arrow.position.set(d.x, 5, d.z); arrow.castShadow = true; this.scene.add(arrow);
      const badge = new THREE.Sprite(new THREE.SpriteMaterial({ transparent: true, depthTest: true })); badge.scale.set(2.4, 2.4, 1);
      const b = BLD[ch.id]; const topY = { home: 9, mouse: 8, keys: 15, office: 22, web: 11.5, mail: 9, android: 11, iphone: 12, security: 15, final: 19.5 }[ch.id];
      badge.position.set(b.x, topY, this.bldCenterZ(ch.id)); this.scene.add(badge);
      this.markers[ch.id] = { ring, beam, arrow, badge, state: null };
    });
    this.refreshMarkers();
  },
  badgeTex(state, n) {
    this._bt = this._bt || {}; const key = state + n; if (this._bt[key]) return this._bt[key];
    const cv = document.createElement('canvas'); cv.width = cv.height = 128; const g = cv.getContext('2d');
    const col = { done: '#23a55a', next: '#f5a000', locked: '#8a94a0' }[state];
    g.fillStyle = '#fff'; g.beginPath(); g.arc(64, 64, 60, 0, 7); g.fill(); g.fillStyle = col; g.beginPath(); g.arc(64, 64, 52, 0, 7); g.fill();
    g.fillStyle = '#fff'; g.textAlign = 'center'; g.textBaseline = 'middle';
    if (state === 'done') { g.strokeStyle = '#fff'; g.lineWidth = 14; g.lineCap = 'round'; g.beginPath(); g.moveTo(38, 66); g.lineTo(56, 84); g.lineTo(90, 44); g.stroke(); }
    else if (state === 'locked') { g.fillRect(40, 60, 48, 36); g.strokeStyle = '#fff'; g.lineWidth = 9; g.beginPath(); g.arc(64, 58, 15, Math.PI, 0); g.stroke(); g.fillStyle = col; g.fillRect(60, 70, 8, 14); }
    else { g.font = '900 64px Nunito, Arial'; g.fillText(String(n), 64, 68); }
    const t = new THREE.CanvasTexture(cv); t.encoding = THREE.sRGBEncoding; return this._bt[key] = t;
  },
  refreshMarkers() {
    if (!this.ready) return;
    const ni = nextIndex();
    CHAPTERS.forEach((ch, i) => {
      const m = this.markers[ch.id]; if (!m) return;
      const state = isDone(i) ? 'done' : (i === ni ? 'next' : 'locked'); m.state = state;
      m.ring.material.color.set(state === 'done' ? 0x23c55e : state === 'next' ? 0xffc400 : 0x9aa4af);
      m.ring.material.opacity = state === 'locked' ? .35 : .85;
      m.beam.visible = state === 'next'; m.arrow.visible = state === 'next';
      m.badge.material.map = this.badgeTex(state, ch.n); m.badge.material.needsUpdate = true;
    });
    if (this.playerLabel && S.name && this.playerLabel.userData.name !== S.name) { this.recolorPlayer(); this.playerLabel.userData.name = S.name; }
  },
  makeGuideArrow() {
    const sh = new THREE.Shape(); sh.moveTo(0, 1.3); sh.lineTo(.75, .2); sh.lineTo(.28, .2); sh.lineTo(.28, -.8); sh.lineTo(-.28, -.8); sh.lineTo(-.28, .2); sh.lineTo(-.75, .2); sh.lineTo(0, 1.3);
    const m = new THREE.Mesh(new THREE.ShapeGeometry(sh), new THREE.MeshBasicMaterial({ color: 0xffc400, transparent: true, opacity: .9, depthWrite: false }));
    m.rotation.x = -Math.PI / 2; const g = new THREE.Group(); g.add(m); m.position.z = 0; this.scene.add(g); this.guide = g;
  },

  makeFoliage() {
    const inBld = (x, z) => Object.keys(BLD).some(id => { const b = BLD[id], zc = this.bldCenterZ(id); return Math.abs(x - b.x) < b.W / 2 + b.ext + 1 && z < zc + b.D / 2 + b.ext + 1.5 && z > zc - b.D / 2 - 1; });
    const onRoad = (x, z) => STREETS.some(s => Math.abs(z - s) < 6) || (Math.abs(Math.abs(x) - 13) < 3.6 && z < 6 && z > -76) || Math.hypot(x, z - 14) < 9.5 || (Math.abs(x) < 1.8 && z > 5 && z < 12);
    const tuft = new THREE.ConeGeometry(.09, .55, 3); tuft.translate(0, .27, 0);
    const blades = new THREE.BufferGeometry(); const parts = [];
    for (let k = 0; k < 3; k++) { const g = tuft.clone(); g.rotateZ((k - 1) * .35); g.rotateY(k * 2.1); parts.push(g); }
    const pos = [], nor = []; parts.forEach(g => { const ng = g.toNonIndexed(); pos.push(...ng.attributes.position.array); nor.push(...ng.attributes.normal.array); });
    blades.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); blades.setAttribute('normal', new THREE.Float32BufferAttribute(nor, 3));
    const gm = new THREE.MeshToonMaterial({ color: 0x4fae3a, gradientMap: this.grad() });
    const N = 2600; const im = new THREE.InstancedMesh(blades, gm, N); const d = new THREE.Object3D(); let n = 0;
    for (let i = 0; i < N * 3 && n < N; i++) {
      const x = -48 + Math.random() * 96, z = -99 + Math.random() * 126; if (onRoad(x, z) || inBld(x, z)) continue;
      d.position.set(x, 0, z); d.rotation.y = Math.random() * 6; d.scale.setScalar(.7 + Math.random() * .8); d.updateMatrix(); im.setMatrixAt(n++, d.matrix);
    }
    im.count = n; im.receiveShadow = true; this.scene.add(im);
    const fl = new THREE.IcosahedronGeometry(.12, 0); fl.translate(0, .32, 0);
    ['#ff5d8f', '#ffd93d', '#ffffff', '#b28dff', '#ff8c42'].forEach(c => {
      const fm = new THREE.InstancedMesh(fl, new THREE.MeshToonMaterial({ color: c, gradientMap: this.grad(), emissive: c, emissiveIntensity: .15 }), 160); let k = 0;
      for (let i = 0; i < 700 && k < 160; i++) { const x = -48 + Math.random() * 96, z = -99 + Math.random() * 126; if (onRoad(x, z) || inBld(x, z)) continue; d.position.set(x, 0, z); d.rotation.set(0, 0, 0); d.scale.setScalar(1); d.updateMatrix(); fm.setMatrixAt(k++, d.matrix); }
      fm.count = k; this.scene.add(fm);
    });
  },
  makeFairy() {
    const g = new THREE.Group();
    const core = new THREE.Mesh(new THREE.SphereGeometry(.16, 12, 8), new THREE.MeshBasicMaterial({ color: 0xfffbe0 }));
    const cv = document.createElement('canvas'); cv.width = cv.height = 64; const c = cv.getContext('2d');
    const gr = c.createRadialGradient(32, 32, 2, 32, 32, 32); gr.addColorStop(0, 'rgba(255,250,210,1)'); gr.addColorStop(.35, 'rgba(160,230,255,.55)'); gr.addColorStop(1, 'rgba(120,200,255,0)');
    c.fillStyle = gr; c.fillRect(0, 0, 64, 64);
    const halo = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(cv), blending: THREE.AdditiveBlending, depthWrite: false, transparent: true })); halo.scale.set(1.6, 1.6, 1);
    const wm = new THREE.MeshBasicMaterial({ color: 0xcff4ff, transparent: true, opacity: .75, side: THREE.DoubleSide, depthWrite: false });
    const wg = new THREE.CircleGeometry(.22, 10); wg.scale(1, .55, 1);
    const w1 = new THREE.Mesh(wg, wm), w2 = new THREE.Mesh(wg, wm); w1.position.x = .22; w2.position.x = -.22;
    g.add(core, halo, w1, w2); g.userData = { w1, w2, pos: new THREE.Vector3(), trip: 0 };
    this.scene.add(g); this.fairy = g; this.idle = 0;
  },
  updateFairy(dt, moving) {
    const f = this.fairy; if (!f) return; const u = f.userData; const pl = this.player.root.position;
    this.idle = moving ? 0 : this.idle + dt;
    let tx = pl.x + Math.cos(this.t * 1.3) * 1.3, tz = pl.z + Math.sin(this.t * 1.3) * 1.3, ty = pl.y + 2.1 + Math.sin(this.t * 2.2) * .2;
    const ni = nextIndex();
    if (this.idle > 5 && ni >= 0 && this.mode === 'play') { // fly toward the next door to show the way
      const d = this.doorPos(CHAPTERS[ni].id); const dx = d.x - pl.x, dz = d.z - pl.z, dist = Math.hypot(dx, dz);
      if (dist > 6) { const k = (Math.sin((this.idle - 5) * 1.2 - Math.PI / 2) + 1) / 2 * Math.min(8, dist - 2); tx = pl.x + dx / dist * k; tz = pl.z + dz / dist * k; }
    }
    const k = 1 - Math.exp(-dt * 3.5); f.position.x += (tx - f.position.x) * k; f.position.y += (ty - f.position.y) * k; f.position.z += (tz - f.position.z) * k;
    const flap = Math.sin(this.t * 40) * .9; u.w1.rotation.y = flap; u.w2.rotation.y = -flap; f.lookAt(this.camera.position.x, f.position.y, this.camera.position.z);
  },
  itemGet(icon, done) {
    const p = this.player; this.mode = 'cutscene'; p.pose = 'cheer'; p.root.rotation.y = 0;
    const cv = document.createElement('canvas'); cv.width = cv.height = 128; const c = cv.getContext('2d');
    const gr = c.createRadialGradient(64, 64, 4, 64, 64, 64); gr.addColorStop(0, 'rgba(255,245,180,.95)'); gr.addColorStop(1, 'rgba(255,200,60,0)');
    c.fillStyle = gr; c.fillRect(0, 0, 128, 128); c.font = '72px serif'; c.textAlign = 'center'; c.textBaseline = 'middle'; c.fillText(icon, 64, 70);
    const t = new THREE.CanvasTexture(cv); t.encoding = THREE.sRGBEncoding;
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, depthTest: false, transparent: true })); sp.scale.set(1.4, 1.4, 1); sp.renderOrder = 12;
    sp.position.set(0, 2.3, 0); p.root.add(sp); this.playerLabel.visible = false;
    const pl = p.root.position; const k = innerWidth / innerHeight < 1 ? 1.2 : 1;
    this.cutCam = { pos: new THREE.Vector3(pl.x, pl.y + 2.4 * k, pl.z + 5.2 * k), look: new THREE.Vector3(pl.x, pl.y + 1.6, pl.z) };
    setTimeout(() => { p.root.remove(sp); p.pose = null; this.playerLabel.visible = true; this.cutCam = null; this.mode = 'play'; done && done(); }, 3200);
  },
  // Bake every static prop/building into one mesh per material: ~2000 meshes become a few dozen draw calls
  mergeStatics() {
    const groups = new Map();
    (this.statics || []).forEach(r => {
      r.updateMatrixWorld(true);
      r.traverse(o => {
        if (!o.isMesh || o.isInstancedMesh) return;
        let g = o.geometry.index ? o.geometry.toNonIndexed() : o.geometry.clone();
        g.applyMatrix4(o.matrixWorld);
        const key = o.material.uuid + (o.castShadow ? 's' : '');
        if (!groups.has(key)) groups.set(key, { mat: o.material, cast: o.castShadow, outline: !!o.userData.isOutline, list: [] });
        groups.get(key).list.push(g);
      });
      this.scene.remove(r);
    });
    groups.forEach(({ mat, cast, outline, list }) => {
      let n = 0; list.forEach(g => n += g.attributes.position.count);
      const pos = new Float32Array(n * 3), nor = new Float32Array(n * 3), uv = new Float32Array(n * 2); let off = 0;
      list.forEach(g => {
        const c = g.attributes.position.count;
        for (let i = 0; i < c; i++) {
          pos[(off + i) * 3] = g.attributes.position.getX(i); pos[(off + i) * 3 + 1] = g.attributes.position.getY(i); pos[(off + i) * 3 + 2] = g.attributes.position.getZ(i);
          if (g.attributes.normal) { nor[(off + i) * 3] = g.attributes.normal.getX(i); nor[(off + i) * 3 + 1] = g.attributes.normal.getY(i); nor[(off + i) * 3 + 2] = g.attributes.normal.getZ(i); }
          if (g.attributes.uv) { uv[(off + i) * 2] = g.attributes.uv.getX(i); uv[(off + i) * 2 + 1] = g.attributes.uv.getY(i); }
        }
        off += c; g.dispose();
      });
      const geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.BufferAttribute(pos, 3)); geo.setAttribute('normal', new THREE.BufferAttribute(nor, 3)); geo.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
      geo.computeBoundingSphere();
      const m = new THREE.Mesh(geo, mat); m.castShadow = cast && !outline; m.receiveShadow = !outline; m.matrixAutoUpdate = false;
      this.scene.add(m);
    });
    this.statics = [];
  },
  /* ---------------- things to PICKUP / USE / THROW ---------------- */
  spawnItems() {
    const T = c => new THREE.MeshToonMaterial({ color: c, gradientMap: this.grad() });
    // Data gems to PICKUP
    const spots = [[-12, 19], [12, 20.5], [-30, 7], [30, 7], [-38, -4.2], [38, -4.2], [-40, -31.8], [40, -40.2], [-13, -24], [13, -50], [-40, -67.8], [40, -76.2]];
    const gcols = [0x3ee6ff, 0xff5dd2, 0x9dff6a, 0xffd23f];
    this.gems = spots.map(([x, z], id) => {
      const m = new THREE.Mesh(new THREE.OctahedronGeometry(.36, 0), new THREE.MeshToonMaterial({ color: gcols[id % 4], gradientMap: this.grad(), emissive: gcols[id % 4], emissiveIntensity: .35 }));
      m.scale.y = 1.4; m.position.set(x, 1, z); m.castShadow = true; this.outline(m, .03); this.scene.add(m);
      const got = (S.gems || []).includes(id); m.visible = !got;
      return { id, x, z, m, got };
    });
    // Basketball + hoop in the park
    const cv = document.createElement('canvas'); cv.width = 128; cv.height = 64; const g = cv.getContext('2d');
    g.fillStyle = '#f07c1e'; g.fillRect(0, 0, 128, 64); g.strokeStyle = '#2a1405'; g.lineWidth = 3;
    [[0, 32, 128, 32], [32, 0, 32, 64], [96, 0, 96, 64]].forEach(([a, b, c, d]) => { g.beginPath(); g.moveTo(a, b); g.lineTo(c, d); g.stroke(); });
    const bt = new THREE.CanvasTexture(cv); bt.encoding = THREE.sRGBEncoding;
    const ball = new THREE.Mesh(new THREE.SphereGeometry(.3, 16, 12), new THREE.MeshToonMaterial({ map: bt, gradientMap: this.grad() }));
    ball.castShadow = true; this.outline(ball, .02); ball.position.set(11, .3, 9.5); this.scene.add(ball);
    this.ball = { m: ball, state: 'ground', t: 0 };
    const hx = 17, hz = 8.5, hoop = new THREE.Group();
    const pole = new THREE.Mesh(new THREE.CylinderGeometry(.09, .09, 3.6, 8), T(0x2e3a46)); pole.position.set(0, 1.8, -.5);
    const board = new THREE.Mesh(new THREE.BoxGeometry(1.6, 1.1, .08), T(0xffffff)); board.position.set(0, 3.5, -.3);
    const sq = new THREE.Mesh(new THREE.BoxGeometry(.6, .45, .09), T(0xe5484d)); sq.position.set(0, 3.4, -.29);
    const rim = new THREE.Mesh(new THREE.TorusGeometry(.32, .035, 6, 16), T(0xff6a00)); rim.rotation.x = Math.PI / 2; rim.position.set(0, 3.05, .08);
    const net = new THREE.Mesh(new THREE.CylinderGeometry(.3, .2, .45, 10, 1, true), new THREE.MeshBasicMaterial({ color: 0xffffff, wireframe: true })); net.position.set(0, 2.8, .08);
    hoop.add(pole, board, sq, rim, net); this.outline(hoop, .03); hoop.position.set(hx, 0, hz); this.scene.add(hoop);
    this.circle(hx, hz - .5, .3);
    this.hoop = { x: hx, z: hz, rim: new THREE.Vector3(hx, 3.05, hz + .08) };
    // Mission items: Ken's lost phone and the Post Office mailbox
    const ph = new THREE.Group(); const body = new THREE.Mesh(new THREE.BoxGeometry(.32, .04, .62), T(0x1f2226)); const scr = new THREE.Mesh(new THREE.PlaneGeometry(.27, .54), new THREE.MeshBasicMaterial({ color: 0x7fd3ff }));
    scr.rotation.x = -Math.PI / 2; scr.position.y = .025; ph.add(body, scr); ph.position.set(-19, .08, 23.5); ph.rotation.y = .6; this.outline(ph, .02); this.scene.add(ph); this.lostPhone = ph;
    this.phoneGlow = this.label('📱 ?', '#e5484d'); this.phoneGlow.position.set(0, .9, 0); this.phoneGlow.scale.set(1.2, .25, 1); ph.add(this.phoneGlow);
    this.mailboxPos = this.bw('mail', 3.55, -7.3);
    this.refreshMissionItems();
    // Benches you can USE (sit on)
    this.benches = [[-6.5, 18, .6], [6.5, 18, -.6], [-6.5, 10, 2.5], [6.5, 10, -2.5]].map(([x, z, r]) => ({ x, z, r: r + Math.PI }));
  },
  refreshMissionItems() {
    if (!this.lostPhone) return;
    const s = S.stats || {};
    this.lostPhone.visible = typeof Missions !== 'undefined' && Missions.active('lostphone') && !s.phoneFound;
  },
  updateItems(dt) {
    (this.gems || []).forEach(g => { if (g.got) { if (g.m.visible) { g.m.scale.multiplyScalar(1 + dt * 4); g.m.position.y += dt * 4; if (g.m.position.y > 4) g.m.visible = false; } return; } g.m.rotation.y += dt * 2; g.m.position.y = 1 + Math.sin(this.t * 2 + g.id) * .15; });
    const b = this.ball; if (!b) return;
    if (b.state === 'flying') {
      b.t += dt / b.dur; const t = Math.min(1, b.t);
      b.m.position.lerpVectors(b.from, b.to, t); b.m.position.y += Math.sin(t * Math.PI) * b.arc; b.m.rotation.x += dt * 8;
      if (t >= 1) { if (b.score) { b.state = 'drop'; b.vy = 0; } else { b.state = 'ground'; b.m.position.y = .3; } }
    } else if (b.state === 'drop') {
      b.vy -= 12 * dt; b.m.position.y += b.vy * dt; b.m.position.z += dt * 1.2;
      if (b.m.position.y <= .3) { b.m.position.y = .3; b.state = 'ground'; }
    }
  },
  collectGem(g) { g.got = true; S.gems = S.gems || []; if (!S.gems.includes(g.id)) S.gems.push(g.id); this.spark(g.m.position, 1.4); return S.gems.length; },
  pickBall() {
    const b = this.ball, arm = this.player.parts.ArmR; b.state = 'held';
    arm.add(b.m); b.m.position.set(0, -.66, .14); b.m.scale.setScalar(1);
  },
  throwBall() {
    const b = this.ball, pl = this.player.root.position; const wp = new THREE.Vector3(); b.m.getWorldPosition(wp);
    this.player.root.rotation.y = Math.atan2(this.hoop.x - pl.x, this.hoop.z - pl.z);
    this.player.parts.ArmR.remove(b.m); this.scene.add(b.m); b.m.scale.setScalar(1); b.m.position.copy(wp);
    const dist = Math.hypot(this.hoop.x - pl.x, this.hoop.z - pl.z), score = dist < 4.8;
    b.from = wp.clone(); b.to = score ? this.hoop.rim.clone().add(new THREE.Vector3(0, .1, 0)) : new THREE.Vector3(pl.x + (this.hoop.x - pl.x) * .6, .3, pl.z + (this.hoop.z - pl.z) * .6);
    b.arc = 1.8; b.dur = .9; b.t = 0; b.score = score; b.state = 'flying';
    this.player.pose = 'attack'; setTimeout(() => { if (this.player.pose === 'attack') this.player.pose = null; }, 300);
    return score;
  },
  sitOn(bench) {
    const pl = this.player; this.sitting = bench; pl.pose = 'sit';
    pl.root.position.x = bench.x - Math.sin(bench.r) * .1; pl.root.position.z = bench.z - Math.cos(bench.r) * .1; pl.root.rotation.y = bench.r;
  },
  /* ---------------- Glitch enemies (typing battles) ---------------- */
  makeEnemy(color, kind = 'mini') {
    const g = new THREE.Group(); const T = c => new THREE.MeshToonMaterial({ color: c, gradientMap: this.grad() });
    const body = new THREE.Mesh(new THREE.IcosahedronGeometry(.6, 1), T(color)); body.scale.set(1, .82, 1); body.position.y = .55; body.castShadow = true;
    const white = T(0xffffff), black = new THREE.MeshBasicMaterial({ color: 0x151520 });
    [-.2, .2].forEach(x => {
      const e = new THREE.Mesh(new THREE.SphereGeometry(.16, 10, 8), white); e.position.set(x, .68, .43); g.add(e);
      const p = new THREE.Mesh(new THREE.BoxGeometry(.09, .12, .04), black); p.position.set(x, .68, .58); g.add(p);
      const a = new THREE.Mesh(new THREE.CylinderGeometry(.025, .025, .45, 5), T(0x2a2340)); a.position.set(x * 1.3, 1.18, 0); a.rotation.z = -x * 1.4; g.add(a);
      const tip = new THREE.Mesh(new THREE.SphereGeometry(.08, 8, 6), new THREE.MeshBasicMaterial({ color: 0x9dff6a })); tip.position.set(x * 2.1, 1.38, 0); g.add(tip);
    });
    const mouth = new THREE.Mesh(new THREE.BoxGeometry(.3, .05, .04), black); mouth.position.set(0, .42, .55); g.add(mouth);
    [[-.3, .25], [.3, .25], [-.3, -.25], [.3, -.25]].forEach(([x, z]) => { const f = new THREE.Mesh(new THREE.SphereGeometry(.13, 8, 6), T(0x2a2340)); f.position.set(x, .1, z); f.scale.y = .6; g.add(f); });
    if (kind === 'big' || kind === 'king') { // spikes
      for (let k = 0; k < 7; k++) { const a = k / 7 * Math.PI * 2; const sp = new THREE.Mesh(new THREE.ConeGeometry(.12, .38, 5), T(0x2a2340)); sp.position.set(Math.cos(a) * .42, .78, Math.sin(a) * .42 - .05); sp.rotation.set(Math.sin(a) * .7, 0, -Math.cos(a) * .7); g.add(sp); }
    }
    if (kind === 'king') { // crown
      const gold = new THREE.MeshToonMaterial({ color: 0xffc83d, gradientMap: this.grad(), emissive: 0x6a4a00, emissiveIntensity: .4 });
      const band = new THREE.Mesh(new THREE.CylinderGeometry(.34, .38, .18, 12, 1, true), gold); band.position.y = 1.14; band.material.side = THREE.DoubleSide; g.add(band);
      for (let k = 0; k < 6; k++) { const a = k / 6 * Math.PI * 2; const pt = new THREE.Mesh(new THREE.ConeGeometry(.07, .22, 4), gold); pt.position.set(Math.cos(a) * .34, 1.33, Math.sin(a) * .34); g.add(pt); }
      const gem = new THREE.Mesh(new THREE.OctahedronGeometry(.08), new THREE.MeshBasicMaterial({ color: 0x39c7ff })); gem.position.set(0, 1.16, .37); g.add(gem);
    }
    g.add(body); this.outline(g, .03);
    const scale = { tiny: .75, mini: 1, big: 1.45, king: 2.3 }[kind]; g.scale.setScalar(scale); g.userData.scale = scale;
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ transparent: true, depthTest: false })); const spriteY = { tiny: 2.9, mini: 2.4, big: 1.75, king: 1.65 }[kind]; sp.position.set(0, spriteY, .4); sp.renderOrder = 11; sp.visible = false; g.add(sp);
    g.userData = { body, sprite: sp, scale, spriteY }; return g;
  },
  letterTex(ch) {
    this._lt = this._lt || {}; if (this._lt[ch]) return this._lt[ch];
    const cv = document.createElement('canvas'); cv.width = cv.height = 128; const c = cv.getContext('2d');
    c.fillStyle = 'rgba(0,0,0,.25)'; c.beginPath(); c.roundRect ? c.roundRect(14, 18, 104, 104, 20) : c.rect(14, 18, 104, 104); c.fill();
    c.fillStyle = '#fff7d6'; c.strokeStyle = '#d8a200'; c.lineWidth = 8; c.beginPath(); c.roundRect ? c.roundRect(10, 8, 104, 104, 20) : c.rect(10, 8, 104, 104); c.fill(); c.stroke();
    c.fillStyle = '#1f2d3d'; c.font = '900 78px Nunito, Arial Black, Arial'; c.textAlign = 'center'; c.textBaseline = 'middle'; c.fillText(ch.toUpperCase(), 62, 64);
    const t = new THREE.CanvasTexture(cv); t.encoding = THREE.sRGBEncoding; return this._lt[ch] = t;
  },
  wordTex(word) {
    this._wt = this._wt || {}; if (this._wt[word]) return this._wt[word];
    const W = 90 + word.length * 62, cv = document.createElement('canvas'); cv.width = W; cv.height = 128; const c = cv.getContext('2d');
    const rr = (x, y, w, h) => { c.beginPath(); c.roundRect ? c.roundRect(x, y, w, h, 20) : c.rect(x, y, w, h); };
    c.fillStyle = 'rgba(0,0,0,.25)'; rr(14, 18, W - 24, 104); c.fill();
    c.fillStyle = '#fff7d6'; c.strokeStyle = '#d8a200'; c.lineWidth = 8; rr(10, 8, W - 24, 104); c.fill(); c.stroke();
    c.fillStyle = '#1f2d3d'; c.font = '900 72px Nunito, Arial Black, Arial'; c.textAlign = 'center'; c.textBaseline = 'middle'; c.fillText(word.toUpperCase(), (W - 14) / 2, 64);
    const t = new THREE.CanvasTexture(cv); t.encoding = THREE.sRGBEncoding; t.userData = { aspect: W / 128 }; return this._wt[word] = t;
  },
  spawnEnemies() {
    this.enemies = [];
    // [x, z, kind]  mini = press a letter, big = type a short word (after Typing School), king = boss (after Cyber Safety HQ)
    const spots = [[-20, 18], [20, 18], [-38, 10], [38, 10], [-18.5, -12, 'big'], [18.5, -12], [40, -12, 'big'], [-18.5, -48], [18.5, -48, 'big'], [-40, -48],
      [-18.5, -84, 'big'], [18.5, -84], [-24, -118, 'big'], [24, -118], [-35, 5.6], [35, 5.6, 'big'], [-6, -30.8], [6, -41.2], [-35, -41.2, 'big'], [35, -30.8],
      [-6, -66.8], [6, -77.2], [-35, -77.2, 'big'], [35, -66.8], [-40, -113], [40, -113, 'big'], [-13, -88], [13, -88], [-30, 16.5], [30, 17], [27, -123, 'king']];
    const cols = [0x9b5de5, 0xf15bb5, 0x00bbf9, 0xff6b35, 0x00c49a];
    spots.forEach(([x, z, kind0], i) => {
      const kind = kind0 || (i % 3 === 0 ? 'tiny' : 'mini');
      const g = this.makeEnemy(kind === 'king' ? 0xd62246 : kind === 'big' ? [0x7b2ff7, 0xe5484d, 0x1f9d55][i % 3] : cols[i % cols.length], kind); g.position.set(x, 0, z); this.scene.add(g);
      const maxHp = { tiny: 2, mini: 3, big: 5, king: 8 }[kind]; // bigger Glitch = more hits
      this.enemies.push({ g, kind, maxHp, radius: { tiny: .45, mini: .55, big: .8, king: 1.3 }[kind], home: { x, z }, hp: maxHp, dead: false, respawn: 0, cool: 0, tx: x, tz: z, wt: 0, hit: 0, letter: null, word: null, typed: '', id: i });
    });
    this.battle = null;
  },
  wordMode(en) { return (en.kind === 'big' && S.done.includes('keys')) || en.kind === 'king'; },
  pickWord(en, prev) {
    const list = en.kind === 'king' ? ['virus', 'update', 'backup', 'spam', 'scam', 'lock', 'safe', 'shield', 'secure', 'report'] : ['cat', 'sun', 'dog', 'bug', 'key', 'fun', 'web', 'app', 'map', 'mail', 'click', 'mouse', 'type', 'home', 'wifi', 'save', 'send'];
    let w; do { w = list[Math.floor(Math.random() * list.length)]; } while (w === prev); return w;
  },
  pickLetter(prev) { const L = 'abcdefghijklmnopqrstuvwxyz'; let c; do { c = L[Math.floor(Math.random() * 26)]; } while (c === prev); return c; },
  updateEnemies(dt) {
    if (!this.enemies) return; const pl = this.player.root.position;
    this.enemies.forEach(en => {
      const g = en.g, u = g.userData;
      if (en.dead) {
        const s = Math.max(0.001, g.scale.x - dt * 3); g.scale.setScalar(s); if (s <= .001) g.visible = false;
        en.respawn -= dt; if (en.respawn <= 0) { en.dead = false; en.hp = en.maxHp; g.visible = true; g.scale.setScalar(.01); g.position.set(en.home.x, 0, en.home.z); }
        return;
      }
      if (en.kind === 'king' && !S.done.includes('security')) { g.visible = false; return; }
      if (!g.visible) g.visible = true;
      const full = u.scale || 1; if (g.scale.x < full) g.scale.setScalar(Math.min(full, g.scale.x + dt * 2 * full));
      en.cool = Math.max(0, en.cool - dt);
      const dist = Math.hypot(pl.x - g.position.x, pl.z - g.position.z);
      const inBattle = this.battle === en;
      if (inBattle) {
        // hop beside the player so the Glitch is never hidden behind them
        const side = en.side || (en.side = (g.position.x >= pl.x ? 1 : -1));
        const tx = pl.x + side * (1.7 + en.radius), tz = pl.z - .4, ddx = tx - g.position.x, ddz = tz - g.position.z, dd = Math.hypot(ddx, ddz);
        if (dd > .1 && en.hit <= 0) { const st = Math.min(dd, dt * 5); const pos = { x: g.position.x + ddx / dd * st, z: g.position.z + ddz / dd * st }; this.collide(pos, en.radius, en); g.position.x = pos.x; g.position.z = pos.z; }
        g.rotation.y = Math.atan2(pl.x - g.position.x, pl.z - g.position.z);
        if (Math.hypot(pl.x - en.bx, pl.z - en.bz) > 7 || this.mode !== 'play') { this.endBattle('fled'); }
      } else {
        en.wt -= dt;
        if (en.wt <= 0) { en.wt = 2 + Math.random() * 3; en.tx = en.home.x + (Math.random() - .5) * 5; en.tz = en.home.z + (Math.random() - .5) * 5; }
        const dx = en.tx - g.position.x, dz = en.tz - g.position.z, d = Math.hypot(dx, dz);
        if (d > .2) { const pos = { x: g.position.x + dx / d * dt * 1.4, z: g.position.z + dz / d * dt * 1.4 }; this.collide(pos, en.radius, en); g.position.x = pos.x; g.position.z = pos.z; g.rotation.y = Math.atan2(dx, dz); }
        if (!this.battle && !this.paused && this.mode === 'play' && S.tutorial && en.cool <= 0 && dist < 3.6 + en.radius && !Dialog.open) this.startBattle(en);
      }
      // hop & hit flash
      const hop = Math.abs(Math.sin(this.t * (inBattle ? 7 : 5) + en.id)) * .18;
      u.body.position.y = .55 + hop; u.sprite.position.y = u.spriteY + hop * .5; u.body.scale.set(1 + hop * .3, .82 - hop * .3, 1 + hop * .3);
      if (en.hit > 0) { en.hit -= dt; const on = Math.sin(en.hit * 40) > 0; u.body.material.emissive = u.body.material.emissive || new THREE.Color(); u.body.material.emissive.setRGB(on ? .9 : 0, on ? .9 : 0, on ? .9 : 0); const kb = { x: g.position.x + (g.position.x - pl.x) * dt * 1.5, z: g.position.z + (g.position.z - pl.z) * dt * 1.5 }; this.collide(kb, en.radius, en); g.position.x = kb.x; g.position.z = kb.z; }
      else if (u.body.material.emissive) u.body.material.emissive.setRGB(0, 0, 0);
    });
    if (this.sparks) this.sparks = this.sparks.filter(s => { s.life -= dt; s.m.scale.multiplyScalar(1 + dt * 3); s.m.material.opacity = Math.max(0, s.life / .5); if (s.life <= 0) { this.scene.remove(s.m); return false; } return true; });
  },
  startBattle(en) {
    this.battle = en; en.side = 0; en.bx = this.player.root.position.x; en.bz = this.player.root.position.z; en.typed = '';
    if (this.wordMode(en)) { en.word = this.pickWord(en); en.letter = null; } else { en.word = null; en.letter = this.pickLetter(); }
    en.g.userData.body.material = en.g.userData.body.material.clone();
    this.showLetter(en); this.onBattle && this.onBattle('start', en);
  },
  showLetter(en) {
    const sp = en.g.userData.sprite, sc = en.g.userData.scale || 1; const tex = en.word ? this.wordTex(en.word) : this.letterTex(en.letter);
    sp.material.map = tex; sp.material.needsUpdate = true; sp.visible = true;
    const h = 1.9 / sc, w = en.word ? h * tex.userData.aspect * .8 : h; let k = .1;
    const grow = () => { k = Math.min(1, k + .12); sp.scale.set(w * k, (en.word ? h * .8 : h) * k, 1); if (k < 1) requestAnimationFrame(grow); }; grow();
  },
  endBattle(why) {
    const en = this.battle; if (!en) return; this.battle = null; en.g.userData.sprite.visible = false; en.cool = why === 'fled' ? 6 : 0;
    this.onBattle && this.onBattle(why, en);
  },
  attack(letter) {
    const en = this.battle; if (!en) return null;
    if (en.word) {
      const want = en.word[en.typed.length];
      if (letter !== want) return { ok: false, want, word: en.word, typed: en.typed };
      en.typed += letter;
      if (en.typed.length < en.word.length) return { ok: true, partial: true, word: en.word, typed: en.typed };
      en.typed = '';
    } else if (letter !== en.letter) return { ok: false, want: en.letter };
    const p = this.player; p.pose = 'attack'; setTimeout(() => { if (p.pose === 'attack') p.pose = null; }, 280);
    const pl = p.root.position; p.root.rotation.y = Math.atan2(en.g.position.x - pl.x, en.g.position.z - pl.z);
    en.hp--; en.hit = .35; this.spark(en.g.position);
    if (en.hp <= 0) { en.dead = true; en.respawn = { tiny: 20, mini: 25, big: 45, king: 120 }[en.kind]; this.spark(en.g.position, 2.2 * (en.g.userData.scale || 1)); this.endBattle('won'); return { ok: true, won: true, kind: en.kind }; }
    if (en.word) en.word = this.pickWord(en, en.word); else en.letter = this.pickLetter(en.letter);
    this.showLetter(en);
    return { ok: true, won: false, next: en.letter, word: en.word, hp: en.hp };
  },
  spark(pos, size = 1) {
    const cv = this._sparkCv || (this._sparkCv = (() => { const cv = document.createElement('canvas'); cv.width = cv.height = 64; const c = cv.getContext('2d'); c.translate(32, 32); c.fillStyle = '#fff6a8';
      for (let i = 0; i < 8; i++) { c.rotate(Math.PI / 4); c.beginPath(); c.moveTo(0, -30); c.lineTo(5, 0); c.lineTo(-5, 0); c.fill(); } return cv; })());
    const m = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(cv), transparent: true, depthTest: false, blending: THREE.AdditiveBlending }));
    m.position.set(pos.x, .9, pos.z); m.scale.set(size, size, 1); m.renderOrder = 13; this.scene.add(m);
    (this.sparks = this.sparks || []).push({ m, life: .5 });
  },
  doorPos(id) {
    const b = BLD[id]; const zc = this.bldCenterZ(id);
    const z = zc + b.D / 2 + b.dz;
    const p = { x: b.x, z }; p.y = this.heightAt(p.x, p.z); return p;
  },
  heightAt(x, z) {
    let h = 0;
    for (const r of this.ramps) {
      if (x < r.x0 || x > r.x1 || z < r.z0 || z > r.z1) continue;
      const t = r.zh1 === r.zh0 ? 0 : (z - r.zh0) / (r.zh1 - r.zh0);
      h = Math.max(h, r.h0 + (r.h1 - r.h0) * Math.max(0, Math.min(1, t)));
    }
    // sidewalks
    if (!h) for (const s of STREETS) { if (Math.abs(z - (s - 4.2)) < 1.2 || Math.abs(z - (s + 4.2)) < 1.2) { if (!(Math.abs(Math.abs(x) - 13) < 3)) h = .16; } }
    return h;
  },

  /* ---------------- input ---------------- */
  bindInput() {
    const map = { ArrowUp: 'up', KeyW: 'up', ArrowDown: 'down', KeyS: 'down', ArrowLeft: 'left', KeyA: 'left', ArrowRight: 'right', KeyD: 'right' };
    addEventListener('keydown', e => {
      if (this.paused || Modal.open) return;
      const tag = (e.target && e.target.tagName) || '';
      if (tag === 'INPUT' || tag === 'TEXTAREA') return;
      if (this.battle && e.key && e.key.length === 1 && /[a-z]/i.test(e.key) && !e.ctrlKey && !e.metaKey && !e.altKey) { e.preventDefault(); this.onLetter && this.onLetter(e.key.toLowerCase()); return; }
      if (!this.battle && this.near && this.near.cmd && e.key && e.key.length === 1 && /[a-z]/i.test(e.key) && !e.ctrlKey && !e.metaKey && !e.altKey) {
        e.preventDefault(); const k = e.key.toLowerCase();
        if (e.repeat || (this.keys[{ w: 'up', a: 'left', s: 'down', d: 'right' }[k]] && performance.now() - this.nearT < 600)) return; // still holding a walking key
        this.onCmdKey && this.onCmdKey(k); return;
      }
      const d = map[e.code] || map[e.key];
      if (d) { this.keys[d] = true; e.preventDefault(); this.onMove && this.onMove(d); }
      if (!this.battle && this.near && this.near.cmd && !e.ctrlKey && !e.metaKey && !e.altKey) {
        if (e.key === 'Backspace' || e.key === 'Enter') { e.preventDefault(); this.onCmdKey && this.onCmdKey(e.key); return; }
      }
    });
    addEventListener('keyup', e => { const d = map[e.code] || map[e.key]; if (d) this.keys[d] = false; });
    addEventListener('blur', () => { this.keys = {}; this.pad = {}; });
    $$('.mp').forEach(b => {
      const d = b.dataset.dir;
      const on = e => { e.preventDefault(); this.pad[d] = true; b.classList.add('down-on'); try { b.setPointerCapture(e.pointerId); } catch (_) { } this.onMove && this.onMove(d); };
      const off = () => { this.pad[d] = false; b.classList.remove('down-on'); };
      b.addEventListener('pointerdown', on); b.addEventListener('pointerup', off); b.addEventListener('pointercancel', off); b.addEventListener('lostpointercapture', off);
    });
  },
  pause(p) { this.paused = p; this.keys = {}; this.pad = {}; if (!p) this.last = performance.now(); },
  held(d) { return this.keys[d] || this.pad[d]; },

  /* ---------------- simulation ---------------- */
  pushBox(pos, r, b) {
    const cx = Math.max(b.x0, Math.min(pos.x, b.x1)), cz = Math.max(b.z0, Math.min(pos.z, b.z1));
    const dx = pos.x - cx, dz = pos.z - cz, d = Math.hypot(dx, dz);
    if (d >= r) return;
    if (d > 1e-4) { pos.x = cx + dx / d * r; pos.z = cz + dz / d * r; return; }
    const opts = [[b.x0 - r - pos.x, 0], [b.x1 + r - pos.x, 0], [0, b.z0 - r - pos.z], [0, b.z1 + r - pos.z]];
    opts.sort((a, c) => Math.abs(a[0] + a[1]) - Math.abs(c[0] + c[1])); pos.x += opts[0][0]; pos.z += opts[0][1];
  },
  // Moving things that are solid too: cars, people, Glitches (rebuilt every frame)
  buildDynamic() {
    const d = this.dynamic = [];
    this.cars.forEach(c => d.push({ ref: c, box: { x0: c.position.x - 2.15, x1: c.position.x + 2.15, z0: c.position.z - 1.05, z1: c.position.z + 1.05 } }));
    this.npcs.forEach(n => d.push({ ref: n, x: n.root.position.x, z: n.root.position.z, r: .35 }));
    d.push({ ref: this.maya, x: this.maya.root.position.x, z: this.maya.root.position.z, r: .35 });
    d.push({ ref: this.player, x: this.player.root.position.x, z: this.player.root.position.z, r: .4 });
    (this.enemies || []).forEach(en => { if (!en.dead && en.g.visible) d.push({ ref: en, x: en.g.position.x, z: en.g.position.z, r: en.radius }); });
  },
  collide(pos, r, self) {
    for (let it = 0; it < 3; it++) {
      if (this.dynamic) for (const o of this.dynamic) {
        if (o.ref === self) continue;
        if (o.box) { this.pushBox(pos, r, o.box); continue; }
        const dx = pos.x - o.x, dz = pos.z - o.z, dd = Math.hypot(dx, dz), m = o.r + r;
        if (dd < m && dd > 1e-4) { pos.x = o.x + dx / dd * m; pos.z = o.z + dz / dd * m; }
      }
      for (const c of this.colliders) {
        const dx = pos.x - c.x, dz = pos.z - c.z, d = Math.hypot(dx, dz), m = c.r + r;
        if (d < m && d > 1e-4) { pos.x = c.x + dx / d * m; pos.z = c.z + dz / d * m; }
      }
      for (const b of this.boxes) this.pushBox(pos, r, b);
    }
    pos.x = Math.max(-46, Math.min(46, pos.x)); pos.z = Math.max(-135, Math.min(25, pos.z));
  },

  update(dt) {
    this.t += dt;
    const pl = this.player.root;
    this.buildDynamic();
    let mx = 0, mz = 0;
    if (!this.paused && this.mode === 'play') {
      if (this.held('up')) mz -= 1; if (this.held('down')) mz += 1; if (this.held('left')) mx -= 1; if (this.held('right')) mx += 1;
    }
    const moving = !!(mx || mz);
    if (moving) {
      const l = Math.hypot(mx, mz); mx /= l; mz /= l;
      const sp = 7.2 * dt; const pos = { x: pl.position.x + mx * sp, z: pl.position.z + mz * sp };
      this.collide(pos, .5, this.player); pl.position.x = pos.x; pl.position.z = pos.z;
      const target = Math.atan2(mx, mz); let da = target - pl.rotation.y; da = Math.atan2(Math.sin(da), Math.cos(da));
      pl.rotation.y += da * Math.min(1, dt * 12);
      this.saveT = (this.saveT || 0) + dt; if (this.saveT > 2) { this.saveT = 0; S.pos = { x: +pl.position.x.toFixed(2), z: +pl.position.z.toFixed(2) }; save(); }
    }
    if (this.sitting && moving) { this.sitting = null; this.player.pose = null; }
    const gy = this.sitting ? -.4 : this.heightAt(pl.position.x, pl.position.z); pl.position.y += (gy - pl.position.y) * Math.min(1, dt * 14);
    this.animatePerson(this.player, dt, moving);
    this.updateItems(dt);
    this.updateFairy(dt, moving);
    this.updateEnemies(dt);

    // Maya faces player when close + waves
    const mp = this.maya.root.position; const dM = Math.hypot(pl.position.x - mp.x, pl.position.z - mp.z);
    const wantM = dM < 9 ? Math.atan2(pl.position.x - mp.x, pl.position.z - mp.z) : .5;
    let dm = wantM - this.maya.root.rotation.y; dm = Math.atan2(Math.sin(dm), Math.cos(dm)); this.maya.root.rotation.y += dm * Math.min(1, dt * 4);
    this.animatePerson(this.maya, dt, false);
    const arm = this.maya.parts.ArmR; if (arm) { const w = (Math.sin(this.t * .7) > .6) ? Math.sin(this.t * 10) * .35 : 0; arm.rotation.z += ((w ? -2.4 : 0) - arm.rotation.z) * Math.min(1, dt * 6); arm.rotation.y = w; }

    // NPCs
    this.npcs.forEach(n => {
      const [a, b] = n.path; const len = Math.hypot(b[0] - a[0], b[1] - a[1]);
      const near = Math.hypot(pl.position.x - n.root.position.x, pl.position.z - n.root.position.z) < 2.6 || n.talking ||
        (this.enemies || []).some(en => !en.dead && en.g.visible && Math.hypot(en.g.position.x - n.root.position.x, en.g.position.z - n.root.position.z) < en.radius + .9);
      if (!near) n.t += dt * 1.6 / len * n.dir;
      if (n.t > 1) { n.t = 1; n.dir = -1; } if (n.t < 0) { n.t = 0; n.dir = 1; }
      n.root.position.x = a[0] + (b[0] - a[0]) * n.t; n.root.position.z = a[1] + (b[1] - a[1]) * n.t;
      n.root.rotation.y = Math.atan2((b[0] - a[0]) * n.dir, (b[1] - a[1]) * n.dir);
      this.animatePerson(n, dt, !near);
    });
    // Cars stop for the player
    this.cars.forEach(c => {
      const u = c.userData; const lane = c.position.z;
      const ahead = (pl.position.x - c.position.x) * u.dir;
      const blocked = (Math.abs(pl.position.z - lane) < 2.4 && ahead > 0 && ahead < 7.5) ||
        (this.enemies || []).some(en => { if (en.dead || !en.g.visible) return false; const a = (en.g.position.x - c.position.x) * u.dir; return Math.abs(en.g.position.z - lane) < 1.2 + en.radius && a > 0 && a < 6.5; });
      const others = this.cars.some(o => o !== c && o.userData.row === u.row && o.userData.dir === u.dir && ((o.position.x - c.position.x) * u.dir) > 0 && ((o.position.x - c.position.x) * u.dir) < 6.5);
      const want = blocked || others ? 0 : u.v; u.cur += (want - u.cur) * Math.min(1, dt * (want ? 1.2 : 5));
      c.position.x += u.cur * u.dir * dt; if (c.position.x > 55) c.position.x = -55; if (c.position.x < -55) c.position.x = 55;
      c.traverse(o => { if (o.isMesh && o.geometry.parameters === undefined) { } });
    });
    this.clouds.forEach(c => { c.position.x += c.userData.v * dt; if (c.position.x > 170) c.position.x = -170; });
    this.waters.forEach(w => { w.offset.x += dt * .05; w.offset.y += dt * .03; });

    // markers
    Object.values(this.markers).forEach(m => {
      const s = 1 + Math.sin(this.t * 3) * .08; m.ring.scale.set(s, s, s);
      if (m.arrow.visible) { m.arrow.position.y = 5 + Math.sin(this.t * 3) * .5; m.arrow.rotation.y += dt * 2; }
      m.badge.position.y += Math.sin(this.t * 2) * .004;
    });

    // proximity
    let near = null; const P = pl.position;
    const cand = (o, x, z, r) => { const dist = Math.hypot(P.x - x, P.z - z); if (dist < r && (!near || dist < near.dist)) near = Object.assign(o, { dist }); };
    CHAPTERS.forEach((ch, i) => { const d = this.doorPos(ch.id); cand({ type: 'door', i, key: 'door' + i, cmd: 'enter' }, d.x, d.z, 3); });
    cand({ type: 'maya', key: 'maya', cmd: 'talk' }, mp.x, mp.z, 3.2);
    this.npcs.forEach((n, i) => cand({ type: 'npc', i, key: 'npc' + i, cmd: 'talk' }, n.root.position.x, n.root.position.z, 2.3));
    (this.gems || []).forEach(g => { if (!g.got) cand({ type: 'gem', g, key: 'gem' + g.id, cmd: 'pickup' }, g.x, g.z, 1.9); });
    if (this.ball && this.ball.state === 'ground') cand({ type: 'ball', key: 'ball', cmd: 'pickup' }, this.ball.m.position.x, this.ball.m.position.z, 1.9);
    if (this.hoop) cand({ type: 'hoop', key: 'hoop' + (this.ball && this.ball.state === 'held'), cmd: this.ball && this.ball.state === 'held' ? 'throw' : null }, this.hoop.x, this.hoop.z + 2.5, 4.2);
    (this.benches || []).forEach((b, i) => cand({ type: 'bench', b, key: 'bench' + i, cmd: this.sitting === b ? null : 'use' }, b.x, b.z, 2.3));
    cand({ type: 'fountain', key: 'fountain', cmd: 'use' }, 0, 14, 4.8);
    if (this.lostPhone && this.lostPhone.visible) cand({ type: 'phone', key: 'phone', cmd: 'pickup' }, this.lostPhone.position.x, this.lostPhone.position.z, 1.9);
    if (this.mailboxPos) cand({ type: 'mailbox', key: 'mailbox' + S.carry, cmd: 'use' }, this.mailboxPos.x, this.mailboxPos.z, 2.4);
    if (this.mode !== 'play' || this.paused) near = null;
    const changed = (near && near.key) !== (this.near && this.near.key);
    this.near = near; if (changed) { this.nearT = performance.now(); if (this.onNear) this.onNear(near); }

    // guide arrow to next chapter
    const ni = nextIndex();
    if (ni >= 0 && this.mode === 'play') {
      const d = this.doorPos(CHAPTERS[ni].id); const dx = d.x - pl.position.x, dz = d.z - pl.position.z; const dist = Math.hypot(dx, dz);
      this.guide.visible = dist > 4; this.guide.position.set(pl.position.x, pl.position.y + .08, pl.position.z);
      this.guide.rotation.y = Math.atan2(dx, dz) + Math.PI; this.guide.children[0].position.z = -1.9 - Math.sin(this.t * 4) * .2;
      this.guide.children[0].rotation.z = 0;
    } else this.guide.visible = false;

    // camera
    const cam = this.camera;
    if (this.cutCam) {
      cam.position.lerp(this.cutCam.pos, 1 - Math.exp(-dt * 3)); this.camTarget.lerp(this.cutCam.look, 1 - Math.exp(-dt * 5)); cam.lookAt(this.camTarget);
    } else if (this.mode === 'title') {
      const a = this.t * .06; cam.position.set(Math.sin(a) * 82, 46, -50 + Math.cos(a) * 82); cam.lookAt(0, 0, -52);
    } else {
      const narrow = innerWidth / innerHeight < 1; const k = narrow ? 1.35 : 1;
      const want = new THREE.Vector3(pl.position.x, pl.position.y + 8.2 * k, pl.position.z + 11.5 * k);
      cam.position.lerp(want, 1 - Math.exp(-dt * 4));
      this.camTarget.lerp(new THREE.Vector3(pl.position.x, pl.position.y + 1.2, pl.position.z - 2.5), 1 - Math.exp(-dt * 6));
      cam.lookAt(this.camTarget);
    }
    // shadows follow the player
    const f = this.mode === 'title' ? new THREE.Vector3(0, 0, -52) : pl.position;
    this.sun.position.set(f.x + 30, 55, f.z + 25); this.sun.target.position.set(f.x, 0, f.z - 6);
    const sc = this.sun.shadow.camera, span = this.mode === 'title' ? 90 : 40;
    if (sc.right !== span) { sc.left = -span; sc.right = span; sc.top = span; sc.bottom = -span; sc.updateProjectionMatrix(); }
  },

  snapCamera() {
    const pl = this.player.root.position; const k = innerWidth / innerHeight < 1 ? 1.35 : 1;
    this.camera.position.set(pl.x, pl.y + 8.2 * k, pl.z + 11.5 * k); this.camTarget.set(pl.x, pl.y + 1.2, pl.z - 2.5); this.camera.lookAt(this.camTarget);
  },
  teleportTo(i) {
    const d = this.doorPos(CHAPTERS[i].id); this.player.root.position.set(d.x, d.y, d.z + 2.2); this.player.root.rotation.y = Math.PI; this.snapCamera();
  },

  loop() {
    this.last = performance.now();
    const frame = (now) => {
      requestAnimationFrame(frame);
      const dt = Math.min(.1, (now - this.last) / 1000); this.last = now;
      if ($('#lesson').classList.contains('on')) return; // hidden behind the lesson: skip work
      this.update(dt);
      this.renderer.render(this.scene, this.camera);
    };
    requestAnimationFrame(frame);
  }
};
