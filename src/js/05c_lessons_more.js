/* =====================================================================
   MORE LESSON STEPS — inserted into each chapter (see addSteps calls).
   ===================================================================== */
function addSteps(id, where, steps) {
  const arr = LESSONS[id].steps;
  const at = typeof where === 'number' ? arr.length - where : arr.findIndex(s => (typeof s.say === 'string' ? s.say : '').includes(where)) + 1;
  arr.splice(at, 0, ...steps);
}
function ctxMenu(parent, e, id, items) {
  const r = parent.getBoundingClientRect(); const m = document.createElement('div'); m.className = 'ctxmenu'; m.id = id; m.style.zIndex = 50;
  m.style.left = Math.max(4, Math.min(r.width - 200, e.clientX - r.left)) + 'px'; m.style.top = Math.max(4, Math.min(r.height - 170, e.clientY - r.top)) + 'px';
  m.innerHTML = items.map(([a, l]) => `<div data-a="${a}">${l}</div>`).join(''); parent.appendChild(m); return m;
}
const QUIZ = (q, emoji, opts, correct, why = {}) => ({ say: 'Quick question!', build: (S, api) => quiz(S, api, { q, emoji, opts, correct, why }) });

/* ---------- Chapter 1: Home — volume, Wi-Fi on a computer, frozen screen, charging ---------- */
addSteps('home', 'This is the <b>desktop</b>', [
  {
    say: 'Let\'s make the computer louder. Click the <b>speaker 🔊</b> at the bottom-right of the screen, next to the clock.', hint: 'The speaker is the little 🔊 next to the time, in the bottom-right corner of the screen.',
    build: (S, api) => {
      ensurePC(S); if (!$('#pcStart')) $('#pcScr').innerHTML = MINI_DESK; const v = $('#pcVol'); api.target(v);
      api.listen(v, 'click', e => {
        e.stopPropagation(); if ($('#volPop')) return;
        $('.mini-desk').insertAdjacentHTML('beforeend', '<div class="tray-pop" id="volPop" style="flex-direction:row;align-items:center">🔈 <input type="range" id="volR" min="0" max="100" value="20" aria-label="Volume"> 🔊</div>');
        const r = $('#volR'); api.say('Now <b>drag the slider</b> to the right: press on the round knob, hold, and slide it right.'); api.setTargets(r);
        api.listen(r, 'input', () => { if (+r.value >= 70) api.done('Louder! Many keyboards also have volume keys: 🔉 and 🔊.'); });
      });
    }
  },
  {
    say: 'Now connect the computer to the internet with <b>Wi-Fi</b>. Click the <b>Wi-Fi icon 📶</b> at the bottom-right, then click <b>TownHome-WiFi</b>.', hint: 'The Wi-Fi icon looks like little bars, next to the speaker.',
    build: (S, api) => {
      ensurePC(S); if (!$('#pcStart')) $('#pcScr').innerHTML = MINI_DESK; const vp = $('#volPop'); if (vp) vp.remove(); const w = $('#pcWifi'); api.target(w);
      api.listen(w, 'click', e => {
        e.stopPropagation(); if ($('#wifiPop')) return;
        $('.mini-desk').insertAdjacentHTML('beforeend', `<div class="tray-pop" id="wifiPop" style="min-width:55%"><b>Wi-Fi networks</b>${['TownHome-WiFi', 'Library-Guest', 'Cafe-Free'].map(n => `<div class="smi btnish" data-n="${n}">📶 ${n}</div>`).join('')}</div>`);
        const t = $('#wifiPop [data-n="TownHome-WiFi"]'); api.say('Click <b>TownHome-WiFi</b>. That\'s your home network.'); api.setTargets(t);
        $$('#wifiPop [data-n]').forEach(d => d.onclick = ev => { ev.stopPropagation(); if (d === t) api.done('quiet'); else api.wrong('That\'s a different network. Choose TownHome-WiFi, your home network.'); });
      });
    }
  },
  {
    say: 'Type the Wi-Fi password <b>town2026</b>, then press <b>Enter</b>. (Your real password is usually on a sticker on your internet box.)', hint: 'Type town2026: the letters, then the numbers from the top row. Then press Enter.',
    build: (S, api) => {
      const p = $('#wifiPop'); p.innerHTML = '<b>TownHome-WiFi</b><div>Network password:</div><input id="wpw" autocomplete="off" style="width:100%;font-size:1em;padding:.2em">';
      const inp = $('#wpw'); const kb = makeKeyboard(api, { input: inp, zones: true }); $('#pcBelow').innerHTML = ''; $('#pcBelow').appendChild(kb.el);
      typeCheck(api, inp, 'town2026', { enter: true, kb, onOk: () => { $('#pcBelow').innerHTML = ''; p.innerHTML = '✅ <b>Connected</b> to TownHome-WiFi'; setTimeout(() => p.remove(), 1500); api.done('Connected! Now this computer can use the internet.'); } });
    }
  },
]);
addSteps('home', 1, [
  QUIZ('The screen is <b>frozen</b> and nothing moves. What should you try first?', '🥶', ['Hit the side of the screen', 'Wait a minute, then restart the computer', 'Unplug everything and never use it again', 'Click everything as fast as you can'], 1,
    { 0: 'Please don\'t hit it! Give it a minute. If it stays frozen, restart it.', 2: 'No need for that! Wait a minute, then restart.', 3: 'Lots of clicking can make it slower. Wait a minute, then restart.', right: 'Right! Waiting a minute, then restarting, fixes most frozen computers. If it won\'t respond at all, hold the power button for 10 seconds.' }),
  QUIZ('Which picture means a laptop is <b>charging</b>?', '🔌', ['🔋⚡ A battery with a lightning bolt', '📶 Bars', '🔇 A speaker with a line', '⏻ A circle with a line'], 0,
    { 1: 'Bars mean Wi-Fi signal.', 2: 'That means the sound is muted.', 3: 'That\'s the power button symbol.', right: 'Yes! The lightning bolt means it\'s plugged in and charging.' }),
]);

