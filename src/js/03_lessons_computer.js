/* =====================================================================
   LESSONS 1–4
   ===================================================================== */
const clockStr = () => new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });

/* ---------------- CHAPTER 1: HOME — meet the computer ---------------- */
function ensurePC(S) {
  if ($('#pcMon')) return;
  S.innerHTML = `<div class="stage-inner"><div class="desk" id="pcDesk"><div class="table"></div>
    <div class="pc-monitor" id="pcMon"><div class="pc-screen" id="pcScr"></div></div><div class="pc-neck"></div><div class="pc-base"></div>
    <div class="pc-tower" id="pcTower"><button class="pwbtn" id="pcPwr" aria-label="Power button">⏻</button><div class="slot"></div><div class="slot slot2"></div><div class="vent"></div></div>
    <div class="pc-kbd" id="pcKbd"></div><div class="pc-mouse" id="pcMouse"></div></div><div id="pcBelow"></div></div>`;
}
function plabel(text, left, top) {
  const d = $('#pcDesk'); if (!d || $(`.plabel[data-t="${text}"]`, d)) return;
  const l = document.createElement('div'); l.className = 'plabel'; l.dataset.t = text; l.textContent = text; l.style.left = left + '%'; l.style.top = top + '%'; d.appendChild(l);
}
function pcPart(sel, name, lx, ly) {
  return (S, api) => { ensurePC(S); const el = $(sel); api.target(el); api.listen(el, 'click', e => { e.stopPropagation(); plabel(name, lx, ly); api.done(); }); };
}
const MINI_DESK = `<div class="mini-desk"><div class="mi" style="left:4%;top:5%"><span>🗑️</span>Recycle Bin</div><div class="mi" style="left:4%;top:32%"><span>🌐</span>Browser</div>
  <div class="mi" style="left:4%;top:59%"><span>📁</span>Documents</div><div class="mini-task"><button class="startb" id="pcStart">⊞</button><button class="tray" id="pcWifi" style="margin-left:auto" aria-label="Wi-Fi">📶</button><button class="tray" id="pcVol" aria-label="Volume">🔊</button><span style="font-size:.8em;padding-right:6px">${clockStr()}</span></div></div>`;

