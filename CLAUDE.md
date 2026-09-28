# CLAUDE.md — Computer Quest: Tech Town

An educational 3D adventure game that teaches absolute beginners to use computers, the web, email,
Android phones, iPhones, and internet safety. It ships as ONE self-contained HTML file.

**Read `docs/PROJECT_BRIEF.md` first.** It holds every requirement and decision so far. When the user
asks for a change, add it to the "Change requests" list in the brief as part of the work.

## Layout

```
computer-quest.html         BUILD OUTPUT, the deliverable (never edit by hand; regenerate)
src/shell.html              page markup + base CSS (title, character creation, HUD, dialog, lesson, modal)
src/adventure.css           adventure theme (dark/gold panels, battle panel, command box, missions tracker)
src/js/01_core.js           state + save, chapters list, levels/XP, voice (speechSynthesis), sound, HUD,
                            Dialog, Modal, LESSON ENGINE (L) and lesson helpers (keyboard, typeCheck, dbl,
                            dragger, swipe, quiz, infoCard)
src/js/02_world.js          three.js world: layout (STREETS, BLD), procedural textures, cel shading +
                            outlines, characters, NPCs, cars, Glitch enemies, items, typed-command
                            proximity, camera, static-mesh merging
src/js/03..05*_lessons_*.js lesson content, one LESSONS[id] per chapter
src/js/06_game.js           final quiz, certificate, game flow (title, creation, tutorial, typed
                            commands, battles, map/badges/settings), NPCS list
src/js/07_missions.js       MISSIONS list + Missions engine (check/tracker/modal)
blender/build_assets.py     Blender script that models every building, prop and the character
blender/town_assets.glb     its output (embedded into the HTML as base64)
vendor/                     three.js r128 + GLTFLoader (MIT)
tools/build.py              concatenates everything into computer-quest.html
tools/tests/*.test.js       Playwright end-to-end tests
```

## Commands

```bash
python3 tools/build.py                        # rebuild computer-quest.html (always after editing src/ or the .glb)
python3 blender/build_assets.py               # rebuild 3D assets (needs `pip install bpy`, Python 3.11)
node --check <(cat src/js/*.js)               # quick syntax check of all game code
PLAYWRIGHT_PATH=/opt/node22/lib/node_modules/playwright node tools/tests/lessons.test.js   # plays all 10 chapters
PLAYWRIGHT_PATH=/opt/node22/lib/node_modules/playwright node tools/tests/missions.test.js  # town + missions
```
Headless Chromium needs `--use-gl=angle --use-angle=swiftshader` (already in the tests). Software
rendering is slow (a few fps), so tests call `World.update()` directly or wait generously.
Set `window.CQ_FAST = true` to skip the pauses between lesson steps. `window.CQ` exposes internals.

## Conventions and gotchas

- All `src/js` files are concatenated into ONE classic `<script>`: top-level `const`s are shared.
  Don't reuse top-level names across files. Check for globals with `typeof World !== 'undefined'`, never
  `window.World`: `const` globals are not properties of `window`.
- Strings in lessons are HTML; `{name}` is replaced with the player's name. Escape apostrophes inside
  single-quoted strings (`don\'t`), and run the syntax check after editing text.
- **Lesson step API** (`build(stage, api, ctx)`): `api.target(el)` marks the right thing to click (clicks
  elsewhere count as a gentle "wrong"; 3 wrongs make the target glow); `api.done(msg)`, `api.wrong(msg)`,
  `api.soft(msg)` (hint, no penalty), `api.say(html)` (change instruction mid-step), `api.setTargets()`,
  `api.listen()`/`api.onKey()`/`api.after()` (auto-cleaned when the step ends). `next: true` shows a Next
  button. `ctx` persists across a chapter's steps, and `setup(stage, ctx)` runs once per chapter.
- Adding a chapter touches: `CHAPTERS` (01_core), `BLD` + `SIGNS` + marker `topY` (02_world), a
  `b_<id>` builder in the Blender script, `LESSONS.<id>`, a `chapterMission`, the tests' plan, and the brief.
- glTF textures use `flipY = false` (handled in `canvasTex` for non-repeating textures).
- Material names from Blender carry meaning (`Brick_<hex>`, `Sign_<id>`, `Glow_<hex>`, `Shirt`, ...); see
  the top of `blender/build_assets.py` and `World.prepareMaterials`.
- Static objects placed with `World.add()` are merged into one mesh per material (`mergeStatics`). Anything
  that must move or be hidden later must be created separately, not via `add()`.
- Accessibility is a core requirement: large text, read-aloud, no time pressure, no failing, touch
  alternatives for every keyboard action. Keep new content consistent with that.
- Don't use Nintendo characters, names, logos or music. The Zelda request is about art style only.
