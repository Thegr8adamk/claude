/* =====================================================================
   MISSIONS — a quest log. Missions unlock one after another and are checked
   whenever something happens in town (Missions.check()).
   ===================================================================== */
const mstats = () => (S.stats = S.stats || { talked: [], sits: 0, wishes: 0, hoops: 0 });
const chapterMission = (i, title, after) => ({
  id: 'ch' + (i + 1), icon: CHAPTERS[i].icon, title, story: true, after,
  desc: `Finish Chapter ${i + 1} at <b>${CHAPTERS[i].name}</b>: stand at the door and type ENTER.`, check: () => isDone(i), reward: 15,
});
const MISSIONS = [
  { id: 'hello', icon: '👋', title: 'Hello, Tech Town!', story: true, desc: 'Walk up to Maya and type <b>TALK</b> to say hello.', check: () => S.tutorial, reward: 10 },
  chapterMission(0, 'First Steps', 'hello'),
  { id: 'bugs3', icon: '⚔️', title: 'Glitch Hunter', after: 'hello', desc: 'Squash 3 Glitches. Walk near one and press the letter that pops up!', progress: () => [S.bugs || 0, 3], reward: 15 },
  { id: 'gems3', icon: '💎', title: 'Gem Seeker', after: 'hello', desc: 'Find 3 shiny Data Gems around town and type <b>PICKUP</b>.', progress: () => [(S.gems || []).length, 3], reward: 15 },
  { id: 'friends', icon: '💬', title: 'Friendly Neighbor', after: 'hello', desc: 'Type <b>TALK</b> next to 3 different townspeople walking around town.', progress: () => [mstats().talked.length, 3], reward: 15 },
  chapterMission(1, 'Mouse Master', 'ch1'),
  { id: 'rest', icon: '🪑', title: 'Take a Break', after: 'ch1', desc: 'Stand next to a park bench and type <b>USE</b> to sit down.', progress: () => [Math.min(1, mstats().sits), 1], reward: 10 },
  { id: 'wish', icon: '⛲', title: 'Make a Wish', after: 'ch1', desc: 'Stand by the park fountain and type <b>USE</b> to toss in a coin.', progress: () => [Math.min(1, mstats().wishes), 1], reward: 10 },
  { id: 'lostphone', icon: '📱', title: 'Lost & Found', story: true, after: 'ch1', desc: 'Ken lost his phone in the park! <b>TALK</b> to Ken, find the phone near the trees on the left side of the park (<b>PICKUP</b>), then bring it back and <b>TALK</b> to Ken.', check: () => !!mstats().phoneReturned, reward: 25 },
  chapterMission(2, 'Typing Time', 'ch2'),
  { id: 'hoop', icon: '🏀', title: 'Hoop Dreams', after: 'ch2', desc: 'Type <b>PICKUP</b> next to the basketball in the park, walk close to the hoop, and type <b>THROW</b>!', progress: () => [Math.min(1, mstats().hoops), 1], reward: 15 },
  chapterMission(3, 'Office Hours', 'ch3'),
  { id: 'delivery', icon: '✉️', title: 'Special Delivery', story: true, after: 'ch3', desc: '<b>TALK</b> to Rosa to get her letter, then walk to the blue mailbox outside the Post Office and type <b>USE</b>.', check: () => !!mstats().delivered, reward: 25 },
  { id: 'bugs10', icon: '🗡️', title: 'Glitch Master', after: 'bugs3', desc: 'Squash 10 Glitches in total. Your typing is getting faster!', progress: () => [S.bugs || 0, 10], reward: 25 },
  chapterMission(4, 'Web Explorer', 'ch4'),
  chapterMission(5, 'You\'ve Got Mail', 'ch5'),
  chapterMission(6, 'Android Adventure', 'ch6'),
  chapterMission(7, 'iPhone Adventure', 'ch7'),
  { id: 'gems12', icon: '👑', title: 'Treasure Hunter', after: 'gems3', desc: 'Find all 12 Data Gems hidden in Tech Town.', progress: () => [(S.gems || []).length, 12], reward: 30 },
  chapterMission(8, 'Cyber Guardian', 'ch8'),
  { id: 'scout', icon: '🦠', title: 'Virus Patrol', after: 'ch3', desc: 'Squash 5 Glitches. Glitches are like computer viruses: stay alert!', progress: () => [S.bugs || 0, 5], reward: 15 },
  chapterMission(9, 'Tech Hero', 'ch9'),
];
const Missions = {
  busy: false,
  doneIds() { return (S.missions = S.missions || []); },
  isComplete(m) { if (m.check) return !!m.check(); const [a, b] = m.progress(); return a >= b; },
  unlocked(m) { return !m.after || this.doneIds().includes(m.after); },
  active(id) { const m = MISSIONS.find(x => x.id === id); return m && this.unlocked(m) && !this.doneIds().includes(id); },
  list() { return MISSIONS.filter(m => this.unlocked(m) && !this.doneIds().includes(m.id)); },
  check(quiet = false) {
    if (this.busy || !S.name) return; this.busy = true;
    let changed = true, finished = [];
    while (changed) {
      changed = false;
      for (const m of MISSIONS) {
        if (this.doneIds().includes(m.id) || !this.unlocked(m) || !this.isComplete(m)) continue;
        this.doneIds().push(m.id); finished.push(m); changed = true;
      }
    }
    this.busy = false;
    if (finished.length) {
      save();
      finished.forEach((m, k) => setTimeout(() => {
        if (!quiet) { toast(`📜 Mission complete: ${m.title}! +${m.reward} XP`, 'lvl'); Sfx.play('level'); confetti(40, innerWidth - 160, 140); }
        addXP(m.reward);
      }, k * 900));
      const fresh = this.list().filter(m => finished.some(f => f.id === m.after));
      if (!quiet) fresh.slice(0, 3).forEach((m, k) => setTimeout(() => toast(`📜 New mission: ${m.icon} ${m.title}`), finished.length * 900 + k * 700));
    }
    this.renderTracker();
    if (typeof World !== 'undefined' && World.ready) World.refreshMissionItems();
  },
  renderTracker() {
    let t = $('#missionTracker');
    if (!t) { t = document.createElement('div'); t.id = 'missionTracker'; $('#hud').appendChild(t); t.onclick = () => this.open(); }
    const act = this.list().sort((a, b) => (b.story ? 1 : 0) - (a.story ? 1 : 0)).slice(0, 3);
    t.innerHTML = `<div class="mt-head">📜 Missions <small>${this.doneIds().length}/${MISSIONS.length}</small></div>` + (act.length ? act.map(m => {
      const pr = m.progress ? m.progress() : null;
      return `<div class="mt-item"><b>${m.icon} ${m.title}</b>${pr ? `<div class="mt-bar"><i style="width:${Math.min(100, pr[0] / pr[1] * 100)}%"></i></div><small>${Math.min(pr[0], pr[1])} / ${pr[1]}</small>` : ''}</div>`;
    }).join('') : '<div class="mt-item">All missions complete! 🏆</div>') + (S.carry ? `<div class="mt-carry">🎒 Carrying: ${S.carry === 'phone' ? 'Ken\'s phone 📱' : 'Rosa\'s letter ✉️'}</div>` : '');
  },
  open() {
    const row = m => {
      const done = this.doneIds().includes(m.id), un = this.unlocked(m); const pr = m.progress ? m.progress() : null;
      return `<div class="mrow ${un || done ? '' : 'locked'}"><div class="mi">${done ? '✅' : un ? m.icon : '🔒'}</div><div class="mt"><b>${m.title}${m.story ? ' <small style="color:#b07a00">★ Story</small>' : ''}</b>
        <small>${un || done ? m.desc : 'Finish more missions to unlock this one.'}</small>${pr && un && !done ? `<div class="mt-bar" style="margin-top:.3em"><i style="width:${Math.min(100, pr[0] / pr[1] * 100)}%"></i></div>` : ''}</div><div style="font-weight:900;white-space:nowrap">${done ? 'Done!' : '+' + m.reward + ' XP'}</div></div>`;
    };
    const act = MISSIONS.filter(m => this.unlocked(m) && !this.doneIds().includes(m.id)), done = MISSIONS.filter(m => this.doneIds().includes(m.id)), locked = MISSIONS.filter(m => !this.unlocked(m) && !this.doneIds().includes(m.id));
    Modal.show(`<h2>📜 Missions</h2><p>Complete missions to earn bonus XP. ★ Story missions tell the tale of Tech Town!</p>
      <h3>Active</h3><div class="maplist">${act.map(row).join('') || '<p>None right now!</p>'}</div>
      ${done.length ? `<h3>Completed</h3><div class="maplist">${done.map(row).join('')}</div>` : ''}
      ${locked.length ? `<h3>Coming up</h3><div class="maplist">${locked.slice(0, 4).map(row).join('')}</div>` : ''}`);
  }
};