LESSONS.home = {
  steps: [
    { say: 'Welcome home, {name}! Before we use a computer, let\'s meet its parts. This is a <b>desktop computer</b>. When you\'re ready, click the green <b>Next</b> button (or press the Enter key).', next: true, build: S => ensurePC(S) },
    { say: 'This is the <b>screen</b>, also called the <b>monitor</b>. It shows you words and pictures. <b>Click on the screen</b>: slide the mouse until the arrow is on top of it, then press the left mouse button once.', hint: 'The screen is the big dark rectangle at the top.', build: pcPart('#pcMon', 'Screen', 40, -6) },
    { say: 'This is the <b>keyboard</b>. You use it to type letters and numbers. <b>Click on the keyboard.</b>', hint: 'The keyboard is the long flat gray board in front of the screen.', build: pcPart('#pcKbd', 'Keyboard', 38, 88) },
    { say: 'This is the <b>mouse</b>. When you slide it on the table, the arrow on the screen moves with it. <b>Click on the mouse.</b>', hint: 'The mouse is the small rounded object to the right of the keyboard.', build: pcPart('#pcMouse', 'Mouse', 73, 90) },
    { say: 'This box is the <b>tower</b>, the computer\'s brain. Your pictures, letters and programs are kept inside it. <b>Click on the tower.</b>', hint: 'The tower is the tall gray box on the right.', build: pcPart('#pcTower', 'Tower', 81, 1) },
    {
      say: 'Many people use a <b>laptop</b> instead. A laptop folds shut and has everything in one: screen, keyboard, and a <b>touchpad</b>. The touchpad is a flat square you slide your finger on. It works just like a mouse.', next: true,
      build: S => {
        S.innerHTML = `<div class="stage-inner"><svg class="laptop" viewBox="0 0 560 330"><rect x="90" y="10" width="380" height="240" rx="16" fill="#2a2f37"/><rect x="108" y="26" width="344" height="208" rx="6" fill="#5aa1d8"/>
        <path d="M40 262 H520 L500 300 H60Z" fill="#c9d0d8"/><rect x="40" y="250" width="480" height="14" rx="6" fill="#aab3bd"/>
        <g fill="#eef1f4">${Array.from({ length: 3 }, (_, r) => Array.from({ length: 13 }, (_, c) => `<rect x="${118 + c * 25}" y="${266 + r * 9}" width="21" height="6" rx="2"/>`).join('')).join('')}</g>
        <rect x="240" y="290" width="80" height="8" rx="3" fill="#b2bac4"/>
        <text x="280" y="138" font-size="26" text-anchor="middle" fill="#fff" font-weight="800" font-family="Nunito,Arial">Screen</text>
        <text x="455" y="320" font-size="18" text-anchor="middle" fill="#1f2d3d" font-weight="800" font-family="Nunito,Arial">Keyboard</text>
        <text x="280" y="324" font-size="18" text-anchor="middle" fill="#1f2d3d" font-weight="800" font-family="Nunito,Arial">↑ Touchpad</text></svg></div>`;
      }
    },
    {
      say: 'Let\'s turn the computer on! Find the <b>power button</b> on the tower. It has this symbol: <b>⏻</b> (a circle with a little line at the top). <b>Click the power button.</b>', hint: 'The power button is the round button near the top of the tower.',
      build: (S, api) => {
        ensurePC(S); const b = $('#pcPwr'); api.target(b);
        api.listen(b, 'click', e => {
          e.stopPropagation(); b.classList.add('on'); api.setTargets();
          $('#pcScr').innerHTML = '<div class="boot"><div style="font-size:2em">💻</div><div class="spinner"></div><div>Starting up…</div></div>';
          api.good('It\'s starting up! Computers take a few seconds to wake up. You don\'t need to press anything while you wait.');
          api.after(3600, () => api.done('quiet'));
        });
      }
    },
    {
      say: 'The computer is asking for a <b>PIN</b>, a secret number, to make sure it\'s really you. <b>Type 1 2 3 4</b> using the number keys on the <b>top row</b> of your keyboard. Then press the <b>Enter</b> key.', hint: 'Look at the top row of the keyboard for the numbers 1, 2, 3 and 4. Then find the big Enter key on the right side.',
      build: (S, api) => {
        ensurePC(S);
        $('#pcScr').innerHTML = `<div class="login"><div class="av">🙂</div><b>${esc(S_name())}</b><input id="pinIn" type="password" inputmode="numeric" maxlength="8" placeholder="PIN" autocomplete="off"><small>Type 1234, then press Enter</small></div>`;
        const inp = $('#pinIn'); const kb = makeKeyboard(api, { input: inp, zones: true });
        $('#pcBelow').innerHTML = ''; $('#pcBelow').appendChild(kb.el);
        typeCheck(api, inp, '1234', { enter: true, kb, onOk: () => { $('#pcBelow').innerHTML = ''; $('#pcScr').innerHTML = MINI_DESK; api.done('You\'re in! In real life, keep your PIN or password a secret. Never tell it to anyone who calls or emails you.'); }, mismatch: 'That PIN isn\'t right yet. Press ⌫ Backspace to erase, then type 1 2 3 4.' });
      }
    },
    { say: 'You\'re in! This is the <b>desktop</b>, your starting place. The small pictures are <b>icons</b>. Each one opens something. The dark bar along the bottom is the <b>taskbar</b>.', next: true, build: S => { ensurePC(S); if (!$('#pcStart')) $('#pcScr').innerHTML = MINI_DESK; } },
    {
      say: 'When you\'re finished, always turn the computer off the right way. <b>Click the Start button</b>: the blue square button in the bottom-left corner of the screen.', hint: 'Look at the very bottom-left corner of the screen for a small blue button.',
      build: (S, api) => {
        ensurePC(S); if (!$('#pcStart')) $('#pcScr').innerHTML = MINI_DESK; const st = $('#pcStart'); api.target(st);
        api.listen(st, 'click', e => { e.stopPropagation(); $('.mini-desk').insertAdjacentHTML('beforeend', `<div class="startmenu" id="sMenu"><div class="smi">📄 Documents</div><div class="smi">⚙️ Settings</div><div class="smi btnish" id="sPow">⏻ Power</div></div>`); api.done('The Start menu opened!'); });
      }
    },
    {
      say: 'This is the <b>Start menu</b>. <b>Click Power ⏻</b> at the bottom of the menu.', hint: 'Power is the last item in the white menu.',
      build: (S, api) => {
        const p = $('#sPow'); if (!p) return api.done('quiet'); api.target(p);
        api.listen(p, 'click', e => { e.stopPropagation(); $('.mini-desk').insertAdjacentHTML('beforeend', `<div class="pwrmenu" id="pwrM"><div>🌙 Sleep</div><div id="pmShut">⏻ Shut down</div><div>🔄 Restart</div></div>`); api.done('quiet'); });
      }
    },
    {
      say: 'You have three choices. <b>Sleep</b> rests the computer. <b>Restart</b> turns it off and on again. <b>Click "Shut down"</b> to turn it off completely.', hint: '"Shut down" is the middle choice.',
      build: (S, api) => {
        const b = $('#pmShut'); if (!b) return api.done('quiet'); api.target(b);
        $$('#pwrM div').forEach(d => { if (d !== b) api.listen(d, 'click', e => { e.stopPropagation(); api.wrong(d.textContent.includes('Sleep') ? 'Sleep is useful for short breaks, but today click “Shut down”.' : 'Restart is great when the computer acts strange, but today click “Shut down”.'); }); });
        api.listen(b, 'click', e => {
          e.stopPropagation(); api.setTargets(); $('#pcScr').innerHTML = '<div class="boot"><div class="spinner"></div><div>Shutting down…</div></div>';
          api.after(2200, () => { $('#pcScr').innerHTML = ''; $('#pcPwr').classList.remove('on'); api.done('The computer is off. Well done!'); });
        });
      }
    },
    {
      say: 'Great tips to remember: 😴 <b>Sleep</b> saves power when you take a short break. 🔄 <b>Restart</b> fixes most problems when a computer acts strange. 🍎 On a <b>Mac</b>, Shut Down is in the Apple menu in the <b>top-left</b> corner.', next: 'Finish chapter ▶',
      build: S => infoCard(S, `<h3>Remember</h3><div class="igrid"><div class="ibox"><div class="ie">⏻</div><b>Power button</b><small>Press once to turn on. Use the Start menu to turn off.</small></div>
        <div class="ibox"><div class="ie">😴</div><b>Sleep</b><small>A quick nap. Touch the mouse or a key to wake it.</small></div><div class="ibox"><div class="ie">🔄</div><b>Restart</b><small>Fixes most problems. Try it first!</small></div>
        <div class="ibox"><div class="ie">🔒</div><b>Keep your PIN secret</b><small>Never share it with callers or emails.</small></div></div>`)
    },
  ]
};
function S_name() { return S.name || 'You'; }

