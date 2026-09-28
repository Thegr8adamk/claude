const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'playwright');
const ONLY = process.argv[2];
(async () => {
  const b = await chromium.launch({ args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
  const p = await b.newPage({ viewport: { width: 1280, height: 860 } });
  const errs = [];
  p.on('console', m => { if (m.type() === 'error' && !/ERR_CERT|Failed to load resource/.test(m.text())) errs.push(m.text()); });
  p.on('pageerror', e => errs.push('PAGEERROR: ' + e.message));
  await p.goto('file://' + require('path').resolve(__dirname, '../../computer-quest.html'));
  await p.waitForTimeout(3000);
  await p.evaluate(() => { window.CQ_FAST = true; const S = CQ.S(); S.name = 'Tester'; S.settings.voice = false; S.settings.sfx = false; });
  const I = () => p.evaluate(() => CQ.L.cur ? CQ.L.i : -1);
  const W = ms => p.waitForTimeout(ms);
  const click = async s => { await p.click(s, { timeout: 4000 }); await W(150); };
  const dbl = async s => { await p.dblclick(s, { timeout: 4000 }); await W(200); };
  const type = async t => { await p.keyboard.type(t, { delay: 20 }); await W(150); };
  const key = async k => { await p.keyboard.press(k); await W(120); };
  const drag = async (a, bSel) => { const A = await p.locator(a).first().boundingBox(), B = await p.locator(bSel).first().boundingBox();
    await p.mouse.move(A.x + A.width/2, A.y + A.height/2); await p.mouse.down(); await p.mouse.move(B.x + B.width/2, B.y + B.height/2, { steps: 12 }); await p.mouse.up(); await W(250); };
  const swipe = async (sel, fromY, toY) => { const r = await p.locator(sel).boundingBox(); const x = r.x + r.width * .6;
    await p.mouse.move(x, r.y + r.height * fromY); await p.mouse.down(); await p.mouse.move(x, r.y + r.height * toY, { steps: 10 }); await p.mouse.up(); await W(250); };
  const app = n => click(`.apps .app[data-app="${n}"]`);
  const nxt = () => click('#gNext');
  const until = async (fn, ms = 5000) => { const t = Date.now(); while (Date.now() - t < ms) { if (await fn()) return true; await W(100); } return false; };
  const plan = {
    home: [nxt, () => click('#pcMon'), () => click('#pcKbd'), () => click('#pcMouse'), () => click('#pcTower'), nxt, async () => { await click('#pcPwr'); await W(4000); },
      async () => { await click('#pinIn'); await type('1234'); await key('Enter'); }, nxt, () => click('#pcStart'), () => click('#sPow'), async () => { await click('#pmShut'); await W(2600); }, nxt],
    mouse: [nxt, async () => { for (let k = 0; k < 3; k++) { await p.hover('.star:not(.got)'); await W(300); } },
      async () => { for (let k = 0; k < 5; k++) { await p.click('.balloon:not(.pop)', { force: true }); await W(200); } },
      () => dbl('#fold'), async () => { await p.click('#dog', { button: 'right' }); await W(300); await click('#rot'); },
      async () => { for (let k = 0; k < 3; k++) { await drag('.apple', '#bask'); } },
      async () => { await p.evaluate(() => { const s = document.querySelector('#sb'); s.scrollTop = s.scrollHeight; }); await W(300); await click('#chest'); }, nxt],
    keys: [nxt, async () => { await key('c'); await key('a'); await key('t'); }, async () => { await key(' '); await key(' '); await key(' '); },
      () => type('hello'), () => key('Backspace'), () => type('Hi'), async () => { await type('Good morning'); await key('Enter'); }, () => type('sam@mail.com'), nxt],
    office: [nxt, () => dbl('#ic-notes'), async () => { await click('#note'); await type('Buy milk'); }, () => click('#saveBtn'),
      async () => { await p.fill('#fname', 'shopping'); await click('#doSave'); }, () => click('.wmin'), () => click('#tbNotes'), () => click('.wmax'), () => click('.wclose'),
      () => dbl('#ic-docs'), () => drag('#fOld', '#ic-bin'), nxt],
    web: [nxt, () => dbl('#ic-browser'), () => click('#bAddr'), async () => { await type('townweather.com'); await key('Enter'); }, () => click('#bLock'), () => click('#lnkForecast'), () => click('#bBack'),
      async () => { await click('#bAddr'); await type('pancake recipe'); await key('Enter'); }, async () => { await p.click('#resAd .st'); await W(200); await click('#resReal .st'); }, () => click('#bNew'),
      () => click('.tab:nth-of-type(2) .tx'), () => click('.tab:last-of-type .tx'), nxt],
    mail: [nxt, () => click('.qopt[data-i="1"]'), () => click('.mitem[data-id="maya"]'), () => click('#mReply'), async () => { await click('#cBody'); await type('Thank you'); await click('#cSend'); },
      () => click('#mCompose'), async () => { await click('#cTo'); await type('sam@townmail.com'); }, async () => { await click('#cSubj'); await type('Hello'); }, async () => { await click('#cBody'); await type('How are you?'); },
      async () => { await click('#cClip'); await click('#picker .file[data-f="cat.jpg"]'); await click('#pkOpen'); }, () => click('#cSend'), () => click('.mitem[data-id="phish"]'),
      async () => { await click('#verifyLink'); await click('#mSpam'); }, async () => { await click('.mitem[data-id="sam"]'); await click('#mAtt'); }, async () => { await click('.mitem[data-id="news"]'); await click('#mDel'); }, nxt],
    android: [nxt, () => click('#pPow'), async () => { await click('#pscr'); await swipe('#pscr', .8, .3); }, async () => { for (const d of '2580') await click(`.pinkeys button[data-k="${d}"]`); await W(400); },
      () => app('Messages'), async () => { await click('#fab'); await click('[data-c="Maya"]'); }, async () => { await click('#chIn'); await type('Hi Maya'); await click('#chSend'); },
      () => click('#nHome'), () => app('Phone'), async () => { await click('[data-c="Maya"]'); await click('#callGo'); await W(2200); }, () => click('#callEnd'),
      async () => { for (let k = 0; k < 3 && (await I()) === 11; k++) await click('#nBack'); }, () => app('Settings'), () => click('[data-s="net"]'),
      async () => { await click('#wTog'); await click('[data-net="TownHome-WiFi"]'); }, async () => { await click('#wPass'); await type('town2026'); await click('#wJoin'); },
      async () => { await click('#nHome'); await app('Play Store'); }, async () => { await click('#psIn'); await type('weather'); await key('Enter'); }, async () => { await click('#stInstall'); await W(2600); },
      () => swipe('#pscr', .03, .5), () => click('#tFlash'), () => click('#pVolUp'), nxt],
    iphone: [nxt, () => click('#pPow'), () => swipe('#pscr', .9, .3), async () => { for (const d of '123456') await click(`.pinkeys button[data-k="${d}"]`); await W(400); },
      () => app('Messages'), () => click('#compose'), () => click('[data-c="Sam Lee"]'), async () => { await click('#chIn'); await type('See you soon'); await click('#chSend'); },
      () => swipe('#pscr', .98, .5), () => app('Camera'), () => click('#shut'), () => click('#thumb'), () => swipe('#pscr', .98, .5), () => swipe('#pscr', .03, .5), () => click('#tFlash'),
      async () => { await W(1000); await app('Settings'); await click('[data-s="wifi"]'); }, async () => { await click('#wTog'); await click('[data-net="TownHome-WiFi"]'); },
      async () => { await click('#wPass'); await type('town2026'); await click('#wJoin'); }, async () => { await swipe('#pscr', .98, .5); await app('App Store'); }, () => click('#tSearch'),
      async () => { await click('#asIn'); await type('weather'); await key('Enter'); }, async () => { await click('#stGet'); await click('#stConfirm'); await W(2700); }, nxt],
    security: [nxt, async () => { await click('#pw'); await type('Blue-Cactus-Moon-7!'); }, () => click('.qopt[data-i="2"]'),
      async () => { const c = (await p.textContent('.pb.them b')).replace(/\s/g, ''); await click('#code2'); await type(c); await key('Enter'); },
      async () => { await click('#updNow'); await W(2500); }, async () => { await click('#fakeDl'); await click('#fakeX'); }, () => click('#realDl'), () => click('.qopt[data-i="1"]'),
      () => click('.mside [data-f="spam"]'), async () => { await click('.mitem[data-id="dentist"]'); await click('#notSpam'); }, async () => { await click('.mitem[data-id="prize"]'); await click('#blockB'); },
      () => click('.qopt[data-i="2"]'), async () => { await click('#smClick'); await click('#smDel'); }, async () => { await click('#scanBtn'); await W(2400); }, () => click('.qopt[data-i="1"]'), nxt],
    final: [nxt, ...[0, 1, 2, 1, 2, 1, 0, 2, 0, 2, 2, 1, 1].map(c => () => click(`.qopt[data-i="${c}"]`)), nxt],
  };
  const ids = Object.keys(plan);
  for (let ci = 0; ci < ids.length; ci++) {
    const id = ids[ci]; if (ONLY && ONLY !== id) continue;
    await p.evaluate(ci => { const S = CQ.S(); S.done = CQ.CHAPTERS.slice(0, ci).map(c => c.id); CQ.L.start(ci); }, ci);
    await W(400);
    let ok = true;
    for (let s = 0; s < plan[id].length; s++) {
      const before = await I();
      try { await plan[id][s](); } catch (e) { console.log(`  ${id} step ${before}: ACTION ERROR ${e.message.split('\n')[0]}`); }
      const moved = await until(async () => (await I()) !== before, 6000);
      if (!moved) { const t = await p.evaluate(() => document.querySelector('#gText').textContent.slice(0, 90) + ' | HINT: ' + document.querySelector('#gHint').textContent.slice(0, 120)); console.log(`  ${id} STUCK at step ${before}: ${t}`); ok = false; await p.screenshot({ path: require('os').tmpdir() + `/cq_stuck_${id}_${before}.png` }); break; }
      if (s === 3 || s === Math.floor(plan[id].length * .6)) await p.screenshot({ path: require('os').tmpdir() + `/cq_L_${id}_${s}.png` });
    }
    const done = await p.evaluate(id => CQ.S().done.includes(id), id);
    console.log(`${id}: ${ok && done ? 'PASS' : 'FAIL'}`);
  }
  console.log('errors:', errs.length ? errs.join('\n') : 'none');
  await b.close();
})();
