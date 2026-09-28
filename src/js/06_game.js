/* =====================================================================
   CHAPTER 9: CITY HALL — final review & certificate
   ===================================================================== */
const FINAL_QS = [
  { q: 'Which symbol is on the <b>power button</b>?', emoji: '💻', opts: ['⏻ a circle with a line', '✉ an envelope', '⌫ an arrow', '🔍 a magnifying glass'], correct: 0 },
  { q: 'To open an icon on the desktop, you usually…', emoji: '🖱️', opts: ['Right-click it', 'Double-click it', 'Scroll the wheel', 'Press Spacebar'], correct: 1, why: { 0: 'Right-click opens a menu of extra choices. To OPEN, double-click.' } },
  { q: 'Which key erases the letter <b>before</b> the blinking line?', emoji: '⌨️', opts: ['Enter', 'Shift', 'Backspace ⌫', 'Spacebar'], correct: 2 },
  { q: 'How do you type a <b>CAPITAL</b> letter?', emoji: '🔠', opts: ['Press Enter twice', 'Hold Shift and press the letter', 'Press the Spacebar', 'Hold Backspace'], correct: 1 },
  { q: 'Where do you type a <b>website address</b>?', emoji: '🌐', opts: ['The taskbar', 'The Recycle Bin', 'The address bar at the top of the browser', 'The desktop'], correct: 2 },
  { q: 'A pop-up says <b>“VIRUS! Call this number now!”</b> What should you do?', emoji: '⚠️', opts: ['Call the number quickly', 'Close the tab or browser, and never call', 'Type your password', 'Pay to fix it'], correct: 1, why: { 0: 'Never call! Real companies don\'t warn you like this. It\'s a scam.', 2: 'Never give your password to a pop-up. It\'s a scam.', 3: 'Never pay! It\'s a scam. Just close the tab.' } },
  { q: 'Which one is an <b>email address</b>?', emoji: '📧', opts: ['maya@townmail.com', 'www.townmail.com', 'Maya Townmail', '555-0140'], correct: 0 },
  { q: 'An email says your bank account is locked and asks for your <b>password</b>. You should…', emoji: '🎣', opts: ['Reply with your password', 'Click the link quickly', 'Report it as spam and call your bank using a number you trust', 'Forward it to friends'], correct: 2 },
  { q: 'On an <b>Android</b> phone, the round <b>●</b> button at the bottom takes you…', emoji: '🤖', opts: ['To the Home screen', 'To the camera', 'To turn the phone off', 'To make a call'], correct: 0 },
  { q: 'On a newer <b>iPhone</b>, how do you go to the Home Screen?', emoji: '📱', opts: ['Shake the phone', 'Press volume down', 'Swipe up from the bottom', 'Tap the clock'], correct: 2 },
  { q: 'Which makes a <b>strong password</b>?', emoji: '🔑', opts: ['Your pet\'s name', '123456', 'Three random words plus a number and a symbol', 'Your birthday'], correct: 2 },
  { q: 'A website pop-up says your video player is <b>out of date</b>. You should…', emoji: '🦠', opts: ['Click “Update now”', 'Close it: real updates come from your computer\'s Settings', 'Call the phone number shown', 'Type your password'], correct: 1 },
  { q: 'A stranger emails you <b>invoice.pdf.exe</b>. It is…', emoji: '📎', opts: ['A safe PDF document', 'Probably malware: don\'t open it', 'A photo', 'A song'], correct: 1 },
];
function certHTML() {
  const lv = levelOf(S.xp);
  return `<div class="cert" id="certCard"><div class="cseal">🏆</div><div style="letter-spacing:3px;font-weight:700">CERTIFICATE OF ACHIEVEMENT</div><h1>Computer Quest: Tech Town</h1><div>This certifies that</div><div class="cname">${esc(S_name())}</div>
    <div>has learned to use a computer, the internet, email, Android phones and iPhones, and to stay safe online,<br>and has earned the title of <b>${LEVELS[lv][1]}</b> (Level ${lv + 1}, ${S.xp} XP).</div>
    <div class="cbadges" style="margin:.4em 0">${CHAPTERS.filter((c, i) => isDone(i) || c.id === 'final').map(c => c.badgeIcon).join(' ')}</div>
    <div style="display:flex;justify-content:space-between;margin-top:1em;font-size:.9em"><span>Date: ${new Date().toLocaleDateString()}</span><span>Signed: Maya, Tech Guide ✒️</span></div></div>`;
}
LESSONS.final = {
  steps: [
    { say: `Welcome to City Hall, {name}! This is your <b>final review</b>: ${FINAL_QS.length} questions about everything you learned. There's no rush and no failing. If you pick a wrong answer, just try another one!`, next: 'Start the review ▶', build: S => infoCard(S, `<div style="text-align:center"><div style="font-size:4em">🏛️</div><h3>Final Review</h3><p>${FINAL_QS.length} questions · unlimited tries · a certificate at the end!</p></div>`) },
    ...FINAL_QS.map((f, i) => ({ say: `Question ${i + 1} of ${FINAL_QS.length}`, build: (S, api) => quiz(S, api, f) })),
    { say: 'You did it, {name}! Here is your certificate. Click <b>Finish</b> to become a Tech Hero!', next: 'Finish ▶', build: S => { S.innerHTML = `<div class="stage-inner">${certHTML()}<div class="cert-actions"><button class="btn gold" id="certPrint">📜 ${window.CQ_ARTIFACT ? 'Show' : 'Print'} certificate</button></div></div>`; $('#certPrint').onclick = () => Game.printCertificate(); } },
  ]
};