/* ---------------- CHAPTER 2: COMMUNITY CENTER — mouse ---------------- */
LESSONS.mouse = {
  steps: [
    {
      say: 'Welcome to the Community Center! Today: the <b>mouse</b>. Rest your hand on it. Your pointer finger sits on the <b>left button</b>, and your middle finger on the <b>right button</b>. The little wheel between them is the <b>scroll wheel</b>.', next: true,
      build: S => infoCard(S, `<div style="display:flex;gap:24px;align-items:center;flex-wrap:wrap;justify-content:center"><svg viewBox="0 0 200 280" style="width:180px"><path d="M100 10 C40 10 20 60 20 130 C20 220 50 270 100 270 C150 270 180 220 180 130 C180 60 160 10 100 10Z" fill="#eef1f5" stroke="#8e9bab" stroke-width="5"/>
      <path d="M100 10 V120 M20 120 H180" stroke="#8e9bab" stroke-width="5"/><path d="M100 10 C40 10 20 60 22 118 H98 V12Z" fill="#cfe6ff"/><rect x="90" y="40" width="20" height="44" rx="10" fill="#1f2d3d"/></svg>
      <div><p>🔵 <b>Left button:</b> click to choose things (you use this the most!)</p><p>⚪ <b>Right button:</b> opens a menu of extra choices</p><p>⚫ <b>Wheel:</b> roll it to move up and down a page</p><p>➡️ The arrow on the screen is called the <b>pointer</b>.</p></div></div>`)
    },
    {
      say: 'Let\'s practice moving. Slide the mouse gently so the <b>pointer touches the star ⭐</b>. You don\'t need to click. Just touch it! Catch all 3 stars.', hint: 'Slowly slide the mouse on the table. Watch the arrow on the screen and move it onto the star.',
      build: (S, api) => {
        S.innerHTML = '<div class="stage-inner"><div class="playfield" id="pf"></div></div>'; const pf = $('#pf');
        const spots = [[15, 25], [72, 60], [45, 15]]; let n = 0;
        const place = () => {
          const [x, y] = spots[n]; const s = document.createElement('div'); s.className = 'star'; s.textContent = '⭐'; s.style.left = x + '%'; s.style.top = y + '%'; pf.appendChild(s); api.hintOnly(s);
          const hit = () => { s.onpointerenter = null; s.classList.add('got'); Sfx.play('good'); n++; if (n >= 3) api.done('You caught them all!'); else { api.good(`${n} caught! ${3 - n} to go.`); place(); } };
          s.onpointerenter = hit; s.onclick = hit;
        };
        place();
      }
    },
    {
      say: 'Now let\'s <b>click</b>. To click, press the <b>left button</b> once and let go. Pop all <b>5 balloons</b> by clicking on them!', hint: 'Move the pointer right on top of a balloon, then press and release the left button.',
      build: (S, api) => {
        S.innerHTML = '<div class="stage-inner"><div class="playfield" id="pf"></div></div>'; const pf = $('#pf'); let left = 5;
        const cols = ['#e5484d', '#2d7ff9', '#f5b700', '#23a55a', '#7b2ff7'];
        cols.forEach((c, i) => {
          const b = document.createElement('div'); b.className = 'balloon'; b.style.background = c; b.style.left = (8 + i * 18) + '%'; b.style.top = (15 + (i % 2) * 30) + '%'; b.style.animationDelay = (i * .4) + 's';
          pf.appendChild(b); api.target(b);
          b.onclick = () => { if (b.classList.contains('pop')) return; b.classList.add('pop'); Sfx.play('click'); left--; if (!left) api.done('Pop! You\'re a great clicker!'); else api.good(`Pop! ${left} left.`); };
        });
      }
    },
    {
      say: 'Some things need a <b>double-click</b>: two quick clicks in a row, <i>click-click!</i> Keep the mouse still while you click. <b>Double-click the yellow folder</b> to open it.', hint: DBL_HINT,
      build: (S, api) => {
        S.innerHTML = '<div class="stage-inner"><div class="playfield" id="pf"><div class="folder" id="fold"><div class="fe">📁</div>My Photos</div></div></div>';
        const f = $('#fold'); api.target(f);
        dbl(api, f, () => { $('#pf').innerHTML = `<div style="display:flex;gap:20px;justify-content:center;align-items:center;height:100%;font-size:70px;flex-wrap:wrap">🏖️ 🐶 🌻 🎂</div>`; api.done('The folder opened! You double-clicked!'); },
          () => api.soft(DBL_HINT));
      }
    },
    {
      say: 'The <b>right button</b> opens a menu of extra choices. Move the pointer onto the dog photo and <b>press the RIGHT mouse button</b>.', hint: 'Use your middle finger on the right side of the mouse. On a touchpad, tap with two fingers.',
      build: (S, api) => {
        S.innerHTML = '<div class="stage-inner"><div class="playfield" id="pf"><div class="photo" id="dog">🐶</div></div></div>';
        const d = $('#dog'); api.target(d); let menu = false;
        api.listen(d, 'click', () => { if (!menu) api.wrong('That was the LEFT button. Try the RIGHT button, on the right side of the mouse.'); });
        api.listen(d, 'contextmenu', e => {
          e.preventDefault(); if (menu) return; menu = true;
          const pf = $('#pf'), r = pf.getBoundingClientRect();
          const m = document.createElement('div'); m.className = 'ctxmenu'; m.style.left = Math.min(r.width - 190, e.clientX - r.left) + 'px'; m.style.top = Math.min(r.height - 150, e.clientY - r.top) + 'px';
          m.innerHTML = '<div>📋 Copy</div><div id="rot">🔄 Rotate</div><div>🗑️ Delete</div>'; pf.appendChild(m);
          api.say('A menu appeared! Now <b>click “Rotate”</b> (with the normal left button) to turn the photo.');
          const rot = $('#rot'); api.setTargets(rot);
          $$('div', m).forEach(it => it.onclick = ev => { ev.stopPropagation(); if (it === rot) { d.style.transform = 'rotate(90deg)'; m.remove(); api.done('The photo turned! You used the right-click menu.'); } else api.wrong('Good try! This time please choose “Rotate”.'); });
        });
      }
    },
    {
      say: '<b>Drag and drop</b> moves things. Put the pointer on an apple, then <b>press and HOLD</b> the left button. Keep holding while you slide the apple into the basket, then <b>let go</b>. Move all 3 apples!', hint: 'Press the button down and keep it down while you move. Only let go when the apple is over the basket.',
      build: (S, api) => {
        S.innerHTML = '<div class="stage-inner"><div class="playfield" id="pf"><div class="basket" id="bask">Basket</div></div></div>';
        const pf = $('#pf'), bask = $('#bask'); let left = 3;
        [[8, 12], [22, 55], [38, 20]].forEach(([x, y]) => {
          const a = document.createElement('div'); a.className = 'apple'; a.textContent = '🍎'; a.style.left = x + '%'; a.style.top = y + '%'; pf.appendChild(a); api.target(a);
          dragger(api, a, [bask], () => { a.remove(); Sfx.play('good'); left--; bask.textContent = '🍎'.repeat(3 - left); if (!left) api.done('All the apples are in the basket! Great dragging!'); else api.good(`${left} more to go!`); },
            moved => moved < 8 ? api.soft('Hold the button DOWN and slide toward the basket before you let go.') : api.wrong('So close! Keep holding the button until the apple is right over the basket.'));
        });
      }
    },
    {
      say: 'Pages are often longer than the screen. To see more, <b>scroll</b>: roll the <b>wheel</b> on your mouse toward you (or slide two fingers up on a touchpad). Scroll down to the bottom of the sea and <b>click the treasure chest</b>!', hint: 'Roll the mouse wheel toward you. You can also drag the gray bar on the right side of the box.',
      build: (S, api) => {
        const layers = [['#7cc6f0', '🏝️', 'Surface: keep going down! ⬇'], ['#3f9ad8', '🐠', 'Little fish'], ['#2a74b8', '🐢', 'A sea turtle'], ['#1d568f', '🐙', 'Octopus'], ['#123b66', '🦑', 'Almost there…'], ['#0b2744', '<span id="chest" style="cursor:pointer">🧰</span>', 'Treasure! Click it!']];
        S.innerHTML = `<div class="stage-inner"><div class="scrollbox" id="sb">${layers.map(l => `<div class="layer" style="background:${l[0]}"><div class="big">${l[1]}</div>${l[2]}</div>`).join('')}</div></div>`;
        const ch = $('#chest'); api.target(ch); api.hintOnly($('#sb'));
        api.listen(ch, 'click', () => { ch.textContent = '💰'; api.done('You found the treasure by scrolling!'); });
      }
    },
    {
      say: 'Using a <b>laptop touchpad</b>? Here are the moves. When you\'re ready, click <b>Finish chapter</b>.', next: 'Finish chapter ▶',
      build: S => infoCard(S, `<h3>Touchpad cheat sheet</h3><div class="igrid"><div class="ibox"><div class="ie">👆</div><b>Tap once</b><small>= left click</small></div><div class="ibox"><div class="ie">✌️</div><b>Two-finger tap</b><small>= right click</small></div>
      <div class="ibox"><div class="ie">↕️</div><b>Two fingers slide</b><small>= scroll up and down</small></div><div class="ibox"><div class="ie">✊</div><b>Press, hold and slide</b><small>= drag and drop</small></div></div>`)
    }
  ]
};

