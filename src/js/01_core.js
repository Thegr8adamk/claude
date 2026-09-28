'use strict';
/* =====================================================================
   COMPUTER QUEST: TECH TOWN — core (state, voice, sound, UI helpers, lesson engine)
   ===================================================================== */
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const pick = a => a[Math.floor(Math.random() * a.length)];
const K = (k) => `<span class="key">${k}</span>`;

/* ---------------- SAVE STATE ---------------- */
const SAVE_KEY = 'computerQuest.techTown.v1';
const DEFAULT_SETTINGS = { textSize: 'normal', voice: true, voiceRate: 'normal', sfx: true, graphics: 'smooth' };
function freshState(settings) {
  return { name: '', shirt: '#2d7ff9', hair: '#3b2416', skin: '#e8b48a', xp: 0, done: [], stepXp: {}, tutorial: false,
    settings: Object.assign({}, DEFAULT_SETTINGS, settings || {}), pos: null };
}
function loadState() {
  try {
    const raw = localStorage.getItem(SAVE_KEY);
    if (raw) { const o = JSON.parse(raw); const st = Object.assign(freshState(), o); st.settings = Object.assign({}, DEFAULT_SETTINGS, o.settings || {}); return st; }
  } catch (e) { /* storage unavailable */ }
  return null;
}
function save() { try { localStorage.setItem(SAVE_KEY, JSON.stringify(S)); } catch (e) { /* ignore */ } }
let S = loadState() || freshState();
const fmt = t => String(t).replace(/\{name\}/g, esc(S.name || 'friend'));

/* ---------------- CHAPTERS ---------------- */
const CHAPTERS = [
  { id: 'home', n: 1, name: 'Home Sweet Home', skill: 'Turning a computer on and off', icon: '🏠', badge: 'Power Starter', badgeIcon: '⚡' },
  { id: 'mouse', n: 2, name: 'Community Center', skill: 'Using the mouse and touchpad', icon: '🖱️', badge: 'Mouse Master', badgeIcon: '🖱️' },
  { id: 'keys', n: 3, name: 'Typing School', skill: 'Using the keyboard', icon: '⌨️', badge: 'Key Keeper', badgeIcon: '⌨️' },
  { id: 'office', n: 4, name: 'The Office', skill: 'Windows, apps and files', icon: '🗂️', badge: 'Window Wrangler', badgeIcon: '🪟' },
  { id: 'web', n: 5, name: 'Public Library', skill: 'Browsing the internet safely', icon: '🌐', badge: 'Web Explorer', badgeIcon: '🧭' },
  { id: 'mail', n: 6, name: 'Post Office', skill: 'Sending and receiving email', icon: '✉️', badge: 'Mail Master', badgeIcon: '📬' },
  { id: 'android', n: 7, name: 'Green Phone Shop', skill: 'Using an Android phone', icon: '🤖', badge: 'Android Ace', badgeIcon: '🤖' },
  { id: 'iphone', n: 8, name: 'Silver Phone Shop', skill: 'Using an iPhone', icon: '📱', badge: 'iPhone Ace', badgeIcon: '📱' },
  { id: 'security', n: 9, name: 'Cyber Safety HQ', skill: 'Staying safe from scams, spam and malware', icon: '🛡️', badge: 'Cyber Guardian', badgeIcon: '🛡️' },
  { id: 'final', n: 10, name: 'City Hall', skill: 'Final review and certificate', icon: '🏛️', badge: 'Tech Hero', badgeIcon: '🏆' },
];
const isDone = i => S.done.includes(CHAPTERS[i].id);
const isUnlocked = i => i === 0 || isDone(i - 1);
const nextIndex = () => { for (let i = 0; i < CHAPTERS.length; i++) if (!isDone(i)) return i; return -1; };

/* ---------------- LEVELS / XP ---------------- */
const LEVELS = [[0, 'Newcomer'], [100, 'Curious Clicker'], [250, 'Button Presser'], [450, 'Keyboard Rookie'], [700, 'Window Wrangler'],
  [950, 'Web Walker'], [1250, 'Mail Carrier'], [1550, 'Phone Friend'], [1850, 'Cyber Guardian'], [2150, 'Tech Explorer'], [2500, 'Tech Hero']];
function levelOf(xp) { let i = 0; LEVELS.forEach((l, k) => { if (xp >= l[0]) i = k; }); return i; }
function addXP(n) {
  const before = levelOf(S.xp);
  S.xp += n; toast('+' + n + ' XP', 'xp');
  const after = levelOf(S.xp);
  if (after > before) { setTimeout(() => { toast('⭐ Level up! Level ' + (after + 1) + ': ' + LEVELS[after][1], 'lvl'); Sfx.play('level'); }, 500); }
  updateHUD(); save();
}

