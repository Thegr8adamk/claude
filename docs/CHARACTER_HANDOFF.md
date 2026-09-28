# Character Handoff: Computer Quest: Tech Town

Build these characters on Adam's PC with the **game-character-builder** skill (MPFB in Blender 5.2).
This cloud container cannot download MPFB, so the characters in the game now are temporary.
They are built from primitive shapes in `blender/build_assets.py` (`c_person`).

## Style card (already known, do not ask again)

```
GAME: Computer Quest: Tech Town   (web game, three.js, one HTML file)
+--------------+---------------------------------------------------------+
| Style        | 2 Realistic (Adam asked: "more realistic, less cartoon")|
| Body         | 1 Normal human (head = 1/7 of height), 1.75 m tall      |
| Detail       | 1 Low: max 5,000 triangles each (7 people on screen,     |
|              |   old laptops and phones). Use an MPFB proxy mesh.      |
| Surface      | 2 Realistic PBR (the town around them is cel-shaded)    |
| Mood         | Bright, friendly, sunny small town                      |
| Type         | Human                                                   |
| Clothes      | Modern, casual                                          |
+--------------+---------------------------------------------------------+
```

## Character list

| Name | Role | Look |
|---|---|---|
| Player | Hero | Casual shirt + jeans. The player picks shirt color, hair color, skin tone, short or long hair. Make one male and one female base, or one neutral base. |
| Maya | Guide (main NPC) | Woman, about 35, dark brown skin, glasses, hair in a bun, purple top, dark trousers. Friendly teacher. |
| Rosa | NPC | Woman, about 60, long black hair, red top. |
| Ken | NPC | Man, about 70, brown hair, green shirt. |
| Priya | NPC | Woman, about 30, long dark hair, yellow top. |
| Leo | NPC | Man, about 25, short dark hair, teal shirt. |
| June | NPC | Woman, about 75, grey hair in a bun, pink cardigan. |

## What the game needs in each GLB

| Need | Why |
|---|---|
| Rig: `mixamo_unity` | Adam's standard. The game will play the clips with a three.js AnimationMixer. |
| Clips: `Idle`, `Walk`, `Run`, `Sit`, `Cheer` (both arms up, holding a badge), `Attack` (quick arm swing), `Wave`, `Talk` | Walk around town, sit on benches, the "item get" pose, Glitch battles, NPC chats. |
| Shape keys: `Blink_L`, `Blink_R`, `Smile`, `Mouth_A`, `Mouth_E`, `Mouth_O`, `Mouth_M` | Talking and blinking. |
| Material names: `Skin`, `Shirt`, `Pants`, `Hair`, `Shoe` | The game recolors these for the player's choices and for each NPC. |
| Hand bone name `mixamorig:RightHand` | The basketball attaches to it. |
| Front faces -Y in Blender (glTF +Z) | Same as the other town assets. |
| Compressed textures, max 1024 px | Keep the single HTML file small. |

Put the files in `blender/characters/<Name>.glb` in this repo (and the .blend, FBX and Unity prefab where the skill says).

## Game code to change after the GLBs exist

The game now animates separate body parts (`Torso`, `Head`, `ArmL`, `ArmR`, `LegL`, `LegR`) in
`src/js/02_world.js` (`makePerson`, `animatePerson`). With a skinned MPFB character:

1. In `tools/build.py`, embed each character GLB (base64), like `TOWN_GLB`.
2. In `makePerson`, clone with `THREE.SkeletonUtils.clone` (add `vendor/SkeletonUtils.js` from three r128),
   and recolor the named materials.
3. Replace `animatePerson` with an `AnimationMixer` per person: crossfade `Idle` / `Walk`, and play
   `Sit`, `Cheer`, `Attack` for the matching poses (`p.pose`).
4. Attach the ball to `mixamorig:RightHand` in `pickBall`.
5. Run both tests in `tools/tests/`, and update `docs/PROJECT_BRIEF.md`.
