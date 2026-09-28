/* =====================================================================
   LESSONS 7–8: Android phone & iPhone
   ===================================================================== */
function makePhone(S, ctx, type) {
  const and = type === 'android';
  S.innerHTML = `<div class="stage-inner"><div class="phone-wrap"><div class="phone ${type}" id="phone">
    ${and ? `<button class="sbtn r" id="pVolUp" style="top:15%;height:8%" aria-label="Volume up"></button><button class="sbtn r" id="pVolDn" style="top:24%;height:8%" aria-label="Volume down"></button><button class="sbtn r" id="pPow" style="top:36%;height:9%" aria-label="Power button"></button>`
      : `<button class="sbtn l" id="pVolUp" style="top:21%;height:7%" aria-label="Volume up"></button><button class="sbtn l" id="pVolDn" style="top:30%;height:7%" aria-label="Volume down"></button><button class="sbtn r" id="pPow" style="top:24%;height:12%" aria-label="Side button"></button>`}
    <div class="pscreen" id="pscr"><div class="notch"></div><div class="pstatus" id="pstat"><span>${clockStr()}</span><span>📶 🔋</span></div><div class="pview" id="pview"></div>
    ${and ? '<div class="pnav" id="pnav"><button id="nBack" aria-label="Back">◀</button><button id="nHome" aria-label="Home">●</button><button id="nRecent" aria-label="Recent apps">■</button></div>' : '<div class="homebar" id="hbar"></div>'}</div></div></div></div>`;
  const P = ctx.P = { type, and, scr: 'off', stack: [], unlocked: false, flash: false, vol: 40 };
  const hook = (ev, d) => ctx.hook && ctx.hook(ev, d);
  P.hook = hook; P.view = $('#pview'); P.screen = $('#pscr');
  P.set = (html, cls = '') => { P.view.className = 'pview ' + cls; P.view.innerHTML = html; $('#pstat').style.visibility = P.scr === 'off' ? 'hidden' : 'visible'; const nav = $('#pnav'); if (nav) nav.style.visibility = P.scr === 'off' ? 'hidden' : 'visible'; const hb = $('#hbar'); if (hb) hb.style.visibility = P.scr === 'off' ? 'hidden' : 'visible'; };
  P.go = (name, arg) => { if (P.scr !== name && P.scr !== 'off' && P.scr !== 'lock' && P.scr !== 'pin') P.stack.push(P.scr); P.scr = name; P.screens[name](arg); hook('screen', name); };
  P.home = () => { if (!P.unlocked) return; P.stack = []; P.scr = 'home'; P.screens.home(); hook('home'); };
  P.back = () => { if (!P.unlocked) return; const prev = P.stack.pop(); if (!prev) { P.home(); hook('back'); return; } P.scr = prev; P.screens[prev](); hook('back'); };
  P.kbInto = (slot, input, api) => { const kb = makeKeyboard(api, { phone: true, input }); slot.appendChild(kb.el); return kb; };
  // side buttons
  $('#pPow').onclick = () => { const b = $('#pPow'); b.classList.add('pressed'); setTimeout(() => b.classList.remove('pressed'), 200); if (P.scr === 'off') { P.scr = 'lock'; P.screens.lock(); } else { P.scr = 'off'; P.unlocked = false; P.stack = []; P.screens.off(); } hook('power', P.scr); };
  const vol = d => { if (P.scr === 'off') return hook('voloff'); P.vol = Math.max(0, Math.min(100, P.vol + d)); let v = $('#volpop'); if (!v) { P.screen.insertAdjacentHTML('beforeend', '<div class="volpop" id="volpop"><i></i></div>'); v = $('#volpop'); } $('i', v).style.height = P.vol + '%'; clearTimeout(P.vt); P.vt = setTimeout(() => v.remove(), 2500); hook(d > 0 ? 'volup' : 'voldown'); };
  $('#pVolUp').onclick = () => vol(20); $('#pVolDn').onclick = () => vol(-20);
  if (and) { $('#nBack').onclick = P.back; $('#nHome').onclick = P.home; $('#nRecent').onclick = () => hook('recent'); }
  // swipe detection on the screen (taps still reach buttons as normal clicks)
  let st = null; const scr = P.screen;
  scr.addEventListener('pointerdown', e => { const r = scr.getBoundingClientRect(); st = { x: e.clientX, y: e.clientY, fy: (e.clientY - r.top) / r.height, fx: (e.clientX - r.left) / r.width }; });
  const up = e => {
    if (!st || !scr.isConnected) { st = null; return; } const dx = e.clientX - st.x, dy = e.clientY - st.y, d = Math.hypot(dx, dy); const s0 = st; st = null;
    if (d > 35 && Math.abs(dy) > Math.abs(dx)) { if (dy < 0 && $('#shade')) { $('#shade').remove(); hook('shadeClosed'); return; } hook('swipe', { dir: dy < 0 ? 'up' : 'down', fy: s0.fy, fx: s0.fx }); }
    else if (d >= 10) hook('smallswipe', { dy });
  };
  addEventListener('pointerup', up);
  const hb = $('#hbar'); if (hb) hb.onclick = () => hook('homebarTap');
  P.screens = type === 'android' ? androidScreens(P) : iphoneScreens(P);
  P.screens.off();
  return P;
}
const lockHTML = (bg) => `<div class="lockscr ${bg}" style="position:absolute;inset:0"><div class="lt">${new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }).replace(/\s?[AP]M/i, '')}</div><div class="ld">${new Date().toLocaleDateString([], { weekday: 'long', month: 'long', day: 'numeric' })}</div><div class="swipehint">⌃<br>Swipe up to unlock</div></div>`;
function pinScreen(P, bg, code, label) {
  P.set(`<div class="${bg}" style="position:absolute;inset:0"></div><div class="pinpad"><div style="font-weight:800">${label}</div><div class="pindots" id="dots">${'<i></i>'.repeat(code.length)}</div><div class="pinkeys">${[1, 2, 3, 4, 5, 6, 7, 8, 9, '', 0, '⌫'].map(k => k === '' ? '<span></span>' : `<button data-k="${k}">${k}</button>`).join('')}</div></div>`);
  let v = '';
  const upd = () => $$('#dots i').forEach((d, i) => d.classList.toggle('f', i < v.length));
  const press = k => {
    if (k === '⌫') v = v.slice(0, -1); else if (v.length < code.length) v += k; upd();
    if (v.length === code.length) { if (v === code) { P.unlocked = true; setTimeout(() => { P.scr = 'home'; P.screens.home(); P.hook('pinok'); }, 250); } else { const d = $('#dots'); d.classList.add('shake'); setTimeout(() => { v = ''; upd(); d.classList.remove('shake'); }, 450); P.hook('pinbad'); } }
  };
  $$('.pinkeys button').forEach(b => b.onclick = () => press(b.dataset.k));
  P.pinKey = e => { if (P.scr === 'pin' && /^[0-9]$/.test(e.key)) press(e.key); if (P.scr === 'pin' && e.key === 'Backspace') press('⌫'); };
}
function appGrid(apps, dock) {
  const a = x => `<div class="app" data-app="${x[0]}"><div class="ai" style="background:${x[2]}">${x[1]}</div><span class="nm">${x[3] || x[0]}</span></div>`;
  return `<div class="apps">${apps.map(a).join('')}</div><div class="dock">${dock.map(a).join('')}</div>`;
}
function wifiNetworks(P, on, connected) {
  if (!on) return '<div style="padding:14px;color:#6b7785">Turn on Wi-Fi to see available networks.</div>';
  return ['TownHome-WiFi', 'Library-Guest', 'Cafe-Free'].map(n => `<div class="prow" data-net="${n}"><div class="pav" style="background:#e8f0ff">📶</div><div class="pmain"><b>${n}</b><small>${connected === n ? '✅ Connected' : (n === 'Cafe-Free' ? 'Open' : '🔒 Secured')}</small></div></div>`).join('');
}
function chatScreen(P, who, msgs, back) {
  P.set(`<div class="papp"><div class="phead"><button id="chBack">${back}</button><span class="sp">${who}</span></div><div class="chat" id="chat">${msgs.map(m => `<div class="pb ${m[0]}">${esc(m[1])}</div>`).join('')}</div><div class="pcomposer"><input id="chIn" placeholder="Text message" autocomplete="off" inputmode="none"><button class="psend" id="chSend">${P.and ? '➤' : '⬆'}</button></div><div id="kbSlot"></div></div>`);
  $('#chBack').onclick = P.back;
  const send = () => { const inp = $('#chIn'); const t = inp.value.trim(); if (!t) return P.hook('sendEmpty'); $('#chat').insertAdjacentHTML('beforeend', `<div class="pb me">${esc(t)}</div>`); inp.value = ''; P.hook('send', t); };
  $('#chSend').onclick = send; $('#chIn').addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); send(); } });
  $('#chIn').addEventListener('focus', () => P.hook('chFocus'));
}
function storeInstall(P, btnSel) {
  const b = $(btnSel); b.onclick = () => {
    if (b.dataset.state === 'open') return P.hook('openApp');
    if (b.dataset.state) return; b.dataset.state = 'busy'; b.outerHTML = `<div class="progress" id="prog"><i></i></div>`;
    requestAnimationFrame(() => requestAnimationFrame(() => { const i = $('#prog i'); if (i) i.style.width = '100%'; }));
    setTimeout(() => { const pr = $('#prog'); if (pr) pr.outerHTML = `<button class="pbtn ${P.and ? 'green' : ''}" id="stOpen" data-state="open">Open</button>`; P.hook('installed'); const o = $('#stOpen'); if (o) o.onclick = () => P.hook('openApp'); }, 2200);
    P.hook('installing');
  };
}