/* ---------------- VOICE (read aloud) ---------------- */
const Voice = {
  voice: null,
  init() {
    if (!('speechSynthesis' in window)) return;
    const choose = () => {
      const vs = speechSynthesis.getVoices(); const en = vs.filter(v => /^en/i.test(v.lang));
      this.voice = en.find(v => /Samantha|Google US English|Aria|Jenny|Natural|Zira/i.test(v.name)) || en.find(v => /en[-_]US/i.test(v.lang)) || en[0] || null;
    };
    choose(); speechSynthesis.onvoiceschanged = choose;
  },
  clean(html) {
    const d = document.createElement('div'); d.innerHTML = html;
    return d.textContent.replace(/[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}\u{FE0F}\u{2190}-\u{21FF}\u{2300}-\u{23FF}\u{25A0}-\u{25FF}\u{2B00}-\u{2BFF}]/gu, ' ')
      .replace(/\s+/g, ' ').trim();
  },
  speak(text) {
    if (!S.settings.voice || !('speechSynthesis' in window)) return;
    const t = this.clean(fmt(text)); if (!t) return;
    try {
      speechSynthesis.cancel();
      const u = new SpeechSynthesisUtterance(t);
      if (this.voice) u.voice = this.voice;
      u.rate = { slow: 0.78, normal: 0.95, fast: 1.15 }[S.settings.voiceRate] || 0.95; u.pitch = 1.05;
      speechSynthesis.speak(u);
    } catch (e) { /* ignore */ }
  },
  stop() { try { if ('speechSynthesis' in window) speechSynthesis.cancel(); } catch (e) { } }
};

/* ---------------- SOUND EFFECTS ---------------- */
const Sfx = {
  ctx: null,
  play(kind) {
    if (!S.settings.sfx) return;
    try {
      this.ctx = this.ctx || new (window.AudioContext || window.webkitAudioContext)();
      const c = this.ctx, now = c.currentTime;
      const seq = { good: [[660, 0], [880, .09]], bad: [[260, 0], [200, .12]], click: [[520, 0]], level: [[523, 0], [659, .1], [784, .2], [1047, .3]],
        done: [[523, 0], [659, .12], [784, .24], [1047, .36], [1319, .5]], door: [[392, 0], [523, .1]] }[kind] || [[440, 0]];
      seq.forEach(([f, t]) => {
        const o = c.createOscillator(), g = c.createGain();
        o.type = kind === 'bad' ? 'triangle' : 'sine'; o.frequency.value = f;
        g.gain.setValueAtTime(0.0001, now + t); g.gain.exponentialRampToValueAtTime(kind === 'bad' ? .08 : .16, now + t + .02);
        g.gain.exponentialRampToValueAtTime(0.0001, now + t + .28);
        o.connect(g); g.connect(c.destination); o.start(now + t); o.stop(now + t + .3);
      });
    } catch (e) { /* ignore */ }
  }
};

/* ---------------- TOASTS / CONFETTI ---------------- */
function toast(msg, cls = '') {
  const t = document.createElement('div'); t.className = 'toast ' + cls; t.textContent = msg; $('#toasts').appendChild(t);
  setTimeout(() => { t.style.transition = 'opacity .4s'; t.style.opacity = 0; }, 2000); setTimeout(() => t.remove(), 2500);
}
function confetti(n = 50, x = innerWidth / 2, y = innerHeight / 3) {
  const cols = ['#f5b700', '#2d7ff9', '#23a55a', '#e5484d', '#7b2ff7', '#ff8a00'];
  for (let i = 0; i < n; i++) {
    const c = document.createElement('div'); c.className = 'confetti'; c.style.background = cols[i % cols.length];
    c.style.left = x + 'px'; c.style.top = y + 'px'; document.body.appendChild(c);
    const a = Math.random() * Math.PI * 2, sp = 120 + Math.random() * 260, dx = Math.cos(a) * sp, dy = Math.sin(a) * sp - 180;
    const an = c.animate([{ transform: 'translate(0,0) rotate(0)', opacity: 1 }, { transform: `translate(${dx}px,${dy + 420}px) rotate(${Math.random() * 720}deg)`, opacity: 0 }],
      { duration: 1300 + Math.random() * 700, easing: 'cubic-bezier(.2,.6,.4,1)' });
    an.onfinish = () => c.remove();
  }
}