/* ---------------- CHAPTER 3: TYPING SCHOOL — keyboard ---------------- */
function typeStep({ say, hint, target, cs = false, enter = false, pre = '', mismatch, after, chat = false }) {
  return {
    say, hint,
    build: (S, api) => {
      S.innerHTML = `<div class="stage-inner typebox"><div class="target-text" id="tt"></div>${chat ? '<div class="chatlog" id="chat"></div>' : ''}<input class="typein" id="ti" autocomplete="off" autocapitalize="off" spellcheck="false"><div id="kbSlot"></div></div>`;
      const inp = $('#ti'); inp.value = pre; const kb = makeKeyboard(api, { input: inp, zones: true }); $('#kbSlot').appendChild(kb.el);
      typeCheck(api, inp, target, { cs, enter, display: $('#tt'), kb, mismatch, onOk: () => { if (chat) { $('#chat').innerHTML = `<div class="cbub">${esc(inp.value)}</div>`; inp.value = ''; } api.done(after); } });
    }
  };
}
LESSONS.keys = {
  steps: [
    {
      say: 'Welcome to Typing School! Here is a keyboard map. <b>Blue</b> keys are letters, <b>purple</b> are numbers, <b>green</b> is the Spacebar, <b>orange</b> is Enter, <b>red</b> is Backspace, and <b>teal</b> is Shift.', next: true,
      build: (S, api) => { S.innerHTML = '<div class="stage-inner" id="kbw"></div>'; $('#kbw').appendChild(makeKeyboard(api, { zones: true }).el); }
    },
    {
      say: 'Find the letter <b>C</b> on your keyboard and press it once. It glows yellow on the picture below to help you.', hint: 'C is on the bottom row of letters, near the left.',
      build: (S, api) => {
        S.innerHTML = '<div class="stage-inner"><div class="target-text" id="tt">C A T</div><div id="kbSlot"></div></div>';
        const kb = makeKeyboard(api, { zones: true }); $('#kbSlot').appendChild(kb.el);
        const seq = ['c', 'a', 't']; let i = 0; kb.highlight([seq[0]]);
        const show = () => { $('#tt').innerHTML = seq.map((c, k) => `<span class="${k < i ? 'ok' : k === i ? 'cur' : ''}">${c.toUpperCase()}</span>`).join(' '); };
        show();
        api.onKey(e => {
          if (e.key.length !== 1) return; const k = e.key.toLowerCase(); if (e.key === ' ') e.preventDefault();
          if (k === seq[i]) { i++; show(); Sfx.play('click'); if (i === 3) { kb.highlight([]); api.done('You spelled CAT!'); } else { kb.highlight([seq[i]]); api.say(`Great! Now find and press <b>${seq[i].toUpperCase()}</b>.`); } }
          else api.soft(`That was “${esc(e.key.toUpperCase())}”. Look for the glowing <b>${seq[i].toUpperCase()}</b> key.`);
        });
      }
    },
    {
      say: 'The long bar at the bottom is the <b>Spacebar</b>. It makes a gap between words. Press the Spacebar <b>3 times</b>.', hint: 'The Spacebar is the longest key, at the very bottom of the keyboard. Press it with your thumb.',
      build: (S, api) => {
        S.innerHTML = '<div class="stage-inner"><div class="target-text" id="tt">○ ○ ○</div><div id="kbSlot"></div></div>';
        const kb = makeKeyboard(api, { zones: true }); $('#kbSlot').appendChild(kb.el); kb.highlight([' ']); let n = 0;
        api.onKey(e => { if (e.key === ' ') { e.preventDefault(); n++; $('#tt').textContent = '● '.repeat(n) + '○ '.repeat(Math.max(0, 3 - n)); Sfx.play('click'); if (n >= 3) api.done(); } else if (e.key.length === 1) api.soft('Look for the long green Spacebar at the bottom.'); });
      }
    },
    typeStep({ say: 'Now type a whole word! Click in the white box if you need to, then type: <b>hello</b>. Letters turn <span style="color:#23a55a;font-weight:900">green</span> when they\'re right.', hint: 'Type h, then e, then l, l, o. The next key glows yellow on the keyboard picture.', target: 'hello', after: 'You typed your first word!' }),
    typeStep({ say: 'Oops! This box says <b>catt</b>, with one extra T. Press <b>Backspace</b> once to erase the last letter. Backspace is the red key at the <b>top-right</b>, with the arrow ⌫.', hint: 'Backspace is at the top-right of the keyboard. It erases the letter just before the blinking line.', target: 'cat', pre: 'catt', mismatch: 'Press ⌫ Backspace to erase the extra letter.', after: 'Fixed! Backspace is your best friend for mistakes.' }),
    typeStep({ say: 'To make a <b>CAPITAL letter</b>, hold down <b>Shift</b> (the teal ⇧ key), press the letter, then let go of both. Type: <b>Hi</b> (a big H, a small i).', hint: 'Hold Shift with one finger, press H with another, then let go. Then press i by itself.', target: 'Hi', cs: true, mismatch: 'Not quite. For a big H, hold ⇧ Shift while pressing H. Press ⌫ Backspace to erase and try again.', after: 'Perfect capital letter!' }),
    typeStep({ say: 'The <b>Enter</b> key sends a message or starts a new line. Type <b>Good morning</b> and then press <b>Enter</b> to send it.', hint: 'Type the words (use the Spacebar between them), then press the big Enter key on the right.', target: 'Good morning', enter: true, chat: true, after: 'Message sent with Enter!' }),
    typeStep({ say: 'Email addresses use the <b>@</b> sign ("at"). Hold <b>Shift</b> and press <b>2</b> to make @. Type: <b>sam@mail.com</b>', hint: 'For @, hold Shift and press 2 on the top number row. The dot (.) is near the bottom-right, next to the / key.', target: 'sam@mail.com', mismatch: 'Something doesn\'t match. Remember: @ is Shift + 2. Press ⌫ Backspace to fix mistakes.', after: 'You typed an email address. You\'re ready for the Post Office later!' }),
    {
      say: 'A few more helpful keys. When you\'re ready, click <b>Finish chapter</b>.', next: 'Finish chapter ▶',
      build: S => infoCard(S, `<h3>Handy keys</h3><div class="igrid"><div class="ibox"><div class="ie">🔠</div><b>Caps Lock</b><small>Makes EVERY letter a capital until you press it again. If your typing LOOKS LIKE THIS, press Caps Lock!</small></div>
      <div class="ibox"><div class="ie">⌫</div><b>Backspace</b><small>Erases the letter to the LEFT of the blinking line.</small></div><div class="ibox"><div class="ie">⌦</div><b>Delete</b><small>Erases the letter to the RIGHT.</small></div>
      <div class="ibox"><div class="ie">⬅️➡️</div><b>Arrow keys</b><small>Move the blinking line without erasing.</small></div><div class="ibox"><div class="ie">⚔️</div><b>Practice tip</b><small>Battle the Glitches around town to practice finding letters!</small></div></div>`)
    }
  ]
};