/* ---------------- ANDROID ---------------- */
function androidScreens(P) {
  const BG = 'pbg-and';
  const apps = [['Messages', '💬', '#1a73e8'], ['Phone', '📞', '#34a853'], ['Camera', '📷', '#5f6368'], ['Chrome', '🌐', '#fbbc04'], ['Gmail', '✉️', '#ea4335'], ['Play Store', '▶️', '#ffffff'], ['Settings', '⚙️', '#9aa0a6'], ['Photos', '🖼️', '#f6aea9'], ['Clock', '⏰', '#1f2d3d'], ['Maps', '🗺️', '#a8dab5'], ['Calendar', '📅', '#4285f4'], ['YouTube', '▶', '#ff0000']];
  const sc = {
    off: () => P.set('<div style="position:absolute;inset:0;background:#000"></div>'),
    lock: () => { P.set(lockHTML(BG)); P.view.onclick = () => { if (P.scr === 'lock') P.hook('lockTap'); }; },
    pin: () => pinScreen(P, BG, '2580', 'Enter PIN'),
    home: () => {
      P.view.onclick = null;
      P.set(`<div class="${BG}" style="position:absolute;inset:0"></div><div style="position:absolute;top:40px;left:14px;right:14px;background:rgba(255,255,255,.9);border-radius:99px;padding:.5em 1em;color:#5f6368">🔍 Search</div>` + appGrid(apps.slice(0, 12), [['Phone', '📞', '#34a853'], ['Messages', '💬', '#1a73e8'], ['Chrome', '🌐', '#fbbc04'], ['Camera', '📷', '#5f6368']]));
      $('.apps', P.view).style.top = '96px';
      $$('.app', P.view).forEach(a => a.onclick = () => { const n = a.dataset.app; P.hook('app', n); if (sc[n]) P.go(n); else P.hook('appNA', n); });
    },
    Messages: () => {
      P.set(`<div class="papp"><div class="phead">Messages</div><div class="plist"><div class="prow"><div class="pav">🧑</div><div class="pmain"><b>Sam Lee</b><small>See you at the park!</small></div></div><div class="prow"><div class="pav">🏦</div><div class="pmain"><b>Pharmacy</b><small>Your prescription is ready.</small></div></div></div><button class="fab" id="fab">💬 Start chat</button></div>`);
      $('#fab').onclick = () => P.go('pickContact');
    },
    pickContact: () => {
      P.set(`<div class="papp"><div class="phead"><button id="pcB">←</button><span class="sp">New conversation</span></div><div style="padding:6px 14px;color:#6b7785">To: type a name or pick a contact</div><div class="plist">${[['Maya', '👩🏾'], ['Sam Lee', '🧑'], ['Grandpa Joe', '👴']].map(c => `<div class="prow" data-c="${c[0]}"><div class="pav">${c[1]}</div><div class="pmain"><b>${c[0]}</b><small>Mobile</small></div></div>`).join('')}</div></div>`);
      $('#pcB').onclick = P.back; $$('[data-c]', P.view).forEach(r => r.onclick = () => { P.hook('contact', r.dataset.c); if (r.dataset.c === 'Maya') P.go('chatMaya'); });
    },
    chatMaya: () => chatScreen(P, 'Maya', [], '←'),
    Phone: () => {
      P.set(`<div class="papp"><div class="phead">Contacts</div><div class="plist">${[['Maya', '👩🏾'], ['Sam Lee', '🧑'], ['Grandpa Joe', '👴']].map(c => `<div class="prow" data-c="${c[0]}"><div class="pav">${c[1]}</div><div class="pmain"><b>${c[0]}</b><small>Mobile · (555) 01${c[0].length}0</small></div></div>`).join('')}</div></div>`);
      $$('[data-c]', P.view).forEach(r => r.onclick = () => { P.hook('contact', r.dataset.c); if (r.dataset.c === 'Maya') P.go('contactMaya'); });
    },
    contactMaya: () => {
      P.set(`<div class="papp"><div class="phead"><button id="cmB">←</button></div><div style="text-align:center;padding:10px"><div class="pav" style="width:4em;height:4em;border-radius:50%;background:#dbe7ff;display:grid;place-items:center;font-size:2em;margin:0 auto">👩🏾</div><h3 style="margin:.3em 0">Maya</h3><div style="color:#6b7785">Mobile (555) 0140</div><div style="display:flex;justify-content:center;gap:18px;margin-top:18px"><button class="callbtn go" id="callGo">📞</button><button class="callbtn" style="background:#1a73e8">💬</button></div><div style="margin-top:6px;font-size:.85em;color:#6b7785">Call · Message</div></div></div>`);
      $('#cmB').onclick = P.back; $('#callGo').onclick = () => { P.hook('call'); P.go('calling'); };
    },
    calling: () => {
      P.set(`<div class="callscr"><div class="cav">👩🏾</div><h3 style="margin:.2em 0">Maya</h3><div id="callSt">Calling…</div><div style="flex:1"></div><button class="callbtn end" id="callEnd" style="margin-bottom:40px">📞</button></div>`);
      let s = 0; P.ct = setTimeout(() => { const st = $('#callSt'); if (!st) return; st.textContent = '00:01 · Connected'; P.hook('connected'); P.ci = setInterval(() => { s++; const e = $('#callSt'); if (!e) return clearInterval(P.ci); e.textContent = `00:${String(s + 1).padStart(2, '0')} · Connected`; }, 1000); }, 1800);
      $('#callEnd').onclick = () => { clearTimeout(P.ct); clearInterval(P.ci); P.hook('endcall'); P.stack.pop(); P.scr = 'contactMaya'; sc.contactMaya(); };
    },
    Settings: () => {
      P.set(`<div class="papp"><div class="phead">Settings</div><div class="plist">${[['net', '📶', 'Network & internet', 'Wi-Fi, mobile data'], ['dev', '🔗', 'Connected devices', 'Bluetooth'], ['apps', '🧩', 'Apps', 'Recent apps'], ['bat', '🔋', 'Battery', '80%'], ['disp', '☀️', 'Display', 'Brightness, text size'], ['snd', '🔊', 'Sound', 'Volume, ringtone']].map(r => `<div class="prow" data-s="${r[0]}"><div class="pav">${r[1]}</div><div class="pmain"><b>${r[2]}</b><small>${r[3]}</small></div></div>`).join('')}</div></div>`);
      $$('[data-s]', P.view).forEach(r => r.onclick = () => { P.hook('setting', r.dataset.s); if (r.dataset.s === 'net') P.go('wifi'); });
    },
    wifi: () => {
      P.set(`<div class="papp"><div class="phead"><button id="wB">←</button><span class="sp">Internet</span></div><div class="prow" style="cursor:default"><div class="pmain"><b>Wi-Fi</b><small>${P.wifi ? 'On' : 'Off'}</small></div><button class="toggle ${P.wifi ? 'on' : ''}" id="wTog" aria-label="Wi-Fi switch"></button></div><div class="plist" id="nets">${wifiNetworks(P, P.wifi, P.wifiConn)}</div></div>`);
      $('#wB').onclick = P.back;
      $('#wTog').onclick = () => { P.wifi = !P.wifi; sc.wifi(); P.hook('wifi', P.wifi); };
      $$('[data-net]', P.view).forEach(r => r.onclick = () => { P.hook('net', r.dataset.net); if (r.dataset.net === 'TownHome-WiFi') wifiSheet(P, sc.wifi, 'Connect', '←'); });
    },
    'Play Store': () => {
      P.set(`<div class="papp"><div class="phead">Play Store</div><div style="padding:0 12px"><input id="psIn" placeholder="🔍 Search apps & games" autocomplete="off" inputmode="none" style="width:100%;border:none;background:#eef1f4;border-radius:99px;padding:.6em 1em;font-size:1em"></div><div class="plist" id="psRes"><div style="padding:14px;color:#6b7785">Top charts · Games · Kids</div></div><div id="kbSlot"></div></div>`);
      const inp = $('#psIn'); inp.addEventListener('focus', () => P.hook('storeFocus'));
      inp.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); inp.blur(); P.hook('search', inp.value.trim()); if (/weather/i.test(inp.value)) { $('#psRes').innerHTML = `<div class="store-res"><div class="sic">⛅</div><div style="flex:1"><b>Town Weather</b><br><small>Tech Town Apps · ⭐ 4.8 · Free</small></div><button class="pbtn green" id="stInstall">Install</button></div>`; storeInstall(P, '#stInstall'); } } });
    },
  };
  return sc;
}
function wifiSheet(P, redraw, btn, back) {
  P.view.insertAdjacentHTML('beforeend', `<div class="sheet" id="wSheet"><b>TownHome-WiFi</b><div style="font-size:.85em;color:#6b7785">Enter the password</div><input id="wPass" placeholder="Password" autocomplete="off" inputmode="none"><div style="display:flex;gap:8px;justify-content:flex-end"><button class="pbtn" style="background:#c7ccd3;color:#111" id="wCancel">Cancel</button><button class="pbtn ${P.and ? 'green' : ''}" id="wJoin">${btn}</button></div><div id="wKb"></div></div>`);
  const inp = $('#wPass');
  const join = () => { if (inp.value.trim() === 'town2026') { P.wifiConn = 'TownHome-WiFi'; $('#wSheet').remove(); redraw(); P.hook('wifiok'); } else { inp.classList.add('shake'); setTimeout(() => inp.classList.remove('shake'), 400); P.hook('wifibad', inp.value); } };
  $('#wJoin').onclick = join; inp.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); join(); } });
  $('#wCancel').onclick = () => { $('#wSheet').remove(); P.hook('wificancel'); };
  P.hook('wifiSheet');
}