/* ---------- Chapter 2: Mouse — hover menus, highlighting, precise clicks ---------- */
addSteps('mouse', 1, [
  {
    say: 'Some menus open when you <b>hover</b>: rest the pointer on them without clicking. Point at <b>☰ Menu</b> to open it, then click <b>⚙️ Settings</b>.', hint: 'Move the pointer onto the word Menu and keep it there. Then move down and click Settings.',
    build: (S, api) => {
      S.innerHTML = '<div class="stage-inner"><div class="playfield" id="pf"><div class="hovermenu" id="hm"><div class="hm-top">☰ Menu</div><div class="hm-list"><div>🏠 Home</div><div id="hmSet">⚙️ Settings</div><div>❓ Help</div></div></div></div></div>';
      const hm = $('#hm'); api.target(hm);
      api.listen(hm, 'pointerenter', () => hm.classList.add('open')); api.listen(hm, 'pointerleave', () => hm.classList.remove('open')); api.listen(hm, 'click', () => hm.classList.add('open'));
      api.listen($('#hmSet'), 'click', e => { e.stopPropagation(); api.done('You used a hover menu! Many websites have menus like this.'); });
      $$('.hm-list div', hm).forEach(d => { if (d.id !== 'hmSet') api.listen(d, 'click', e => { e.stopPropagation(); api.soft('That\'s a different item. Click ⚙️ Settings.'); }); });
    }
  },
  {
    say: 'You can <b>highlight words</b> by dragging across them. Press and hold the button just before a word, slide to the end, then let go. Highlight the words <b>good morning</b>.', hint: 'Put the pointer just before the word "good", hold the button down, slide to the end of "morning", then let go.',
    build: (S, api) => {
      S.innerHTML = '<div class="stage-inner"><div class="icard neutral" id="selTxt" style="max-width:680px;user-select:text;-webkit-user-select:text;font-size:1.45em;line-height:1.7;cursor:text">Maya says <span id="gm">good morning</span> to everyone in Tech Town.</div></div>';
      api.hintOnly($('#gm'));
      const chk = () => { const t = (getSelection().toString() || '').replace(/\s+/g, ' ').trim().toLowerCase(); if (t.includes('good morning')) api.done('Highlighted! Now you could copy it or make it bold. Tip: double-click a single word to highlight just that word.'); else if (t) api.soft(`You highlighted “${esc(t)}”. Try to cover the words good morning.`); };
      api.listen(document, 'mouseup', () => setTimeout(chk, 30)); api.listen(document, 'touchend', () => setTimeout(chk, 30));
    }
  },
  {
    say: 'Let\'s make your clicks more exact. Click each <b>dot</b> as it appears. Each one gets a little smaller! Take your time. There\'s no rush.', hint: 'Move slowly until the pointer is right on the dot, then click.',
    build: (S, api) => {
      S.innerHTML = '<div class="stage-inner"><div class="playfield" id="pf"></div></div>'; const pf = $('#pf');
      const sizes = [90, 74, 60, 50, 42, 36], spots = [[20, 30], [72, 22], [46, 64], [16, 72], [82, 72], [52, 32]]; let k = 0;
      const next = () => {
        const d = document.createElement('button'); d.className = 'dot'; d.style.cssText = `width:${sizes[k]}px;height:${sizes[k]}px;left:${spots[k][0]}%;top:${spots[k][1]}%`; d.textContent = k + 1; pf.appendChild(d); api.setTargets(d);
        d.onclick = () => { d.remove(); Sfx.play('click'); k++; if (k >= sizes.length) api.done('Bullseye! Your mouse skills are sharp.'); else next(); };
      };
      next();
    }
  },
]);