/* =====================================================================
   GAME FLOW
   ===================================================================== */
const SHIRTS = ['#2d7ff9', '#e5484d', '#23a55a', '#f5b700', '#7b2ff7', '#ff7aa2', '#00a3a3', '#1f2d3d'];
const HAIRS = ['#1b1b1b', '#3b2416', '#8a5a2b', '#d9a066', '#f2d16b', '#b0b0b0', '#ffffff', '#c0392b'];
const SKINS = ['#f3d1b8', '#e8b48a', '#d9a27f', '#c68642', '#a86b45', '#8d5524', '#5c3a21'];
const NPCS = [
  { name: 'Rosa', long: true, shirt: '#e84a5f', hair: '#1b1b1b', skin: '#e8b48a', tip: 'Did you know? On most websites you can make the words bigger by holding <b>Ctrl</b> and pressing <b>+</b>.' },
  { name: 'Ken', shirt: '#23a55a', hair: '#8a5a2b', skin: '#8d5524', tip: 'My grandson taught me: if something freezes, try <b>turning it off and on again</b>. It fixes almost everything!' },
  { name: 'Priya', long: true, shirt: '#f5b700', hair: '#2b1a12', skin: '#c68642', tip: 'Never share your <b>passwords</b>, not even with someone who says they\'re from the bank.' },
  { name: 'Leo', shirt: '#00a3a3', hair: '#2b1a12', skin: '#c68642', tip: 'Watch out for <b>Glitches</b>! Type the letter that pops up above them to zap them!' },
  { name: 'June', bun: true, shirt: '#ff7aa2', hair: '#b0b0b0', skin: '#f1c27d', tip: 'I <b>video-call</b> my family every Sunday on my phone. You\'ll get there too!' },
];
const rowOf = c => 'qwertyuiop'.includes(c) ? 'top row of letters' : 'asdfghjkl'.includes(c) ? 'middle row of letters' : 'bottom row of letters';

function typewrite(el) {
  const walk = n => {
    [...n.childNodes].forEach(c => {
      if (c.nodeType === 3) {
        const frag = document.createDocumentFragment();
        c.textContent.split(/(\s+)/).forEach(w => { if (!w) return; if (/^\s+$/.test(w)) frag.appendChild(document.createTextNode(w)); else { const s = document.createElement('span'); s.className = 'tw'; s.textContent = w; frag.appendChild(s); } });
        c.replaceWith(frag);
      } else if (c.nodeType === 1 && !c.classList.contains('key')) walk(c);
      else if (c.nodeType === 1) c.classList.add('tw');
    });
  };
  walk(el); $$('.tw', el).forEach((s, i) => s.style.animationDelay = (i * 45) + 'ms');
  el.onclick = () => $$('.tw', el).forEach(s => s.style.animationDelay = '0ms');
}