/* ---------------- iPHONE ---------------- */
function iphoneScreens(P) {
  const BG = 'pbg-ios';
  const apps = [['Messages', '💬', '#34c759'], ['Phone', '📞', '#34c759'], ['Camera', '📷', '#8e8e93'], ['Photos', '🌸', '#ffffff'], ['Settings', '⚙️', '#8e8e93'], ['App Store', '🅰️', '#0a84ff'], ['Mail', '✉️', '#0a84ff'], ['Maps', '🗺️', '#a8dab5'], ['Clock', '⏰', '#1c1c1e'], ['Calendar', '📅', '#ffffff'], ['Notes', '📝', '#ffd60a'], ['Weather', '⛅', '#4aa3df']];
  const sc = {
    off: () => P.set('<div style="position:absolute;inset:0;background:#000"></div>'),
    lock: () => { P.set(lockHTML(BG)); P.view.onclick = () => { if (P.scr === 'lock') P.hook('lockTap'); }; },
    pin: () => pinScreen(P, BG, '123456', 'Enter Passcode'),
    home: () => {
      P.view.onclick = null;
      P.set(`<div class="${BG}" style="position:absolute;inset:0"></div>` + appGrid(apps, [['Phone', '📞', '#34c759'], ['Safari', '🧭', '#ffffff'], ['Messages', '💬', '#34c759'], ['Mail', '✉️', '#0a84ff']]));
      $$('.app', P.view).forEach(a => a.onclick = () => { const n = a.dataset.app; P.hook('app', n); if (sc[n]) P.go(n); else P.hook('appNA', n); });
    },
    Messages: () => {
      P.set(`<div class="papp"><div class="phead"><span class="sp">Messages</span><button id="compose" style="font-size:1.2em" aria-label="New message">✏️</button></div><div class="plist"><div class="prow"><div class="pav">👩🏾</div><div class="pmain"><b>Maya</b><small>You're doing great! 😊</small></div></div><div class="prow"><div class="pav">👴</div><div class="pmain"><b>Grandpa Joe</b><small>Call me when you can</small></div></div></div></div>`);
      $('#compose').onclick = () => { P.hook('compose'); P.go('newMsg'); };
    },
    newMsg: () => {
      P.set(`<div class="papp"><div class="phead"><span class="sp" style="font-size:.8em">New Message</span><button id="nmCancel">Cancel</button></div><div style="padding:6px 14px;border-bottom:1px solid #eee">To: <span style="color:#8e8e93">tap a name below</span></div><div class="plist">${[['Sam Lee', '🧑'], ['Maya', '👩🏾'], ['Grandpa Joe', '👴']].map(c => `<div class="prow" data-c="${c[0]}"><div class="pav">${c[1]}</div><div class="pmain"><b>${c[0]}</b><small>mobile</small></div></div>`).join('')}</div></div>`);
      $('#nmCancel').onclick = P.back; $$('[data-c]', P.view).forEach(r => r.onclick = () => { P.hook('contact', r.dataset.c); if (r.dataset.c === 'Sam Lee') P.go('chatSam'); });
    },
    chatSam: () => chatScreen(P, 'Sam Lee', [['them', 'Hey! Are you coming to the park?']], '‹ Back'),
    Camera: () => {
      P.set(`<div class="papp dark" style="padding-top:0"><div class="viewfinder" id="vf">🌻</div><button class="shutter" id="shut" aria-label="Take photo"></button>${P.photo ? '<div class="thumb" id="thumb">🌻</div>' : ''}</div>`);
      $('#shut').onclick = () => { $('#vf').insertAdjacentHTML('afterend', '<div class="flash"></div>'); P.photo = true; if (!$('#thumb')) $('.papp', P.view).insertAdjacentHTML('beforeend', '<div class="thumb" id="thumb">🌻</div>'); $('#thumb').onclick = () => P.go('photoView'); P.hook('shutter'); };
      const t = $('#thumb'); if (t) t.onclick = () => P.go('photoView');
    },
    photoView: () => { P.set(`<div class="papp dark"><div class="phead"><button id="pvB">‹ Back</button></div><div style="flex:1;display:grid;place-items:center"><div style="width:90%;aspect-ratio:3/4;background:linear-gradient(#8fd3ff 0 45%,#6cc070 45%);display:grid;place-items:center;font-size:5em;border-radius:6px">🌻</div></div></div>`); $('#pvB').onclick = P.back; P.hook('photoView'); },
    Settings: () => {
      P.set(`<div class="papp" style="background:#f2f2f7"><div class="phead">Settings</div><div class="plist" style="background:#fff;margin:0 10px;border-radius:12px">${[['air', '✈️', 'Airplane Mode'], ['wifi', '📶', 'Wi-Fi'], ['bt', '🔵', 'Bluetooth'], ['notif', '🔔', 'Notifications'], ['snd', '🔊', 'Sounds & Haptics'], ['disp', '☀️', 'Display & Brightness']].map(r => `<div class="prow" data-s="${r[0]}"><div class="pav">${r[1]}</div><div class="pmain"><b>${r[2]}</b></div><span style="color:#c7c7cc">›</span></div>`).join('')}</div></div>`);
      $$('[data-s]', P.view).forEach(r => r.onclick = () => { P.hook('setting', r.dataset.s); if (r.dataset.s === 'wifi') P.go('wifi'); });
    },
    wifi: () => {
      P.set(`<div class="papp" style="background:#f2f2f7"><div class="phead"><button id="wB">‹ Settings</button></div><div style="padding:0 14px;font-weight:900;font-size:1.3em">Wi-Fi</div><div class="prow" style="background:#fff;margin:8px 10px;border-radius:12px;cursor:default"><div class="pmain"><b>Wi-Fi</b></div><button class="toggle ${P.wifi ? 'on' : ''}" id="wTog" aria-label="Wi-Fi switch"></button></div><div class="plist" style="background:#fff;margin:0 10px;border-radius:12px;flex:none">${wifiNetworks(P, P.wifi, P.wifiConn)}</div></div>`);
      $('#wB').onclick = P.back; $('#wTog').onclick = () => { P.wifi = !P.wifi; sc.wifi(); P.hook('wifi', P.wifi); };
      $$('[data-net]', P.view).forEach(r => r.onclick = () => { P.hook('net', r.dataset.net); if (r.dataset.net === 'TownHome-WiFi') wifiSheet(P, sc.wifi, 'Join', '‹'); });
    },
    'App Store': (tab) => {
      const search = tab === 'search';
      P.set(`<div class="papp"><div class="phead">${search ? 'Search' : 'Today'}</div>${search ? `<div style="padding:0 12px"><input id="asIn" placeholder="🔍 Games, Apps and more" autocomplete="off" inputmode="none" style="width:100%;border:none;background:#eef1f4;border-radius:12px;padding:.6em 1em;font-size:1em"></div><div class="plist" id="asRes"></div><div id="kbSlot"></div>` : '<div class="plist"><div style="margin:10px;height:60%;border-radius:18px;background:linear-gradient(135deg,#7c4dff,#ff9ec7);display:grid;place-items:center;color:#fff;font-weight:900;font-size:1.4em">App of the Day</div></div>'}<div class="ptabbar"><button class="${search ? '' : 'on'}" id="tToday">📰<span>Today</span></button><button>🎮<span>Games</span></button><button>📦<span>Apps</span></button><button class="${search ? 'on' : ''}" id="tSearch">🔍<span>Search</span></button></div></div>`);
      $('#tSearch').onclick = () => { P.hook('searchTab'); P.scr = 'App Store'; sc['App Store']('search'); };
      $('#tToday').onclick = () => sc['App Store']();
      const inp = $('#asIn'); if (inp) {
        inp.addEventListener('focus', () => P.hook('storeFocus'));
        inp.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); inp.blur(); P.hook('search', inp.value.trim()); if (/weather/i.test(inp.value)) { $('#asRes').innerHTML = `<div class="store-res"><div class="sic">⛅</div><div style="flex:1"><b>Town Weather</b><br><small>Tech Town Apps · ⭐ 4.8</small></div><button class="pbtn" id="stGet">GET</button></div>`; $('#stGet').onclick = () => { P.hook('get'); P.view.insertAdjacentHTML('beforeend', '<div class="sheet" id="getSheet"><b>⛅ Town Weather</b><div style="font-size:.85em;color:#6b7785;margin:.3em 0 .8em">Free · Tech Town Apps</div><button class="pbtn" id="stConfirm" style="width:100%;padding:.6em">Install</button></div>'); $('#stConfirm').onclick = () => { $('#getSheet').remove(); const r = $('#asRes .store-res'); r.insertAdjacentHTML('beforeend', '<button class="pbtn" id="stInstall2">…</button>'); $('#stGet').remove(); storeInstall(P, '#stInstall2'); $('#stInstall2').click(); }; }; } } });
      }
    },
  };
  return sc;
}

