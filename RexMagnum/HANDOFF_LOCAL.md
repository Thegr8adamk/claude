# Handoff: cloud session -> local session on the owner's PC (3 Oct 2026)

Read this first, then `CLAUDE.md` (the game brief and how to talk to the owner).

## Why we moved
The cloud session had no Unity and no browser. It could only compile-check C# and hand the owner long
build-and-report steps. You run on the owner's Windows PC: you can run Unity (batch mode) and Blender
yourself, read the logs, look at screenshots, and fix things in one place. Do the work yourself; ask the
owner only for real decisions. Keep replies short (owner's weekly token budget matters).

## Owner and rules
- Hobbyist, no code. Write in Simplified Technical English (short sentences, active voice), with tables and pictures.
- Never ask for tokens/passwords in chat. Never create accounts for the owner.
- Commit with clear messages; branch `claude-serene-carson-m4zf28` holds all work (repo thegr8adamk/claude).
- No model names in commits or files.
- Blender files are Blender 5.0. Do not save them with an older Blender.

## Where things are
| What | Path |
|---|---|
| Unity project (Assets only in git) | `RexMagnum/Unity/Assets/RexMagnum` (owner's live project: `C:\Users\adam8\Games\Rex Magnum`; check the folder) |
| Editor menus | `Scripts/Editor`: ProjectSetup, RexBuilder, BlobBuilder, GuestBuilder, IslandBuilder, BuildTools (Win .exe, Android .apk, Web) |
| Runtime code | `Scripts/Runtime` (GameFlow, RexController, RexWeapons, QualityAuto, ZoneStreamer, ...) |
| Blender sources + export scripts | `RexMagnum/Blender` (`island_export.py`, `rex_export.py`, `blob_rig.py`, `weapons_bake.py`, `island_hq.py`) |
| Browser mini-game (three.js, one file) | `RexMagnum/Web/rex-magnum.html`, published at https://claude.ai/artifact/4JmP3hWFb4zVdfqc3RR9c1 |
| Progress pictures + log | `RexMagnum/Renders/progress/` (+ `LOG.md`) |
| Owner test reports | ROUND3..ROUND6 reports were uploads; key facts are below |

## Build order inside Unity (menu "Rex Magnum")
Setup Project -> Build Blobs -> Build Guests -> Build Weapons -> Build Rex + Test Scene -> Build Island ->
(optional) Make Windows Game (.exe) / Make Android Game (.apk) / Make Web Game (HTML) / Play Web Game (last build).
Run them in batch mode yourself, e.g.:
`"<Unity.exe>" -batchmode -quit -projectPath "<project>" -executeMethod RexMagnum.EditorTools.BuildTools.Web -logFile build.log`
(check the method names in the Editor scripts; builders are static menu methods).

## State (all at 8.5+ in code reviews; real-Unity tests below)
- PC game in the Unity editor: 0 red errors, all 9 walk checks PASS, "FP arms hands on screen: PASS",
  NavMesh warnings gone, Room 304 correct.
- First-person arms (Unity): fixed in code (bone anchors in Rex_FPArms.fbx, guard IK). Owner has not
  confirmed by eye yet. The arms import may fall back to Generic ("13/24 clips mapped" warning).

## Open problems (do these first)
1. **Web build draws no 3D world** (only skybox + HUD). Facts from the browser console:
   `Web scene: renderer Mobile_Renderer (Forward), camera RexCamera at 18,12.4,-293 far 2500 near 0.03 mask 0xFFFFFFFF,
   renderers within 40 m: 362, enabled 361, drawn 68; lodBias 0.50, maxLOD 0`. ~52 fps.
   Forward+ was ruled out. Latest attempt (not yet tested): web runtime render scale 1, no depth texture, no HDR;
   web build turns off depth priming and renderer features. Diagnose for real now: build Web, open it in a
   browser with devtools, check for WebGL/shader errors; press **F8** in the web game (swaps lit materials to
   unlit; if the world appears, the lit materials/shaders are the cause). Also try a tiny test scene with one
   cube in a WebGL build to see if URP Lit draws at all.
2. `screen.orientation.lock()` error on PC browsers: the web build now uses AutoRotation; confirm it is gone.
3. Pointer lock in the web game: confirm by hand in a normal Chrome tab.
4. Graphics quality in the real game: the owner wants the graphics right. Look at Room 304, the
   first-person arms, the pool and the island in the editor and in builds, and fix what looks wrong
   (Blender models + Unity materials/lighting).
5. Web build size ~186 MB; itch.io limit ~200 MB per file. Builds also write `Builds/RexMagnum_Web.zip`.

## Paused
- The hourly cloud routine (trig_019tiZYoU4ZonWjxtEiGtWze) is disabled. Do not turn it on unless asked.
