# Computer Quest: Tech Town — Project Brief

This is the running record of the project: what was asked for, the decisions made, and what exists today.
When you want to add to the game, add a new entry under **Change requests** and describe it; a future
Claude session will read this file (via `CLAUDE.md`) and pick up from here.

## The goal

A game that takes an **absolute beginner** to someone who can use a computer and a smartphone and
**feel comfortable doing it**: powering on, mouse, typing, windows and files, browsing websites, email,
Android phones, iPhones, and staying safe online. It should look and feel like a **3D RPG adventure**
and ship as **one full HTML file**.

## Decisions (answers from the design Q&A)

| Question | Decision |
|---|---|
| Main player | **Mixed / family**: balanced defaults, plus a settings menu (text size, voice speed) |
| Movement | **Arrow keys / WASD**, plus large on-screen arrow buttons for people without a keyboard. Walking is the very first lesson. |
| Narration | **Read-aloud voice with an on/off toggle** (browser text-to-speech), speed Slow / Normal / Fast |
| World theme | **Modern town** ("Tech Town") with **Maya, a friendly tech guide** |
| Saving | **Auto-save in the browser** (localStorage) with a Continue button |
| Mistakes | **Gentle hints, unlimited tries.** After 3 misses the right answer glows and a hand points at it. No lives, no failing. |
| Rewards | **Badges per chapter, XP and levels, and a printable certificate** at the end |
| Character | **Name + look** (shirt, hair color, hair style, skin tone). Typing the name is the first typing practice. |

## Change requests (in the order they were made)

1. **Graphics at least PlayStation 1 level; use Blender and Unity if needed.**
   Blender 5 (the `bpy` Python module) builds every 3D asset from a script: `blender/build_assets.py`.
   Unity was not usable: its servers are blocked from the build container, it needs a licensed login, and
   its web export is a folder of files, not a single HTML file. The game renders with three.js
   (embedded) and has an optional **"Retro PS1"** graphics mode in Settings.
2. **Make it a Project so I can add to it later.** This repository is the project: source files in `src/`,
   the Blender script, a build script, automated tests, this brief and `CLAUDE.md` for future sessions.
3. **Legend of Zelda style.** The *art style* is evoked, without any Nintendo characters, names, logos or
   music: cel-shaded town with bold outlines, lush grass and flowers, a glowing fairy-like companion that
   points the way, dark-blue/gold dialog boxes with word-by-word text, a fantasy title logo, and an
   "item get" moment (hold the new badge overhead + fanfare) after every chapter.
4. **Typing battles.** Small "Glitch" creatures wander town. Walk near one and a letter pops up in front of
   it; press that letter to attack (3 hits each). Wrong key = a gentle hint telling you which keyboard row
   to look in. There are tap buttons for touch screens, and a "Walk away" option.