/* Generic phone step builder: say, hint, targets (fn returning elements), handler(api, ctx, ev, data) */
function pStep(say, hint, targets, handler, prep) {
  return { say, hint, build: (S, api, ctx) => { if (ctx.P && ctx.P.type === 'iphone') { const sh = $('#shade'); if (sh) sh.remove(); } if (prep) prep(S, api, ctx); const t = targets ? targets(ctx) : []; api.target(t); ctx.hook = (ev, d) => handler(api, ctx, ev, d); api.cleanup(() => ctx.hook = null); if (ctx.P) api.onKey(e => ctx.P.pinKey && ctx.P.pinKey(e)); } };
}
const byApp = n => () => [$(`.apps .app[data-app="${n}"]`) || $(`.dock .app[data-app="${n}"]`)];
const openApp = (n, msg) => (api, ctx, ev, d) => { if (ev === 'app') { if (d === n) api.done(msg); else api.wrong(`That opened ${d}. Tap the ${n} app instead. Go back to the Home screen first.`); } };
function phoneKb(api, ctx, inputSel) { const inp = $(inputSel); const slot = $('#kbSlot') || $('#wKb'); if (inp && slot && !slot.childElementCount) ctx.P.kbInto(slot, inp, api); if (inp) setTimeout(() => inp.focus({ preventScroll: true }), 100); }

