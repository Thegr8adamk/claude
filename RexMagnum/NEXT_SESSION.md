# Handoff for the next Claude session (Rex Magnum)

Option B (cloud Unity test) is ON HOLD: env settings only reach new sessions and the owner wants to stay in the current session. Default = option A (owner tests on PC).
Owner writes to Claude in short messages; answer in Simplified Technical English with tables and pictures.
Branch: push to `claude-serene-carson-m4zf28` (`git push origin HEAD:claude-serene-carson-m4zf28`).
Brief: `RexMagnum/CLAUDE.md`. Status: `RexMagnum/README.md`. Progress log + pictures: `RexMagnum/Renders/progress/LOG.md`.

## What to do first
0. Fast check any time (no license, ~1 min): `bash RexMagnum/Tools/compile_check.sh`. Last run 2026-10-02: 0 errors
   (editor + player pass). It finds C# errors only, not builder exceptions or looks. Run it before every push.
1. Check the environment has `UNITY_LICENSE` (the owner's Unity_lic.ulf text) and network access to the Unity hosts
   listed in `Tools/unity_cloud_test.sh`. Never print the license.
2. Run `bash RexMagnum/Tools/unity_cloud_test.sh` (long: editor download ~3 GB, first import 20-40 min). Run it in the
   background and watch `/tmp/unity/*.log`.
3. Fix every `error CS` and builder exception in `Unity/Assets/RexMagnum/Scripts`, re-run, repeat until clean.
   Package versions in the script may need adjusting to what the downloaded editor ships with.
4. Commit + push. Tell the owner the result. The game still needs the owner's PC test for looks + play
   (no graphics card in the cloud): steps in `RexMagnum/TESTING.md`.

## Blocked on 2026-10-02 (session 2)
The cloud environment had no `UNITY_LICENSE` and its network policy blocked all Unity sites (403). The owner must add
both in the environment settings (Edit > environment variables + allowed domains), then start a new session.

## State (2026-10-02)
All 8 build steps exist (Rex, blobs, weapons, island, room 304 + first playable, all zones incl. hotel interiors,
19 guests + outfit copies, golf + cart, day/night, blob factory, cave tunnels, King Glorp + ending, accomplishments,
album, pause menu, settings, keyboard/controller/touch remap, save). Nothing has run in real Unity yet. All scripts pass `compile_check.sh` (0 errors).
Old routine "Rex Magnum hourly build" stays off. New routine "Rex Magnum hourly build (session 2)" is ON (owner asked:
"keep building without me telling you to"); it wakes the session hourly at :31.

## Tools that work in the cloud (session 2)
| Tool | How |
|---|---|
| C# compile check | `bash RexMagnum/Tools/compile_check.sh` (installs dotnet-sdk-8.0 from apt if missing) |
| Blender 5.0 | `pip install bpy pillow` (Python 3.11), then MPFB: `git clone --depth 1 https://github.com/makehumancommunity/mpfb2` and copy `src/mpfb` to `~/.config/blender/5.0/extensions/user_default/mpfb` |
| Guests | `cd RexMagnum/Blender && python guests.py --only Name1,Name2` (re-export; ~3 min per guest) |
| Rex swim clips | `python swim_export.py [--render out.png]` (no re-bake of Rex) |
Blender previews of Rex in extreme poses can show a shirt spike from the Smooth/Solidify modifiers; the game mesh does not.
Session 1 (Blender island work) also runs its routine "Rex Magnum hourly build" at :01. Both sessions push to this branch: always fetch + rebase before push.