/* ---------------- PORTRAITS ---------------- */
const MAYA_SVG = `<svg viewBox="0 0 100 100"><circle cx="50" cy="50" r="50" fill="#bfe3ff"/>
<path d="M16 100 Q18 70 50 67 Q82 70 84 100Z" fill="#7b4fd6"/><path d="M41 66 L50 80 L59 66Z" fill="#fff"/>
<rect x="44" y="55" width="12" height="13" fill="#8d5a3b"/><ellipse cx="50" cy="42" rx="20" ry="22" fill="#a86b45"/>
<path d="M28 46 Q22 14 50 15 Q78 14 72 46 Q70 28 50 26 Q31 27 28 46Z" fill="#2b1a12"/><circle cx="50" cy="12" r="9" fill="#2b1a12"/>
<circle cx="42" cy="44" r="6.5" fill="#fff" fill-opacity=".35" stroke="#1f2d3d" stroke-width="2.2"/><circle cx="58" cy="44" r="6.5" fill="#fff" fill-opacity=".35" stroke="#1f2d3d" stroke-width="2.2"/>
<path d="M48.5 44h3" stroke="#1f2d3d" stroke-width="2"/><circle cx="42" cy="44.5" r="2.3" fill="#1f2d3d"/><circle cx="58" cy="44.5" r="2.3" fill="#1f2d3d"/>
<path d="M42 55 Q50 61 58 55" stroke="#5b2418" stroke-width="2.6" fill="none" stroke-linecap="round"/>
<circle cx="35" cy="51" r="3.2" fill="#e98b7a" opacity=".55"/><circle cx="65" cy="51" r="3.2" fill="#e98b7a" opacity=".55"/></svg>`;
function avatarSVG(shirt, hair, skin, style = 'short') {
  const long = style === 'long';
  return `<svg viewBox="0 0 120 260" style="height:94%"><ellipse cx="60" cy="254" rx="30" ry="5" fill="#000" opacity=".15"/>
  ${long ? `<path d="M40 40 Q36 80 42 98 L78 98 Q84 80 80 40Z" fill="${hair}"/>` : ''}
  <path d="M47 150 L45 240 L57 240 L60 160 L63 240 L75 240 L73 150Z" fill="#34405a"/>
  <rect x="41" y="238" width="18" height="9" rx="4" fill="#262626"/><rect x="61" y="238" width="18" height="9" rx="4" fill="#262626"/>
  <path d="M42 80 Q60 74 78 80 L81 100 L77 150 L43 150 L39 100Z" fill="${shirt}"/>
  <rect x="44" y="143" width="32" height="6" fill="#2a1d14"/>
  <path d="M40 82 Q33 86 32 100 L30 148 L37 148 L41 104Z" fill="${shirt}"/><path d="M80 82 Q87 86 88 100 L90 148 L83 148 L79 104Z" fill="${shirt}"/>
  <ellipse cx="33.5" cy="153" rx="4" ry="7" fill="${skin}"/><ellipse cx="86.5" cy="153" rx="4" ry="7" fill="${skin}"/>
  <rect x="55" y="66" width="10" height="14" fill="${skin}"/>
  <path d="M45 44 Q45 26 60 25 Q75 26 75 44 Q75 60 67 68 Q60 73 53 68 Q45 60 45 44Z" fill="${skin}"/>
  <ellipse cx="44.5" cy="47" rx="2.5" ry="5" fill="${skin}"/><ellipse cx="75.5" cy="47" rx="2.5" ry="5" fill="${skin}"/>
  <path d="M44 42 Q42 22 60 21 Q78 22 76 42 Q74 30 60 31 Q47 31 44 42Z" fill="${hair}"/>
  <ellipse cx="54" cy="46" rx="3" ry="2" fill="#fff"/><ellipse cx="66" cy="46" rx="3" ry="2" fill="#fff"/>
  <circle cx="54" cy="46" r="1.4" fill="#4a2e1a"/><circle cx="66" cy="46" r="1.4" fill="#4a2e1a"/>
  <path d="M50.5 42 L57 41.5 M63 41.5 L69.5 42" stroke="${hair}" stroke-width="1.6" stroke-linecap="round"/>
  <path d="M60 47 L58.5 54 L61 54.5" stroke="#00000033" stroke-width="1.2" fill="none"/>
  <path d="M56 59 Q60 61.5 64 59" stroke="#a0564c" stroke-width="2" fill="none" stroke-linecap="round"/></svg>`;
}
/* ---------------- HUD ---------------- */
function updateHUD() {
  const lv = levelOf(S.xp), cur = LEVELS[lv][0], nxt = LEVELS[lv + 1] ? LEVELS[lv + 1][0] : cur;
  $('#pcName').textContent = S.name || 'Player';
  $('#pcLevel').textContent = `Level ${lv + 1} · ${LEVELS[lv][1]}`;
  $('#xpfill').style.width = (nxt > cur ? Math.min(100, (S.xp - cur) / (nxt - cur) * 100) : 100) + '%';
  $('#pcXp').textContent = (nxt > cur ? `${S.xp} XP · ${nxt - S.xp} to next level` : `${S.xp} XP · Max level!`) + ` · 💎 ${(S.gems || []).length}/12`;
  const ni = nextIndex();
  $('#objective').innerHTML = ni < 0 ? '🏆 You finished every chapter! Visit any building to practice.' :
    `Next: <b>${CHAPTERS[ni].icon} ${CHAPTERS[ni].name}</b> · follow the golden light`;
  $('#hbVoice').classList.toggle('off', !S.settings.voice);
  $('#hbVoice').innerHTML = (S.settings.voice ? '🔊' : '🔇') + ' <span class="hl">Voice ' + (S.settings.voice ? 'On' : 'Off') + '</span>';
  $('#lVoice').textContent = S.settings.voice ? '🔊 Voice On' : '🔇 Voice Off';
}
function applySettings() {
  const base = innerWidth < 820 ? 17 : 20;
  const f = { normal: 1, large: 1.2, xl: 1.4 }[S.settings.textSize] || 1;
  document.documentElement.style.setProperty('--fs', Math.round(base * f) + 'px');
  document.body.classList.toggle('retro', S.settings.graphics === 'retro');
  if (typeof World !== 'undefined' && World.ready) World.setRetro(S.settings.graphics === 'retro');
  updateHUD();
}
function toggleVoice() {
  S.settings.voice = !S.settings.voice; save(); updateHUD();
  if (!S.settings.voice) Voice.stop(); else Voice.speak('Voice is on. I will read the instructions out loud.');
}

