"""Move the owner's uploaded ElevenLabs downloads from RexMagnum/AudioInbox into the game folders.

Input: a JSON file of picks from the Sound Sorter page, e.g.
  [{"file": "New sfx node (3).mp3", "kind": "sfx", "target": "pistol"}, ...]
Music goes to Unity/Assets/RexMagnum/Resources/Music/<target>.mp3, sound effects to .../Resources/Sfx/<target>.mp3.
Picks with target "skip" or empty are left in the inbox. Uses git mv so history follows the file.

  python RexMagnum/Tools/sort_audio.py picks.json
"""
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INBOX = os.path.join(ROOT, "AudioInbox")
DEST = {"music": "Music", "sfx": "Sfx"}


def main(path):
    picks = json.load(open(path))
    seen = {}
    for p in picks:
        t = (p.get("target") or "").strip()
        if not t or t == "skip":
            continue
        if t in seen:
            print(f"[skip] {p['file']}: '{t}' already used by {seen[t]}")
            continue
        src = os.path.join(INBOX, p["file"])
        if not os.path.exists(src):
            print(f"[missing] {p['file']}")
            continue
        d = os.path.join(ROOT, "Unity", "Assets", "RexMagnum", "Resources", DEST[p["kind"]])
        os.makedirs(d, exist_ok=True)
        dst = os.path.join(d, t + ".mp3")
        subprocess.run(["git", "mv", "-f", src, dst], check=True)
        seen[t] = p["file"]
        print(f"[ok] {p['file']} -> {os.path.relpath(dst, ROOT)}")


if __name__ == "__main__":
    main(sys.argv[1])