/* ---------- Chapter 3: Typing — punctuation, numbers, a sentence, copy & paste ---------- */
addSteps('keys', 1, [
  typeStep({ say: 'Questions end with a <b>question mark</b>. Hold <b>Shift</b> and press the <b>/</b> key (bottom-right) to make <b>?</b>. Type: <b>How are you?</b>', hint: 'Type the words with spaces between them. For ?, hold Shift and press the / key, next to the right Shift key.', target: 'How are you?', mismatch: 'Something doesn\'t match. Remember: ? is Shift and the / key. Press ⌫ Backspace to fix it.', after: 'Great question!' }),
  typeStep({ say: 'Numbers are on the <b>top row</b>. The dash <b>-</b> is next to the 0. Type this phone number: <b>555-0123</b>', hint: 'Use the number keys on the top row. The - key is just to the right of 0.', target: '555-0123', after: 'Perfect phone number!' }),
  typeStep({ say: 'Now a whole sentence! Start with a <b>capital I</b> and end with a period (<b>.</b>). Type: <b>I love my family.</b>', hint: 'Hold Shift for the capital I. Spaces between words. The period key is near the bottom-right.', target: 'I love my family.', cs: true, mismatch: 'Not quite. Use a capital I (hold Shift), small letters after, and a period at the end. ⌫ Backspace fixes mistakes.', after: 'What a lovely sentence!' }),
  {
    say: '<b>Copy and paste</b> saves typing. In the top box, press <b>Ctrl + A</b> to select all the words, then <b>Ctrl + C</b> to copy. Click the bottom box and press <b>Ctrl + V</b> to paste. (On a Mac, use <b>⌘ Command</b> instead of Ctrl.)', hint: 'Hold Ctrl (bottom-left corner of the keyboard) and tap A, then hold Ctrl and tap C. Click the lower box, hold Ctrl and tap V. You can also use the Copy and Paste buttons.',
    build: (S, api) => {
      S.innerHTML = `<div class="stage-inner typebox"><div class="icard neutral" style="width:min(640px,94vw)"><label for="cpA"><b>Copy from here:</b></label><input class="typein" id="cpA" value="Tech Town is fun!" style="width:100%">
        <div style="display:flex;gap:8px;margin:.5em 0"><button class="btn ghost" id="cpBtn">📋 Copy</button><button class="btn ghost" id="psBtn">📥 Paste</button></div>
        <label for="cpB"><b>Paste here:</b></label><input class="typein" id="cpB" style="width:100%" placeholder="Click here, then press Ctrl + V"></div><div id="kbSlot"></div></div>`;
      const kb = makeKeyboard(api, { zones: true }); $('#kbSlot').appendChild(kb.el); kb.highlight(['Control', 'a']);
      const A = $('#cpA'), B = $('#cpB'); let clip = ''; A.focus();
      const check = () => { if (B.value.trim() === 'Tech Town is fun!') api.done('Copied and pasted! This works almost everywhere: emails, documents and websites.'); };
      api.listen(A, 'keydown', e => {
        const m = e.ctrlKey || e.metaKey, k = e.key.toLowerCase();
        if (m && k === 'a') setTimeout(() => { kb.highlight(['Control', 'c']); api.soft('All selected! Now press <b>Ctrl + C</b> to copy.', false); }, 10);
        if (m && k === 'c') { clip = A.value.substring(A.selectionStart, A.selectionEnd) || A.value; kb.highlight(['Control', 'v']); api.soft('Copied! Now click the bottom box and press <b>Ctrl + V</b>.', false); }
      });
      api.listen(B, 'keydown', e => { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'v' && clip) { e.preventDefault(); B.value = clip; check(); } });
      api.listen(B, 'paste', () => setTimeout(check, 30)); api.listen(B, 'input', check);
      api.listen($('#cpBtn'), 'click', () => { A.select(); clip = A.value; api.soft('Copied! Now click 📥 Paste (or click the bottom box and press Ctrl + V).', false); });
      api.listen($('#psBtn'), 'click', () => { if (!clip) return api.soft('Copy first: press Ctrl + A then Ctrl + C in the top box, or click 📋 Copy.'); B.value = clip; check(); });
    }
  },
]);

