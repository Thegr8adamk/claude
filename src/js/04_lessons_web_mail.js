/* =====================================================================
   LESSONS 5–6: internet & email
   ===================================================================== */
function overlayCard(parent, html) {
  const d = document.createElement('div'); d.className = 'dlg neutral'; d.style.maxWidth = '92%'; d.innerHTML = html; parent.appendChild(d); return d;
}

/* ---------------- CHAPTER 5: LIBRARY — the web ---------------- */
const WEB_PAGES = {
  start: () => ({ url: '', title: 'New Tab', secure: true, html: `<div class="searchhome"><div class="slogo"><span style="color:#2d7ff9">S</span><span style="color:#e5484d">e</span><span style="color:#f5b700">a</span><span style="color:#2d7ff9">r</span><span style="color:#23a55a">c</span><span style="color:#e5484d">hly</span></div><div style="color:#5b6b7c">Type a website address or a search in the bar at the top ↑</div></div>` }),
  weather: () => ({ url: 'https://www.townweather.com', title: 'Town Weather', secure: true, html: `<div class="page"><h1>☀️ Town Weather</h1><div class="weather-hero"><div class="big">☀️</div><div><div style="font-size:2em;font-weight:900">72°F · Sunny</div><div>Tech Town · light breeze</div></div></div>
    <p>Perfect day for a walk in the park! <span class="link" id="lnkForecast">7-Day Forecast</span> · <span class="link">Radar map</span> · <span class="link">Pollen report</span></p></div>` }),
  forecast: () => ({ url: 'https://www.townweather.com/7-day', title: '7-Day Forecast', secure: true, html: `<div class="page"><h1>7-Day Forecast</h1><div class="fc">${['Mon ☀️ 72°', 'Tue ⛅ 68°', 'Wed 🌧️ 61°', 'Thu 🌦️ 64°', 'Fri ☀️ 70°', 'Sat ☀️ 75°', 'Sun ⛅ 71°'].map(d => `<div>${d.replace(' ', '<br>')}</div>`).join('')}</div><p>To go back to today's weather, use the ← Back button at the top left.</p></div>` }),
  results: (q) => ({ url: 'https://www.searchly.com/search?q=' + encodeURIComponent(q), title: q + ' · Searchly', secure: true, html: `<div class="page"><div style="color:#5b6b7c">About 1,240,000 results for “${esc(q)}”</div>
    <div class="sresult ad" id="resAd"><div class="su"><span class="adtag">Sponsored</span>free-prizes-winner.biz</div><div class="st link">🎁 WIN a FREE tablet!!! Best pancakes CLICK NOW</div><div>Limited time!!! Enter your card details to claim…</div></div>
    <div class="sresult" id="resReal"><div class="su">https://www.townkitchen.com › pancakes</div><div class="st link">Easy Fluffy Pancakes · Town Kitchen</div><div>A simple recipe with flour, milk, eggs and a pinch of sugar. Ready in 20 minutes.</div></div>
    <div class="sresult"><div class="su">https://www.townwiki.org › Pancake</div><div class="st link">Pancake · Town Wiki</div><div>The history of pancakes around the world…</div></div></div>` }),
  recipe: () => ({ url: 'https://www.townkitchen.com/pancakes', title: 'Easy Fluffy Pancakes', secure: true, html: `<div class="page"><h1>🥞 Easy Fluffy Pancakes</h1><p><b>You need:</b> 1 cup flour · 1 cup milk · 1 egg · 1 tbsp sugar · 2 tsp baking powder · pinch of salt</p><ol><li>Mix everything in a bowl.</li><li>Heat a pan with a little butter.</li><li>Pour small circles and flip when you see bubbles.</li></ol></div>` }),
  scam: () => ({ url: 'http://win-free-prize.biz/alert', title: '⚠ WARNING!!!', secure: false, html: `<div class="scam"><div style="font-size:3em">⚠️</div><div style="font-size:1.6em;font-weight:900">VIRUS DETECTED!!!</div><div>Your computer is INFECTED. Do NOT turn it off.<br>Call Tech Support NOW: <b>1-800-555-0199</b></div><div style="display:flex;gap:10px"><button id="scamCall">📞 Call Now</button><button id="scamOk">OK</button></div></div>` }),
  notfound: (u) => ({ url: u, title: 'Not found', secure: true, html: `<div class="page"><h1>🤔 Hmm, can't find that site</h1><p>“${esc(u)}” doesn't exist in Tech Town. Check the spelling in the address bar and try again.</p></div>` }),
};
function makeBrowser(ctx) {
  const w = openWin(ctx.dsk, { id: 'br', title: '🌐 Web Browser', cls: 'browser max', tool: `<div class="tabs" id="tabs"></div><div class="btool"><button class="navb" id="bBack" title="Back">←</button><button class="navb" id="bFwd" title="Forward">→</button><button class="navb" id="bReload" title="Reload">⟳</button><div class="addr"><button class="lock" id="bLock" title="View site information">🔒</button><input id="bAddr" autocomplete="off" spellcheck="false" placeholder="Search or type a web address"></div><button class="navb" title="Bookmark">☆</button></div>`, body: '<div class="loadbar" id="bLoad"></div><div id="bPage" style="height:100%;position:relative"></div>' });
  const B = ctx.B = { tabs: [], cur: 0, w };
  const hook = (ev, d) => ctx.hook && ctx.hook(ev, d);
  B.tab = () => B.tabs[B.cur];
  B.renderTabs = () => {
    $('#tabs').innerHTML = B.tabs.map((t, i) => `<div class="tab ${i === B.cur ? 'on' : ''}" data-i="${i}"><span class="tt">${esc(t.page.title)}</span><button class="tx" data-x="${i}" title="Close tab">✕</button></div>`).join('') + '<button class="newtab" id="bNew" title="New tab">+</button>';
    $$('.tab', w).forEach(el => el.onclick = e => { if (e.target.closest('.tx')) return; B.cur = +el.dataset.i; B.render(); });
    $$('.tx', w).forEach(el => el.onclick = e => { e.stopPropagation(); B.closeTab(+el.dataset.x); });
    $('#bNew').onclick = () => { B.open('start'); hook('newtab'); };
  };
  B.render = () => {
    const t = B.tab(); const p = t.page; B.renderTabs();
    $('#bPage').innerHTML = p.html; $('#bAddr').value = p.url; $('#bLock').textContent = p.secure ? '🔒' : '⚠️';
    const lf = $('#lnkForecast'); if (lf) lf.onclick = () => { B.go('forecast'); hook('link', 'forecast'); };
    const ra = $('#resAd'); if (ra) ra.onclick = () => hook('ad');
    const rr = $('#resReal'); if (rr) rr.onclick = () => { B.go('recipe'); hook('result'); };
    const sc = $('#scamCall'), so = $('#scamOk'); if (sc) sc.onclick = () => hook('scambtn'); if (so) so.onclick = () => hook('scambtn');
  };
  B.go = (key, arg) => {
    const t = B.tab(); t.hist.push({ page: t.page, key: t.key }); t.page = WEB_PAGES[key](arg); t.key = key;
    const lb = $('#bLoad'); lb.style.transition = 'none'; lb.style.width = '0'; requestAnimationFrame(() => { lb.style.transition = 'width .5s'; lb.style.width = '100%'; setTimeout(() => { lb.style.width = '0'; lb.style.transition = 'none'; }, 600); });
    B.render(); hook('nav', key);
  };
  B.open = (key) => { B.tabs.push({ page: WEB_PAGES[key](), hist: [], key }); B.cur = B.tabs.length - 1; B.render(); };
  B.closeTab = (i) => { const k = B.tabs[i].key; B.tabs.splice(i, 1); if (i < B.cur) B.cur--; if (!B.tabs.length) B.tabs.push({ page: WEB_PAGES.start(), hist: [], key: 'start' }); B.cur = Math.max(0, Math.min(B.cur, B.tabs.length - 1)); B.render(); hook('closetab', k); };
  $('#bBack').onclick = () => { const t = B.tab(); if (!t.hist.length) return hook('backnone'); const h = t.hist.pop(); t.page = h.page; t.key = h.key; B.render(); hook('back'); };
  $('#bLock').onclick = () => { const p = B.tab().page; const pop = document.createElement('div'); pop.className = 'pop'; pop.innerHTML = p.secure ? '🔒 <b>Connection is secure</b><br><small>Information you send to this site is private.</small>' : '⚠️ <b>Not secure</b><br><small>Don\'t type passwords or card numbers here.</small>'; $('#bPage').appendChild(pop); setTimeout(() => pop.remove(), 4000); hook('lock'); };
  const addr = $('#bAddr');
  addr.addEventListener('focus', () => { addr.select(); hook('addrFocus'); });
  addr.addEventListener('keydown', e => {
    if (e.key !== 'Enter') return; e.preventDefault(); const v = addr.value.trim(); if (!v) return; addr.blur();
    const low = v.toLowerCase().replace(/^https?:\/\//, '');
    if (/^[^\s]+\.[a-z]{2,}(\/.*)?$/.test(low)) { if (low.replace(/^www\./, '').startsWith('townweather.com')) B.go('weather'); else B.go('notfound', v); }
    else B.go('results', v);
  });
  B.open('start');
  return B;
}
LESSONS.web = {
  setup: (S, ctx) => { ctx.dsk = desktopSim(S, [{ id: 'browser', e: '🌐', n: 'Web Browser', x: 2, y: 3 }, { id: 'docs', e: '📁', n: 'Documents', x: 2, y: 27 }, { id: 'bin', e: '🗑️', n: 'Recycle Bin', x: 88, y: 3 }]); },
  steps: [
    {
      say: 'Welcome to the Library! The <b>internet</b> connects computers all over the world. To visit <b>websites</b>, you use an app called a <b>web browser</b>. Popular ones are Chrome, Edge, Safari and Firefox. They all work the same way.', next: true,
      build: (S, api, ctx) => { const c = overlayCard(ctx.dsk, `<h4>Web browsers</h4><div class="igrid" style="min-width:min(520px,80vw)"><div class="ibox"><div class="ie">🟢</div><b>Chrome</b></div><div class="ibox"><div class="ie">🔵</div><b>Edge</b></div><div class="ibox"><div class="ie">🧭</div><b>Safari</b></div><div class="ibox"><div class="ie">🦊</div><b>Firefox</b></div></div>`); api.cleanup(() => c.remove()); }
    },
    {
      say: '<b>Double-click the Web Browser icon 🌐</b> on the desktop to open it.', hint: DBL_HINT,
      build: (S, api, ctx) => { const ic = $('#ic-browser'); api.target(ic); dbl(api, ic, () => { makeBrowser(ctx); api.done('The browser is open!'); }, () => api.soft(DBL_HINT)); }
    },
    {
      say: 'At the top is the <b>address bar</b>, the long white box where you type a website\'s address. <b>Click on the address bar.</b>', hint: 'The address bar is the long rounded box near the top of the window, next to the 🔒.',
      build: (S, api, ctx) => { const a = $('#bAddr'); api.target(a); ctx.hook = ev => { if (ev === 'addrFocus') api.done(); }; api.cleanup(() => ctx.hook = null); }
    },
    {
      say: 'Now type the website address <b>townweather.com</b> and press <b>Enter</b>.', hint: 'Click in the address bar, type townweather.com (the dot is near the bottom-right of the keyboard), then press Enter.',
      build: (S, api, ctx) => {
        const a = $('#bAddr'); api.target(a); setTimeout(() => a.focus(), 120);
        ctx.hook = (ev, k) => { if (ev === 'nav') { if (k === 'weather') api.done('You visited a website!'); else api.wrong('That went somewhere else. Click the address bar, type townweather.com exactly, and press Enter.'); } };
        api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: 'See the little <b>padlock 🔒</b> at the start of the address bar? It means your connection to the site is private. <b>Click the padlock.</b>', hint: 'The padlock is at the left end of the address bar.',
      build: (S, api, ctx) => { api.target($('#bLock')); ctx.hook = ev => { if (ev === 'lock') api.done('Secure! Only type passwords or card numbers on sites with a padlock, and only on sites you trust.'); }; api.cleanup(() => ctx.hook = null); }
    },
    {
      say: 'Blue underlined words are <b>links</b>. Clicking a link takes you to another page. Your pointer turns into a pointing hand 👆 over a link. <b>Click “7-Day Forecast”.</b>', hint: 'Look for the blue underlined words “7-Day Forecast” under the sunny picture.',
      build: (S, api, ctx) => { const l = $('#lnkForecast'); if (!l) { ctx.B.go('weather'); } api.target($('#lnkForecast')); ctx.hook = (ev, k) => { if (ev === 'link' && k === 'forecast') api.done(); }; api.cleanup(() => ctx.hook = null); }
    },
    {
      say: 'To go back to the page you were on before, click the <b>Back arrow ←</b> in the top-left corner.', hint: 'The Back arrow is the first button at the top left of the browser, before the address bar.',
      build: (S, api, ctx) => { api.target($('#bBack')); ctx.hook = ev => { if (ev === 'back') api.done('Back again! The Back arrow is very handy.'); }; api.cleanup(() => ctx.hook = null); }
    },
    {
      say: 'You can also <b>search</b> for anything. Click the address bar, type <b>pancake recipe</b>, and press <b>Enter</b>.', hint: 'Click the address bar, type the words pancake recipe, then press Enter.',
      build: (S, api, ctx) => {
        api.target($('#bAddr'));
        ctx.hook = (ev, k) => { if (ev === 'nav') { if (k === 'results' && /pancake/i.test(ctx.B.tab().page.title)) api.done('Search results!'); else if (k === 'results') api.soft('That searched for something else. Try typing: pancake recipe'); } };
        api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: 'Careful! The top result says <b>“Sponsored”</b>. That\'s an <b>ad</b>, and this one looks like a trick. <b>Click the real recipe: “Easy Fluffy Pancakes”.</b>', hint: '“Easy Fluffy Pancakes” is the second result, the one without the Sponsored label.',
      build: (S, api, ctx) => {
        api.target($('#resReal'));
        ctx.hook = ev => { if (ev === 'ad') api.wrong('Good thing this is practice! That was an AD promising free prizes. That\'s a warning sign. Choose “Easy Fluffy Pancakes”.'); if (ev === 'result') api.done('Yum! You picked the real website and skipped the ad.'); };
        api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: '<b>Tabs</b> let you keep several pages open at once, like folders on a desk. Click the <b>+</b> next to the tab at the top to open a <b>new tab</b>.', hint: 'The + is at the very top, just to the right of the “Easy Fluffy Pancakes” tab.',
      build: (S, api, ctx) => { api.target($('#bNew')); ctx.hook = ev => { if (ev === 'newtab') api.done('A new tab! Your recipe is still open in the first tab.'); }; api.cleanup(() => ctx.hook = null); }
    },
    {
      say: 'Now close the new tab: click the small <b>✕</b> on the <b>New Tab</b>.', hint: 'Each tab has its own little ✕. Click the one on the tab that says “New Tab”.',
      build: (S, api, ctx) => {
        const i = ctx.B.tabs.findIndex(t => t.key === 'start'); api.target($(`.tx[data-x="${i}"]`));
        ctx.hook = (ev, k) => { if (ev === 'closetab') { if (k === 'start') api.done('Tab closed!'); else api.soft('Oops, that closed a different tab. No problem!'); } };
        api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: 'Uh-oh! A scary page popped up in a new tab. This is a <b>SCAM</b>. Real companies <b>never</b> warn you like this or ask you to call. Don\'t call and don\'t click inside it. <b>Close the tab with its ✕</b>.', hint: 'Click the ✕ on the tab that says “WARNING!!!” at the very top. If it won\'t close, you can close the whole browser or restart the computer.',
      build: (S, api, ctx) => {
        ctx.B.open('scam'); const i = ctx.B.tabs.length - 1; api.target($(`.tx[data-x="${i}"]`));
        ctx.hook = (ev, k) => {
          if (ev === 'scambtn') api.wrong('Never click inside a scary pop-up, and never call the number. Scammers pretend to be tech support. Close the tab with its ✕ instead.');
          if (ev === 'closetab' && k === 'scam') api.done('Scam closed! If this ever happens for real: close the tab, or restart the computer. Never call the number.');
        };
        api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: 'Wonderful browsing! A few last tips, then click <b>Finish chapter</b>.', next: 'Finish chapter ▶',
      build: (S, api, ctx) => { const c = overlayCard(ctx.dsk, `<h4>Web tips</h4><p>🔍 <b>Bigger text:</b> hold <span class="key">Ctrl</span> and press <span class="key">+</span> (on a Mac: <span class="key">⌘</span> and <span class="key">+</span>).</p><p>☆ <b>Bookmarks:</b> click the star to save a favorite site.</p><p>🚫 <b>Scams:</b> “You won!”, “Virus found!” and “Act now!” are warning signs.</p><p>🔒 Only enter passwords and card numbers on sites you trust that show a padlock.</p>`); api.cleanup(() => c.remove()); }
    }
  ]
};

/* ---------------- CHAPTER 6: POST OFFICE — email ---------------- */
function mailSim(S, ctx) {
  ctx.dsk = desktopSim(S, []);
  ctx.emails = [
    { id: 'maya', from: 'Maya (Tech Guide)', addr: 'maya@townmail.com', subj: 'Welcome to TownMail!', body: `Hi ${esc(S_name())}!<br><br>Welcome to email! This is your very first message. Could you <b>reply</b> and say thank you? 😊<br><br>– Maya`, unread: true, folder: 'inbox', time: '9:12 AM' },
    { id: 'sam', from: 'Sam Lee', addr: 'sam@townmail.com', subj: 'Beach day! 📎', body: 'Look at this photo from our trip! Wish you were there.<br><br>– Sam', attach: 'beach.jpg', unread: true, folder: 'inbox', time: '8:40 AM' },
    { id: 'lib', from: 'Tech Town Library', addr: 'desk@townlibrary.org', subj: 'Your book is ready', body: 'The book you reserved is ready to pick up at the front desk.', unread: true, folder: 'inbox', time: 'Yesterday' },
    { id: 'news', from: 'Town Newsletter', addr: 'news@techtown.gov', subj: 'Weekly Town Newsletter', body: 'This week: the park fountain has been cleaned! (Old news.)', unread: false, folder: 'inbox', time: 'Mon' },
  ];
  ctx.folder = 'inbox'; ctx.sel = null; ctx.attachment = null;
  const w = openWin(ctx.dsk, { id: 'mail', title: '✉️ TownMail', cls: 'max', body: `<div class="mail"><div class="mside"><button class="compose" id="mCompose">✏️ Compose</button><button data-f="inbox" class="on">📥 Inbox</button><button data-f="sent">📤 Sent</button><button data-f="spam">🚫 Spam</button><button data-f="trash">🗑️ Trash</button></div><div class="mlist" id="mList"></div><div class="mread" id="mRead"><div style="color:#8a94a0;margin-top:30%;text-align:center">Click an email to read it</div></div></div>` });
  const hook = (ev, d) => ctx.hook && ctx.hook(ev, d);
  ctx.renderSide = () => { const n = ctx.emails.filter(e => e.folder === 'inbox' && e.unread).length; $$('.mside [data-f]', w).forEach(b => { b.classList.toggle('on', b.dataset.f === ctx.folder); if (b.dataset.f === 'inbox') b.textContent = '📥 Inbox' + (n ? ` (${n})` : ''); }); };
  ctx.renderList = () => {
    const list = ctx.emails.filter(e => e.folder === ctx.folder);
    $('#mList').innerHTML = list.length ? list.map(e => `<div class="mitem ${e.unread ? 'unread' : ''} ${ctx.sel === e.id ? 'sel' : ''}" data-id="${e.id}"><div class="mf">${esc(ctx.folder === 'sent' ? 'To: ' + e.to : e.from)} <span style="float:right;font-weight:400;font-size:.8em">${e.time}</span></div><div class="ms">${esc(e.subj)}</div></div>`).join('') : '<div style="padding:1em;color:#8a94a0">Nothing here.</div>';
    $$('.mitem', w).forEach(el => el.onclick = () => ctx.openMail(el.dataset.id));
    ctx.renderSide();
  };
  ctx.openMail = id => {
    const e = ctx.emails.find(x => x.id === id); if (!e) return; e.unread = false; ctx.sel = id; ctx.renderList();
    $('#mRead').innerHTML = `<div class="mact"><button id="mReply">↩️ Reply</button><button>➡️ Forward</button><button id="mDel">🗑️ Delete</button><button id="mSpam">🚫 Report spam</button></div><h3>${esc(e.subj)}</h3><div class="mmeta">From: <b>${esc(e.from)}</b> &lt;${esc(e.addr)}&gt; · ${e.time}</div><div>${e.body}</div>${e.attach ? `<div class="attach" id="mAtt">🖼️ ${e.attach}</div>` : ''}`;
    $('#mReply').onclick = () => { ctx.compose({ to: e.addr, subj: 'Re: ' + e.subj }); hook('reply', id); };
    $('#mDel').onclick = () => { e.folder = 'trash'; ctx.sel = null; ctx.renderList(); $('#mRead').innerHTML = '<div style="color:#8a94a0;margin-top:30%;text-align:center">Moved to Trash</div>'; hook('delete', id); };
    $('#mSpam').onclick = () => { e.folder = 'spam'; ctx.sel = null; ctx.renderList(); $('#mRead').innerHTML = '<div style="color:#8a94a0;margin-top:30%;text-align:center">🚫 Reported as spam and moved to the Spam folder</div>'; hook('spam', id); };
    const att = $('#mAtt'); if (att) att.onclick = () => { $('#mRead').insertAdjacentHTML('beforeend', '<div class="dlg" id="imgView"><h4>beach.jpg</h4><div style="font-size:110px;text-align:center;background:linear-gradient(#8fd3ff 0 55%,#f4d58d 55%);border-radius:10px">🏖️</div><div class="row"><button onclick="this.closest(\'.dlg\').remove()">Close</button></div></div>'); hook('attach', id); };
    const vl = $('#verifyLink'); if (vl) vl.onclick = () => hook('phishlink');
    hook('open', id);
  };
  ctx.compose = ({ to = '', subj = '' } = {}) => {
    ctx.attachment = null;
    $('#mRead').innerHTML = `<div class="compose-box"><b>${subj ? 'Reply' : 'New message'}</b><input id="cTo" placeholder="To" value="${esc(to)}" autocomplete="off" spellcheck="false"><input id="cSubj" placeholder="Subject" value="${esc(subj)}" autocomplete="off"><textarea id="cBody" placeholder="Write your message here…"></textarea><div id="cAtt"></div><div style="display:flex;gap:10px;align-items:center"><button class="sendb" id="cSend">Send ➤</button><button class="attach" id="cClip" style="margin:0">📎 Attach</button></div></div>`;
    $('#cSend').onclick = () => hook('send');
    $('#cClip').onclick = () => {
      $('#mRead').insertAdjacentHTML('beforeend', `<div class="dlg" id="picker"><h4>Choose a file</h4><div class="files" style="padding:0"><div class="file" data-f="beach.jpg"><span class="ico">🏖️</span>beach.jpg</div><div class="file" data-f="cat.jpg"><span class="ico">🐱</span>cat.jpg</div><div class="file" data-f="recipe.pdf"><span class="ico">📄</span>recipe.pdf</div></div><div class="row"><button id="pkCancel">Cancel</button><button class="pri" id="pkOpen">Open</button></div></div>`);
      let chosen = null;
      $$('#picker .file').forEach(f => f.onclick = () => { $$('#picker .file').forEach(x => x.classList.remove('sel')); f.style.background = '#dbe8ff'; $$('#picker .file').forEach(x => { if (x !== f) x.style.background = ''; }); chosen = f.dataset.f; hook('pick', chosen); });
      $('#pkCancel').onclick = () => { $('#picker').remove(); hook('pickcancel'); };
      $('#pkOpen').onclick = () => { if (!chosen) return hook('picknone'); $('#picker').remove(); ctx.attachment = chosen; $('#cAtt').innerHTML = `<span class="attach">📎 ${chosen}</span>`; hook('attached', chosen); };
      hook('clip');
    };
  };
  ctx.send = () => {
    const to = $('#cTo').value.trim(), subj = $('#cSubj').value.trim() || '(no subject)';
    ctx.emails.push({ id: 's' + Date.now(), to, from: 'Me', addr: '', subj, body: esc($('#cBody').value), folder: 'sent', time: clockStr(), unread: false });
    $('#mRead').innerHTML = '<div style="color:#23a55a;margin-top:30%;text-align:center;font-weight:900;font-size:1.2em">✅ Message sent!</div>'; ctx.renderList();
  };
  $('#mCompose').onclick = () => { ctx.compose(); hook('compose'); };
  $$('.mside [data-f]', w).forEach(b => b.onclick = () => { ctx.folder = b.dataset.f; ctx.sel = null; ctx.renderList(); hook('folder', ctx.folder); });
  ctx.renderList();
}
function hookStep(say, hint, targetSel, handler) {
  return { say, hint, build: (S, api, ctx) => { const t = typeof targetSel === 'function' ? targetSel(ctx) : $$(targetSel); api.target(t); ctx.hook = (ev, d) => handler(api, ctx, ev, d); api.cleanup(() => ctx.hook = null); } };
}
function fieldStep(say, hint, sel, target, loose) {
  return { say, hint, build: (S, api) => { const el = $(sel); api.target(el); typeCheck(api, el, target, { loose, mismatch: `Not quite. It should say “${esc(target)}”. Press ⌫ Backspace to fix mistakes.` }); } };
}
LESSONS.mail = {
  steps: [
    {
      say: 'Welcome to the Post Office! <b>Email</b> is like a letter that arrives in seconds. Everyone has an <b>email address</b>. It always has three parts: a <b>name</b>, the <b>@</b> sign (said “at”), and the <b>email company</b>.', next: true,
      build: S => infoCard(S, `<div style="text-align:center"><div style="font-size:2.1em;font-weight:900;font-family:ui-monospace,Menlo,monospace;margin:.4em 0"><span style="background:#e6f0ff;border-radius:10px;padding:0 .2em">sam</span><span style="background:#ffe9b3;border-radius:10px;padding:0 .2em">@</span><span style="background:#e3f8ea;border-radius:10px;padding:0 .2em">townmail.com</span></div>
      <div class="igrid"><div class="ibox"><b>sam</b><small>the person's name</small></div><div class="ibox"><b>@</b><small>“at” (Shift + 2)</small></div><div class="ibox"><b>townmail.com</b><small>their email company, like gmail.com or outlook.com</small></div></div><p>No spaces are allowed in an email address!</p></div>`)
    },
    { say: 'Quick question!', build: (S, api) => quiz(S, api, { q: 'Which one of these is an <b>email address</b>?', emoji: '📬', opts: ['www.townmail.com', 'sam@townmail.com', '555-0123', 'Sam Lee'], correct: 1, why: { 0: 'That\'s a WEBSITE address (it starts with www). An email address has an @ sign.', 2: 'That\'s a phone number. Look for the @ sign!', 3: 'That\'s a person\'s name. An email address has an @ sign.' } }) },
    {
      say: 'This is your <b>inbox</b>, where new emails arrive. Emails in <b>bold</b> with a blue dot are <b>unread</b>. <b>Click the email from Maya</b> to open it.', hint: 'It\'s the first email in the list: “Maya (Tech Guide)”.',
      build: (S, api, ctx) => { mailSim(S, ctx); api.target($('.mitem[data-id="maya"]')); ctx.hook = (ev, d) => { if (ev === 'open' && d === 'maya') api.done(); }; api.cleanup(() => ctx.hook = null); }
    },
    hookStep('Maya asked you to reply. Click the <b>Reply ↩️</b> button above her message.', 'Reply is the first button above the message.', () => [$('#mReply')], (api, ctx, ev) => { if (ev === 'reply') api.done('The reply is ready. See? The “To” address filled itself in.'); }),
    {
      say: 'Click in the big message box and type <b>Thank you</b>. Then click the blue <b>Send ➤</b> button.', hint: 'Click the big empty box, type Thank you, then click Send.',
      build: (S, api, ctx) => {
        api.target($('#cBody'), $('#cSend')); setTimeout(() => $('#cBody') && $('#cBody').focus(), 120);
        ctx.hook = ev => { if (ev === 'send') { if (/thank/i.test($('#cBody').value)) { ctx.send(); api.done('Sent! Maya will get it in seconds.'); } else api.wrong('First type “Thank you” in the big box, then click Send.'); } };
        api.cleanup(() => ctx.hook = null);
      }
    },
    hookStep('Now write a <b>brand-new email</b>. Click the yellow <b>✏️ Compose</b> button.', 'Compose is the yellow button at the top left.', () => [$('#mCompose')], (api, ctx, ev) => { if (ev === 'compose') api.done('quiet'); }),
    fieldStep('Click the <b>To</b> box and type Sam\'s address: <b>sam@townmail.com</b>', 'Remember: @ is Shift + 2. No spaces.', '#cTo', 'sam@townmail.com'),
    fieldStep('Click the <b>Subject</b> box and type <b>Hello</b>. The subject is like the title of your email.', 'The Subject box is right under the To box.', '#cSubj', 'Hello'),
    fieldStep('Click the big message box and type: <b>How are you?</b>', 'The question mark is Shift and the / key, near the bottom-right.', '#cBody', 'How are you?', true),
    {
      say: 'Let\'s share a picture! Click <b>📎 Attach</b> next to the Send button.', hint: 'The paperclip button is next to Send.',
      build: (S, api, ctx) => {
        api.target($('#cClip'));
        ctx.hook = (ev, d) => {
          if (ev === 'clip') { api.say('Pick the file to send. Click <b>cat.jpg</b>, then click <b>Open</b>.'); api.setTargets($('#picker .file[data-f="cat.jpg"]'), $('#pkOpen')); }
          if (ev === 'pick' && d !== 'cat.jpg') api.soft('This time, please choose cat.jpg 🐱.');
          if (ev === 'picknone') api.soft('Click cat.jpg first, then Open.');
          if (ev === 'pickcancel') { api.say('Let\'s try again. Click <b>📎 Attach</b>.'); api.setTargets($('#cClip')); }
          if (ev === 'attached') { if (d === 'cat.jpg') api.done('The photo is attached. Look for the 📎 cat.jpg under your message.'); else { api.wrong('That attached a different file, but that\'s okay. Let\'s continue.'); api.done('quiet'); } }
        };
        api.cleanup(() => ctx.hook = null);
      }
    },
    hookStep('Everything is ready. Click <b>Send ➤</b>!', 'The blue Send button is at the bottom of your message.', () => [$('#cSend')], (api, ctx, ev) => { if (ev === 'send') { ctx.send(); api.done('Your email is on its way to Sam!'); } }),
    {
      say: '📨 A new email just arrived! <b>Click it to open it</b>. (Opening an email is safe. It\'s the links inside that can be tricky.)', hint: 'It\'s the email at the top of the inbox list.',
      build: (S, api, ctx) => {
        ctx.folder = 'inbox';
        ctx.emails.unshift({ id: 'phish', from: 'Bank Security Team', addr: 'security@bank-verify-alert.co', subj: 'URGENT: Your bank account is LOCKED!!', body: 'Dear Customer,<br><br>We detected a problem. Your account will be <b>DELETED in 24 hours</b> unless you verify your password now.<br><br><span class="link" id="verifyLink">👉 VERIFY NOW</span>', unread: true, folder: 'inbox', time: clockStr() });
        ctx.renderList(); toast('📨 New email!'); api.target($('.mitem[data-id="phish"]'));
        ctx.hook = (ev, d) => { if (ev === 'open' && d === 'phish') api.done('quiet'); }; api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: '🚩 This is a <b>scam email</b>, called <b>phishing</b>. Warning signs: it\'s <b>urgent and scary</b>, the sender\'s address looks <b>strange</b>, and it asks for your <b>password</b>. Don\'t click the link! Click <b>🚫 Report spam</b> instead.', hint: 'Report spam is the last button above the message.',
      build: (S, api, ctx) => {
        api.target($('#mSpam'));
        ctx.hook = ev => {
          if (ev === 'phishlink') api.wrong('Never click links in a suspicious email! Real banks never ask for your password by email. Click “Report spam”.');
          if (ev === 'reply') api.wrong('Don\'t reply to scammers. That tells them your address works. Click “Report spam”.');
          if (ev === 'spam') api.done('Reported! If you\'re ever unsure, call your bank using the phone number on your bank card, not the one in the email.');
          if (ev === 'delete') api.done('Deleted. That works too! Next time you can also click “Report spam”.');
        };
        api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: 'Sam sent you a photo. <b>Click Sam\'s email “Beach day!”</b>, then click the <b>🖼️ beach.jpg</b> attachment to look at it.', hint: 'First click the email from Sam Lee in the list. Then click the box that says beach.jpg under the message.',
      build: (S, api, ctx) => {
        if (ctx.folder !== 'inbox') { ctx.folder = 'inbox'; ctx.renderList(); }
        api.target($('.mitem[data-id="sam"]'));
        ctx.hook = (ev, d) => { if (ev === 'open' && d === 'sam') { api.say('Now click the <b>🖼️ beach.jpg</b> attachment under the message.'); api.setTargets($('#mAtt')); } if (ev === 'attach') api.done('What a lovely beach! Only open attachments from people you know and were expecting.'); };
        api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: 'Let\'s tidy up. Click the old <b>“Weekly Town Newsletter”</b> email, then click <b>🗑️ Delete</b>.', hint: 'The newsletter is at the bottom of the list. After opening it, the Delete button is above the message.',
      build: (S, api, ctx) => {
        const v = $('#imgView'); if (v) v.remove();
        api.target($('.mitem[data-id="news"]'));
        ctx.hook = (ev, d) => { if (ev === 'open' && d === 'news') { api.say('Now click <b>🗑️ Delete</b> above the message.'); api.setTargets($('#mDel')); } if (ev === 'delete' && d === 'news') api.done('Deleted! It\'s in the Trash if you ever need it back.'); };
        api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: 'You\'re an email expert! Gmail, Outlook, Yahoo and the Mail app on phones all have the same buttons: <b>Compose</b>, <b>Reply</b>, <b>Send</b>, <b>Inbox</b> and <b>Trash</b>. Click <b>Finish chapter</b>.', next: 'Finish chapter ▶',
      build: (S, api, ctx) => { const c = overlayCard(ctx.dsk, `<h4>Email safety rules</h4><p>🔑 Never send your <b>password</b> by email. Real companies won't ask.</p><p>🚩 Be careful with emails that are <b>urgent</b>, <b>scary</b> or <b>too good to be true</b>.</p><p>📞 Unsure? <b>Call</b> the company using a number you already know.</p><p>📎 Only open attachments you were <b>expecting</b>.</p>`); api.cleanup(() => c.remove()); }
    }
  ]
};
