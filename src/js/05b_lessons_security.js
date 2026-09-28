/* =====================================================================
   CHAPTER 9: CYBER SAFETY HQ — internet security, spam and malware
   ===================================================================== */
function pwScore(p) {
  const checks = { len: p.length >= 12, lower: /[a-z]/.test(p), upper: /[A-Z]/.test(p), num: /[0-9]/.test(p), sym: /[^A-Za-z0-9]/.test(p) };
  const common = /password|123456|qwerty|letmein|abc123|iloveyou/i.test(p);
  const kinds = ['lower', 'upper', 'num', 'sym'].filter(k => checks[k]).length;
  return { checks, common, strong: checks.len && kinds >= 3 && !common, kinds };
}
function secBrowser(S, ctx, url, secure, html) {
  if (!$('#dsk')) ctx.dsk = desktopSim(S, [{ id: 'bin', e: '🗑️', n: 'Recycle Bin', x: 88, y: 3 }]);
  $$('.win', ctx.dsk).forEach(w => w.remove());
  const w = openWin(ctx.dsk, { id: 'sb', title: '🌐 Web Browser', cls: 'browser max', tool: `<div class="btool"><button class="navb">←</button><button class="navb">→</button><button class="navb">⟳</button><div class="addr"><span class="lock">${secure ? '🔒' : '⚠️'}</span><input value="${esc(url)}" readonly></div></div>`, body: `<div style="position:relative;height:100%">${html}</div>` });
  return w;
}
LESSONS.security = {
  steps: [
    {
      say: 'Welcome to <b>Cyber Safety HQ</b>, {name}! Here you\'ll become a <b>Cyber Guardian</b>. First, meet the bad guys. <b>Malware</b> means “bad software”, programs made to cause harm. <b>Spam</b> is junk email. <b>Scams</b> are tricks to steal your money or passwords.', next: true,
      build: S => infoCard(S, `<h3>🦠 Know the bad guys</h3><div class="igrid">
        <div class="ibox"><div class="ie">🦠</div><b>Virus</b><small>Spreads from computer to computer and can damage files.</small></div>
        <div class="ibox"><div class="ie">🔐</div><b>Ransomware</b><small>Locks your files and demands money. Backups protect you!</small></div>
        <div class="ibox"><div class="ie">🕵️</div><b>Spyware</b><small>Secretly watches what you type, like passwords.</small></div>
        <div class="ibox"><div class="ie">🎣</div><b>Phishing</b><small>Fake emails, texts or sites that trick you into giving away information.</small></div>
        <div class="ibox"><div class="ie">🗑️</div><b>Spam</b><small>Junk email you didn't ask for. Often full of scams.</small></div></div>`)
    },
    {
      say: 'Your first shield is a <b>strong password</b>. The best trick: join <b>3 random words</b> plus a number and a symbol, like <b>Purple-Tiger-Rain-42!</b> Make up your own practice password in the box until the meter says <b>STRONG</b>. (Don\'t type a real password you use!)', hint: 'Make it at least 12 characters long, with a capital letter, a number, and a symbol like ! or -. Example: Blue-Cactus-Moon-7!',
      build: (S, api) => {
        S.innerHTML = `<div class="stage-inner typebox"><div class="icard neutral" style="max-width:640px"><h3>🔑 Password strength meter</h3>
          <input class="typein" id="pw" autocomplete="off" spellcheck="false" placeholder="Type a practice password" style="width:100%">
          <div style="height:16px;background:#e7ecf3;border-radius:99px;overflow:hidden;margin:.6em 0"><i id="pwBar" style="display:block;height:100%;width:0;transition:width .3s,background .3s"></i></div>
          <div id="pwLbl" style="font-weight:900">Start typing…</div><div id="pwList" style="font-size:.9em;margin-top:.4em"></div></div><div id="kbSlot"></div></div>`;
        const inp = $('#pw'); const kb = makeKeyboard(api, { input: inp, zones: true }); $('#kbSlot').appendChild(kb.el);
        inp.focus(); api.onKey(e => { if (document.activeElement !== inp && e.key.length === 1) inp.focus(); });
        const upd = () => {
          const r = pwScore(inp.value), c = r.checks; const pts = (c.len ? 2 : Math.min(1.5, inp.value.length / 8)) + r.kinds * .5 - (r.common ? 2 : 0);
          const pct = Math.max(0, Math.min(100, pts / 4 * 100));
          $('#pwBar').style.width = pct + '%'; $('#pwBar').style.background = r.strong ? '#23a55a' : pct > 50 ? '#f5b700' : '#e5484d';
          $('#pwLbl').textContent = r.strong ? '💪 STRONG!' : pct > 50 ? '😐 Okay, but not strong yet' : '😟 Weak';
          const it = (ok, t) => `<div>${ok ? '✅' : '⬜'} ${t}</div>`;
          $('#pwList').innerHTML = it(c.len, 'At least 12 characters') + it(c.upper && c.lower, 'Capital and small letters') + it(c.num, 'A number') + it(c.sym, 'A symbol like ! - or #') + (r.common ? '<div>❌ Contains a very common password. Hackers guess those first!</div>' : '');
          if (r.strong) api.done('Super strong! Use a different password for every account. A password manager app can remember them for you.');
        };
        api.listen(inp, 'input', upd); upd();
      }
    },
    { say: 'Quick check!', build: (S, api) => quiz(S, api, { q: 'Which password is the <b>strongest</b>?', emoji: '🔑', opts: ['password123', 'Fluffy', 'Purple-Tiger-Rain-42!', '12345678'], correct: 2, why: { 0: '“password123” is one of the most common passwords. Hackers try it first!', 1: 'A pet\'s name is short and easy to guess.', 3: 'Only numbers, and a very common pattern. Too easy to guess!' } }) },
    {
      say: '<b>Two-step verification</b> adds a second lock. After your password, the website texts a <b>code</b> to your phone. Read the code on the phone and <b>type it in the box</b> on the bank website.', hint: 'Look at the text message on the little phone on the right. Type those 6 numbers in the box, then press Enter.',
      build: (S, api) => {
        const code = String(100000 + Math.floor(Math.random() * 899999));
        S.innerHTML = `<div class="stage-inner"><div style="display:flex;gap:26px;align-items:center;flex-wrap:wrap;justify-content:center">
          <div class="icard neutral" style="max-width:380px"><h3>🏦 TownBank · Sign in</h3><p>✅ Password correct.<br>We sent a 6-digit code to your phone.</p><input class="typein" id="code2" inputmode="numeric" maxlength="6" placeholder="Enter code" style="width:100%;letter-spacing:.3em"><div style="font-size:.85em;color:#5b6b7c;margin-top:.5em">Press Enter when you're done.</div></div>
          <div style="width:220px;height:400px;background:#15171a;border-radius:32px;padding:10px"><div style="background:#f2f2f7;height:100%;border-radius:24px;padding:40px 10px 10px;font-size:.85em;color:#111">
            <div style="text-align:center;font-weight:800;margin-bottom:8px">Messages</div><div class="pb them" style="max-width:100%;background:#e9ecef;border-radius:14px;padding:8px">TownBank: Your sign-in code is <b style="font-size:1.2em">${code.slice(0, 3)} ${code.slice(3)}</b>. <b>Never share this code.</b> TownBank will never call you to ask for it.</div></div></div></div></div>`;
        typeCheck(api, $('#code2'), code, { enter: true, mismatch: 'That doesn\'t match the code on the phone. Press ⌫ Backspace and try again.', onOk: () => api.done('Signed in! Even if a hacker steals your password, they can\'t get in without your phone. If anyone CALLS asking for this code, it\'s a scam. Hang up!') });
      }
    },
    {
      say: '<b>Updates</b> fix security holes, so install them! Your computer shows a real update from <b>Settings</b>. <b>Click “Install now”.</b>', hint: 'Click the blue “Install now” button in the Windows Update window.',
      build: (S, api, ctx) => {
        ctx.dsk = desktopSim(S, [{ id: 'bin', e: '🗑️', n: 'Recycle Bin', x: 88, y: 3 }]);
        const w = openWin(ctx.dsk, { id: 'upd', title: '⚙️ Settings › Windows Update', body: `<div class="page"><h2 style="margin:.2em 0">🔄 Updates are available</h2><p>Security update for your computer<br><small>Keeps your computer protected from new viruses.</small></p><div style="display:flex;gap:10px"><button class="btn" id="updNow">Install now</button><button class="btn ghost" id="updLater">Remind me later</button></div><div id="updProg" style="margin-top:12px"></div></div>` });
        w.style.left = '18%'; w.style.width = '60%'; w.style.height = '60%';
        api.target($('#updNow'));
        api.listen($('#updLater'), 'click', () => api.wrong('Putting it off leaves the door open for viruses. Install security updates soon. Click “Install now”.'));
        api.listen($('#updNow'), 'click', () => { api.setTargets(); $('#updProg').innerHTML = '<div class="progress" style="width:100%"><i id="upi"></i></div><small>Installing…</small>'; requestAnimationFrame(() => requestAnimationFrame(() => { $('#upi').style.width = '100%'; })); api.after(2100, () => { $('#updProg').innerHTML = '✅ Your computer is up to date!'; api.done('Up to date! Turn on automatic updates so this happens by itself.'); }); });
      }
    },
    {
      say: 'Now you\'re browsing a recipe site and a <b>pop-up</b> says you must update your video player. Real updates come from <b>Settings</b>, never from a pop-up on a website. This is <b>malware</b> in disguise! <b>Close the pop-up with its ✕.</b>', hint: 'Click the small ✕ in the corner of the yellow pop-up box. Don\'t click the green button!',
      build: (S, api, ctx) => {
        secBrowser(S, ctx, 'https://www.tastyrecipes-daily.com/cookies', true, `<div class="page"><h1>🍪 Grandma's Cookies</h1><p>Watch our video to learn the secret recipe!</p><div style="background:#000;height:120px;border-radius:10px;display:grid;place-items:center;color:#fff">▶ Video</div></div>
          <div class="dlg" id="fakePop" style="background:#fff8d6;border:3px solid #e5a000;max-width:420px"><button id="fakeX" style="position:absolute;right:8px;top:6px;border:none;background:transparent;font-size:1.1em;font-weight:900;cursor:pointer">✕</button><h4>⚠️ Your Video Player is OUT OF DATE!</h4><p>You must update to watch this video. Your computer may be at risk!</p><div class="row"><button class="pri" id="fakeDl" style="background:#23a55a;border-color:#23a55a">⬇ UPDATE NOW (free)</button></div></div>`);
        api.target($('#fakeX'));
        api.listen($('#fakeDl'), 'click', () => api.wrong('Good thing this is practice! That button would download MALWARE. Websites can\'t update your computer. Close the pop-up with the ✕.'));
        api.listen($('#fakeX'), 'click', () => { $('#fakePop').remove(); api.done('Pop-up closed safely! If a pop-up won\'t close, close the whole browser, or restart the computer.'); });
      }
    },
    {
      say: 'Downloading a file? Watch out for <b>fake download buttons</b>. They\'re ads that install malware. This page has 3 big shiny buttons but only one real link. <b>Click the real link: “recipe-book.pdf”</b>.', hint: 'The real download is the small plain blue link that says recipe-book.pdf, under “Your file”. The big flashy buttons are ads.',
      build: (S, api, ctx) => {
        secBrowser(S, ctx, 'https://www.townlibrary.org/downloads', true, `<div class="page"><h1>📚 Free Recipe Book</h1>
          <div style="display:flex;gap:10px;flex-wrap:wrap;margin:.6em 0"><button class="fakedl" style="background:#23a55a;color:#fff;border:none;border-radius:8px;padding:.7em 1.4em;font-weight:900;font-size:1.1em">⬇ DOWNLOAD NOW</button><button class="fakedl" style="background:#e5484d;color:#fff;border:none;border-radius:8px;padding:.7em 1.4em;font-weight:900">▶ START DOWNLOAD</button><button class="fakedl" style="background:#2d7ff9;color:#fff;border:none;border-radius:8px;padding:.7em 1.4em;font-weight:900">⚡ FAST DOWNLOAD</button><span style="font-size:.7em;color:#888;align-self:flex-start">Ad</span></div>
          <p><b>Your file:</b> <span class="link" id="realDl">recipe-book.pdf</span> <small>(2 MB, from townlibrary.org)</small></p></div>`);
        api.target($('#realDl'));
        $$('.fakedl').forEach(b => api.listen(b, 'click', () => api.wrong('That big button is an AD. Fake download buttons often install malware. Look for the plain link with the file\'s real name.')));
        api.listen($('#realDl'), 'click', () => api.done('You found the real download! Tip: only download from websites you trust, and be very careful with files ending in .exe.'));
      }
    },
    { say: 'Attachments can hide malware too. Look closely at the <b>end</b> of each file name, called the <b>extension</b>.', build: (S, api) => quiz(S, api, { q: 'You get an email from someone you don\'t know. Which attachment is the <b>most dangerous</b> to open?', emoji: '📎', opts: ['photo.jpg', 'invoice.pdf.exe', 'recipe.pdf', 'letter.docx'], correct: 1, why: { right: 'Right! “.exe” means it\'s a PROGRAM. It\'s pretending to be a PDF. Never open surprise .exe, .zip or .scr files.', 0: 'A .jpg is a picture. Still be careful with strangers, but look for a sneakier one!', 2: 'A .pdf is a document. Look for a file with TWO endings…', 3: 'A .docx is a Word document. Look for a file with TWO endings…' } }) },
    {
      say: 'Good emails sometimes land in the <b>Spam</b> folder by mistake, so it\'s worth checking. <b>Click the Spam folder 🚫</b>.', hint: 'The Spam folder is on the left side of the mail window.',
      build: (S, api, ctx) => {
        mailSim(S, ctx);
        ctx.emails.push({ id: 'dentist', from: 'Smile Dental Clinic', addr: 'office@smiledental.com', subj: 'Your appointment is Tuesday at 10am', body: 'Hello! This is a reminder of your dental check-up on Tuesday at 10am. See you then!', unread: true, folder: 'spam', time: 'Mon' });
        ctx.emails.push({ id: 'prize', from: 'WINNER NOTIFICATION', addr: 'claim@prize-winner-now.biz', subj: '🎉 YOU WON $1,000,000!!! Claim NOW', body: 'Congratulations!!! Send your bank details and a $50 fee to claim your prize.', unread: true, folder: 'spam', time: 'Sun' });
        ctx.renderList(); api.target($('.mside [data-f="spam"]'));
        ctx.hook = (ev, d) => { if (ev === 'folder' && d === 'spam') api.done('quiet'); }; api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: 'The dentist reminder is real, but it landed in Spam by mistake! Open <b>Smile Dental Clinic</b>\'s email, then click <b>✅ Not spam</b> to move it back to your inbox.', hint: 'Click the email from Smile Dental Clinic. Then click the “Not spam” button above it.',
      build: (S, api, ctx) => {
        api.target($('.mitem[data-id="dentist"]'));
        ctx.hook = (ev, d) => {
          if (ev === 'open' && d === 'dentist') {
            const bar = $('.mact'); bar.innerHTML = '<button id="notSpam">✅ Not spam</button><button id="mDel2">🗑️ Delete</button>';
            api.say('Now click <b>✅ Not spam</b>.'); api.setTargets($('#notSpam'));
            $('#notSpam').onclick = () => { ctx.emails.find(e => e.id === 'dentist').folder = 'inbox'; ctx.sel = null; ctx.renderList(); $('#mRead').innerHTML = '<div style="color:#23a55a;margin-top:30%;text-align:center;font-weight:900">✅ Moved to your Inbox</div>'; api.done('Rescued! Now your email knows the dentist is a real sender.'); };
            $('#mDel2').onclick = () => api.wrong('This one is a real appointment reminder. Click “Not spam” to keep it.');
          }
          if (ev === 'open' && d === 'prize') api.soft('That one IS spam. “You won a million dollars” is a classic scam! Find the dentist email instead.');
        };
        api.cleanup(() => ctx.hook = null);
      }
    },
    {
      say: 'Now the prize email: <b>“YOU WON $1,000,000!!!”</b>. It\'s spam. Open it and click <b>⛔ Block sender</b> so they can\'t email you again.', hint: 'Click the “WINNER NOTIFICATION” email, then click Block sender.',
      build: (S, api, ctx) => {
        api.target($('.mitem[data-id="prize"]'));
        ctx.hook = (ev, d) => {
          if (ev === 'open' && d === 'prize') {
            $('.mact').innerHTML = '<button id="blockB">⛔ Block sender</button><button id="replyP">↩️ Reply</button>';
            api.say('Now click <b>⛔ Block sender</b>.'); api.setTargets($('#blockB'));
            $('#blockB').onclick = () => { ctx.emails.find(e => e.id === 'prize').folder = 'trash'; ctx.sel = null; ctx.renderList(); $('#mRead').innerHTML = '<div style="color:#23a55a;margin-top:30%;text-align:center;font-weight:900">⛔ Sender blocked</div>'; api.done('Blocked! Remember: you can\'t win a contest you never entered.'); };
            $('#replyP').onclick = () => api.wrong('Never reply to spam. It tells scammers your email address is real. Block them instead.');
          }
        };
        api.cleanup(() => ctx.hook = null);
      }
    },
    { say: 'Scammers make <b>fake websites</b> with addresses that look almost right. Read carefully!', build: (S, api) => quiz(S, api, { q: 'Your bank is <b>TownBank</b>. Which web address is the <b>real</b> one?', emoji: '🏦', opts: ['www.t0wnbank-login.co', 'townbank.secure-verify.net', 'www.townbank.com', 'www.townbank.com.account-check.biz'], correct: 2, why: { right: 'Correct! Look at the end of the name, right before the first “/”. That\'s the real owner.', 0: 'Look closely: that\'s a ZERO instead of an “o”, and it ends in .co. Fake!', 1: 'The real owner here is “secure-verify.net”, not TownBank. Fake!', 3: 'Sneaky! The real owner is at the END: “account-check.biz”. Fake!' } }) },
    {
      say: 'Scams come by <b>text message</b> too. A text says your package is stuck and asks you to pay a small fee with a link. What should you do? <b>Click the safest choice.</b>', hint: 'Delivery companies don\'t text you strange links asking for money. Choose “Delete & report junk”.',
      build: (S, api) => {
        S.innerHTML = `<div class="stage-inner"><div style="display:flex;gap:26px;align-items:center;flex-wrap:wrap;justify-content:center">
          <div style="width:230px;height:420px;background:#15171a;border-radius:32px;padding:10px"><div style="background:#f2f2f7;height:100%;border-radius:24px;padding:40px 10px 10px;font-size:.85em;color:#111">
            <div style="text-align:center;font-weight:800;margin-bottom:8px">+1 (555) 019-4412</div><div style="background:#e9ecef;border-radius:14px;padding:8px">📦 PARCEL-SERVICE: Your package is ON HOLD. Pay a $1.99 fee within 24h or it will be returned: <span style="color:#1a5fd0;text-decoration:underline">http://parcel-fee-pay.co/x7</span></div></div></div>
          <div style="display:flex;flex-direction:column;gap:12px;min-width:260px"><button class="qopt" id="smClick">🔗 Tap the link and pay $1.99</button><button class="qopt" id="smReply">💬 Reply “STOP”</button><button class="qopt" id="smDel">🗑️ Delete & report junk</button></div></div></div>`;
        api.target($('#smDel'));
        api.listen($('#smClick'), 'click', () => api.wrong('Never tap links in surprise texts! The fee is a trick to steal your card number. If you\'re expecting a package, check the shipper\'s real app or website yourself.'));
        api.listen($('#smReply'), 'click', () => api.wrong('Replying tells scammers your number works. Don\'t reply. Delete it and report it as junk.'));
        api.listen($('#smDel'), 'click', () => { $('#smDel').classList.add('right'); api.done('Exactly! On most phones you can tap “Report junk”. Then delete the text.'); });
      }
    },
    {
      say: 'Your computer has a built-in <b>antivirus</b> that hunts for malware. On Windows it\'s called <b>Windows Security</b>. Let\'s run a check: <b>click “Quick scan”</b>.', hint: 'Click the blue Quick scan button.',
      build: (S, api, ctx) => {
        ctx.dsk = desktopSim(S, [{ id: 'bin', e: '🗑️', n: 'Recycle Bin', x: 88, y: 3 }]);
        const w = openWin(ctx.dsk, { id: 'av', title: '🛡️ Windows Security', body: `<div class="page"><h2 style="margin:.2em 0">🛡️ Virus & threat protection</h2><p>Last scan: 12 days ago</p><button class="btn" id="scanBtn">Quick scan</button><div id="scanOut" style="margin-top:14px"></div></div>` });
        w.style.left = '18%'; w.style.width = '60%'; w.style.height = '64%';
        api.target($('#scanBtn'));
        api.listen($('#scanBtn'), 'click', () => {
          api.setTargets(); let n = 0; const out = $('#scanOut');
          const t = setInterval(() => { n += 7; if (!out.isConnected) return clearInterval(t); out.innerHTML = `Scanning… ${Math.min(100, n)}%<div class="progress" style="width:100%;margin-top:6px"><i style="width:${Math.min(100, n)}%;transition:none"></i></div><small>${n * 413} files checked</small>`; if (n >= 100) { clearInterval(t); out.innerHTML = '✅ <b>No current threats.</b> Your computer is clean!'; api.done('All clean! Keep your antivirus turned on. On a Mac, security is built in too. Just keep it updated.'); } }, 120);
          api.cleanup(() => clearInterval(t));
        });
      }
    },
    { say: 'One more: <b>free public Wi-Fi</b> in cafes, airports and libraries is handy, but strangers can share it.', build: (S, api) => quiz(S, api, { q: 'You\'re on a coffee shop\'s <b>free Wi-Fi</b>. Which is the <b>safest</b> thing to do?', emoji: '☕', opts: ['Log in to online banking', 'Check the weather forecast', 'Type your credit card number', 'Change your email password'], correct: 1, why: { right: 'Right! Save banking and shopping for your home Wi-Fi or your phone\'s mobile data.', 0: 'Better to wait for your home Wi-Fi or use mobile data for banking.', 2: 'Wait until you\'re on a network you trust before entering card numbers.', 3: 'Change passwords at home on your own trusted network.' } }) },
    {
      say: 'You\'re a <b>Cyber Guardian</b>! Here\'s what to do if something ever goes wrong. Click <b>Finish chapter</b>.', next: 'Finish chapter ▶',
      build: S => infoCard(S, `<h3>🚨 Uh-oh plan: if you think you've been hacked or scammed</h3><div class="igrid">
        <div class="ibox"><div class="ie">🔌</div><b>1. Disconnect</b><small>Turn off Wi-Fi so the malware can't send your info.</small></div>
        <div class="ibox"><div class="ie">🛡️</div><b>2. Scan</b><small>Run your antivirus (Windows Security).</small></div>
        <div class="ibox"><div class="ie">🔑</div><b>3. Change passwords</b><small>From a different, safe device. Start with email and bank.</small></div>
        <div class="ibox"><div class="ie">🏦</div><b>4. Call your bank</b><small>Use the number on the back of your card if money is involved.</small></div>
        <div class="ibox"><div class="ie">💾</div><b>Back up</b><small>Keep copies of photos and files on a USB drive or cloud storage.</small></div>
        <div class="ibox"><div class="ie">🤝</div><b>Ask for help</b><small>Talk to a trusted friend or family member. No shame. Scams fool everyone!</small></div></div>`)
    }
  ]
};