/* ---------- Chapter 4: Office — Recycle Bin restore, rename, new folder, search ---------- */
addSteps('office', 1, [
  {
    say: 'Oops, you need “old list” back! Deleted files wait in the <b>Recycle Bin</b>. <b>Double-click the Recycle Bin 🗑️</b> to open it.', hint: DBL_HINT,
    build: (S, api, ctx) => {
      const bin = $('#ic-bin'); api.target(bin);
      dbl(api, bin, () => { ctx.bw = openWin(ctx.dsk, { id: 'bin', title: '🗑️ Recycle Bin', body: '<div class="files"><div class="file" id="binOld"><span class="ico">📄</span>old list</div></div>' }); Object.assign(ctx.bw.style, { left: '42%', top: '18%', width: '44%', height: '52%', zIndex: 12 }); api.done('quiet'); }, () => api.soft(DBL_HINT));
    }
  },
  {
    say: '<b>Right-click</b> “old list”, then click <b>↩️ Restore</b> to put it back.', hint: 'Point at old list and press the RIGHT mouse button. Then click Restore in the menu.',
    build: (S, api, ctx) => {
      const f = $('#binOld'); api.target(f);
      api.listen(f, 'click', () => api.soft('That was a left click. Use the RIGHT button to open the menu.'));
      api.listen(f, 'contextmenu', e => {
        e.preventDefault(); if ($('#binMenu')) return;
        ctxMenu(ctx.dsk, e, 'binMenu', [['restore', '↩️ Restore'], ['del', '❌ Delete forever']]);
        api.say('Now click <b>↩️ Restore</b>.'); const r = $('#binMenu [data-a="restore"]'); api.setTargets(r);
        r.onclick = ev => { ev.stopPropagation(); $('#binMenu').remove(); ctx.bw.remove(); const files = $('#win-docs .files'); if (files) files.insertAdjacentHTML('beforeend', '<div class="file"><span class="ico">📄</span>old list</div>'); api.done('Restored! It\'s back in Documents. Once you empty the Recycle Bin, files are gone for good.'); };
        $('#binMenu [data-a="del"]').onclick = ev => { ev.stopPropagation(); api.wrong('Careful! That deletes it forever. Choose Restore.'); };
      });
    }
  },
  {
    say: 'You can <b>rename</b> a file. In the Documents window, <b>right-click “shopping”</b> and choose <b>✏️ Rename</b>.', hint: 'Right-click the file called shopping in the Documents window.',
    build: (S, api, ctx) => {
      const f = $('#fShop'); api.target(f);
      api.listen(f, 'click', () => api.soft('Use the RIGHT mouse button to open the menu.'));
      api.listen(f, 'contextmenu', e => {
        e.preventDefault(); if ($('#renMenu')) return;
        ctxMenu(ctx.dsk, e, 'renMenu', [['open', '📂 Open'], ['ren', '✏️ Rename'], ['del', '🗑️ Delete']]);
        api.say('Click <b>✏️ Rename</b>.'); api.setTargets($('#renMenu [data-a="ren"]'));
        $$('#renMenu div').forEach(d => d.onclick = ev => { ev.stopPropagation(); if (d.dataset.a !== 'ren') return api.soft('Choose Rename this time.'); $('#renMenu').remove(); api.done('quiet'); });
      });
    }
  },
  {
    say: 'Type the new name <b>groceries</b> and press <b>Enter</b>.', hint: 'The old name is highlighted, so just start typing: groceries. Then press Enter.',
    build: (S, api) => {
      const f = $('#fShop'); f.innerHTML = '<span class="ico">📄</span><input id="renIn" value="shopping" style="width:6.5em;font-size:.9em">';
      const inp = $('#renIn'); inp.focus(); inp.select();
      typeCheck(api, inp, 'groceries', { enter: true, autoFocus: false, onOk: () => { f.innerHTML = '<span class="ico">📄</span>groceries'; api.done('Renamed!'); } });
    }
  },
  {
    say: 'Let\'s make a <b>new folder</b> for photos. <b>Right-click an empty spot on the desktop</b> (the blue sky), then choose <b>📁 New folder</b>.', hint: 'Point at an empty part of the desktop background, press the RIGHT button, then click New folder.',
    build: (S, api, ctx) => {
      $$('.win', ctx.dsk).forEach(w => w.classList.add('min')); api.target(ctx.dsk);
      api.listen(ctx.dsk, 'contextmenu', e => {
        e.preventDefault(); if (e.target.closest('.dicon,.win,.taskbar,.ctxmenu') || $('#nfMenu')) return;
        ctxMenu(ctx.dsk, e, 'nfMenu', [['nf', '📁 New folder'], ['view', '👁️ View'], ['sort', '↕️ Sort by']]);
        api.say('Click <b>📁 New folder</b>.'); api.setTargets($('#nfMenu [data-a="nf"]'));
        $$('#nfMenu div').forEach(d => d.onclick = ev => { ev.stopPropagation(); if (d.dataset.a !== 'nf') return api.soft('Choose New folder.'); $('#nfMenu').remove(); ctx.dsk.insertAdjacentHTML('beforeend', '<div class="dicon" id="ic-new" style="left:2%;top:72%"><span class="ico">📁</span><input id="nfIn" value="New folder" style="width:6em;font-size:.85em"></div>'); api.done('quiet'); });
      });
      api.listen(ctx.dsk, 'click', e => { if (!e.target.closest('.ctxmenu')) api.soft('Use the RIGHT mouse button on an empty part of the desktop.'); });
    }
  },
  {
    say: 'Name the folder: type <b>Family Photos</b> and press <b>Enter</b>.', hint: 'Just start typing: Family Photos. Then press Enter.',
    build: (S, api) => { const inp = $('#nfIn'); inp.focus(); inp.select(); typeCheck(api, inp, 'Family Photos', { enter: true, autoFocus: false, onOk: () => { $('#ic-new').innerHTML = '<span class="ico">📁</span>Family Photos'; api.done('Your new folder is ready! You can drag photos into it.'); } }); }
  },
  {
    say: 'Can\'t find something? <b>Search</b> for it! Click the <b>Start button ⊞</b>, type <b>notes</b>, then click the <b>📝 Notes</b> app in the results.', hint: 'Start is the blue button in the bottom-left corner. Then type the word notes.',
    build: (S, api, ctx) => {
      const st = $('.taskbar .startb', ctx.dsk); api.target(st);
      api.listen(st, 'click', () => {
        if ($('#smenu2')) return;
        ctx.dsk.insertAdjacentHTML('beforeend', '<div class="startmenu" id="smenu2" style="z-index:30;bottom:52px;width:50%"><input id="sSearch" placeholder="🔍 Type here to search" autocomplete="off" style="width:100%;font-size:1em;padding:.3em;border-radius:8px;border:2px solid #9fb3c8"><div id="sRes" style="margin-top:6px"></div></div>');
        const inp = $('#sSearch'); inp.focus(); api.say('Now type <b>notes</b>.'); api.setTargets(inp);
        api.listen(inp, 'input', () => {
          const v = inp.value.trim().toLowerCase();
          $('#sRes').innerHTML = v && 'notes'.startsWith(v) ? '<div class="smi btnish" id="sNotes">📝 Notes <small>App</small></div>' : (v ? '<div class="smi">No results. Check the spelling.</div>' : '');
          const n = $('#sNotes');
          if (n) { if (v === 'notes') { api.say('Click <b>📝 Notes</b> in the results.'); api.setTargets(n); } n.onclick = ev => { ev.stopPropagation(); $('#smenu2').remove(); openWin(ctx.dsk, { id: 'notes2', title: 'Notes · Untitled', body: '<textarea class="notepad"></textarea>' }); api.done('Found it and opened it! Search finds files too.'); }; }
        });
      });
    }
  },
]);