LESSONS.android = {
  setup: (S, ctx) => makePhone(S, ctx, 'android'),
  steps: [
    {
      say: 'Welcome to the Green Phone Shop! This is an <b>Android phone</b>, a small computer you control with your <b>finger</b>. <b>Tap</b> = touch and let go (like a click). <b>Swipe</b> = slide your finger across the screen. On this practice phone, use your mouse: click to tap, and click-and-drag to swipe.', next: true,
      build: (S, api, ctx) => { const ph = $('#phone'); const l1 = document.createElement('div'); l1.className = 'sbtn-label'; l1.style.cssText = 'right:-104px;top:38%'; l1.textContent = '← Power'; const l2 = l1.cloneNode(); l2.style.top = '18%'; l2.textContent = '← Volume'; ph.append(l1, l2); api.cleanup(() => { l1.remove(); l2.remove(); }); }
    },
    pStep('Press the <b>power button</b> on the right side of the phone to wake it up.', 'The power button is the lower button on the right edge of the phone.', () => [$('#pPow')], (api, ctx, ev, d) => { if (ev === 'power' && d === 'lock') api.done('The phone woke up! This is the lock screen.'); }),
    pStep('<b>Swipe up</b> to unlock: press and hold on the lower part of the screen, <b>slide upward</b>, then let go.', 'Press the mouse button down on the screen, keep holding, move up, then let go.', () => [$('#pscr')], (api, ctx, ev, d) => {
      if (ev === 'swipe') { if (d.dir === 'up') { ctx.P.scr = 'pin'; ctx.P.screens.pin(); api.done('Great swipe!'); } else api.soft('That was a swipe DOWN. Slide UP instead!'); }
      if (ev === 'lockTap') api.wrong('That was a tap. To swipe, hold the button down and SLIDE upward before letting go.');
      if (ev === 'smallswipe') api.soft('Almost! Slide a little farther up.');
    }),
    pStep('Now enter the PIN <b>2 5 8 0</b> by tapping the numbers. (Real PINs should be secret!)', 'Tap 2, then 5, then 8, then 0. You can also type them on your keyboard.', () => $$('.pinkeys button'), (api, ctx, ev) => { if (ev === 'pinok') api.done('Unlocked! This is the Home screen, where all your apps live.'); if (ev === 'pinbad') api.soft('That PIN didn\'t match. Try 2 5 8 0 again.'); }),
    pStep('Each picture is an <b>app</b>. Let\'s send a text message. <b>Tap the Messages app 💬</b>.', 'Messages is the blue speech bubble at the top left of the apps.', byApp('Messages'), openApp('Messages')),
    pStep('Tap <b>💬 Start chat</b>, then tap <b>Maya</b>.', 'Start chat is the green button at the bottom right.', () => [$('#fab')], (api, ctx, ev, d) => {
      if (ev === 'screen' && d === 'pickContact') { api.say('Now tap <b>Maya</b>.'); api.setTargets($('[data-c="Maya"]')); }
      if (ev === 'contact' && d !== 'Maya') api.soft('Please choose Maya this time.');
      if (ev === 'screen' && d === 'chatMaya') api.done('quiet');
    }),
    {
      say: 'Tap the message box, type <b>Hi Maya</b> using the phone keyboard (or your real keyboard), then tap the <b>send arrow ➤</b>.', hint: 'Tap the box that says “Text message”, type Hi Maya, then tap the round arrow button.',
      build: (S, api, ctx) => {
        api.target($('#chIn'), $('#chSend')); phoneKb(api, ctx, '#chIn');
        ctx.hook = (ev, d) => { if (ev === 'send') { if (/hi\s*maya/i.test(d)) { api.after(1200, () => { const c = $('#chat'); if (c) c.insertAdjacentHTML('beforeend', '<div class="pb them">Hi! You\'re doing great 😊</div>'); }); api.done('Sent! Maya is typing back…'); } else api.soft('Message sent! For this lesson, please send “Hi Maya”.'); } if (ev === 'sendEmpty') api.soft('Type Hi Maya first, then tap send.'); };
        api.cleanup(() => ctx.hook = null);
      }
    },
    pStep('To go back to the Home screen at any time, tap the <b>Home button ●</b> (the circle at the bottom). Some phones use a <b>swipe up from the bottom edge</b> instead.', 'The Home button is the circle in the black bar at the bottom of the phone.', () => [$('#nHome')], (api, ctx, ev) => { if (ev === 'home') api.done('Home again!'); if (ev === 'back') api.soft('That was Back ◀. It works too, but tap the round Home button ●.'); if (ev === 'recent') api.soft('That shows recent apps. Tap the round Home button ● in the middle.'); }),
    pStep('Let\'s make a phone call. <b>Tap the Phone app 📞</b>.', 'The Phone app is the green phone, in the apps and in the bottom row.', byApp('Phone'), openApp('Phone')),
    pStep('Tap <b>Maya</b>, then tap the green <b>📞 Call</b> button.', 'Tap Maya\'s name first.', () => [$('[data-c="Maya"]')], (api, ctx, ev, d) => {
      if (ev === 'screen' && d === 'contactMaya') { api.say('Now tap the green <b>📞</b> button to call.'); api.setTargets($('#callGo')); }
      if (ev === 'contact' && d !== 'Maya') api.soft('Please choose Maya this time.');
      if (ev === 'connected') api.done('You\'re talking to Maya!');
    }),
    pStep('To <b>hang up</b>, tap the <b>red</b> button.', 'The red button is at the bottom of the call screen.', () => [$('#callEnd')], (api, ctx, ev) => { if (ev === 'endcall') api.done('Call ended.'); }),
    pStep('The <b>Back button ◀</b> takes you back one step. Tap <b>◀</b> until you reach the Home screen.', 'Back is the triangle on the left of the black bar at the bottom.', () => [$('#nBack')], (api, ctx, ev) => { if (ev === 'home') api.done(); if (ev === 'back' && ctx.P.scr === 'home') api.done(); else if (ev === 'back') api.soft('Good! Keep tapping ◀ until you see the Home screen.'); }),
    pStep('<b>Wi-Fi</b> connects your phone to the internet at home, for free. <b>Tap Settings ⚙️</b>.', 'Settings is the gray gear.', byApp('Settings'), openApp('Settings')),
    pStep('Tap <b>Network & internet</b>.', 'It\'s the first line, with the 📶 signal bars.', () => [$('[data-s="net"]')], (api, ctx, ev, d) => { if (ev === 'screen' && d === 'wifi') api.done('quiet'); else if (ev === 'setting') api.soft('Please tap “Network & internet”, the first line.'); }),
    pStep('Wi-Fi is off. Tap the <b>switch</b> to turn it on, then tap <b>TownHome-WiFi</b>.', 'The switch is on the right side of the Wi-Fi row.', () => [$('#wTog')], (api, ctx, ev, d) => {
      if (ev === 'wifi') { if (d) { api.say('Wi-Fi is on! Now tap <b>TownHome-WiFi</b>.'); api.setTargets($('[data-net="TownHome-WiFi"]')); } else api.soft('That turned it off. Tap the switch again to turn it ON.'); }
      if (ev === 'net' && d !== 'TownHome-WiFi') api.soft('Choose TownHome-WiFi, your home network.');
      if (ev === 'wifiSheet') api.done('quiet');
    }),
    {
      say: 'Type the Wi-Fi password <b>town2026</b>, then tap <b>Connect</b>. (Your real password is usually on a sticker on your internet box.)', hint: 'Tap the Password box, type town2026 (the numbers are on the top row), then tap Connect.',
      build: (S, api, ctx) => {
        api.target($('#wPass'), $('#wJoin')); phoneKb(api, ctx, '#wPass');
        ctx.hook = (ev) => { if (ev === 'wifiok') api.done('Connected to Wi-Fi!'); if (ev === 'wifibad') api.wrong('That password isn\'t right. It\'s town2026: lowercase letters, then numbers.'); if (ev === 'wificancel') { wifiSheet(ctx.P, ctx.P.screens.wifi, 'Connect', '←'); api.soft('Let\'s try again. Type town2026 and tap Connect.'); } };
        api.cleanup(() => ctx.hook = null);
      }
    },
    pStep('New apps come from the <b>Play Store</b>. Tap <b>Home ●</b>, then tap <b>Play Store ▶️</b>.', 'First tap the round Home button at the bottom. Then find the Play Store (a white square with a colorful triangle).', () => [$('#nHome')], (api, ctx, ev, d) => {
      if (ev === 'home') { api.say('Now tap <b>Play Store ▶️</b>.'); api.setTargets(byApp('Play Store')()); }
      if (ev === 'app') { if (d === 'Play Store') api.done('quiet'); else api.soft(`That opened ${d}. Go Home ● and tap Play Store.`); }
    }),
    {
      say: 'Tap the <b>search box</b>, type <b>weather</b>, then press <b>⏎</b> (the Enter key) to search.', hint: 'Tap “Search apps & games”, type weather, then tap the ⏎ key at the bottom right of the keyboard.',
      build: (S, api, ctx) => {
        api.target($('#psIn')); phoneKb(api, ctx, '#psIn');
        ctx.hook = (ev, d) => { if (ev === 'search') { if (/weather/i.test(d)) api.done('Found it!'); else api.soft('Try searching for: weather'); } }; api.cleanup(() => ctx.hook = null);
      }
    },
    pStep('Tap <b>Install</b>. It\'s free! Wait for it to finish. (Tip: only install apps from the Play Store, and check the star ⭐ rating.)', 'The green Install button is next to Town Weather.', () => [$('#stInstall')], (api, ctx, ev) => { if (ev === 'installed') api.done('Installed! The app is now on your phone.'); }),
    pStep('<b>Swipe DOWN from the very top</b> of the screen to see your <b>notifications</b> and quick settings.', 'Press and hold at the very top edge of the screen, slide down, then let go.', () => [$('#pscr')], (api, ctx, ev, d) => {
      if (ev === 'swipe') {
        if (d.dir === 'down' && d.fy < .3) {
          ctx.P.screen.insertAdjacentHTML('beforeend', `<div class="shade" id="shade"><div style="font-weight:800;margin-bottom:10px">${clockStr()} · Quick settings</div><div class="tiles"><button class="tile on">📶 Wi-Fi</button><button class="tile">🔵 Bluetooth</button><button class="tile" id="tFlash">🔦 Flashlight</button><button class="tile">🌙 Do not disturb</button><button class="tile">✈️ Airplane</button><button class="tile">🔄 Auto-rotate</button></div><div style="margin-top:12px;background:#3b4048;border-radius:14px;padding:10px">💬 Maya: Hi! You're doing great 😊</div></div>`);
          api.done('These are quick settings and notifications!');
        } else if (d.dir === 'down') api.soft('Start your swipe at the very TOP of the screen.'); else api.soft('Swipe DOWN, from the top edge.');
      }
    }),
    {
      say: 'Tap the <b>🔦 Flashlight</b> tile to turn on the phone\'s light.', hint: 'Flashlight is in the second row of tiles.',
      build: (S, api) => { const t = $('#tFlash'); api.target(t); api.listen(t, 'click', () => { t.classList.toggle('on'); if (t.classList.contains('on')) api.done('Light on! Tap it again later to turn it off.'); }); }
    },
    pStep('Make the phone louder: press the <b>volume up</b> button (the <b>top</b> button on the right side).', 'Volume up is the highest button on the right edge.', () => [$('#pVolUp')], (api, ctx, ev) => { if (ev === 'volup') { const sh = $('#shade'); if (sh) sh.remove(); api.done('Louder!'); } if (ev === 'voldown') api.soft('That was volume DOWN. Try the button above it.'); if (ev === 'power') api.soft('That was the power button. Press it again to wake the phone, then try the top button.'); }),
    {
      say: 'You can use an Android phone! 🔋 Charge it every night. 🔒 Press power to lock it when you\'re done. 🔄 Say yes to updates, because they keep you safe. Click <b>Finish chapter</b>.', next: 'Finish chapter ▶',
      build: (S, api) => { const c = document.createElement('div'); c.className = 'icard neutral'; c.style.maxWidth = '340px'; c.innerHTML = '<h3>Android buttons</h3><p>◀ <b>Back</b>: go back one step</p><p>● <b>Home</b>: go to your apps</p><p>■ <b>Recent</b>: switch between open apps</p><p>⬇️ <b>Swipe down</b>: notifications and quick settings</p>'; $('.phone-wrap').appendChild(c); api.cleanup(() => c.remove()); }
    }
  ]
};

