const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'playwright');
(async () => {
  const b = await chromium.launch({ args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'] });
  const p = await b.newPage({ viewport: { width: 1280, height: 800 } });
  const errs = []; p.on('pageerror', e => errs.push('PAGEERROR: ' + e.message)); p.on('console', m => { if (m.type()==='error' && !/ERR_CERT|Failed to load/.test(m.text())) errs.push(m.text()); });
  await p.goto('file://' + require('path').resolve(__dirname, '../../computer-quest.html')); await p.waitForTimeout(3500);
  await p.screenshot({ path: require('os').tmpdir() + '/cq_40_title.png' });
  await p.evaluate(() => { CQ.S().settings.voice = false; });
  await p.click('#btnNew'); await p.fill('#nameIn', 'Adam'); await p.click('#btnCreateDone'); await p.waitForTimeout(1500);
  const W = ms => p.waitForTimeout(ms);
  await p.evaluate(() => CQ.World.enemies.forEach(e => { e.dead = true; e.respawn = 1e9; })); // keep Glitch battles out of this test
  const act = async (setup) => { await p.evaluate(setup); await W(200); await p.evaluate(() => { const w = CQ.World; w.update(0.016); if (w.near) CQ.Game.action(w.near); }); await W(300); };
  const dlg = async () => (await p.textContent('#dlgText')).replace(/\s+/g, ' ').slice(0, 110);
  const tracker = async () => (await p.textContent('#missionTracker')).replace(/\s+/g, ' ');
  console.log('tracker start:', await tracker());
  // hello: talk to Maya
  await act(() => { const w = CQ.World; w.player.root.position.set(-2.5, 0, 9.5); });
  await W(2500); console.log('done missions:', await p.evaluate(() => CQ.S().missions));
  console.log('tracker:', await tracker());
  // unlock ch1 via state, then lost phone flow
  await p.evaluate(() => { const S = CQ.S(); S.done = ['home']; Missions.check(true); });
  console.log('lostphone active:', await p.evaluate(() => Missions.active('lostphone')), 'phone visible:', await p.evaluate(() => CQ.World.lostPhone.visible));
  const talkKen = () => { const w = CQ.World, n = w.npcs[1]; w.player.root.position.set(n.root.position.x, 0, n.root.position.z + 1.2); };
  await act(talkKen); console.log('ken says:', await dlg()); await p.click('#dlgBtns button');
  await act(() => { const w = CQ.World; w.player.root.position.set(w.lostPhone.position.x, 0, w.lostPhone.position.z + 1); });
  console.log('carry:', await p.evaluate(() => CQ.S().carry), '|', await dlg()); await p.click('#dlgBtns button');
  await act(talkKen); console.log('ken says:', await dlg()); await p.click('#dlgBtns button');
  await W(1500); console.log('phoneReturned mission done:', await p.evaluate(() => CQ.S().missions.includes('lostphone')));
  // delivery
  await p.evaluate(() => { const S = CQ.S(); S.done = ['home', 'mouse', 'keys']; Missions.check(true); });
  const talkRosa = () => { const w = CQ.World, n = w.npcs[0]; w.player.root.position.set(n.root.position.x, 0, n.root.position.z + 1.2); };
  await act(talkRosa); console.log('rosa says:', await dlg(), 'carry', await p.evaluate(() => CQ.S().carry)); await p.click('#dlgBtns button');
  await act(() => { const w = CQ.World; w.player.root.position.set(w.mailboxPos.x, 0, w.mailboxPos.z + 1.3); });
  await W(1500); console.log('delivery done:', await p.evaluate(() => CQ.S().missions.includes('delivery')), 'carry', await p.evaluate(() => CQ.S().carry));
  console.log('tracker:', await tracker());
  await p.screenshot({ path: require('os').tmpdir() + '/cq_41_missions_hud.png' });
  await p.click('#hbMissions'); await W(300); await p.screenshot({ path: require('os').tmpdir() + '/cq_42_missions_modal.png' }); await p.click('#mClose');
  // look at security HQ and city hall
  await p.evaluate(() => { const S = CQ.S(); S.done = ['home','mouse','keys','office','web','mail','android','iphone']; CQ.World.refreshMarkers(); CQ.World.teleportTo(8); });
  await W(2500); await p.screenshot({ path: require('os').tmpdir() + '/cq_43_security.png' });
  await p.evaluate(() => CQ.World.teleportTo(9)); await W(2500); await p.screenshot({ path: require('os').tmpdir() + '/cq_44_cityhall.png' });
  console.log('errors:', errs.join('\n') || 'none');
  await b.close();
})();