/* ---------- Chapter 5: Web — bookmarks, zoom, cookie banners, online forms ---------- */
WEB_PAGES.news = () => ({ url: 'https://www.techtownnews.com', title: 'Tech Town News', secure: true, html: `<div class="page"><h1>📰 Tech Town News</h1><p><b>Park fountain gets a makeover!</b> The town's favorite fountain is sparkling again…</p><p><b>Library adds free computer classes</b> every Tuesday at 10am.</p></div>
  <div class="dlg" id="cookieBar" style="top:auto;bottom:10px;transform:translateX(-50%);max-width:92%"><h4>🍪 This site uses cookies</h4><p style="margin:.2em 0 .6em">Cookies are small files that remember things about your visit. Some are needed, others track you for ads.</p><div class="row"><button id="ckSet">Settings</button><button id="ckRej">Reject all</button><button class="pri" id="ckAcc">Accept all</button></div></div>` });
WEB_PAGES.card = () => ({ url: 'https://www.townlibrary.org/library-card', title: 'Get a Library Card', secure: true, html: `<div class="page"><h1>📚 Get a Free Library Card</h1><form id="cardForm" style="display:flex;flex-direction:column;gap:.5em;max-width:420px">
  <label for="fName"><b>Your name</b></label><input id="fName" autocomplete="off" style="font-size:1.05em;padding:.3em;border:2px solid #9fb3c8;border-radius:8px">
  <label for="fEmail"><b>Email address</b></label><input id="fEmail" autocomplete="off" style="font-size:1.05em;padding:.3em;border:2px solid #9fb3c8;border-radius:8px">
  <label style="display:flex;gap:.5em;align-items:center"><input type="checkbox" id="fAgree" style="width:1.3em;height:1.3em"> I agree to the library rules</label>
  <button class="btn" id="fSubmit" type="submit" style="align-self:flex-start">Submit</button><div id="fMsg" style="font-weight:800"></div></form></div>` });
addSteps('web', 1, [
  hookStep('Like a website? <b>Bookmark</b> it to find it again easily. Click the <b>☆ star</b> at the right end of the address bar.', 'The star is just right of the address bar, near the top right.', () => [$('#bStar')], (api, ctx, ev) => { if (ev === 'bookmark') api.done('Bookmarked! Your saved sites are in the ⋮ menu under Bookmarks.'); }),
  {
    say: 'Text too small? <b>Zoom in!</b> Click the <b>⋮</b> menu (top right), then click the <b>+</b> next to Zoom to make the page bigger.', hint: 'The ⋮ is the three dots at the far top-right of the browser.',
    build: (S, api, ctx) => {
      api.target($('#bMenu'));
      ctx.hook = (ev, d) => { if (ev === 'menu' && $('#zIn')) { api.say('Now click <b>+</b> to zoom in.'); api.setTargets($('#zIn')); } if (ev === 'zoom') { if (d > 100) { const dd = $('#bDrop'); if (dd) dd.remove(); api.done('Bigger text! You can also hold Ctrl and press + (on a Mac: ⌘ and +).'); } else api.soft('That made it smaller. Click + to make it bigger.'); } };
      api.cleanup(() => ctx.hook = null);
    }
  },
  {
    say: 'Many websites show a <b>cookie</b> message. Cookies remember things about your visit, and some track you for ads. It\'s okay to choose <b>Reject all</b>: the site still works. <b>Click “Reject all”.</b>', hint: 'The cookie box is at the bottom of the page. Click Reject all.',
    build: (S, api, ctx) => {
      const d = $('#bDrop'); if (d) d.remove(); ctx.B.go('news'); api.target($('#ckRej'));
      api.listen($('#ckRej'), 'click', () => { $('#cookieBar').remove(); api.done('Done! Rejecting cookies shares less of your information with advertisers.'); });
      api.listen($('#ckAcc'), 'click', () => { $('#cookieBar').remove(); api.done('That works too! Accepting is fine on sites you trust. Reject all shares less with advertisers.'); });
      api.listen($('#ckSet'), 'click', () => api.soft('Settings lets you choose each type. For now, click Reject all.'));
    }
  },
  {
    say: 'Websites often have <b>forms</b> to fill in. Let\'s get a library card! Type your <b>name</b>, type the practice email <b>sam@mail.com</b>, tick the <b>checkbox</b>, then click <b>Submit</b>.', hint: 'Click in each white box to type. Click the small square to tick it. Then click Submit.',
    build: (S, api, ctx) => {
      ctx.B.go('card'); api.target($('#cardForm'));
      const form = $('#cardForm'); $('#fName').focus();
      api.listen(form, 'submit', e => {
        e.preventDefault(); const n = $('#fName').value.trim(), m = $('#fEmail').value.trim();
        if (!n) return api.soft('Type your name in the first box.'); if (!/^[^@\s]+@[^@\s]+\.[a-z]{2,}$/i.test(m)) return api.soft('Check the email: it needs an @ and a dot, like sam@mail.com.');
        if (!$('#fAgree').checked) return api.soft('Tick the small square next to “I agree”. Click it once.');
        $('#fMsg').textContent = '✅ Thank you! Your library card is on its way.'; api.done('Form sent! Only fill in forms on websites you trust, with a 🔒 padlock.');
      });
    }
  },
]);