/* ---------------- CHAPTER 4: THE OFFICE — windows & files ---------------- */
function desktopSim(S, icons) {
  S.innerHTML = `<div class="stage-inner"><div class="sim-screen" id="dsk">${icons.map(ic => `<div class="dicon" id="ic-${ic.id}" style="left:${ic.x}%;top:${ic.y}%"><span class="ico">${ic.e}</span>${ic.n}</div>`).join('')}
    <div class="taskbar"><button class="startb" style="height:36px;width:40px">⊞</button><div id="tbItems" style="display:flex;gap:6px"></div><div class="tclock">${clockStr()}</div></div></div></div>`;
  return $('#dsk');
}
function openWin(dsk, { id, title, body, tool = '', cls = '' }) {
  const w = document.createElement('div'); w.className = 'win ' + cls; w.id = 'win-' + id;
  w.innerHTML = `<div class="wbar"><span class="wt">${title}</span><button class="wbtn wmin" title="Minimize">&#8211;</button><button class="wbtn wmax" title="Maximize">☐</button><button class="wbtn x wclose" title="Close">✕</button></div>${tool}<div class="wbody">${body}</div>`;
  dsk.appendChild(w); return w;
}
const OFFICE_ICONS = [{ id: 'notes', e: '📝', n: 'Notes', x: 2, y: 3 }, { id: 'docs', e: '📁', n: 'Documents', x: 2, y: 27 }, { id: 'photos', e: '🖼️', n: 'Photos', x: 2, y: 51 }, { id: 'bin', e: '🗑️', n: 'Recycle Bin', x: 88, y: 3 }];
LESSONS.office = {
  setup: (S, ctx) => { ctx.dsk = desktopSim(S, OFFICE_ICONS); },
  steps: [
    { say: 'Welcome to The Office! This is a computer <b>desktop</b>. The pictures are <b>icons</b>, and the bar at the bottom is the <b>taskbar</b>. Programs open inside boxes called <b>windows</b>.', next: true },
    {
      say: 'To open a program, <b>double-click</b> its icon. <b>Double-click the Notes icon 📝</b>.', hint: DBL_HINT,
      build: (S, api, ctx) => {
        const ic = $('#ic-notes'); api.target(ic);
        dbl(api, ic, () => {
          ctx.win = openWin(ctx.dsk, { id: 'notes', title: 'Notes · Untitled', tool: '<div class="wtool"><button id="saveBtn">💾 Save</button><button>✂️ Cut</button><button>📋 Paste</button></div>', body: '<textarea class="notepad" id="note" placeholder="Click here and start typing…"></textarea>' });
          $('#tbItems').innerHTML = '<button class="tbitem active" id="tbNotes">📝 Notes</button>'; api.done('The Notes window opened!');
        }, () => api.soft(DBL_HINT));
      }
    },
    {
      say: 'Click inside the big white area of the window, then type: <b>Buy milk</b>', hint: 'Click in the white page first. A blinking line shows you can type.',
      build: (S, api) => { const n = $('#note'); api.target(n); typeCheck(api, n, 'Buy milk', { after: 1 }); }
    },
    {
      say: 'Let\'s <b>save</b> your note so the computer keeps it. <b>Click the Save button 💾</b> at the top of the window.', hint: 'The Save button has a little blue square (a floppy disk) on it.',
      build: (S, api, ctx) => {
        const b = $('#saveBtn'); api.target(b);
        api.listen(b, 'click', () => {
          ctx.win.insertAdjacentHTML('beforeend', '<div class="dlg" id="saveDlg"><h4>Save as</h4>File name:<input id="fname" autocomplete="off"><div class="row"><button id="cancelSave">Cancel</button><button class="pri" id="doSave">Save</button></div></div>');
          api.done('quiet');
        });
      }
    },
    {
      say: 'Give your file a name. Type <b>shopping</b> in the File name box, then click the blue <b>Save</b> button.', hint: 'Type the word shopping in the white box, then click Save.',
      build: (S, api, ctx) => {
        const inp = $('#fname'), b = $('#doSave'); api.target(inp, b, $('#cancelSave'));
        setTimeout(() => inp.focus(), 100);
        const tryIt = () => { if (inp.value.trim().toLowerCase() === 'shopping') { $('#saveDlg').remove(); $('.wt', ctx.win).textContent = 'Notes · shopping'; ctx.saved = true; api.done('Saved! The window title now shows “shopping”.'); } else api.wrong('First type the name “shopping” in the box, then click Save.'); };
        api.listen(b, 'click', tryIt); api.listen(inp, 'keydown', e => { if (e.key === 'Enter') { e.preventDefault(); tryIt(); } });
        api.listen($('#cancelSave'), 'click', () => api.wrong('Cancel would close without saving. Please click Save.'));
      }
    },
    {
      say: 'Every window has 3 buttons in its <b>top-right corner</b>. Click the <b>Minimize</b> button, the little line <b>–</b>. It hides the window without closing it.', hint: 'Minimize is the first of the three buttons in the top-right corner of the window.',
      build: (S, api, ctx) => { const b = $('.wmin', ctx.win); api.target(b); api.listen(b, 'click', () => { ctx.win.classList.add('min'); $('#tbNotes').classList.remove('active'); api.done('The window is hiding, not closed!'); }); }
    },
    {
      say: 'Your window is resting in the <b>taskbar</b> at the bottom. <b>Click “Notes” in the taskbar</b> to bring it back.', hint: 'Look at the dark bar at the bottom of the screen for the Notes button.',
      build: (S, api, ctx) => { const b = $('#tbNotes'); api.target(b); api.listen(b, 'click', () => { ctx.win.classList.remove('min'); b.classList.add('active'); api.done('It\'s back!'); }); }
    },
    {
      say: 'Now click <b>Maximize ☐</b> (the middle button) to make the window fill the whole screen.', hint: 'Maximize is the square button between – and ✕.',
      build: (S, api, ctx) => { const b = $('.wmax', ctx.win); api.target(b); api.listen(b, 'click', () => { ctx.win.classList.add('max'); api.done('Now it fills the screen.'); }); }
    },
    {
      say: 'Finished with a program? Click the <b>✕</b> in the top-right corner to <b>close</b> it. Don\'t worry, your note is saved!', hint: 'The ✕ is the very last button in the top-right corner. It turns red when you point at it.',
      build: (S, api, ctx) => { const b = $('.wclose', ctx.win); api.target(b); api.listen(b, 'click', () => { ctx.win.remove(); $('#tbItems').innerHTML = ''; api.done('Closed!'); }); }
    },
    {
      say: 'Files are kept in <b>folders</b>, like paper in a filing cabinet. <b>Double-click the Documents folder 📁</b>.', hint: DBL_HINT,
      build: (S, api, ctx) => {
        const ic = $('#ic-docs'); api.target(ic);
        dbl(api, ic, () => {
          ctx.fw = openWin(ctx.dsk, { id: 'docs', title: '📁 Documents', body: '<div class="files"><div class="file" id="fShop"><span class="ico">📄</span>shopping</div><div class="file" id="fOld"><span class="ico">📄</span>old list</div><div class="file"><span class="ico">📄</span>recipes</div></div>' });
          ctx.fw.style.left = '28%'; ctx.fw.style.top = '8%'; ctx.fw.style.width = '52%'; ctx.fw.style.height = '62%';
          api.done('There\'s your “shopping” file, saved safely!');
        }, () => api.soft(DBL_HINT));
      }
    },
    {
      say: 'Let\'s throw away “old list”. <b>Drag</b> it onto the <b>Recycle Bin 🗑️</b> (top-right of the desktop): press and hold on the file, slide it to the bin, then let go.', hint: 'Press and hold on “old list”, keep holding while you move to the Recycle Bin, then let go.',
      build: (S, api) => {
        const f = $('#fOld'), bin = $('#ic-bin'); bin.classList.add('bin'); api.target(f);
        dragger(api, f, [bin], () => { f.remove(); $('.ico', bin).textContent = '🗑️'; api.done('Deleted! If you change your mind, you can open the Recycle Bin and restore it.'); },
          m => m < 8 ? api.soft('Press and HOLD the button, then slide to the Recycle Bin.') : api.wrong('Almost! Let go only when the file is right over the Recycle Bin.'));
      }
    },
    {
      say: 'On an <b>Apple Mac</b>, windows work the same way, but the buttons are colored circles in the <b>top-LEFT</b> corner: 🔴 close, 🟡 minimize, 🟢 full screen. The taskbar is called the <b>Dock</b>.', next: 'Finish chapter ▶',
      build: S => infoCard(S, `<h3>Windows PC vs Mac</h3><div class="macwin"><div class="macbar"><i style="background:#ff5f57"></i><i style="background:#febc2e"></i><i style="background:#28c840"></i><span style="margin-left:8px">Notes</span></div><div style="padding:14px;height:70px">Buy milk</div></div>
      <table class="cmp" style="margin-top:12px"><tr><th></th><th>Windows PC</th><th>Mac</th></tr><tr><td>Close / minimize buttons</td><td>Top-right: – ☐ ✕</td><td>Top-left: 🔴 🟡 🟢</td></tr><tr><td>Bottom bar</td><td>Taskbar</td><td>Dock</td></tr><tr><td>Files</td><td>File Explorer</td><td>Finder</td></tr><tr><td>Deleted files</td><td>Recycle Bin</td><td>Trash</td></tr></table>`)
    }
  ]
};