5. **Typed commands for every interaction.** You type the word to act: **ENTER** (buildings),
   **TALK** (Maya and townspeople), **PICKUP** (gems, the basketball, Ken's phone),
   **USE** (benches, the fountain, the mailbox), **THROW** (basketball at the hoop).
   A command box shows the word as key caps (next letter glows, with a keyboard-row hint).
   While standing next to something, letters go to the command, so walk away with the arrow keys.
6. **More realistic, less cartoony characters.** People were re-modelled in Blender with real proportions,
   shaped faces (eyes with irises, nose, lips, eyebrows, ears), hands, shoes, and short/long hair styles.
   They use realistic (PBR) shading while the town keeps its cel-shaded look, closer to
   *Twilight Princess*. A hair-style choice was added to character creation.
7. **Missions.** A quest log (📜 button + tracker on the right) with 22 missions that unlock in sequence:
   chapter missions, story missions (**Lost & Found**: find Ken's phone; **Special Delivery**: mail
   Rosa's letter), and challenges (Glitch Hunter / Master, Gem Seeker / Treasure Hunter, Friendly
   Neighbor, Take a Break, Make a Wish, Hoop Dreams, Virus Patrol). Each gives bonus XP.
8. **Internet security, spam and malware.** New **Chapter 9: Cyber Safety HQ** (new building on a new
   4th street; City Hall moved back to become Chapter 10). See the chapter list below.

9. **Character builder rules (saved skill).** Adam's saved rules want realistic humans made from **MPFB**
   (MakeHuman in Blender) with a Mixamo/Unity rig, face shape keys, animations, and front/side approval
   pictures first. MPFB cannot be downloaded in the cloud container, so the current characters are
   temporary. The plan is in **`docs/CHARACTER_HANDOFF.md`**: build them on Adam's PC with the
   game-character-builder skill, then switch the game to skinned characters.

10. **Open in a browser from a link, and make the link shareable.** The game is published as a
    hosted page: https://claude.ai/artifact/2D8cuwX1Fwq64zeSBSJStk (private until shared from the
    page's Share menu). Build that version with `python3 tools/build.py --artifact OUT.html`: it drops the
    html/head/body wrapper (the host adds its own) and replaces the certificate's Print button with a
    "take a screenshot" note, because hosted pages cannot open the print dialog. Republish to the same
    link after every change.

11. **Print the certificate from the certificate window.** The certificate window has **🖨️ Print**,
    **📄 Save as PDF** and **🖼️ Save as picture**. In the downloaded HTML file, Print opens the browser's print
    dialog (landscape). On the hosted link, where pages cannot open the print dialog, Print makes a PDF and
    hands it over with the `downloads` capability (publish with `capabilities: {downloads: true}`); open
    the PDF and print it. The PDF is built without a library (`Game.jpegPdf`).
12. **More Glitches, and bigger ones take more hits.** 31 Glitches: Tiny (2 hits, a letter each),
    Glitch (3), Big Glitch (5 hits, type a short word each, after Typing School) and the
    **Virus King** boss (8 hits, type a safety word each, City Hall garden, after Cyber Safety HQ).
    New missions: Big Game Hunter, Glitch Legend (25), Defeat the Virus King.
13. **A longer game with more in every chapter.** About 45 new steps (`src/js/05c_lessons_more.js`):
    volume, Wi-Fi on a computer, frozen screen, charging (Ch1); hover menus, highlighting text, precise
    clicks (Ch2); question marks, phone numbers, a sentence, copy & paste (Ch3); Recycle Bin restore,
    rename, new folder, search (Ch4); bookmarks, zoom, cookie banners, online forms (Ch5); forward,
    search, star, unsubscribe (Ch6); alarms, bigger text, Maps / Notes (Ch7 and Ch8); tech-support
    scam calls, app permissions, gift-card scams, locking the screen, backups (Ch9); 4 more final
    questions (Ch10).
14. **Cars drive the correct way, and things are solid.** Cars face their driving direction and keep
    to the right. Cars, townspeople, Maya and Glitches are solid for the player and each other
    (`World.buildDynamic` + `collide`), cars stop for people and Glitches, and sidewalk props were
    moved off the walking lanes.

15. **Make all the streets connect.** Two edge roads (x = ±43) join both ends of all four streets, and the
    avenues (x = ±13) now run exactly from the first street to the last, making one connected road grid
    (`CROSS_X` in `02_world.js`). Sidewalks stop at every crossing, with crosswalks. Cars no longer vanish
    at the town edge: they drive loops through the grid (`lanePath`, `updateCars`), turn at corners,
    keep right, and stop for people, Glitches and other cars.

## Chapters (one building each)

| # | Building | Skill | Badge |
|---|---|---|---|
| 1 | Home Sweet Home | Parts of a computer, laptop vs desktop, power on, PIN sign-in, desktop, shutting down (Sleep / Restart) | ⚡ Power Starter |
| 2 | Community Center | Mouse: moving, clicking, double-click, right-click, drag & drop, scrolling, touchpad gestures | 🖱️ Mouse Master |
| 3 | Typing School | Finding letters, Spacebar, words, Backspace, Shift for capitals, Enter, @ and . in email addresses, Caps Lock | ⌨️ Key Keeper |
| 4 | The Office | Desktop icons, opening apps, typing a note, Save / file names, minimize / taskbar / maximize / close, folders, Recycle Bin, Mac differences | 🪟 Window Wrangler |
| 5 | Public Library | Browsers, address bar, padlock, links, Back, searching, spotting ads, tabs, closing a scam "virus" pop-up | 🧭 Web Explorer |
| 6 | Post Office | Email addresses, inbox, reading, replying, composing (To / Subject / body), attachments, phishing + Report spam, deleting | 📬 Mail Master |
| 7 | Green Phone Shop | Android: wake, swipe to unlock, PIN, apps, texting, Home/Back, calls, Wi-Fi, Play Store, quick settings, volume | 🤖 Android Ace |
| 8 | Silver Phone Shop | iPhone: side button, swipe up, passcode, Messages, going Home, Camera/Photos, Control Center, Wi-Fi, App Store; Android vs iPhone table | 📱 iPhone Ace |
| 9 | Cyber Safety HQ | Malware types, strong passwords (live meter), two-step codes, real updates vs fake update pop-ups, fake download buttons, dangerous attachments (.exe), Spam folder (Not spam / Block), look-alike web addresses, scam texts, antivirus scan, public Wi-Fi, "if you've been hacked" plan | 🛡️ Cyber Guardian |
| 10 | City Hall | Final review quiz (17 questions) and printable certificate | 🏆 Tech Hero |

## Also in the town

- **Maya** (the guide) in the park, 5 townspeople with tips (Rosa, Ken, Priya, Leo, June), moving cars that
  stop for you, a fairy-like companion that flies toward your next goal when you stand still, a golden beam
  and floor arrow pointing at the next building.
- **12 Data Gems** to find (PICKUP), a basketball hoop, benches, a wishing fountain.
- 🗺️ Map (teleport to any unlocked building), 🏅 Badges (with certificate), ⚙️ Settings (text size,
  voice on/off and speed, sound effects, Smooth / Retro PS1 graphics, erase progress).
- Works on phones and tablets too (touch arrow pad, tap-able command letters and on-screen keyboards).

## Ideas for later (not built yet)

- Video calling lesson (FaceTime / WhatsApp / Zoom)
- Online shopping and online banking safety walkthrough
- Social media basics and privacy settings
- Printing, photos (transfer from phone to computer), cloud backup
- A two-player "helper mode" where a family member can follow along
- Localization (Spanish, etc.)