/* ---------- Chapter 6: Email — forward, search, star, unsubscribe ---------- */
addSteps('mail', 1, [
  hookStep('You can <b>forward</b> an email to someone else. Open <b>“Your book is ready”</b> from the Library, then click <b>➡️ Forward</b>.', 'Click the Library email in the list first. Then click Forward above the message.', () => [$('.mitem[data-id="lib"]')], (api, ctx, ev, d) => {
    if (ev === 'open' && d === 'lib') { api.say('Now click <b>➡️ Forward</b>.'); api.setTargets($('#mFwd')); }
    if (ev === 'forward') api.done('quiet');
  }),
  fieldStep('Type who to send it to. In the <b>To</b> box, type <b>sam@townmail.com</b>', 'Click the To box. Remember: @ is Shift + 2.', '#cTo', 'sam@townmail.com'),
  hookStep('The original message is already included. Click <b>Send ➤</b>.', 'Send is the blue button at the bottom.', () => [$('#cSend')], (api, ctx, ev) => { if (ev === 'send') { ctx.send(); api.done('Forwarded! Sam can now see the library message too.'); } }),
  {
    say: 'Lots of emails? <b>Search</b> for one! Click the <b>🔍 Search mail</b> box above the list, type <b>library</b>, and press <b>Enter</b>.', hint: 'The search box is at the top of the email list.',
    build: (S, api, ctx) => {
      const ms = $('#mSearch'); api.target(ms); ms.focus();
      ctx.hook = (ev, d) => { if (ev === 'search') { if (/library/i.test(d)) api.done('Found it! Search looks through every email for your words.'); else api.soft('Try searching for: library'); } };
      api.cleanup(() => { ctx.hook = null; ctx.query = ''; const s = $('#mSearch'); if (s) s.value = ''; ctx.renderList(); });
    }
  },
  hookStep('<b>Star</b> important emails so they\'re easy to find. Open <b>Sam\'s “Beach day!”</b> email and click <b>☆ Star</b>.', 'Click Sam Lee\'s email in the list, then click the Star button above the message.', () => [$('.mitem[data-id="sam"]')], (api, ctx, ev, d) => {
    if (ev === 'open' && d === 'sam') { api.say('Now click <b>☆ Star</b>.'); api.setTargets($('#mStar')); }
    if (ev === 'star') api.done('Starred! ⭐ Important emails are now easy to spot.');
  }),
  {
    say: 'A store keeps sending you ads. If you know the company, you can <b>unsubscribe</b>. Open <b>“Shoe Barn: Weekly deals”</b> and click <b>Unsubscribe</b> at the bottom of the email.', hint: 'Open the Shoe Barn email. Scroll to the bottom of the message and click the small Unsubscribe link.',
    build: (S, api, ctx) => {
      if (!ctx.emails.find(e => e.id === 'shoes')) ctx.emails.unshift({ id: 'shoes', from: 'Shoe Barn', addr: 'deals@shoebarn.com', subj: 'Shoe Barn: Weekly deals 👟', body: 'This week: 20% off walking shoes!<br><br><small>You get this email because you shopped at Shoe Barn. <span class="link" id="unsubLink">Unsubscribe</span></small>', unread: true, folder: 'inbox', time: clockStr() });
      ctx.folder = 'inbox'; ctx.renderList(); api.target($('.mitem[data-id="shoes"]'));
      ctx.hook = (ev, d) => {
        if (ev === 'open' && d === 'shoes') { api.say('Now click the small <b>Unsubscribe</b> link at the bottom of the email.'); api.setTargets($('#unsubLink')); }
        if (ev === 'unsub') { $('#mRead').innerHTML = '<div style="color:#23a55a;margin-top:30%;text-align:center;font-weight:900">✅ You won\'t get these emails anymore.</div>'; api.done('Unsubscribed! Only do this for companies you know. For scam emails, use Report spam instead.'); }
      };
      api.cleanup(() => ctx.hook = null);
    }
  },
]);

