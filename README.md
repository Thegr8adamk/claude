# Computer Quest: Tech Town

A 3D adventure game that teaches **absolute beginners** to use a computer and a smartphone, from pressing
the power button to email, Android, iPhone and staying safe from scams, spam and malware.

**To play:** open **`computer-quest.html`** in any modern web browser (Chrome, Edge, Safari or Firefox).
It's a single file and works offline (only the fonts need internet).

## How it works

- Walk around **Tech Town** with the arrow keys (or the on-screen arrows). Maya, your guide, teaches you how.
- **Type what you want to do:** `ENTER` a building, `TALK` to people, `PICKUP` items, `USE` things, `THROW` the ball.
- Each of the **10 buildings** is a chapter with hands-on practice on a pretend computer or phone.
  Nothing can break, mistakes just bring gentle hints, and every lesson can be read aloud.
- Battle **Glitches** by pressing the letter above them, find **12 Data Gems**, and complete **missions**.
- Earn XP, levels and badges, and print a **certificate** at City Hall.

## For developers

See [`CLAUDE.md`](CLAUDE.md) and [`docs/PROJECT_BRIEF.md`](docs/PROJECT_BRIEF.md).

```bash
python3 tools/build.py               # build computer-quest.html from src/
python3 blender/build_assets.py      # rebuild the 3D models (pip install bpy)
```

3D models were made with Blender; rendering uses [three.js](https://threejs.org) (MIT).
