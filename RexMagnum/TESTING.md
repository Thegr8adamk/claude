# How to test Rex Magnum on your PC

## 1. Get the files (5 minutes)
1. Open this page in your browser: https://github.com/Thegr8adamk/claude/tree/claude-serene-carson-m4zf28
2. Click the green **Code** button > **Download ZIP**. (The ZIP is large: the island and characters are inside.)
3. Unzip it. Inside, find the folder `RexMagnum/Unity/Assets/RexMagnum`.

## 2. Make the Unity project (10 minutes, one time)
1. Open **Unity Hub** > **New project**.
2. Pick **Unity 6** and the template **Universal 3D** (URP).
3. Name: `Rex Magnum`. Location: `C:\Users\adam8\Games`. Click **Create project**.
4. Close Unity when it is open.
5. Copy the folder `RexMagnum` (from step 1) into `C:\Users\adam8\Games\Rex Magnum\Assets\`.
   You get: `...\Rex Magnum\Assets\RexMagnum\Scripts`, `...\Island`, `...\Characters`, and so on.
6. Open the project again in Unity Hub. The first import takes 10–30 minutes (many textures). Do not close Unity.

## 3. Setup (automatic)
- A window "Rex Magnum setup" opens. Click **OK**. If it asks to restart, click **Restart**.
- If it says the **Input System** is missing: Window > Package Manager > Unity Registry > Input System > Install.

## 4. Build the island and play (in this order)
1. Menu **Rex Magnum > Setup Project**.
2. Menu **Rex Magnum > Build Rex + Test Scene**. Wait. (This also makes the blob and weapon prefabs the island needs.)
3. Menu **Rex Magnum > Build Island**. Wait (a progress bar shows; 5–15 minutes).
4. Open `Assets/RexMagnum/Scenes/Island.unity`. Press **Play**.

## Getting a newer version later
Download the ZIP again. Close Unity. Delete the old `Assets\RexMagnum` folder, copy in the new one, open Unity, then do step 4.3 and 4.4 again.

### Fixes from your bug report + new things to try (2026-10-02, second test)
**Important:** the sound files are now `.ogg` (they were `.mp3`). Delete the old `Assets\RexMagnum` folder first.
If you keep old `.mp3` files next to the new `.ogg` files, Unity finds two files with one name.
Also delete `Assets\New Shader Graph.shadergraph` (our scripts did not make it; it is safe to delete).

| Your bug | What I changed | What you should see |
|---|---|---|
| 1, 2: giant slanted walls in Room 304 and on the balcony | Each building had two LOD groups, so the low-detail copies showed inside the room. The builder now removes the FBX's own LOD group. Mesh compression is off. | A normal square room. A small balcony with a rail. |
| 3: 26 "registered with more than one LODGroup" warnings | Same fix | 0 of these warnings |
| 4, 8: rig errors, no arms in first person | The arms copy Rex's avatar. The swim file has the right "Rex_Rig" parent now. | 0 "Rig Error" lines. Press **V**: arms and the held weapon show and punch. |
| 5: obsolete `FindFirstObjectByType` | Changed to `FindAnyObjectByType` everywhere | No CS0618 warning |
| 7: subtitles change very fast | Crowd screams show only when no other line is on screen. Each line stays as long as its voice clip. | Lines are readable |
| 7: hint text small, help text hard to read | Bigger hint box with a dark back. All HUD text has a black outline. | Easy to read on bright sky |
| 7: "Whip" vs "Punch" | Correct: Rex starts with fists ("Punch"). "Whip" is the towel. A new label above the action word shows the weapon name. | FISTS / BEER BOTTLE / BEACH TOWEL label |
| 9: white glare on the wall | Bedside lamps, ceiling and TV lights are softer | No white spot |
| 10: bottle pick-up | Pick-up range ignores height (bottle is on the table). "GOT: BEER BOTTLE" shows. The hints now wait for you to do each step. | Label changes to SWING; Tab shows the bottle |
| Camera inside Rex's head at a wall | Your fix, built in: body hides (shadow stays) when the camera is closer than 0.9 m | Clean view at walls |

| New | How to try it |
|---|---|
| Title screen | A picture of the hotel and the beach. **PLAY**, **SETTINGS**, **QUIT**. Mouse, arrow keys + Enter, or controller (D-pad / stick + A). |
| Settings (title screen and pause menu) | 4 pages: **GRAPHICS** (quality, render scale, frame limit, V-sync, full screen, field of view), **CAMERA & VR** (third / first person, head bob, look speeds, invert, kill cam, VR mode), **SOUND** (master, music, voices, effects, subtitles), **CONTROLS** (change keys and controller buttons). Controller: LB / RB page, D-pad choose and change, A press, B back. |
| Controller | Plug in an Xbox / PlayStation controller. All menus and the game work with it. |
| VR mode | One time: menu **Rex Magnum > Add VR Support**, then follow the box (tick OpenXR). Start your headset app (Quest Link or SteamVR). Settings > CAMERA & VR > VR mode **ON**. |
| Sound sync | Voices start at once (silence cut from the start of each file). Rex's lines no longer talk over each other. Flirt answers wait for Rex's line to end. |

### New things to try (2026-10-02)
| Try this | What you should see |
|---|---|
| Walk into the pool or the sea until the water is at Rex's chest | Rex swims. Shift = swim faster. Space = kick up (climb out at the edge). |
| Hold **Tab** (controller: hold **Y**, phone: hold **Weapon**) | Time slows. A ring of your weapons shows. Point with the mouse / right stick / finger, then let go to take that weapon. |
| Take the towel into the water, then hit a blob | Towel is darker, drips, hits harder for 90 s. |
| Look for a blob in a flowered dress and a sun hat | It wanders and leaves slime. Go close or hit it: Rex says a line, and it attacks. |
| Wait near the pool | Blobs rise out of the water with a splash. |
| Third person, daytime, look down at Rex's head | A small star of sun light flashes on his bald spot. |
| Play about 4 minutes with no blob near | Rex calls Marty (voice mail greeting, then Rex's message). |
| Leave the controls alone for 35 s | Rex says an idle line. |
| Pause > Controls > CONTROLLER BUTTONS: click an action, press a new button | The action moves to that button; a clash swaps the two. On-screen names change to match. |
| Get hurt, then go to the buffet, the lounge stage or the swim-up bar; press F near the food | Cocktail / pizza / fruit heals Rex. It comes back after 2 min. Rescued guests sometimes drop food. |

## 5. Send me the result
- Window > General > **Console**. If there are red lines: click the first red line, copy the text (Ctrl+C), and paste it in the chat.
- Take screenshots (Windows key + Shift + S) of what looks wrong and send them.

Smaller test (faster): menu **Rex Magnum > Build Rex + Test Scene**, then open `Scenes/RexTest.unity` and press Play.