const Game = {
  hasWorld: false,
  boot() {
    Voice.init(); L.init(); applySettings(); updateHUD();
    addEventListener('resize', applySettings);
    $('#btnContinue').classList.toggle('hidden', !S.name);
    $('#btnNew').onclick = () => { Sfx.play('click'); if (S.name) Modal.confirm('Start over? This erases your saved progress.', 'Yes, start over', () => this.newGame()); else this.newGame(); };
    $('#btnContinue').onclick = () => { Sfx.play('click'); this.enterTown(false); };
    $('#btnTitleSettings').onclick = () => this.settings();
    $('#hbMissions').onclick = () => Missions.open(); $('#hbMap').onclick = () => this.map(); $('#hbBadges').onclick = () => this.badges(); $('#hbSettings').onclick = () => this.settings(); $('#hbVoice').onclick = toggleVoice;
    const ok = this.initWorld();
    if (!ok) this.hasWorld = false;
    setTimeout(() => Voice.speak('Welcome to Computer Quest! Click Start a New Adventure to begin.'), 400);
  },
  initWorld() {
    if (typeof TOWN_GLB === 'undefined' || !window.THREE || !THREE.GLTFLoader) return false;
    World.init(TOWN_GLB, ok => {
      this.hasWorld = ok;
      if (!ok) { $('#scene').innerHTML = ''; document.body.style.background = 'linear-gradient(#9ed8ff,#d8f0c8)'; return; }
      World.onNear = n => this.near(n); World.onAction = n => this.action(n);
      World.onBattle = (ev, en) => this.battleEvent(ev, en); World.onLetter = k => this.letter(k); World.onCmdKey = k => this.cmdKey(k);
    });
    return true;
  },
  newGame() {
    const st = S.settings; S = freshState(st); save();
    $('#title').classList.remove('on'); $('#create').classList.add('on');
    const mk = (id, list, key) => { const w = $(id); w.innerHTML = list.map(c => `<button class="sw ${S[key] === c ? 'sel' : ''}" style="background:${c}" data-c="${c}" aria-label="color"></button>`).join(''); $$('.sw', w).forEach(b => b.onclick = () => { S[key] = b.dataset.c; $$('.sw', w).forEach(x => x.classList.toggle('sel', x === b)); this.preview(); Sfx.play('click'); }); };
    mk('#shirtSw', SHIRTS, 'shirt'); mk('#hairSw', HAIRS, 'hair'); mk('#skinSw', SKINS, 'skin');
    S.hairStyle = S.hairStyle || 'short';
    $('#styleSw').innerHTML = [['short', 'Short hair'], ['long', 'Long hair']].map(([v, l]) => `<button class="btn ghost ${S.hairStyle === v ? 'sel' : ''}" data-v="${v}" style="font-size:.85em">${l}</button>`).join(' ');
    $$('#styleSw button').forEach(b => b.onclick = () => { S.hairStyle = b.dataset.v; $$('#styleSw button').forEach(x => x.classList.toggle('sel', x === b)); this.preview(); Sfx.play('click'); });
    this.preview();
    const inp = $('#nameIn'); inp.value = ''; setTimeout(() => inp.focus(), 150);
    inp.onkeydown = e => { if (e.key === 'Enter') { e.preventDefault(); $('#btnCreateDone').click(); } };
    $('#btnCreateDone').onclick = () => {
      const n = inp.value.trim();
      if (!n) { inp.focus(); inp.classList.add('shake'); setTimeout(() => inp.classList.remove('shake'), 400); Voice.speak('Please type your name in the white box first. Click inside the box, then press the letter keys.'); return; }
      S.name = n.slice(0, 18); save(); Sfx.play('good'); $('#create').classList.remove('on'); this.enterTown(true);
    };
    Voice.speak('Create your character! First, click in the white box and type your name. Then pick your colors and press Let\'s go.');
  },
  preview() { $('#avPreview').innerHTML = avatarSVG(S.shirt, S.hair, S.skin, S.hairStyle); },
  enterTown(isNew) {
    $('#title').classList.remove('on'); $('#hud').classList.add('on'); updateHUD();
    if (!this.hasWorld) { $('#movepad').style.display = 'none'; $('#objective').style.display = 'none'; this.map(true); return; }
    if (isNew) S.pos = null;
    World.recolorPlayer(); World.player.root.position.set(S.pos ? S.pos.x : -2, 0, S.pos ? S.pos.z : 8.5); World.player.root.rotation.y = Math.PI;
    World.mode = 'play'; World.pause(false); World.snapCamera(); World.refreshMarkers(); Missions.check(true); Missions.renderTracker();
    if (!S.tutorial) this.tutorial();
    else { const ni = nextIndex(); Dialog.show(ni < 0 ? 'Welcome back, {name}! You\'ve finished every chapter. Visit any building to practice, or battle Glitches to sharpen your typing!' : `Welcome back, {name}! Your next stop is <b>${CHAPTERS[ni].icon} ${CHAPTERS[ni].name}</b>. Follow the golden light!`, [{ label: 'Let\'s go! ▶' }]); }
  },

  /* ---- movement tutorial ---- */
  tutorial() {
    const keysHTML = cur => `<div class="arrowkeys">${['up', 'left', 'down', 'right'].map(d => `${d === 'left' ? '<br>' : ''}<span class="key ${cur === d ? 'hl' : ''} ${this.tdone && this.tdone[d] ? 'ok' : ''}">${{ up: '↑', down: '↓', left: '←', right: '→' }[d]}</span>`).join('')}</div>`;
    Dialog.show('Hi {name}! I\'m <b>Maya</b>, your guide in Tech Town. Each building here teaches a new computer skill. You can\'t break anything, so relax and have fun! And watch out for little <b>Glitches</b>. We\'ll deal with them later!', [{
      label: 'Next ▶', fn: () => {
        this.tdone = {}; const order = ['up', 'left', 'right', 'down']; let k = 0, held = 0;
        const names = { up: 'UP arrow ↑', down: 'DOWN arrow ↓', left: 'LEFT arrow ←', right: 'RIGHT arrow →' };
        const show = () => Dialog.show(k === 0 ? 'Let\'s learn to walk! Find the <b>arrow keys</b> on your keyboard: four keys with arrows, at the bottom right. <b>Press and hold the UP arrow ↑</b> to walk forward. (No keyboard? Press and hold the big arrow buttons on the screen.)' : `Great! Now press and hold the <b>${names[order[k]]}</b>.`, [], keysHTML(order[k]));
        show();
        clearInterval(this.tInt);
        this.tInt = setInterval(() => {
          if (!World.held) return; const d = order[k];
          if (World.held(d)) held += .1; if (held >= .6) { this.tdone[d] = true; Sfx.play('good'); k++; held = 0; if (k >= order.length) { clearInterval(this.tInt); this.tutorialEnd(); } else show(); }
        }, 100);
      }
    }]);
  },
  tutorialEnd() {
    Dialog.show('Perfect walking! (You can also walk with the <b>W A S D</b> keys.) Now here\'s the secret of Tech Town: you <b>TYPE what you want to do!</b> Walk back next to me and type the word <b>TALK</b>, one letter at a time. The box at the top of the screen will help you.', [{ label: 'OK! ▶' }]);
  },

  /* ---- proximity & typed commands ---- */
  near(n) {
    let box = $('#cmdBox'); if (!box) { box = document.createElement('div'); box.id = 'cmdBox'; document.body.appendChild(box); }
    $('#actionBtn').classList.remove('on'); $('#nearLabel').classList.remove('on');
    this.typed = '';
    document.body.classList.toggle('cmd-on', !!n);
    if (!n) { box.classList.remove('on'); return; }
    this.renderCmd(n);
  },
  cmdInfo(n) {
    const ch = n.type === 'door' ? CHAPTERS[n.i] : null;
    const npc = n.type === 'npc' ? NPCS[n.i] : null;
    return {
      door: () => ({ title: `${ch.icon} Chapter ${ch.n}: ${ch.name}${isDone(n.i) ? ' ✅' : !isUnlocked(n.i) ? ' 🔒' : ''}`, what: isUnlocked(n.i) ? (isDone(n.i) ? 'to practice again' : 'to go inside') : 'to knock on the door' }),
      maya: () => ({ title: '💬 Maya, your Tech Guide', what: 'to talk to Maya' }),
      npc: () => ({ title: `💬 ${npc.name}`, what: `to talk to ${npc.name}` }),
      gem: () => ({ title: '💎 A shiny Data Gem!', what: 'to pick it up' }),
      ball: () => ({ title: '🏀 A basketball', what: 'to pick it up' }),
      hoop: () => ({ title: '🏀 Basketball hoop', what: n.cmd ? 'to shoot the ball' : 'Find the basketball on the grass nearby and type PICKUP first!' }),
      bench: () => ({ title: '🪑 Park bench', what: n.cmd ? 'to sit down and rest' : 'Relaxing… Walk with the arrow keys to stand up.' }),
      fountain: () => ({ title: '⛲ Wishing fountain', what: 'to toss in a coin and make a wish' }),
      phone: () => ({ title: '📱 Ken\'s lost phone!', what: 'to pick it up' }),
      mailbox: () => ({ title: '📮 Blue mailbox', what: S.carry === 'letter' ? 'to mail Rosa\'s letter' : 'to look at the mailbox' }),
    }[n.type]();
  },
  renderCmd(n, msg = '') {
    const box = $('#cmdBox'); const info = this.cmdInfo(n); const w = n.cmd;
    if (!w) { box.innerHTML = `<div class="cb-title">${info.title}</div><div class="cb-hint">${info.what}</div>`; box.classList.add('on'); return; }
    const t = this.typed, nextCh = w[t.length];
    box.innerHTML = `<div class="cb-title">${info.title}</div>
      <div class="cb-row"><span>Type</span><span class="cb-word">${[...w].map((c, i) => `<button class="cb-k ${i < t.length ? 'ok' : i === t.length ? 'nx' : ''}" data-i="${i}">${c.toUpperCase()}</button>`).join('')}</span><span>${info.what}</span></div>
      <div class="cb-hint">${msg || `Next letter: <b>${nextCh.toUpperCase()}</b> (in the ${rowOf(nextCh)})`}</div>
      <div class="cb-foot">Made a mistake? Just type the right letter. · Walk away with the arrow keys ← ↑ → ↓</div>`;
    box.classList.add('on');
    $$('.cb-k', box).forEach(b => b.onclick = () => { if (+b.dataset.i === this.typed.length) this.cmdKey(w[this.typed.length]); });
  },
  cmdKey(k) {
    const n = World.near; if (!n || !n.cmd) return; const w = n.cmd;
    if (k === 'Backspace') { this.typed = this.typed.slice(0, -1); return this.renderCmd(n); }
    if (k === 'Enter') { Sfx.play('bad'); return this.renderCmd(n, `Type the whole word <b>${w.toUpperCase()}</b>, one letter at a time, using the letter keys.`); }
    const want = w[this.typed.length];
    if (k === want) {
      this.typed += k; Sfx.play('click');
      if (this.typed === w) { this.typed = ''; Sfx.play('good'); $('#cmdBox').classList.remove('on'); this.action(n); if (World.near === n) setTimeout(() => { if (World.near === n && !$('#lesson').classList.contains('on')) this.renderCmd(World.near); }, 700); }
      else this.renderCmd(n);
    } else {
      Sfx.play('bad'); const m = `That was <b>${esc(k.toUpperCase())}</b>. Now find <b>${want.toUpperCase()}</b>, in the ${rowOf(want)}.`;
      this.renderCmd(n, m); Voice.speak(Voice.clean(m));
    }
  },
  action(n) {
    if (!n || World.battle) return;
    const P = World.player.root.position;
    switch (n.type) {
      case 'maya': return this.talkMaya();
      case 'npc': {
        const npc = NPCS[n.i], who = World.npcs[n.i]; who.talking = true; const stt = mstats();
        if (!stt.talked.includes(npc.name)) stt.talked.push(npc.name);
        let text = `Hi {name}! I'm <b>${npc.name}</b>. ${npc.tip}`;
        if (npc.name === 'Ken' && Missions.active('lostphone')) {
          if (S.carry === 'phone') { S.carry = null; stt.phoneReturned = true; text = 'My phone! You found it! Thank you so much, {name}! Now I can call my grandkids again. 📱'; }
          else { stt.phoneAsked = true; text = 'Oh dear, {name}, I lost my <b>phone</b> somewhere in the park, near the trees on the <b>left side</b>. Could you find it for me? When you see it, type <b>PICKUP</b>!'; }
        }
        if (npc.name === 'Rosa' && Missions.active('delivery') && !stt.delivered) {
          if (S.carry !== 'letter') { S.carry = 'letter'; text = 'Could you do me a favor, {name}? Please mail this <b>letter</b> for me. Walk to the <b>blue mailbox</b> outside the Post Office and type <b>USE</b>. Thank you, dear!'; }
          else text = 'Thank you! The blue mailbox is right outside the Post Office.';
        }
        save(); Missions.check();
        return Dialog.show(text, [{ label: 'Thanks! ▶', fn: () => { who.talking = false; } }], '', { name: npc.name, portrait: `<div style="height:100%;display:grid;place-items:end center;background:#dff1ff">${avatarSVG(npc.shirt, npc.hair, npc.skin)}</div>` });
      }
      case 'gem': {
        const count = World.collectGem(n.g); addXP(2); save(); toast(`💎 Data Gem ${count} of 12!`); Missions.check();
        if (count === 12) { setTimeout(() => { addXP(20); confetti(80); Dialog.show('WOW, {name}! You found all <b>12 Data Gems</b>! You\'re a true explorer. That\'s a 20 XP bonus!', [{ label: 'Yay! ▶' }]); }, 600); }
        else if (count === 1) Dialog.show('You picked up a <b>Data Gem</b>! 💎 There are <b>12</b> hidden around Tech Town. Type <b>PICKUP</b> whenever you find one.', [{ label: 'OK ▶' }]);
        updateHUD(); return;
      }
      case 'ball': World.pickBall(); toast('🏀 You picked up the ball!'); Voice.speak('You picked up the ball! Walk to the hoop and type THROW.'); return;
      case 'hoop': {
        const score = World.throwBall();
        setTimeout(() => { if (score) { Sfx.play('level'); confetti(40); toast('🏀 SWISH! Nice shot!'); mstats().hoops++; save(); Missions.check(); if (!S.hoop) { S.hoop = true; addXP(5); } } else { toast('So close! Pick up the ball and get nearer to the hoop.'); } }, 900);
        return;
      }
      case 'bench': World.sitOn(n.b); toast('🪑 Ahh, a nice rest.'); mstats().sits++; save(); Missions.check(); Voice.speak('Ahh, a nice rest. Use the arrow keys to stand up.'); return;
      case 'fountain': {
        World.spark({ x: 0, z: 14 }, 2); Sfx.play('level');
        const wishes = ['May all your passwords be strong!', 'May your Wi-Fi always be fast!', 'May your inbox be free of spam!', 'May your batteries stay charged!'];
        toast('🪙 You made a wish: ' + pick(wishes)); mstats().wishes++; save(); Missions.check(); if (!S.wish) { S.wish = true; addXP(2); } return;
      }
      case 'phone': { S.carry = 'phone'; mstats().phoneFound = true; save(); World.refreshMissionItems(); Missions.renderTracker(); toast('📱 You found Ken\'s phone!'); Dialog.show('You found Ken\'s phone! 📱 Now walk back to <b>Ken</b> and type <b>TALK</b> to give it back.', [{ label: 'OK ▶' }]); return; }
      case 'mailbox': {
        if (S.carry !== 'letter') { Dialog.show('This is the <b>blue mailbox</b>. Rosa might need it for a letter someday!', [{ label: 'OK ▶' }]); return; }
        S.carry = null; mstats().delivered = true; save(); World.spark(World.mailboxPos, 1.6); Sfx.play('level'); toast('✉️ Letter mailed! Rosa will be so happy.'); Missions.check(); return;
      }
    }
    // door
    if (!isUnlocked(n.i)) { const ni = nextIndex(); Dialog.show(`Knock knock! The <b>${CHAPTERS[n.i].name}</b> opens after you finish <b>${CHAPTERS[n.i - 1].name}</b>. Follow the golden light to <b>${CHAPTERS[ni].name}</b>!`, [{ label: 'OK ▶' }]); return; }
    Sfx.play('door'); Dialog.hide(); $('#cmdBox').classList.remove('on');
    void P; L.start(n.i);
  },
  talkMaya() {
    if (!S.tutorial) {
      S.tutorial = true; save(); confetti(40); setTimeout(() => Missions.check(), 400);
      return Dialog.show('You did it! 🎉 That\'s how everything works in Tech Town: you <b>type what you want to do</b>. Type <b>ENTER</b> at a door to go inside, <b>TALK</b> to chat with people, <b>PICKUP</b> to grab things, <b>USE</b> to use things like benches, and <b>THROW</b> to throw. Now follow the golden arrow to your <b>Home</b> and type ENTER at the door!', [{ label: 'Let\'s go! ▶' }]);
    }
    const ni = nextIndex();
    const tips = ['Tip: battle the <b>Glitches</b> around town. Press the letter that pops up in front of them to practice typing!', 'Tip: there are <b>12 Data Gems</b> hidden in town. Type PICKUP to grab them!', 'Tip: there\'s a basketball in the park. PICKUP the ball, walk to the hoop and type THROW!', 'Tip: you can make the text bigger in ⚙️ <b>Settings</b>.', 'Tip: the 🗺️ <b>Map</b> can take you straight to any building you\'ve unlocked.'];
    Dialog.show(ni < 0 ? 'You did it all, {name}! You\'re a real Tech Hero. Come back any time to practice.' : `Hi {name}! Your next stop is <b>${CHAPTERS[ni].icon} ${CHAPTERS[ni].name}</b>, where you'll learn <b>${CHAPTERS[ni].skill.toLowerCase()}</b>. ${pick(tips)}`, [{ label: '🗺️ Map', cls: 'ghost', fn: () => this.map() }, { label: 'Thanks! ▶' }]);
  },
  afterChapter(ci) {
    const ch = CHAPTERS[ci]; const nxt = CHAPTERS[nextIndex()];
    setTimeout(() => Missions.check(), 3400);
    if (!this.hasWorld) return this.map(true);
    const b = document.createElement('div'); b.id = 'itemBanner'; b.innerHTML = `<div class="ib-icon">${ch.badgeIcon}</div><div>You got the <b>${ch.badge}</b> badge!<small>${ch.skill}</small></div>`; document.body.appendChild(b);
    Sfx.play('done'); confetti(60);
    World.itemGet(ch.badgeIcon, () => {
      b.remove();
      if (ch.id === 'final') Dialog.show('Congratulations, {name}! You are officially a <b>Tech Hero</b>! 🏆 Open 🏅 Badges any time to print your certificate.', [{ label: '📜 Certificate', cls: 'gold', fn: () => this.printCertificate() }, { label: 'Yay! ▶' }]);
      else Dialog.show(`Fantastic, {name}! Your next adventure is <b>${nxt.icon} ${nxt.name}</b>. Follow the golden light!`, [{ label: 'Let\'s go! ▶' }]);
    });
  },

  /* ---- typing battles ---- */
  battleEvent(ev, en) {
    let p = $('#battle');
    if (!p) { p = document.createElement('div'); p.id = 'battle'; document.body.appendChild(p); }
    if (ev === 'start') {
      Sfx.play('bad');
      if (!S.metGlitch) { S.metGlitch = true; save(); }
      this.renderBattle(en, `⚔️ A <b>Glitch</b> appeared! Press the letter to attack!`);
      Voice.speak(`A Glitch! Press the letter ${en.letter.toUpperCase()} to attack.`);
    } else if (ev === 'won') {
      S.bugs = (S.bugs || 0) + 1; addXP(3); Sfx.play('level'); Missions.check();
      p.innerHTML = `<div class="bt-title">✨ Glitch squashed! ✨</div><div class="bt-msg">+3 XP · Glitches squashed: ${S.bugs}</div>`; p.classList.add('on');
      Voice.speak('Glitch squashed! Great typing!'); clearTimeout(this.bt); this.bt = setTimeout(() => p.classList.remove('on'), 2200);
    } else if (ev === 'fled') { p.classList.remove('on'); }
  },
  renderBattle(en, title, msg = '') {
    const p = $('#battle'); const c = en.letter; const L2 = 'abcdefghijklmnopqrstuvwxyz'.split('').filter(x => x !== c).sort(() => Math.random() - .5).slice(0, 2);
    const opts = [c, ...L2].sort(() => Math.random() - .5);
    p.innerHTML = `<div class="bt-title">${title}</div><div class="bt-row"><span>Press</span><span class="bigkey">${c.toUpperCase()}</span><span>to attack!</span></div>
      <div class="bt-hp">Glitch energy: ${'<i class="on"></i>'.repeat(en.hp)}${'<i></i>'.repeat(3 - en.hp)}</div>
      <div class="bt-msg">${msg || `Hint: ${c.toUpperCase()} is in the <b>${rowOf(c)}</b>.`}</div>
      <div class="bt-taps"><small>No keyboard? Tap:</small>${opts.map(o => `<button class="btn ghost" data-l="${o}">${o.toUpperCase()}</button>`).join('')}<button class="btn ghost" id="btRun">🏃 Walk away</button></div>`;
    p.classList.add('on');
    $$('[data-l]', p).forEach(b => b.onclick = () => this.letter(b.dataset.l));
    $('#btRun').onclick = () => { World.endBattle('fled'); };
  },
  letter(k) {
    const r = World.attack(k); if (!r) return;
    const en = World.battle;
    if (r.ok) { Sfx.play('good'); if (!r.won && en) { this.renderBattle(en, `💥 Hit! Keep going!`); Voice.speak(`Hit! Now press ${en.letter.toUpperCase()}.`); } }
    else if (en) { Sfx.play('bad'); this.renderBattle(en, '⚔️ Almost!', `That was <b>${esc(k.toUpperCase())}</b>. Find <b>${r.want.toUpperCase()}</b>, in the <b>${rowOf(r.want)}</b>.`); Voice.speak(`That was ${k.toUpperCase()}. Find the letter ${r.want.toUpperCase()}.`); }
  },

  /* ---- modals ---- */
  map(hub = false) {
    const rows = CHAPTERS.map((c, i) => {
      const st = isDone(i) ? '✅ Done' : isUnlocked(i) ? '⭐ Next up!' : '🔒 Locked';
      const btn = !isUnlocked(i) ? '' : this.hasWorld ? `<button class="btn ${isDone(i) ? 'ghost' : 'gold'}" data-go="${i}">Walk there</button>` : `<button class="btn ${isDone(i) ? 'ghost' : 'gold'}" data-enter="${i}">${isDone(i) ? 'Practice' : 'Enter'}</button>`;
      return `<div class="mrow ${isUnlocked(i) ? '' : 'locked'}"><div class="mi">${c.icon}</div><div class="mt"><b>Chapter ${c.n}: ${c.name}</b><small>${c.skill} · ${st}</small></div>${btn}</div>`;
    }).join('');
    Modal.show(`<h2>🗺️ Tech Town Map</h2>${hub && !this.hasWorld ? '<p>Choose a building to enter. (The 3D town needs a device that supports 3D graphics.)</p>' : ''}<div class="maplist">${rows}</div>`, { closable: !hub || this.hasWorld });
    $$('[data-go]').forEach(b => b.onclick = () => { Modal.hide(); World.teleportTo(+b.dataset.go); });
    $$('[data-enter]').forEach(b => b.onclick = () => { Modal.hide(); L.start(+b.dataset.enter); });
  },
  badges() {
    const lv = levelOf(S.xp);
    Modal.show(`<h2>🏅 Your Badges</h2><p><b>${esc(S_name())}</b> · Level ${lv + 1}: ${LEVELS[lv][1]} · ${S.xp} XP · ⚔️ Glitches squashed: ${S.bugs || 0}</p>
      <div class="badges">${CHAPTERS.map((c, i) => `<div class="badge ${isDone(i) ? 'got' : ''}"><div class="bi">${c.badgeIcon}</div><div class="bn">${c.badge}</div><div class="bs">${isDone(i) ? c.skill : 'Not earned yet'}</div></div>`).join('')}</div>
      ${isDone(CHAPTERS.length - 1) ? '<p style="text-align:center;margin-top:1em"><button class="btn gold big" id="bCert">📜 View & print certificate</button></p>' : `<p style="margin-top:1em;color:var(--muted)">Finish all ${CHAPTERS.length} chapters to earn your printable certificate!</p>`}`);
    const b = $('#bCert'); if (b) b.onclick = () => this.printCertificate();
  },
  settings() {
    const st = S.settings;
    const seg = (key, opts) => `<div class="seg" data-key="${key}">${opts.map(([v, l]) => `<button data-v="${v}" class="${String(st[key]) === String(v) ? 'sel' : ''}">${l}</button>`).join('')}</div>`;
    Modal.show(`<h2>⚙️ Settings</h2>
      <div class="setrow"><b>Text size</b>${seg('textSize', [['normal', 'A'], ['large', 'A+'], ['xl', 'A++']])}</div>
      <div class="setrow"><b>Read instructions aloud</b>${seg('voice', [[true, '🔊 On'], [false, '🔇 Off']])}</div>
      <div class="setrow"><b>Voice speed</b>${seg('voiceRate', [['slow', '🐢 Slow'], ['normal', 'Normal'], ['fast', '🐇 Fast']])}</div>
      <div class="setrow"><b>Sound effects</b>${seg('sfx', [[true, 'On'], [false, 'Off']])}</div>
      <div class="setrow"><b>Graphics</b>${seg('graphics', [['smooth', '✨ Smooth'], ['retro', '👾 Retro PS1']])}</div>
      <div class="setrow"><b>Start over</b><button class="btn red" id="resetBtn">Erase my progress</button></div>`);
    $$('.seg').forEach(s => $$('button', s).forEach(b => b.onclick = () => {
      let v = b.dataset.v; if (v === 'true') v = true; if (v === 'false') v = false;
      S.settings[s.dataset.key] = v; save(); applySettings(); $$('button', s).forEach(x => x.classList.toggle('sel', x === b)); Sfx.play('click');
      if (s.dataset.key === 'voice' && v) Voice.speak('Voice is on.'); if (s.dataset.key === 'voiceRate') Voice.speak('This is how fast I will talk.'); if (!S.settings.voice) Voice.stop();
    }));
    $('#resetBtn').onclick = () => Modal.confirm('Erase all progress and start over?', 'Yes, erase everything', () => { const st2 = S.settings; S = freshState(st2); save(); location.reload(); });
  },
  printCertificate() {
    $('#printArea').innerHTML = certHTML();
    const canPrint = !window.CQ_ARTIFACT;
    Modal.show(`<h2>📜 Your Certificate</h2>${certHTML()}<div class="cert-actions" style="margin-top:14px">${canPrint ? '<button class="btn gold big" id="doPrint">🖨️ Print</button>' : '<p>📸 To keep it, take a screenshot or a photo of your certificate.</p>'}</div>`);
    if (canPrint) $('#doPrint').onclick = () => window.print();
  }
};
window.addEventListener('DOMContentLoaded', () => Game.boot());
window.CQ = { S: () => S, L, World, Game, LESSONS, CHAPTERS };
