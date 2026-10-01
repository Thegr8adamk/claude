# Handoff for the next Claude session (Rex Magnum)

The owner chose **option B**: run Unity in the cloud to find build errors before the PC test.
Owner writes to Claude in short messages; answer in Simplified Technical English with tables and pictures.
Branch: push to `claude-serene-carson-m4zf28` (`git push origin HEAD:claude-serene-carson-m4zf28`).
Brief: `RexMagnum/CLAUDE.md`. Status: `RexMagnum/README.md`. Progress log + pictures: `RexMagnum/Renders/progress/LOG.md`.

## What to do first
1. Check the environment has `UNITY_LICENSE` (the owner's Unity_lic.ulf text) and network access to the Unity hosts
   listed in `Tools/unity_cloud_test.sh`. Never print the license.
2. Run `bash RexMagnum/Tools/unity_cloud_test.sh` (long: editor download ~3 GB, first import 20-40 min). Run it in the
   background and watch `/tmp/unity/*.log`.
3. Fix every `error CS` and builder exception in `Unity/Assets/RexMagnum/Scripts`, re-run, repeat until clean.
   Package versions in the script may need adjusting to what the downloaded editor ships with.
4. Commit + push. Tell the owner the result. The game still needs the owner's PC test for looks + play
   (no graphics card in the cloud): steps in `RexMagnum/TESTING.md`.

## State (2026-10-02)
All 8 build steps exist (Rex, blobs, weapons, island, room 304 + first playable, all zones incl. hotel interiors,
19 guests + outfit copies, golf + cart, day/night, blob factory, cave tunnels, King Glorp + ending, accomplishments,
album, pause menu, settings, keyboard/controller/touch remap, save). Nothing has run in real Unity yet.
The hourly routine "Rex Magnum hourly build" is paused (owner request). Ask before turning it on again.
