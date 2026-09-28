#!/usr/bin/env python3
"""Build computer-quest.html: one self-contained file (works offline except web fonts).

    python3 tools/build.py

Inputs
  src/shell.html          page markup + base CSS (ends right after the markup, before scripts)
  src/adventure.css       adventure (Zelda-inspired) theme, inserted before </style>
  src/js/*.js             game code, concatenated in filename order into one <script>
  vendor/three.min.js     three.js r128 (MIT)
  vendor/GLTFLoader.js    three.js glTF loader r128 (MIT)
  blender/town_assets.glb 3D models made by blender/build_assets.py, embedded as base64
"""
import base64, glob, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
rd = lambda p: open(os.path.join(ROOT, p), encoding='utf-8').read()

shell = rd('src/shell.html')
shell = shell.replace('</style>', rd('src/adventure.css') + '\n</style>', 1)
game_js = '\n'.join(rd(os.path.relpath(p, ROOT)) for p in sorted(glob.glob(os.path.join(ROOT, 'src/js/*.js'))))
glb = base64.b64encode(open(os.path.join(ROOT, 'blender/town_assets.glb'), 'rb').read()).decode()

out = (shell
       + '\n<script>/* three.js r128 — MIT License, (c) three.js authors */\n' + rd('vendor/three.min.js') + '\n</script>'
       + '\n<script>' + rd('vendor/GLTFLoader.js') + '\n</script>'
       + '\n<script>/* 3D models built with Blender (blender/build_assets.py) */\nconst TOWN_GLB = "' + glb + '";\n</script>'
       + '\n<script>\n' + game_js + '\n</script>\n</body>\n</html>\n')
dst = os.path.join(ROOT, 'computer-quest.html')
open(dst, 'w', encoding='utf-8').write(out)
print('Wrote', dst, f'{len(out) / 1024:.0f} KB')
