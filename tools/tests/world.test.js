// Town checks: car direction, solid cars/people, Glitch sizes and hits, Virus King, certificate PDF.
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'playwright');
(async () => {
  const b = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const p = await b.newPage({ viewport: { width: 1280, height: 800 }, acceptDownloads: true });
  const errs = []; p.on('pageerror', e => errs.push('PAGEERROR: ' + e.message));
  p.on('console', m => { if (m.type() === 'error' && !/ERR_CERT|Failed to load/.test(m.text())) errs.push(m.text()); });
  await p.goto('file://' + require('path').resolve(__dirname, '../../computer-quest.html'));
  await p.waitForTimeout(3500);
  await p.evaluate(() => { const S = CQ.S(); S.name = 'Tester'; S.tutorial = true; S.settings.voice = false; S.settings.sfx = false; CQ.Game.enterTown(false); });
  await p.waitForTimeout(800);
  let fails = 0; const check = (name, ok, extra = '') => { console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${extra ? ' · ' + extra : ''}`); if (!ok) fails++; };

  // 1. Cars drive loops through the connected roads, facing where they go, on the right-hand side
  const drive = await p.evaluate(() => {
    const W = CQ.World; const onRoad = (x, z) => STREETS.some(s => Math.abs(z - s) <= 3.05 && Math.abs(x) <= 46) || (CROSS_X.some(cx => Math.abs(x - cx) <= 3.05) && z <= STREETS[0] + 3 && z >= STREETS[STREETS.length - 1] - 3);
    let facing = 0, off = 0, samples = 0; const start = W.cars.map(c => c.userData.d);
    for (let k = 0; k < 600; k++) { W.buildDynamic(); W.updateCars(.05); W.cars.forEach(c => { samples++; const u = c.userData; if (Math.sin(c.rotation.y) * u.dx + Math.cos(c.rotation.y) * u.dz > .7) facing++; if (!onRoad(c.position.x, c.position.z)) off++; }); }
    const moved = W.cars.map((c, i) => c.userData.d - start[i]);
    // right-hand lane: the road center is on the car's left
    const right = W.cars.every(c => { const u = c.userData, lx = -u.dz, lz = u.dx; const cx = CROSS_X.find(x => Math.abs(c.position.x - x) < 3.1), sz = STREETS.find(s => Math.abs(c.position.z - s) < 3.1);
      if (Math.abs(u.dx) > .9 && sz !== undefined) return (c.position.z - sz) * lz > 0; if (Math.abs(u.dz) > .9 && cx !== undefined) return (c.position.x - cx) * lx > 0; return true; });
    return { facing: facing / samples, off, moved: Math.min(...moved), right };
  });
  check('cars face their driving direction', drive.facing > .97, drive.facing.toFixed(3));
  check('cars stay on the roads', drive.off === 0, drive.off + ' off-road samples');
  check('cars keep driving (no teleport, no deadlock)', drive.moved > 40, 'min distance ' + drive.moved.toFixed(0));
  check('cars keep to the right-hand lane', drive.right);
  const joined = await p.evaluate(() => STREETS.every(s => [-43, 43].every(x => CROSS_X.includes(x))) && CQ.World.boxes.every(b => !(b.x0 < 46 && b.x1 > 40 && b.z0 < STREETS[0] && b.z1 > STREETS[STREETS.length - 1])));
  check('edge roads join every street and are not blocked', joined);

  // 2. Solid: the player cannot walk into a car or through a townsperson
  const solid = await p.evaluate(() => {
    const W = CQ.World; W.buildDynamic(); const c = W.cars[0];
    const pos = { x: c.position.x, z: c.position.z }; W.collide(pos, .5, W.player);
    const outCar = Math.hypot(pos.x - c.position.x, pos.z - c.position.z) > 1.0;
    const n = W.npcs[0]; const q = { x: n.root.position.x + .1, z: n.root.position.z }; W.collide(q, .5, W.player);
    const outNpc = Math.hypot(q.x - n.root.position.x, q.z - n.root.position.z) >= .84;
    return { outCar, outNpc };
  });
  check('player is pushed out of a car', solid.outCar); check('player is pushed off a townsperson', solid.outNpc);

  // 3. NPC walking lanes are clear of lamps, hydrants and signs
  const blocked = await p.evaluate(() => {
    const W = CQ.World; let hits = 0;
    W.npcs.forEach(n => { const [a, b] = n.path; for (let t = 0; t <= 1; t += .01) { const x = a[0] + (b[0] - a[0]) * t, z = a[1] + (b[1] - a[1]) * t; if (W.colliders.some(c => Math.hypot(x - c.x, z - c.z) < c.r + .35)) hits++; } });
    return hits;
  });
  check('NPC paths never pass through props', blocked === 0, `${blocked} blocked samples`);

  // 4. Glitch sizes: bigger = more hits
  const kinds = await p.evaluate(() => { const o = {}; CQ.World.enemies.forEach(e => { o[e.kind] = o[e.kind] || { n: 0, hp: e.maxHp, scale: e.g.userData.scale }; o[e.kind].n++; }); return o; });
  check('30+ Glitches of 4 kinds', Object.values(kinds).reduce((a, k) => a + k.n, 0) >= 30 && kinds.tiny && kinds.mini && kinds.big && kinds.king, JSON.stringify(kinds));
  const order = ['tiny', 'mini', 'big', 'king'];
  check('bigger Glitches need more hits', order.every((k, i) => i === 0 || (kinds[k].hp > kinds[order[i - 1]].hp && kinds[k].scale > kinds[order[i - 1]].scale)));

  // 5. Fight a Big Glitch (word mode after Typing School) and count hits
  const fight = async (kind, chapters) => p.evaluate(async ([kind, chapters]) => {
    const S = CQ.S(); S.done = chapters; const W = CQ.World; Dialog.hide(); if (W.battle) W.endBattle('fled');
    W.enemies.forEach(e => { if (e.kind !== kind) { e.dead = true; e.respawn = 1e9; } });
    const en = W.enemies.find(e => e.kind === kind); en.dead = false; en.hp = en.maxHp; en.cool = 0; en.g.visible = true;
    W.player.root.position.set(en.g.position.x, 0, en.g.position.z + 2); W.update(.016);
    if (W.battle !== en) return { err: 'no battle', visible: en.g.visible };
    let hits = 0, keys = 0, wordMode = !!en.word;
    while (W.battle === en && keys < 200) {
      const k = en.word ? en.word[en.typed.length] : en.letter; const r = W.attack(k); keys++;
      if (r && r.ok && !r.partial) hits++;
    }
    return { hits, keys, wordMode, won: en.dead };
  }, [kind, chapters]);
  const big = await fight('big', ['home', 'mouse', 'keys']);
  check('Big Glitch: word battle, 5 hits', big.wordMode && big.hits === 5 && big.won, JSON.stringify(big));
  const kingHidden = await p.evaluate(() => { const S = CQ.S(); S.done = ['home']; const W = CQ.World; const k = W.enemies.find(e => e.kind === 'king'); k.dead = false; W.update(.016); return !k.g.visible; });
  check('Virus King hidden before Cyber Safety HQ', kingHidden);
  const king = await fight('king', ['home', 'mouse', 'keys', 'office', 'web', 'mail', 'android', 'iphone', 'security']);
  check('Virus King: word battle, 8 hits', king.wordMode && king.hits === 8 && king.won, JSON.stringify(king));
  const tiny = await fight('tiny', ['home']);
  check('Tiny Glitch: 2 letters', !tiny.wordMode && tiny.hits === 2 && tiny.won, JSON.stringify(tiny));

  // 6. Certificate PDF + PNG are real files
  const cert = await p.evaluate(async () => {
    const G = CQ.Game; let got = null; G._realSave = G.saveFile; G.saveFile = async (name, blob) => { got = { name, blob }; return 'saved'; };
    await G.saveCert('pdf'); const pdf = new Uint8Array(await got.blob.arrayBuffer()); const txt = new TextDecoder('latin1').decode(pdf);
    const sx = +txt.match(/startxref\n(\d+)/)[1]; const xrefOk = txt.slice(sx, sx + 4) === 'xref';
    const offs = [...txt.matchAll(/(\d{10}) 00000 n /g)].map(m => +m[1]); const objsOk = offs.every((o, i) => txt.slice(o, o + 8).startsWith(`${i + 1} 0 obj`));
    const pdfName = got.name;
    await G.saveCert('png'); const png = new Uint8Array(await got.blob.arrayBuffer());
    return { head: txt.slice(0, 8), xrefOk, objsOk, pdfSize: pdf.length, pdfName, png: png[1] === 0x50 && png[2] === 0x4e && png[3] === 0x47, pngName: got.name };
  });
  check('certificate PDF is valid', cert.head.startsWith('%PDF-1.4') && cert.xrefOk && cert.objsOk && cert.pdfSize > 20000, JSON.stringify(cert));
  check('certificate PNG is valid', cert.png);
  await p.evaluate(() => { CQ.Game.saveFile = CQ.Game._realSave; }); // back to the real download code
  await p.evaluate(() => CQ.Game.printCertificate()); await p.waitForTimeout(600);
  const imgW = await p.evaluate(() => { const i = document.querySelector('#certImg'); return i && i.naturalWidth; });
  check('certificate window shows the certificate as a picture (right-click › Save image)', imgW === 1600, 'width ' + imgW);
  for (const [btn, ext] of [['#doPdf', 'pdf'], ['#doPng', 'png']]) {
    const [dl] = await Promise.all([p.waitForEvent('download', { timeout: 15000 }).catch(() => null), p.click(btn)]);
    const name = dl && dl.suggestedFilename(); const size = dl ? require('fs').statSync(await dl.path()).size : 0;
    check(`${ext.toUpperCase()} button downloads the certificate`, !!dl && name.endsWith('.' + ext) && size > 20000, `${name} ${size} bytes`);
  }
  await p.evaluate(() => { window.__printed = 0; window.print = () => { window.__printed++; }; }); await p.click('#doPrint'); await p.waitForTimeout(300);
  check('Print opens the print dialog in the game file', await p.evaluate(() => window.__printed === 1));
  // Inside a viewer that blocks downloads: explain the right-click fallback (no "download the game" message)
  const blockedMsg = await p.evaluate(async () => { window.CQ_ARTIFACT = true; CQ.Game._realFramed = CQ.Game.framed; CQ.Game.framed = () => true; document.querySelector('#doPdf').click(); await new Promise(r => setTimeout(r, 1500)); const t = document.querySelector('#certMsg').textContent; window.CQ_ARTIFACT = false; CQ.Game.framed = CQ.Game._realFramed; return t; });
  check('blocked viewer shows the right-click tip', /Save image as/.test(blockedMsg) && !/game file/i.test(blockedMsg), blockedMsg);

  check('certificate window has Print, PDF and picture buttons', await p.evaluate(() => !!(document.querySelector('#doPrint') && document.querySelector('#doPdf') && document.querySelector('#doPng'))));
  await p.screenshot({ path: require('os').tmpdir() + '/cq_cert_modal.png' });

  console.log('errors:', errs.join('\n') || 'none');
  console.log(fails ? `${fails} FAILED` : 'ALL PASS');
  await b.close(); process.exit(fails ? 1 : 0);
})();