/* ---------- Chapters 7 & 8: phones — alarms, bigger text, Maps / Notes ---------- */
const andToApp = (app, then) => (api, ctx, ev, d) => {
  if (ev === 'home' || (ev === 'back' && ctx.P.scr === 'home')) { api.say(`Now tap <b>${app}</b>.`); api.setTargets(byApp(app)()); return; }
  if (ev === 'app') { if (d !== app) api.soft(`That opened ${d}. Tap Home ● and then ${app}.`); else then.enter && then.enter(api, ctx); return; }
  then(api, ctx, ev, d);
};
const iosToApp = (app, then) => (api, ctx, ev, d) => {
  if ((ev === 'swipe' && d.dir === 'up' && d.fy > .7) || (ev === 'homebarTap' && L.wrongs >= 1)) { ctx.P.home(); api.say(`Now tap <b>${app}</b>.`); api.setTargets(byApp(app)()); return; }
  if (ev === 'homebarTap') { api.wrong('Hold down on the line and SLIDE up.'); return; }
  if (ev === 'app') { if (d !== app) api.soft(`That opened ${d}. Swipe up to go Home, then tap ${app}.`); else then.enter && then.enter(api, ctx); return; }
  then(api, ctx, ev, d);
};
const startAt = (home, app) => ctx => ctx.P.scr === 'home' ? byApp(app)() : [$(home)];
function alarmStep(os) {
  const h = (api, ctx, ev, d) => { if (ev === 'alarm') { if (d.id === 'a700' && d.on) api.done('Alarm set! ⏰ The phone will ring at 7:00 AM.'); else if (d.id !== 'a700') api.soft('That\'s a different alarm. Turn on the 7:00 AM one.'); } };
  h.enter = (api) => { api.say('Tap the switch next to <b>7:00 AM</b> to turn that alarm on.'); api.setTargets($('[data-al="a700"]')); };
  return pStep(os === 'and' ? 'Let\'s set an <b>alarm</b>. Tap <b>Home ●</b>, then tap the <b>Clock ⏰</b> app, and turn on the <b>7:00 AM</b> alarm.' : 'Let\'s set an <b>alarm</b>. <b>Swipe up</b> from the bottom to go Home, tap the <b>Clock ⏰</b> app, and turn on the <b>7:00 AM</b> alarm.',
    'Go to the Home screen first, then look for the Clock app with the ⏰.', startAt(os === 'and' ? '#nHome' : '#hbar', 'Clock'), os === 'and' ? andToApp('Clock', h) : iosToApp('Clock', h));
}
function fontStep(os) {
  const h = (api, ctx, ev, d) => {
    if (ev === 'setting') { if (d === 'disp') { api.say('Tap <b>A+</b> until the text size says <b>Large</b>.'); } else api.soft('Tap the Display line this time.'); }
    if (ev === 'screen' && d === 'display') api.setTargets($('#fPlus'));
    if (ev === 'font') { if (d >= 2) api.done('Bigger text is easier to read! This changes the text in almost every app.'); }
  };
  h.enter = (api) => { api.say(`Now tap <b>${os === 'and' ? 'Display' : 'Display & Brightness'}</b>.`); api.setTargets($('[data-s="disp"]')); };
  return pStep(`Make the phone's words <b>bigger</b>. Go Home, open <b>Settings ⚙️</b>, tap <b>${os === 'and' ? 'Display' : 'Display & Brightness'}</b>, then tap <b>A+</b> until it says <b>Large</b>.`,
    'Home screen first, then the gray Settings gear.', startAt(os === 'and' ? '#nHome' : '#hbar', 'Settings'), os === 'and' ? andToApp('Settings', h) : iosToApp('Settings', h));
}
{
  const maps = (api, ctx, ev, d) => {
    if (ev === 'mapSearch') { if (/library/i.test(d)) { api.say('Found it! Tap <b>↱ Directions</b>.'); api.setTargets($('#mapDir')); } else api.soft('Search for: library'); }
    if (ev === 'mapDir') { api.say('Tap <b>▶ Start</b> to begin step-by-step directions.'); api.setTargets($('#mapGo')); }
    if (ev === 'mapStart') api.done('Let\'s go! 🧭 The phone will tell you each turn out loud. Great for finding new places.');
  };
  maps.enter = (api, ctx) => { api.say('Tap the <b>search box</b>, type <b>library</b>, and press <b>⏎</b>.'); const inp = $('#mapIn'); api.setTargets(inp); phoneKb(api, ctx, '#mapIn'); };
  addSteps('android', 1, [
    alarmStep('and'), fontStep('and'),
    pStep('Find your way with <b>Maps</b>! Go Home ●, tap <b>Maps 🗺️</b>, search for <b>library</b>, and get <b>Directions</b>.', 'Home screen first, then the Maps app.', startAt('#nHome', 'Maps'), andToApp('Maps', maps)),
  ]);
  const notes = (api, ctx, ev, d) => {
    if (ev === 'noteNew') { api.say('Type <b>Buy milk</b>, then tap <b>Done</b>.'); const t = $('#nText'); api.setTargets(t, $('#nDone')); phoneKb(api, ctx, '#nText'); }
    if (ev === 'noteDone') { if (/buy milk/i.test(d)) api.done('Note saved! 📝 The Notes app is great for shopping lists.'); else api.soft('Type Buy milk, then tap Done.'); }
  };
  notes.enter = (api) => { api.say('Tap the <b>✏️</b> at the top right to start a new note.'); api.setTargets($('#nNew')); };
  addSteps('iphone', 1, [
    alarmStep('ios'), fontStep('ios'),
    pStep('Write a quick <b>note</b>. Swipe up to go Home, open <b>Notes 📝</b>, tap <b>✏️</b>, type <b>Buy milk</b>, and tap <b>Done</b>.', 'Swipe up from the bottom first, then tap the yellow Notes app.', startAt('#hbar', 'Notes'), iosToApp('Notes', notes)),
  ]);
}

