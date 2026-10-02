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

## 4. Build the island and play
1. Menu **Rex Magnum > Build Island**. Wait (a progress bar shows; 5–15 minutes).
2. Open `Assets/RexMagnum/Scenes/Island.unity`. Press **Play**.

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