/* ---------------- DIALOG (in town) ---------------- */
const Dialog = {
  open: false,
  show(text, btns = [{ label: 'OK ▶' }], extra = '', who = null) {
    $('#dlgPortrait').innerHTML = who ? who.portrait : MAYA_SVG; $('#dlgName').textContent = who ? who.name : 'Maya · Tech Guide';
    $('#dlgText').innerHTML = fmt(text); typewrite($('#dlgText')); $('#dlgExtra').innerHTML = extra;
    const b = $('#dlgBtns'); b.innerHTML = '';
    btns.forEach(o => {
      const x = document.createElement('button'); x.className = 'btn ' + (o.cls || 'green'); x.innerHTML = o.label;
      x.onclick = () => { Sfx.play('click'); if (!o.keep) Dialog.hide(); o.fn && o.fn(); };
      b.appendChild(x);
    });
    $('#dialog').classList.add('on'); this.open = true;
    Voice.speak(text);
  },
  hide() { $('#dialog').classList.remove('on'); this.open = false; }
};

/* ---------------- MODAL ---------------- */
const Modal = {
  open: false,
  show(html, { closable = true } = {}) {
    $('#mbox').innerHTML = (closable ? '<button class="btn ghost mclose" id="mClose">✕ Close</button>' : '') + html;
    $('#modal').classList.add('on'); this.open = true;
    if (closable) $('#mClose').onclick = () => Modal.hide();
  },
  hide() { $('#modal').classList.remove('on'); this.open = false; if (this.onHide) { const f = this.onHide; this.onHide = null; f(); } },
  confirm(text, yesLabel, onYes) {
    this.show(`<h2>${text}</h2><div style="display:flex;gap:12px;flex-wrap:wrap;margin-top:1em">
      <button class="btn big red" id="cYes">${yesLabel}</button><button class="btn big ghost" id="cNo">No, stay here</button></div>`, { closable: false });
    $('#cYes').onclick = () => { this.hide(); onYes(); }; $('#cNo').onclick = () => this.hide();
    Voice.speak(text);
  }
};

/* =====================================================================
   LESSON ENGINE
   Each step: { say, hint?, next?: true|'label', build(stage, api, ctx) }
   ===================================================================== */
const PRAISE = ['Great job!', 'Well done!', 'Perfect!', 'You got it!', 'Excellent!', 'Nice work!', 'Fantastic!', 'That\'s right!'];
const LESSONS = {};
function showHint(msg, good) { const h = $('#gHint'); h.innerHTML = fmt(msg); h.className = 'on' + (good ? ' good' : ''); }
function hideHint() { const h = $('#gHint'); h.className = ''; h.innerHTML = ''; }