/* ---------- Chapter 9: Cyber Safety — scam calls, app permissions, gift cards, locking, backups ---------- */
addSteps('security', 1, [
  {
    say: 'Your phone rings. The caller says they are from <b>“Windows Support”</b> and your computer has a virus. They want to connect to it. <b>What do you do?</b>', hint: 'Real tech companies never call you out of the blue. Hang up.',
    build: (S, api) => {
      S.innerHTML = `<div class="stage-inner"><div style="display:flex;gap:26px;align-items:center;flex-wrap:wrap;justify-content:center">
        <div style="width:230px;height:420px;background:#15171a;border-radius:32px;padding:10px"><div style="position:relative;height:100%;border-radius:24px;overflow:hidden"><div class="callin"><div style="font-size:3em">🖥️</div><b>“Windows Support”</b><small>+1 (888) 555-0100</small><div style="background:rgba(255,255,255,.12);border-radius:12px;padding:8px;font-size:.85em">“Hello, your computer is sending us virus alerts. Please let us connect to fix it right now.”</div></div></div></div>
        <div style="display:flex;flex-direction:column;gap:12px;min-width:260px"><button class="qopt" id="tsLet">🖱️ Let them connect to my computer</button><button class="qopt" id="tsCard">💳 Pay them to fix it</button><button class="qopt" id="tsHang">📞 Hang up</button></div></div></div>`;
      api.target($('#tsHang'));
      api.listen($('#tsLet'), 'click', () => api.wrong('Never let a stranger connect to your computer. They could steal your files and passwords. Hang up!'));
      api.listen($('#tsCard'), 'click', () => api.wrong('Never pay a surprise caller. This is a well-known scam. Hang up!'));
      api.listen($('#tsHang'), 'click', () => { $('#tsHang').classList.add('right'); api.done('Exactly! Microsoft, Apple and banks never call you out of the blue about viruses. If worried, call the company using a number from its real website.'); });
    }
  },
  {
    say: 'You installed a <b>flashlight app</b>. It asks to see your <b>contacts</b> and your <b>location</b>. A flashlight doesn\'t need those! <b>Tap “Don\'t allow”.</b>', hint: 'Only allow what an app really needs. Tap Don\'t allow.',
    build: (S, api) => {
      S.innerHTML = `<div class="stage-inner"><div style="width:250px;height:440px;background:#15171a;border-radius:32px;padding:10px"><div style="position:relative;height:100%;border-radius:24px;overflow:hidden;background:linear-gradient(#333,#111);display:grid;place-items:center">
        <div style="background:#fff;border-radius:16px;padding:14px;width:88%;text-align:center;color:#111"><div style="font-size:2em">🔦</div><b>“Super Flashlight” wants to access your Contacts and Location</b>
        <div style="display:flex;flex-direction:column;gap:6px;margin-top:10px"><button class="pbtn" id="pmAllow">Allow</button><button class="pbtn" id="pmDeny" style="background:#e9ecef;color:#111">Don't allow</button></div></div></div></div></div>`;
      api.target($('#pmDeny'));
      api.listen($('#pmAllow'), 'click', () => api.wrong('A flashlight doesn\'t need your contacts or location. Apps that ask for too much may be collecting your information. Tap Don\'t allow.'));
      api.listen($('#pmDeny'), 'click', () => api.done('Smart! You can check what every app is allowed to see in Settings, under Privacy or Permissions.'));
    }
  },
  QUIZ('Someone asks you to pay a bill with <b>gift cards</b> and read them the numbers. It\'s…', '🎁', ['A normal way to pay bills', 'Always a scam', 'Fine if they sound nice', 'Fine if it\'s a small amount'], 1,
    { right: 'Right! Real companies and government offices never ask for payment in gift cards. Hang up and tell someone you trust.', 0: 'No real business or government office asks you to pay with gift cards.', 2: 'Scammers are often very friendly. Gift cards = scam, every time.', 3: 'Even small amounts: gift card payment requests are always scams.' }),
  QUIZ('You\'re using a computer at the <b>library</b> and step away for a minute. What should you do?', '🔒', ['Leave it open, it\'s only a minute', 'Lock the screen (Windows key + L) or sign out', 'Turn off the monitor only', 'Ask a stranger to watch it'], 1,
    { right: 'Yes! Windows key + L locks a PC (on a Mac: Control + Command + Q). And always sign out of public computers when you\'re done.', 0: 'A minute is enough for someone to read your email. Lock it!', 2: 'The computer is still unlocked. Lock it instead.', 3: 'Better to lock it or sign out.' }),
  QUIZ('What protects your photos if your computer breaks or gets <b>ransomware</b>?', '💾', ['A backup copy on a USB drive or cloud storage', 'A bigger monitor', 'A new mouse', 'Deleting your photos'], 0,
    { right: 'Right! With a backup, you can get everything back. Many phones back up photos automatically to the cloud.', 1: 'A monitor doesn\'t save files. A backup does!', 2: 'A mouse doesn\'t save files. A backup does!', 3: 'Oh no! A backup keeps them safe instead.' }),
]);

/* ---------- Final review — questions about the new skills ---------- */
const FINAL_QS_EXTRA = [
  { q: 'Which keys <b>copy</b> and then <b>paste</b>?', emoji: '📋', opts: ['Ctrl + C, then Ctrl + V', 'Shift + A, then Shift + B', 'Enter, then Space', 'Alt + F4'], correct: 0 },
  { q: 'You deleted a file by mistake. Where can you find it?', emoji: '🗑️', opts: ['The Recycle Bin', 'The printer', 'The address bar', 'It\'s gone forever'], correct: 0 },
  { q: 'Someone asks you to pay with <b>gift cards</b>. That\'s…', emoji: '🎁', opts: ['Normal', 'A scam', 'A good deal', 'Required by law'], correct: 1 },
  { q: 'A caller says they\'re from “tech support” and wants to <b>connect to your computer</b>. You…', emoji: '📞', opts: ['Let them connect', 'Hang up', 'Give them your password', 'Pay them'], correct: 1 },
];