LESSONS.iphone = {
  setup: (S, ctx) => makePhone(S, ctx, 'iphone'),
  steps: [
    {
      say: 'Welcome to the Silver Phone Shop! This is an <b>iPhone</b>. Newer iPhones have <b>no Home button</b>. Instead, you <b>swipe up from the bottom</b>. The <b>side button</b> is on the right, and the <b>volume</b> buttons are on the left.', next: true,
      build: (S, api) => { const ph = $('#phone'); const l1 = document.createElement('div'); l1.className = 'sbtn-label'; l1.style.cssText = 'right:-126px;top:28%'; l1.textContent = '← Side button'; const l2 = document.createElement('div'); l2.className = 'sbtn-label'; l2.style.cssText = 'left:-108px;top:25%'; l2.textContent = 'Volume →'; ph.append(l1, l2); api.cleanup(() => { l1.remove(); l2.remove(); }); }
    },
    pStep('Press the <b>side button</b> on the right to wake the iPhone.', 'The side button is the long button on the right edge.', () => [$('#pPow')], (api, ctx, ev, d) => { if (ev === 'power' && d === 'lock') api.done('It\'s awake!'); }),
    pStep('<b>Swipe up from the bottom</b> of the screen to unlock. (Many iPhones also recognize your face. That\'s called Face ID.)', 'Hold down near the bottom of the screen, slide up, then let go.', () => [$('#pscr')], (api, ctx, ev, d) => {
      if (ev === 'swipe') { if (d.dir === 'up') { ctx.P.scr = 'pin'; ctx.P.screens.pin(); api.done('quiet'); } else api.soft('Slide UP, not down.'); }
      if (ev === 'lockTap' || ev === 'homebarTap') api.wrong('That was a tap. Hold down and SLIDE upward before letting go.');
      if (ev === 'smallswipe') api.soft('Almost! Slide a little farther up.');
    }),
    pStep('Enter the passcode <b>1 2 3 4 5 6</b>. (Choose a harder one on your real phone!)', 'Tap 1, 2, 3, 4, 5, 6 in order. You can also type them on your keyboard.', () => $$('.pinkeys button'), (api, ctx, ev) => { if (ev === 'pinok') api.done('Unlocked! Welcome to the Home Screen.'); if (ev === 'pinbad') api.soft('Not quite. Try 1 2 3 4 5 6 again.'); }),
    pStep('<b>Tap Messages 💬</b> (the green speech bubble).', 'Messages is the first app, top left, and also in the bottom row.', byApp('Messages'), openApp('Messages')),
    pStep('Tap the <b>compose button ✏️</b> at the top right to start a new message.', 'It\'s the little pencil in the top-right corner.', () => [$('#compose')], (api, ctx, ev) => { if (ev === 'compose') api.done('quiet'); }),
    pStep('Tap <b>Sam Lee</b> to choose who gets the message.', 'Sam Lee is first in the list.', () => [$('[data-c="Sam Lee"]')], (api, ctx, ev, d) => { if (ev === 'screen' && d === 'chatSam') api.done('quiet'); else if (ev === 'contact') api.soft('Please choose Sam Lee this time.'); }),
    {
      say: 'Sam asked if you\'re coming. Type <b>See you soon</b> and tap the blue <b>⬆ send</b> arrow.', hint: 'Tap the “Text message” box, type See you soon, then tap the blue arrow.',
      build: (S, api, ctx) => {
        api.target($('#chIn'), $('#chSend')); phoneKb(api, ctx, '#chIn');
        ctx.hook = (ev, d) => { if (ev === 'send') { if (/see\s*you\s*soon/i.test(d)) { api.after(1200, () => { const c = $('#chat'); if (c) c.insertAdjacentHTML('beforeend', '<div class="pb them">Great! 👋</div>'); }); api.done('Sent! Blue bubbles are iMessages.'); } else api.soft('Sent! For this lesson, please send “See you soon”.'); } if (ev === 'sendEmpty') api.soft('Type See you soon first.'); };
        api.cleanup(() => ctx.hook = null);
      }
    },
    pStep('Now go <b>Home</b>: <b>swipe up</b> starting on the thin white line at the very bottom of the screen.', 'Put the pointer on the white line at the bottom, hold down, slide up, let go.', () => [$('#hbar')], (api, ctx, ev, d) => {
      if (ev === 'swipe') { if (d.dir === 'up' && d.fy > .7) { ctx.P.home(); api.done('Home! Swiping up from the bottom always takes you home.'); } else if (d.dir === 'up') api.soft('Start lower, right at the bottom edge of the screen.'); }
      if (ev === 'homebarTap') { if (L.wrongs >= 2) { ctx.P.home(); api.done('Home! (Next time try sliding up from the line.)'); } else api.wrong('That was a tap. Hold down on the line and SLIDE up.'); }
    }),
    pStep('Let\'s take a photo! <b>Tap the Camera app 📷</b>.', 'Camera is gray with a camera picture.', byApp('Camera'), openApp('Camera')),
    pStep('Tap the big white <b>shutter button</b> to take a picture.', 'The shutter is the big white circle at the bottom.', () => [$('#shut')], (api, ctx, ev) => { if (ev === 'shutter') api.done('Snap! 📸'); }),
    pStep('Tap the little <b>photo in the corner</b> to see your picture.', 'It\'s the small square at the bottom left.', () => [$('#thumb')], (api, ctx, ev) => { if (ev === 'photoView') api.done('Beautiful! Your photos are saved in the Photos app.'); }),
    pStep('<b>Swipe up from the bottom</b> to go Home again.', 'Hold down at the very bottom of the screen and slide up.', () => [$('#hbar')], (api, ctx, ev, d) => {
      if (ev === 'swipe' && d.dir === 'up' && d.fy > .7) { ctx.P.home(); api.done(); } else if (ev === 'swipe') api.soft('Start right at the bottom edge and slide UP.');
      if (ev === 'homebarTap') { if (L.wrongs >= 2) { ctx.P.home(); api.done(); } else api.wrong('Hold down and slide up. Don\'t just tap.'); }
    }),
    pStep('Open <b>Control Center</b>: <b>swipe DOWN from the top-right corner</b> of the screen (where the battery is).', 'Hold down at the top-right corner, near 🔋, and slide down.', () => [$('#pscr')], (api, ctx, ev, d) => {
      if (ev === 'swipe') {
        if (d.dir === 'down' && d.fy < .3) {
          ctx.P.screen.insertAdjacentHTML('beforeend', `<div class="shade" id="shade"><div class="tiles"><button class="tile on">📶</button><button class="tile">✈️</button><button class="tile">🔵</button><button class="tile">🌙</button><button class="tile" id="tFlash">🔦</button><button class="tile">⏱️</button><button class="tile">🧮</button><button class="tile">📷</button></div><div style="margin-top:12px;text-align:center;font-size:.85em;opacity:.8">Swipe up to close</div></div>`);
          api.done('This is Control Center!');
        } else if (d.dir === 'down') api.soft('Start at the very TOP of the screen.'); else api.soft('Swipe DOWN from the top-right corner.');
      }
    }),
    {
      say: 'Tap the <b>🔦 flashlight</b> button.', hint: 'The flashlight is in the second row.',
      build: (S, api) => { const t = $('#tFlash'); api.target(t); api.listen(t, 'click', () => { t.classList.add('on'); setTimeout(() => { const sh = $('#shade'); if (sh) sh.remove(); }, 900); api.done('Light on!'); }); }
    },
    pStep('Let\'s connect to Wi-Fi. <b>Tap Settings ⚙️</b>, then tap <b>Wi-Fi</b>.', 'Settings is the gray gear icon.', byApp('Settings'), (api, ctx, ev, d) => {
      if (ev === 'app') { if (d === 'Settings') { api.say('Now tap <b>Wi-Fi</b>.'); api.setTargets($('[data-s="wifi"]')); } else api.soft(`That opened ${d}. Swipe up to go Home, then tap Settings.`); }
      if (ev === 'screen' && d === 'wifi') api.done('quiet'); else if (ev === 'setting') api.soft('Tap “Wi-Fi”, the second line.');
    }),
    pStep('Turn on the <b>Wi-Fi switch</b>, then tap <b>TownHome-WiFi</b>.', 'The switch is on the right of the Wi-Fi row.', () => [$('#wTog')], (api, ctx, ev, d) => {
      if (ev === 'wifi') { if (d) { api.say('Now tap <b>TownHome-WiFi</b>.'); api.setTargets($('[data-net="TownHome-WiFi"]')); } else api.soft('That turned it off. Tap it again to turn it ON (green).'); }
      if (ev === 'net' && d !== 'TownHome-WiFi') api.soft('Choose TownHome-WiFi.');
      if (ev === 'wifiSheet') api.done('quiet');
    }),
    {
      say: 'Type the password <b>town2026</b> and tap <b>Join</b>.', hint: 'Tap the Password box, type town2026, then tap Join.',
      build: (S, api, ctx) => {
        api.target($('#wPass'), $('#wJoin')); phoneKb(api, ctx, '#wPass');
        ctx.hook = ev => { if (ev === 'wifiok') api.done('Connected!'); if (ev === 'wifibad') api.wrong('Not quite. The password is town2026.'); if (ev === 'wificancel') { wifiSheet(ctx.P, ctx.P.screens.wifi, 'Join', '‹'); api.soft('Let\'s try again.'); } };
        api.cleanup(() => ctx.hook = null);
      }
    },
    pStep('Get new apps from the <b>App Store</b>. Swipe up from the bottom to go Home, then tap <b>App Store 🅰️</b>.', 'Swipe up from the white line at the bottom first.', () => [$('#hbar')], (api, ctx, ev, d) => {
      if (ev === 'swipe' && d.dir === 'up' && d.fy > .7) { ctx.P.home(); api.say('Now tap <b>App Store 🅰️</b>.'); api.setTargets(byApp('App Store')()); }
      if (ev === 'homebarTap') { ctx.P.home(); api.say('Now tap <b>App Store 🅰️</b>.'); api.setTargets(byApp('App Store')()); }
      if (ev === 'app') { if (d === 'App Store') api.done('quiet'); else api.soft(`That opened ${d}. Go Home and tap App Store.`); }
    }),
    pStep('Tap <b>🔍 Search</b> at the bottom right.', 'Search is the last button in the bottom bar.', () => [$('#tSearch')], (api, ctx, ev) => { if (ev === 'searchTab') api.done('quiet'); }),
    {
      say: 'Tap the search box, type <b>weather</b>, and press <b>⏎</b> (Enter) to search.', hint: 'Tap the search box, type weather, then tap ⏎.',
      build: (S, api, ctx) => {
        api.target($('#asIn')); phoneKb(api, ctx, '#asIn');
        ctx.hook = (ev, d) => { if (ev === 'search') { if (/weather/i.test(d)) api.done('Found it!'); else api.soft('Search for: weather'); } }; api.cleanup(() => ctx.hook = null);
      }
    },
    pStep('Tap <b>GET</b>, then tap <b>Install</b> to confirm. (On a real iPhone, you may need to confirm with your face or password.)', 'GET is the blue button next to Town Weather.', () => [$('#stGet')], (api, ctx, ev) => {
      if (ev === 'get') { api.say('Now tap <b>Install</b> to confirm.'); api.setTargets($('#stConfirm')); }
      if (ev === 'installed') api.done('Installed!');
    }),
    {
      say: 'You can use an iPhone too! Here\'s how Android and iPhone compare. Click <b>Finish chapter</b>.', next: 'Finish chapter ▶',
      build: S => infoCard(S, `<h3>Android vs iPhone</h3><table class="cmp"><tr><th>To do this…</th><th>🤖 Android</th><th>📱 iPhone</th></tr><tr><td>Go Home</td><td>Tap ● or swipe up from the bottom</td><td>Swipe up from the bottom</td></tr><tr><td>Go back</td><td>Tap ◀ or swipe in from the side</td><td>Tap “‹ Back” at the top left</td></tr><tr><td>Quick settings</td><td>Swipe down from the top</td><td>Swipe down from the top-right</td></tr><tr><td>Get apps</td><td>Play Store</td><td>App Store</td></tr><tr><td>Wake / lock</td><td>Power button</td><td>Side button</td></tr><tr><td>Unlock</td><td>Fingerprint, face or PIN</td><td>Face ID or passcode</td></tr></table>`)
    }
  ]
};