const L = {
  ch: null, def: null, ci: 0, i: 0, sid: 0, cur: null, busy: false, handled: false,
  targets: [], hintEls: [], cleanups: [], wrongs: 0, handEl: null, handRaf: 0,

  init() {
    const st = $('#stage');
    st.addEventListener('click', () => { this.handled = false; }, true);
    st.addEventListener('click', e => {
      if (this.handled || !this.cur || this.busy || !this.targets.length) return;
      const t = e.target;
      if (!t.isConnected || t.closest('.neutral,input,textarea,.vkb,.pkb')) return;
      if (this.targets.some(el => el.contains(t))) return;
      this.wrong(null, this.cur.step);
    });
    $('#gRepeat').onclick = () => { Voice.speak($('#gText').innerHTML + ' ' + ($('#gHint').classList.contains('on') ? $('#gHint').innerHTML : '')); };
    $('#gShow').onclick = () => { if (!this.cur) return; this.glow(); const h = this.cur.step.hint || 'Look for the glowing spot with the pointing hand.'; showHint('💡 ' + h, false); Voice.speak(h); };
    $('#lLeave').onclick = () => Modal.confirm('Leave this lesson and go back to town?', 'Yes, back to town', () => this.close(false));
    $('#lVoice').onclick = toggleVoice;
    addEventListener('resize', () => this.placeHand());
  },

  start(ci) {
    this.ci = ci; this.ch = CHAPTERS[ci]; this.def = LESSONS[this.ch.id];
    Dialog.hide(); if (typeof World !== 'undefined' && World.ready) World.pause(true);
    $('#hud').classList.remove('on');
    $('#lesson').classList.add('on'); $('#gPortrait').innerHTML = MAYA_SVG;
    $('#lTitle').textContent = `Chapter ${this.ch.n}: ${this.ch.name}`;
    const st = $('#stage'); st.innerHTML = ''; st.scrollTop = 0; this.ctx = {};
    updateHUD();
    if (this.def.setup) this.def.setup(st, this.ctx);
    this.go(0);
  },

  teardown() {
    this.cleanups.forEach(f => { try { f(); } catch (e) { } });
    this.cleanups = []; this.unglow();
  },

  go(i) {
    this.teardown();
    this.i = i; const step = this.def.steps[i]; const sid = ++this.sid; const n = this.def.steps.length;
    $('#lProg').style.width = (i / n * 100) + '%'; $('#lStep').textContent = `Step ${i + 1} of ${n}`;
    this.wrongs = 0; this.targets = []; this.hintEls = []; this.busy = false;
    hideHint(); $('#gNext').classList.add('hidden');
    const api = this.makeApi(sid, step); this.cur = { step, api };
    const text = typeof step.say === 'function' ? step.say(this.ctx) : step.say;
    $('#gText').innerHTML = fmt(text);
    try { step.build && step.build($('#stage'), api, this.ctx); } catch (e) { console.error('Step build error', this.ch.id, i, e); }
    if (step.next) api.next(step.next === true ? 'Next ▶' : step.next);
    Voice.speak(text);
  },

  makeApi(sid, step) {
    const live = () => sid === this.sid && !this.busy;
    const api = {
      sid, live,
      done: (msg) => { if (!live()) return; this.handled = true; this.success(msg); },
      wrong: (msg) => { if (!live()) return; this.handled = true; this.wrong(msg, step); },
      soft: (msg, speak = true) => { if (!live()) return; this.handled = true; showHint(msg, false); if (speak) Voice.speak(msg); },
      good: (msg) => { if (!live()) return; this.handled = true; showHint('✅ ' + msg, true); Sfx.play('good'); Voice.speak(msg); },
      say: (txt) => { if (!live()) return; this.handled = true; $('#gText').innerHTML = fmt(txt); hideHint(); this.wrongs = 0; this.unglow(); Voice.speak(txt); },
      target: (...els) => { els.flat().forEach(e => e && this.targets.push(e)); return els[0]; },
      setTargets: (...els) => { this.handled = true; this.unglow(); this.wrongs = 0; this.targets = els.flat().filter(Boolean); },
      hintOnly: (...els) => { this.hintEls = els.flat().filter(Boolean); },
      listen: (el, type, fn, opt) => { el.addEventListener(type, fn, opt); this.cleanups.push(() => el.removeEventListener(type, fn, opt)); },
      onKey: (fn) => { const h = e => { if (live()) fn(e); }; document.addEventListener('keydown', h); this.cleanups.push(() => document.removeEventListener('keydown', h)); },
      after: (ms, fn) => { const t = setTimeout(() => { if (sid === this.sid) fn(); }, ms); this.cleanups.push(() => clearTimeout(t)); },
      cleanup: (fn) => this.cleanups.push(fn),
      next: (label = 'Next ▶') => {
        const b = $('#gNext'); b.textContent = label; b.classList.remove('hidden');
        b.onclick = () => api.done('quiet'); setTimeout(() => { if (live()) b.focus({ preventScroll: true }); }, 60);
      },
    };
    return api;
  },

  success(msg) {
    this.busy = true; this.unglow(); $('#gNext').classList.add('hidden');
    const quiet = msg === 'quiet';
    if (!quiet) { Sfx.play('good'); const p = msg || pick(PRAISE); showHint('✅ ' + p, true); Voice.speak(p); }
    const key = this.ch.id + ':' + this.i;
    if (!S.stepXp[key]) { S.stepXp[key] = 1; addXP(10); }
    const delay = window.CQ_FAST ? 60 : quiet ? 150 : (msg && msg.length > 40 ? 2600 : 1400);
    const sid = this.sid;
    setTimeout(() => { if (sid !== this.sid) return; if (this.i + 1 < this.def.steps.length) this.go(this.i + 1); else this.complete(); }, delay);
  },

  wrong(msg, step) {
    this.wrongs++; Sfx.play('bad');
    let m = msg || step.hint || 'Not quite. Try again! You can press 💡 Show me for help.';
    if (this.wrongs >= 3) { this.glow(); m += ' Look for the glowing spot and the pointing hand 👆.'; }
    showHint(m, false); Voice.speak(m);
  },

  glow() {
    this.unglow();
    const els = [...this.targets, ...this.hintEls].filter(e => e && e.isConnected);
    els.forEach(e => e.classList.add('glow'));
    if (els.length) {
      this.handEl = document.createElement('div'); this.handEl.className = 'hand'; this.handEl.textContent = '👆';
      this.handEl._t = els[0]; document.body.appendChild(this.handEl); this.placeHand();
      const loop = () => { this.placeHand(); this.handRaf = requestAnimationFrame(loop); }; this.handRaf = requestAnimationFrame(loop);
    }
  },
  placeHand() {
    const h = this.handEl; if (!h) return; const t = h._t;
    if (!t || !t.isConnected) { h.style.display = 'none'; return; }
    const r = t.getBoundingClientRect(); if (!r.width && !r.height) { h.style.display = 'none'; return; }
    h.style.display = ''; h.style.left = (r.left + r.width / 2) + 'px'; h.style.top = Math.min(innerHeight - 60, r.bottom + 4) + 'px';
  },
  unglow() {
    $$('.glow').forEach(e => e.classList.remove('glow'));
    if (this.handEl) { this.handEl.remove(); this.handEl = null; }
    cancelAnimationFrame(this.handRaf);
  },

  complete() {
    this.teardown(); this.cur = null; $('#lProg').style.width = '100%';
    const first = !S.done.includes(this.ch.id);
    if (first) { S.done.push(this.ch.id); addXP(100); }
    save(); Sfx.play('done'); confetti(90);
    const ch = this.ch, nxt = CHAPTERS[this.ci + 1], isFinal = ch.id === 'final';
    const say = isFinal ? `Congratulations, {name}! You finished Computer Quest. You are officially a Tech Hero!` :
      `Wonderful work, {name}! You finished ${ch.name} and earned the ${ch.badge} badge.` + (nxt ? ` Next stop: ${nxt.name}.` : '');
    $('#gText').innerHTML = fmt(say); hideHint(); Voice.speak(say);
    $('#stage').innerHTML = `<div class="icard" style="text-align:center;max-width:640px">
      <div style="font-size:4.5em;line-height:1.1">${ch.badgeIcon}</div><h3>Chapter complete!</h3>
      <p>You earned the <b>${ch.badge}</b> badge for <b>${ch.skill.toLowerCase()}</b>.${first ? '<br>⭐ +100 bonus XP' : ''}</p>
      <div style="display:flex;gap:12px;justify-content:center;flex-wrap:wrap">
        ${isFinal ? `<button class="btn big gold" id="cCert">🖨️ Print my certificate</button>` : ''}
        <button class="btn big green" id="cBack">Back to Town ▶</button>
        <button class="btn ghost" id="cAgain">↺ Practice this chapter again</button></div></div>`;
    $('#cBack').onclick = () => this.close(true);
    $('#cAgain').onclick = () => this.start(this.ci);
    if (isFinal) $('#cCert').onclick = () => Game.printCertificate();
    setTimeout(() => $('#cBack') && $('#cBack').focus(), 80);
  },

  close(fromComplete) {
    this.teardown(); this.sid++; this.cur = null; Voice.stop();
    $('#lesson').classList.remove('on'); $('#stage').innerHTML = '';
    $('#hud').classList.add('on');
    if (typeof World !== 'undefined' && World.ready) { World.pause(false); World.refreshMarkers(); }
    updateHUD();
    if (fromComplete) Game.afterChapter(this.ci);
  }
};

/* =====================================================================
   LESSON HELPERS
   ===================================================================== */
// Virtual keyboard ------------------------------------------------------
const KB_FULL = [
  [['`', '~'], ['1', '!'], ['2', '@'], ['3', '#'], ['4', '$'], ['5', '%'], ['6', '^'], ['7', '&'], ['8', '*'], ['9', '('], ['0', ')'], ['-', '_'], ['=', '+'], { k: 'Backspace', l: '⌫ Backspace', c: 'w2 z-back' }],
  [{ k: 'Tab', l: 'Tab', c: 'w15' }, 'q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p', ['[', '{'], [']', '}'], { k: '\\', l: '\\', c: 'w15' }],
  [{ k: 'CapsLock', l: 'Caps Lock', c: 'w2' }, 'a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l', [';', ':'], ["'", '"'], { k: 'Enter', l: 'Enter ⏎', c: 'w25 z-enter' }],
  [{ k: 'Shift', l: '⇧ Shift', c: 'w25 z-shift shk' }, 'z', 'x', 'c', 'v', 'b', 'n', 'm', [',', '<'], ['.', '>'], ['/', '?'], { k: 'ShiftR', l: '⇧ Shift', c: 'w25 z-shift shk' }],
  [{ k: 'Control', l: 'Ctrl', c: 'w15' }, { k: 'Alt', l: 'Alt', c: 'w15' }, { k: ' ', l: 'Space', c: 'w6 z-space' }, { k: 'ArrowLeft', l: '←' }, { k: 'ArrowUp', l: '↑' }, { k: 'ArrowDown', l: '↓' }, { k: 'ArrowRight', l: '→' }]
];
const KB_PHONE = [
  ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0'],
  ['q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p'],
  ['a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l'],
  [{ k: 'Shift', l: '⇧', c: 'wide shk' }, 'z', 'x', 'c', 'v', 'b', 'n', 'm', { k: 'Backspace', l: '⌫', c: 'wide' }],
  [{ k: '@', l: '@', c: 'wide' }, { k: ' ', l: 'space', c: 'space' }, { k: '.', l: '.', c: 'wide' }, { k: 'Enter', l: '⏎', c: 'wide' }]
];
const SHIFTMAP = {};
KB_FULL.forEach(r => r.forEach(k => { if (Array.isArray(k)) SHIFTMAP[k[1]] = k[0]; }));
function keysFor(ch) {
  if (ch === ' ') return [' '];
  if (/[a-z0-9]/.test(ch)) return [ch];
  if (/[A-Z]/.test(ch)) return ['Shift', ch.toLowerCase()];
  if (SHIFTMAP[ch]) return ['Shift', SHIFTMAP[ch]];
  return [ch];
}
function keyIdFromEvent(e) {
  if (e.key === 'Shift') return e.code === 'ShiftRight' ? 'ShiftR' : 'Shift';
  if (e.key.length === 1) { const k = e.key.toLowerCase(); return SHIFTMAP[e.key] || k; }
  return e.key;
}
function makeKeyboard(api, { phone = false, input = null, zones = false } = {}) {
  const el = document.createElement('div'); el.className = phone ? 'pkb' : 'vkb';
  const rows = phone ? KB_PHONE : KB_FULL; const map = {};
  rows.forEach(r => {
    const row = document.createElement('div'); row.className = 'row';
    r.forEach(k => {
      let id, label, cls = '';
      if (typeof k === 'string') { id = k; label = /[a-z]/.test(k) ? k.toUpperCase() : k; cls = /[a-z]/.test(k) ? 'z-letter' : (/[0-9]/.test(k) ? 'z-num' : ''); }
      else if (Array.isArray(k)) { id = k[0]; label = `<small>${esc(k[1])}</small>${esc(k[0])}`; cls = /[0-9]/.test(k[0]) ? 'z-num' : ''; }
      else { id = k.k; label = k.l; cls = k.c || ''; }
      const b = document.createElement('button'); b.type = 'button'; b.innerHTML = label; b.className = (zones ? cls : cls.replace(/z-\w+/g, '')); b.dataset.k = id;
      b.tabIndex = -1; row.appendChild(b); (map[id] = map[id] || []).push(b);
    });
    el.appendChild(row);
  });
  const kb = {
    el, map, shift: false, input,
    highlight(ids) { $$('.hl', el).forEach(b => b.classList.remove('hl')); (ids || []).forEach(id => (map[id] || []).forEach(b => b.classList.add('hl'))); },
    flash(id) { (map[id] || []).forEach(b => { b.classList.add('press'); setTimeout(() => b.classList.remove('press'), 160); }); },
    highlightNext(target, value, cs, needEnter) {
      const tt = cs ? target : target.toLowerCase(), vv = cs ? value : value.toLowerCase();
      if (!tt.startsWith(vv)) return this.highlight(['Backspace']);
      if (value.length >= target.length) return this.highlight(needEnter ? ['Enter'] : []);
      const ch = target[value.length]; let ids = keysFor(cs ? ch : ch.toLowerCase());
      if (phone) ids = ids.map(x => x === 'Shift' ? 'Shift' : x);
      this.highlight(ids);
    }
  };
  // Clicking on-screen keys
  el.addEventListener('pointerdown', e => e.preventDefault());
  el.addEventListener('click', e => {
    const b = e.target.closest('button'); if (!b) return; const id = b.dataset.k; kb.flash(id);
    if (id === 'Shift' || id === 'ShiftR') { kb.shift = !kb.shift; el.classList.toggle('sticky-shift', kb.shift); document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', bubbles: true })); return; }
    let ch = null;
    if (id.length === 1) {
      ch = id;
      if (kb.shift) { const pair = Object.keys(SHIFTMAP).find(s => SHIFTMAP[s] === id); ch = /[a-z]/.test(id) ? id.toUpperCase() : (pair || id); kb.shift = false; el.classList.remove('sticky-shift'); }
    }
    const inp = kb.input;
    if (inp) {
      if (ch !== null) { inp.value += ch; inp.dispatchEvent(new Event('input', { bubbles: true })); }
      else if (id === 'Backspace') { inp.value = inp.value.slice(0, -1); inp.dispatchEvent(new Event('input', { bubbles: true })); }
      else if (id === 'Enter') inp.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
      else document.dispatchEvent(new KeyboardEvent('keydown', { key: id, bubbles: true }));
    } else {
      document.dispatchEvent(new KeyboardEvent('keydown', { key: ch !== null ? ch : id, bubbles: true }));
    }
  });
  // Physical key feedback
  if (api) api.listen(document, 'keydown', e => { if (e.isTrusted) kb.flash(keyIdFromEvent(e)); });
  return kb;
}

// Type-checking an input -----------------------------------------------
function typeCheck(api, input, target, o = {}) {
  const { cs = false, enter = false, display = null, kb = null, onOk = null, loose = false, mismatch = null, autoFocus = true } = o;
  const norm = s => { s = cs ? s : s.toLowerCase(); if (loose) s = s.replace(/[^a-z0-9@. ]/gi, '').replace(/\s+/g, ' ').trim(); return s; };
  const T = norm(target); let warned = false, enterHinted = false, finished = false;
  const matches = () => norm(input.value) === T || (!loose && !cs && input.value.trim().toLowerCase() === target.toLowerCase());
  const render = () => {
    const v = input.value;
    if (display) display.innerHTML = [...target].map((ch, i) => {
      const t = v[i]; let c = '';
      if (t === undefined) c = i === v.length ? 'cur' : '';
      else c = (cs ? t === ch : t.toLowerCase() === ch.toLowerCase()) ? 'ok' : 'bad';
      return `<span class="${c}">${ch === ' ' ? '&nbsp;' : esc(ch)}</span>`;
    }).join('');
    if (kb) kb.highlightNext(target, v, cs, enter);
  };
  const finish = () => { if (finished) return; finished = true; onOk ? onOk() : api.done(); };
  api.listen(input, 'input', () => {
    render(); if (finished) return;
    const v = input.value, vv = cs ? v : v.toLowerCase(), tt = cs ? target : target.toLowerCase();
    if (!loose && !tt.startsWith(vv) && !(vv.trim() === tt)) {
      if (!warned) { warned = true; api.soft(mismatch || 'Oops, one letter doesn\'t match. Press the ⌫ Backspace key to erase it, then try again.'); }
    } else if (warned) { warned = false; hideHint(); }
    if (matches()) { if (!enter) finish(); else if (!enterHinted) { enterHinted = true; api.soft('Perfect! Now press the Enter key ⏎.'); } }
  });
  api.listen(input, 'keydown', e => {
    if (e.key === 'Enter') {
      e.preventDefault();
      if (matches()) finish(); else if (enter) api.soft(`Not yet. Make the box say “${esc(target)}” first, then press Enter.`);
    }
  });
  render();
  if (autoFocus) {
    try { input.focus({ preventScroll: true }); } catch (e) { }
    setTimeout(() => { if (api.live() && document.activeElement !== input) input.focus({ preventScroll: true }); }, 120);
    // Typing while the box isn't selected still goes into the box
    api.onKey(e => { if (document.activeElement !== input && e.isTrusted && (e.key.length === 1 || e.key === 'Backspace') && !e.ctrlKey && !e.metaKey && !(document.activeElement && /INPUT|TEXTAREA/.test(document.activeElement.tagName))) input.focus({ preventScroll: true }); });
  }
  return { render };
}

// Double-click ----------------------------------------------------------
function dbl(api, el, onDouble, onSingle) {
  let last = 0, t = null;
  api.listen(el, 'click', () => {
    const now = Date.now(); el.classList.add('sel');
    if (now - last < 800) { clearTimeout(t); last = 0; onDouble(); }
    else { last = now; clearTimeout(t); t = setTimeout(() => { if (api.live() && onSingle) onSingle(); }, 850); }
  });
  api.cleanup(() => clearTimeout(t));
}
const DBL_HINT = 'That was one click. A double-click is two clicks, very quickly: click-click! Keep the mouse still while you click.';

// Drag and drop ---------------------------------------------------------
function dragger(api, el, zones, onDrop, onFail) {
  el.style.touchAction = 'none';
  api.listen(el, 'pointerdown', e => {
    if (e.button !== 0 && e.pointerType === 'mouse') return;
    e.preventDefault();
    const r = el.getBoundingClientRect(); const g = el.cloneNode(true);
    g.classList.remove('glow'); g.classList.add('ghost-drag'); g.style.position = 'fixed'; g.style.width = r.width + 'px'; g.style.height = r.height + 'px';
    g.style.left = e.clientX + 'px'; g.style.top = e.clientY + 'px'; g.style.right = 'auto'; g.style.bottom = 'auto'; g.style.margin = '0'; g.style.animation = 'none';
    document.body.appendChild(g); el.style.opacity = .3;
    const over = (x, y) => zones.find(z => { const b = z.getBoundingClientRect(); return x >= b.left && x <= b.right && y >= b.top && y <= b.bottom; });
    const mv = ev => { g.style.left = ev.clientX + 'px'; g.style.top = ev.clientY + 'px'; const o = over(ev.clientX, ev.clientY); zones.forEach(z => z.classList.toggle('hot', z === o)); };
    const up = ev => {
      removeEventListener('pointermove', mv); removeEventListener('pointerup', up); removeEventListener('pointercancel', up);
      g.remove(); el.style.opacity = ''; zones.forEach(z => z.classList.remove('hot'));
      if (!api.live()) return;
      const z = over(ev.clientX, ev.clientY);
      const moved = Math.hypot(ev.clientX - e.clientX, ev.clientY - e.clientY);
      if (z) onDrop(z, el); else if (onFail) onFail(moved);
    };
    addEventListener('pointermove', mv); addEventListener('pointerup', up); addEventListener('pointercancel', up);
  });
}

// Swipe on touch screens --------------------------------------------------
function swipe(api, el, dir, fn, { zone = null, onTap = null, tooShort = null } = {}) {
  let st = null;
  api.listen(el, 'pointerdown', e => {
    const r = el.getBoundingClientRect(); const rx = (e.clientX - r.left) / r.width, ry = (e.clientY - r.top) / r.height;
    if (zone && !zone(rx, ry)) { st = null; return; }
    st = { x: e.clientX, y: e.clientY };
  });
  api.listen(window, 'pointerup', e => {
    if (!st) return; const dx = e.clientX - st.x, dy = e.clientY - st.y; st = null;
    const d = Math.hypot(dx, dy), vert = Math.abs(dy) > Math.abs(dx);
    if (d > 35 && vert && ((dir === 'up' && dy < 0) || (dir === 'down' && dy > 0))) fn();
    else if (d < 10) { if (onTap) onTap(e); }
    else api.soft(tooShort || (dir === 'up' ? 'Almost! Hold down, slide UP toward the top of the screen, then let go.' : 'Almost! Hold down, slide DOWN toward the bottom, then let go.'));
  });
}

// Multiple choice ---------------------------------------------------------
function quiz(stage, api, { q, emoji = '', opts, correct, why = {} }) {
  stage.innerHTML = `<div class="stage-inner"><div class="qq">${emoji ? `<span class="qe">${emoji}</span>` : ''}${q}</div>
    <div class="quiz">${opts.map((o, i) => `<button class="qopt" data-i="${i}">${String.fromCharCode(65 + i)}. ${o}</button>`).join('')}</div></div>`;
  const btns = $$('.qopt', stage); api.target(btns[correct]);
  btns.forEach((b, i) => b.onclick = () => {
    if (i === correct) { b.classList.add('right'); api.done(why.right || null); }
    else { b.classList.add('nope'); api.wrong(why[i] || 'Not that one. Have another look and try a different answer!'); }
  });
}
function infoCard(stage, html) { stage.innerHTML = `<div class="stage-inner"><div class="icard neutral">${html}</div></div>`; }
