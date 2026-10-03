# REX MAGNUM: VACATION VIOLENCE — Project brief for Claude Code

> **New session on the owner's PC: read `HANDOFF_LOCAL.md` first** (current state, open problems, how to build).


## 0. How to work with the owner
- Owner is a hobbyist. Knows no code or command line. Uses Windows PC (Android Studio installed). Time zone US Central.
- Write to owner in ASD-STE100 Simplified Technical English: short sentences, active voice, simple words. Owner is a visual learner: show pictures/renders, tables.
- One step at a time. Show result. Wait for approval before next big step.
- Approval rule for every character/asset: render front + side pictures first (Cycles). Build full asset only after owner says yes.
- Tools: **Blender** (models, rigs, animation) + **Unity** (game). Graphics: **best quality the PC can run without crashing** (see §1). This replaces older plans (PS2 look, single HTML file, three.js). Blobs stay cartoon; all else realistic.
- Targets: Windows PC build + Android APK. One Unity project, URP pipeline. Optional later: WebGL folder.

## 1. System safety (do this FIRST)
1. Check PC specs: GPU model + VRAM, RAM, CPU, free disk. Check installed Blender + Unity versions (Unity needs Android Build Support module). Report to owner in a simple table. Pick settings from specs.
2. Blender:
   - Use Cycles with GPU (OptiX/CUDA/HIP) if available. Denoise on.
   - Preview renders: 50% res, 64–128 samples. Approval renders: max 1920x1080, ≤256 samples.
   - Prefer background renders (`blender -b`) to avoid UI crashes. Save .blend before every render. Incremental saves (_v01, _v02).
   - One character/asset per session. Close Blender between big jobs.
   - Source textures max 4K (hero only); game textures 2K PC / 1K phone.
3. Unity:
   - URP. Quality tiers: Ultra/High (PC, if GPU allows), Medium, Low (phones). Target 60 fps PC, 30 fps phones.
   - Island in zones (additive scene streaming). LODs on all meshes. Occlusion culling. GPU instancing for plants. Baked lightmaps for static buildings + realtime sun for day/night. ASTC textures for Android.
   - Import assets in small batches. Test each system in a small test scene before the full island.
   - Watch memory in Profiler. If editor gets slow: lower editor quality, close other apps, split scenes.
4. Never run long jobs without telling owner how long and how to stop them.

## 2. Game summary
First-person AND third-person comedy shooter (player can switch any time; see §7a). Adult crude humor (Duke Nukem 3D + Leisure Suit Larry + Conker + Sniper Elite kill cam). Rex Magnum, 58, washed-up 1986 action star (film: *Maximum Justice*), lives free at a jungle island resort (room 304, floor 3) in exchange for a bad nightly lounge act. Owes big bar tab. Agent Marty ignores him. Blob aliens crash on the island by accident (need water + sugar to breed). Aliens do NOT know Rex. Rex fights only because: he dies, or he gets publicity and a job. One player. Content warning on title screen. Subtitles for all lines.

**Open free play.** No chapters, no required path, no fame meter. After leaving his room, whole island is open. Accomplishments list instead of chapters (groups: Fight, Sniper, Rescue, Explore, Big goals, Rex, Photos). Big goals: stop blob factory (ruins), pop King Glorp (ship; open from start, very hard early). Free play continues after boss.

**Opening:** Chaos (screams, glass, splat) wakes Rex face-down on bed. Empty beer bottles on floor/table. Shirt half buttoned. Poster of *Maximum Justice*, TV plays it. Room teaches controls: look, walk (kick bottles), punch stuck door, pick up full beer bottle (first weapon), mirror (taunt), towel on balcony rail. Balcony view: pool chaos below.

**Ending (after King Glorp):** guest video of Rex slipping on slime goes viral; agent calls with floor-cleaner TV ad; resort adds damage to bar tab. Rex: "I'm back."

## 3. Characters
**Rex** (realistic human, MPFB/MakeHuman base in Blender, Cycles): normal man who thinks he is a hero. Medium height, thin arms, narrow shoulders, soft belly; hero pose (chest out, belly in), lets belly out when idle. Strong tan, eye lines, very white teeth. Partly bald: bald top, thin blond sides, short mullet back. Scruffy 3-day stubble with gray, NO mustache. Mirror sunglasses always (separate object). Loose BLUE Hawaiian shirt with PINK flamingos, only bottom half buttoned, gold chain showing. Loose knee-length shorts. Flip-flops + white socks. Gold chain, gold pinky ring, cheap gold watch. BARE hands: no gloves, no tattoo. First-person arms get the most detail (separate arms model, full finger bones). Full humanoid Mixamo-compatible rig. Face blend shapes: blink, talk, smile, smug grin.
**Carmen Vega**: head of resort security, early 30s, light Latin American accent, warm but sarcastic. Does the real work. Always rejects Rex.
**King Glorp**: gold boss blob, huge, high squeaky dramatic voice. Does not know who Rex is.
**Marty**: Rex's agent, 20s frat boy, rich parents, voice mail only.
**Guests**: run, hide, can be rescued (gives ammo/health/clue). Talk only when Rex's hands are down. React to taunt (laugh / throw drink).

## 4. Blobs (cartoon, original — do not copy any film/TV character)
All: one smooth joined mesh, gumdrop body melting into slime puddle base, no legs, no mouth, no hat/clothes. Clear jelly body (gummy candy look) with small pink brain inside (no stem). Big white cartoon eyes, each in a slime socket. Long tentacles (thick at body, thin tip, S-bend, curled end, grow out like melted wax — no gaps/seams). Drips, wet shine, slime trail. 2–3 color shades per type.
| Type | Color | Shape | Eyes | Tentacles | Behavior |
|---|---|---|---|---|---|
| Tourist | Green | medium (Rex chest height) | 3 front | 4 | basic, slow, spits slime |
| Lifeguard | Red | thin, tall | 2 on stalks | 2 | fast, throws floats/chairs |
| Bouncer | Purple | wide, taller than Rex | 1 big | 4 thick | strong, splits into 2 half-size blobs |
| King Glorp | Gold | very large | 5 | 6 | boss, crown spikes grow from head, absorbs objects as armor |
Disguise variant: tourist blob in dress/shirt; slime trail gives it away.
Rig: bone chain per tentacle (4–6), 2–3 body bones (squash/stretch), eye-stalk bones. Wobble via bones/blend shapes (no physics).
Anims: idle wobble, slide, fast slide, tentacle whip, spit, throw, hit ripple, stun (eyes roll), angry (darker, shake), rude tentacle gesture (answer to taunt), photo pose (confused; fights back if not stunned), leak (knife), freeze+break (swordfish), pinned (harpoon), pop (slime burst, decals on walls/floor/lens), split (bouncer).
X-ray kill-cam look: clear body, pink brain, swallowed flip-flop / cocktail glass / room key, bullet path. Night: soft glow in own color.
Spawn from water (pool, sea, green river). Fewer after factory stopped. Blobs come back at night.

## 5. Weapons (realistic objects; generic guns, no real brands)
Rex starts with fists. One Action button; its word + color change per weapon. Tap = light, hold = heavy/aim.
| # | Weapon | Word | Found | Tap / Hold | Kill cam |
|---|---|---|---|---|---|
| 1 | Fists (bare hands) | Punch | start | jab / heavy punch | X-ray, fist in jelly, brain bounces |
| 2 | Beer bottle (brown glass, held by neck) | Swing | Rex's room | side swing / overhead. Breaks after 3 hits → Stab weapon (broken-neck model) | ripple + pop |
| 3 | Beach towel | Whip | balcony rail | snap / wind-up hard snap (stuns 2 s) | slow-mo towel tip, wave through jelly |
| 4 | Steak knife (buffet) | Stab | buffet fl.1 | stab / lunge | blob leaks, shrinks like balloon |
| 5 | Golf club | Swing | pro shop | swing / full golf swing | blob flies into pool/sea |
| 6 | Frozen swordfish (held by tail, swing only) | Swing | buffet freezer | slow swing / smash | freeze then shatter |
| 7 | Chrome pistol (wood grip, 80s movie style) | Shoot | casino lounge fl.1 | shot / aim+shot | short bullet cam |
| 8 | Harpoon gun (yellow, rope reel) | Shoot | beach dive shop | shot / aim; pull rope to drag blob | blob pinned to wall |
| 9 | Sniper rifle (scope, bipod) | Shoot | hotel roof | shot / scope | full bullet cam + X-ray |
| 10 | Exploding coconut (fuse, 3 s) | Throw | beach + tiki bar | short / long throw with aim line | camera rides coconut, slime rain |
Towel: two looks. Carry = flat striped towel (coral/yellow/teal/white stripes, white fringe) hangs from hand (on shoulder when hands down). Whip = twisted roll (spiral stripes, flat fringed tip). Twist 0.25 s → snap → opens flat. Wet towel (dip in water): darker, drips, hits harder. Use bones/blend shapes, not cloth sim.
Guns: limited ammo, counter on HUD; ammo found in world + from rescued guests. Pickup models for bullets, harpoons, rifle rounds. Each weapon: grip point, muzzle point (guns), world pickup copy with soft glow, pick up / idle / put away / take out anims.

## 6. Island (realistic, open world, jungle)
Big jungle island: thick forest, rivers, waterfalls. Zones (play in any order):
| Zone | Where | Contents |
|---|---|---|
| Resort hotel | south coast | 5 floors (see below) |
| Pool area | south, by hotel | large main pool, swim-up bar, slides, hot tubs, stage, chairs/umbrellas |
| Beach | south shore | white sand, dive shop, tiki bar |
| Golf course | west | holes, sand traps, pro shop, cart shed |
| Thick jungle | center | trees, vines, green slime river, small waterfalls |
| Jungle ruins | east | old stone temple; inside: hidden alien base / blob factory (slime pipes, glow) |
| Cave | north-west | entrance behind waterfall; tunnel to ruins and up to ship |
| Crashed alien ship | north, on top of big waterfall | King Glorp boss; slime runs down waterfall |
Hotel floors: 5+roof = penthouse, roof terrace, sniper spot, sniper rifle · 4 = rooms, many hidden guests · 3 = rooms, Rex's room 304 with balcony over pool · 2 = rooms, gym, spa · 1 = lobby, bar (Rex's lounge act), casino lounge, buffet + freezer, doors to pool. Stairs + elevator (soft music, sometimes slime-filled). Reuse one guest-room model with variations.
Start state: chaos — guests run/hide (rooms, closets, under chairs), tables over, drinks/luggage on ground, slime on walls.
Cart road loop: resort → golf → cave → ruins → resort; foot paths into jungle.
**Golf cart:** walk/run or drive. Battery meter on HUD. Fast driving, hills, headlights at night drain more. At 0 the cart stops; walk or push to charge station (resort, golf course, ruins).
**Day/night:** ~20 min full cycle. Day: few slow blobs, long view, sunglasses on. Night: many fast glowing blobs, dark jungle, tiki torches, guests locked in rooms, headlights drain battery.
**Swim:** Rex swims in pool and sea (shore water deep enough).
Unity: terrain + zone streaming, LODs, instanced vegetation, water shader for sea/pool/green river/waterfall.

## 7. Controls (remappable; device auto-detected; on-screen icons change)
| Action | Touch | Keyboard/Mouse | Controller (Xbox names) |
|---|---|---|---|
| Move | left stick | WASD | left stick |
| Look | right stick / drag right side | mouse | right stick |
| Run | push left stick to edge | hold Shift | click L-stick |
| Jump | Jump button | Space | A |
| Action tap | Action button | left click | RT |
| Action hold | hold Action | hold left click | hold RT |
| Aim/scope | hold Action with gun | hold right click | hold LT |
| Next weapon | tap Weapon | wheel / Q,E | RB (LB = previous) |
| Weapon wheel | hold Weapon | hold Tab | hold Y |
| Hands down/up | Hands | H | D-pad down |
| Taunt (middle finger) | Taunt | T | D-pad up |
| Photo | Photo | P | D-pad right |
| Use (pick up, open, cart in/out) | Use (shows near objects) | F | X |
| Pause (map, album, accomplishments, settings) | Pause | Esc | Menu |
| Camera: first ↔ third person | Camera | V | click R-stick |
| Swap shoulder (third person) | — (setting) | C | D-pad left |
Phone layout: move stick bottom-left; look stick + Action bottom-right; Jump above Action; Taunt, Photo, Hands, Weapon along top; Pause top corner. Landscape. Controller connected on phone → hide touch buttons.
Remap: key/button lists per device (swap on conflict), drag/resize/opacity for touch buttons, swap sticks (left-hand), swap triggers/bumpers, separate look speed X/Y, invert Y, hold-or-tap for Run/Aim/Hands, aim assist toggle (phone/controller), reset per device. Save 3 sets (touch, KB/M, controller). Use Unity Input System package.

## 7a. Camera modes (owner decision: game plays in BOTH)
- Two modes: **First person (FP)** and **Third person (TP)**. Switch any time: keyboard **V**, controller **R-stick click**, touch **Camera** button (top row). Setting: default mode + remember last mode.
- **FP:** separate high-detail arms model (full finger bones), weapon in view, head bob setting.
- **TP:** over-the-shoulder camera behind Rex (right shoulder; swap shoulder: keyboard **C** / controller D-pad left). Camera collides with walls (no clipping), zooms in when aiming, crosshair in center, aim ray from camera. Full Rex body visible: needs full-body animations for every action (walk, run, jump, swim, drive cart, slip, all weapon attacks, taunt, photo, hit, death, idle belly-out / hero-pose) — same Mixamo-compatible rig.
- Both modes share one gameplay system. Weapons: one world model with grip point; FP and TP use the same weapon data. Kill cam, photo/selfie and cutscenes work from both modes.
- Mirror sunglasses, bald-spot glint, slime on shirt must look good up close in TP.
- Sniper scope always switches to FP scope view, then back to the mode before.
- Phone: TP default can be chosen in settings; same touch layout.

## 8. Rex rules
- **Hands down:** no weapon shown, runs faster, guests talk, Action word = Talk.
- **Taunt:** middle finger (free hand when holding weapon). Blobs get angry, run at Rex (pull them into sniper view), drop more loot. Blob answers with rude tentacle shape.
- **Photo button:** any time. Rex pulls nearest blob/guest close, front-camera selfie view, 2 s, can't attack, can be hit. Stunned blob stays still; un-stunned fights back (shown in photo). No target → selfie alone. Saved to album with caption. King Glorp photo = hardest.
- **Kill cam:** on hold-attack kill, sniper kill, last blob in group. Slow-mo, follow bullet/weapon, X-ray, pop, one-liner. 3–5 s, skippable; setting: off / less often.
- **Health bar:** food/drinks heal. At 0: falls, says line, wakes in room 304, keeps weapons.
- **One-liners** after kills, taunts, photos, flirts, cart dying. Never same line twice in a row.
- Slips on slime puddles (comedy fall). Mirrors: Rex combs side hair over bald spot. Sun glints off bald spot.
Photo captions (text only): "Me and a fan. #blessed #hireme" · "New co-star. Less slimy than my last agent." · "Still got it. (Not sure what it is. Seeing a doctor.)" · "Available for weddings, funerals, and car lot openings." · "Working vacation. #stunts #ownhip" · "He wanted a selfie. He really didn't." · "58 and wet. #goals" · "Swipe up to hire me. Please. Seriously." · "My plus-one for the premiere. If there is one." · (Glorp) "Me and royalty. He's the one on the left. I think."

## 9. Audio (all made in ElevenLabs — download the SELECTED take of each)
Flows: Voice lines → https://elevenlabs.io/app/flows/k7pwjMgxc3Fxr3Tjea1m · Sounds + music → https://elevenlabs.io/app/flows/MY5E3j0FjCh2AJ5LwKf5
Voice IDs: Rex 73PNnMEYR7SFaRCZoUpq · Carmen y3CfH7z4dKrH4hDLOEQK · King Glorp tZf57zeY9FrfkOI99yrG · Marty LOK3d1lfTieh4Cksl31d · Guests: Callie 7YaUDeaStRuoYg3FKsmU, Heather Rey MnUw1cSnpiLoLhpd3Hqp, Lucy lcMyyd2HUfFzxdCaC4Ta, Gerald fGIZlgPQ75MMlvQ6WxgY, Justin fV5vDeUO8ghvK7ntjiVC, Fin MjEQaRiSe6jP1b0vagRU.
Name files `group_##_takeN.mp3`. Format below: `take: line`.

**Music:** title theme v2 (80s action, synth/guitar/sax, steel drums) · day resort v2 (calm tropical loop) · night jungle v1 (horror + tropical loop) · fight v2 (80s hair band loop) · King Glorp power ballad v1 (80s singer) · Rex lounge act v1 (piano lounge, hotel bar).
**Blob SFX (all v2):** idle gurgle, slide, spit, angry, hit, pop.
**Weapon SFX:** towel crack 1 · bottle break 1 · knife stab+leak 1 · golf swing 2 · swordfish freeze/break 2 · pistol 1 · harpoon 2 · sniper+slow bullet 1 · coconut fuse+boom 2. Fists use blob hit SFX.

**Rex lines (151):**
- Wake: 1 Who put the floor so close to my face? · 1 Room service is loud today. · 2 Never again. Until tonight. · 1 Why not. I die, or I trend. Win-win.
- Fists: 2 Talk to the hand. It's the only one still returning my calls. · 2 These fists are registered weapons. In Guam. · 1 Knuckle sandwich. Hold the mayo. You ARE the mayo. · 1 I've punched better. I've punched worse. I've punched a director. · 2 That's gonna leave a stain.
- Bottle: 1 This round's on you. · 1 Last call, snot-rocket. · 1 Bottle service. · 2 I don't usually waste a beer on the first date. · 2 Damn. That one was half full.
- Towel: 1 Towel service, jackass. · 1 Snap out of it. · 1 Locker room rules, baby. · 2 I've been whipped before. I paid extra. · 2 Wet T-shirt contest is over. You lost.
- Knife: 2 Well done. · 2 Stick a fork in you. All I got is the knife. · 1 Relax. It's just the tip. · 1 You're leaking. Happens to me too. It's an age thing. · 2 Medium rare. Mostly rare. Mostly goo.
- Golf: 1 Fore... play. · 1 Hole in one. You're the hole. · 2 Nice balls. Oh wait. You don't have any. · 2 I never play fair. Or on the fairway. · 2 That's a bogey. I don't know what a bogey is.
- Swordfish: 2 Catch of the day. · 2 Sleep with the fishes. Or get beaten with one. Your call. · 1 Biggest thing I've swung in years. Second biggest. · 2 Frozen stiff. Haven't said that since '92. · 1 Something smells fishy. It's both of us.
- Pistol: 1 Checkout time. · 1 Chrome, baby. Like my new hip. · 1 I did my own stunts. You did your last. · 2 Premature? No. Right on time. For once. · 1 Bang. That's the noise it makes. I checked.
- Harpoon: 1 Stick around. · 2 Shaft happens. · 1 Get the point? · 1 First thing I've nailed in this hotel. · 2 Hang in there. No, really. You're stuck.
- Sniper: 1 That's a wrap. · 1 One take. Like my wedding nights. All four. · 1 Long-distance relationship. Over. · 1 Head shot. I used to have one of those. Eight by ten. Glossy. · 2 I can hit anything from up here. Except rock bottom. Already did.
- Coconut: 1 Go nuts. · 2 Drinks are on me. And on you. And on the wall. · 2 Blow me. I'm talking to the coconut. · 2 Nice coconuts. Sorry, ma'am. I meant mine. · 1 Fire in the hole. That's what she said. Then she took the house.
- Fight: 1 Clean-up at the pool. · 1 You're all wet. · 2 You look like something I coughed up in '87. · 1 I've seen scarier things in the hot tub filter. · 2 Smells like the buffet on a Sunday. · 1 Who sneezed you out? · 1 Next!
- Slime on Rex: 2 Ugh. It's in my mouth. Not the worst thing this week. · 2 I've had worse on me. Ask housekeeping. · 2 This shirt is vintage, you snot. · 1 Great. Now I'm moist.
- Tourist: 2 Vacation's over, booger. · 1 Three eyes and you still didn't see that coming.
- Lifeguard: 2 No running by the pool. · 1 Adult swim, pal.
- Bouncer: 2 I've been thrown out by bigger. Last night. · 1 (split) Great. Twins. My third wife had a twin. Long story.
- Disguise: 2 Nice dress. Your slime is showing. · 2 I knew it. Nobody at this resort looks that good.
- Factory: 1 I've shut down bigger productions. By showing up drunk. · 2 Whoops. Was that important?
- King Glorp: 1 You must be the big cheese. You smell like it. · 2 You don't know who I am? That hurts more than the slime. · 2 I've worked with bigger slimeballs. They ran studios. · 1 Nice crown. Compensating for something? · win 1 Long live the king. Nah. · win 2 And... cut. Print it. Somebody print it. Anybody?
- Taunt: 1 Read between the lines. · 1 That's my autograph. · 1 Count with me. One. · 1 Here's my head shot. · 2 This finger's single. Like me. Four times. · 2 Sit on it and wobble. · 2 This one's free. Autographs are ten bucks. · 1 (after rude tentacle) Oh, real mature.
- Photo: 1 Smile. Or whatever the hell you do. · 1 Get my good side. It's this one. No. Other one. · 1 This one's for the fans. Both of them. · 2 Say cheese. You smell like it. · 1 Hold still. My thumb's on the lens. Every damn time. · 1 (guest) Closer. Closer. OK, that's my wallet. · 2 (alone) Just me. Again. Story of my life.
- Cart dead: 2 I do my own stunts. I don't do my own walking. · 1 Dead battery. We have so much in common. · 1 (pushing) Pushing. Great. My heart doctor will hear about this. If I had one.
- Slip: 2 I meant to do that. · 2 My hip! No, it's fine. It's the fake one. · 2 Stunt fall. Nailed it.
- Hurt: 1 Not the face! It's all I've got left! · 1 I've been hit harder by critics. · 2 Ow. My everything.
- Low health: 1 I need a drink. And a medic. Drink first. · 1 I'm seeing two of you. Both ugly.
- Death: 2 Tell my agent... he's fired. · 2 Finally. Publicity. · 1 Bury me... in something slimming.
- Respawn: 1 Huh. Still here. Damn. · 2 Take two.
- Night: 1 I can see fine. (walks into wall) Who moved the wall? · 1 Sunglasses at night. It's a look. It's also a problem.
- Mirror: 1 It's not bald. It's aerodynamic. · 2 Still got it. Most of it. Some of it. · 2 Hello, handsome. Hello, handsome's father.
- Rescue: 1 Did you film that? Tag me. · 1 You're welcome. Cash is fine. · 2 Tell your friends. Tell a casting director.
- Weapon found: 2 Oh, that'll do. · 1 Finders keepers. Losers get goo'd.
- Idle: 1 Hello? I'm being heroic over here. · 1 Back in '86 I had a trailer. With a door. · 1 Is this thing on? Is anything on?
- Voice mail (play Marty greeting take 1 first; messages in order, one per call): 2 Marty. It's Rex. Aliens. Call me. · 2 Marty. I'm killing aliens with a fish. This is a franchise. · 2 Marty, I know you're there. I can hear you breathing. No, wait. That's me. · 2 Marty. If I die, I want a big funeral. If I live, I want ten percent more. · 1 Marty. You're dead to me. Call me back. · 2 Marty. It's Rex. I forgot why I called. I'm covered in something. · 2 Marty. Your mailbox is full. That's the most action either of us has had all year.
- Marty greeting (Marty voice, take 1): "Hey, hey, hey! You've reached Marty at Big Star Talent. I'm on the other line with somebody more famous than you. Leave a message. If this is Rex, stop calling. Love ya, babe!"
- Flirt → answer (Rex take / answer voice, answer take 1): 2 I'm Rex Magnum. → Callie "Who?" · 1 You might know me from the movies. → Heather "My grandma does." · 2 I just saved your life. Dinner? → Lucy "Put me back in the closet with the slime." · 2 Is it hot out here, or is it me? → Lucy "It's the building. It's on fire." · 1 Room 304. The door's broken, so come right in. → Justin "No." · 1 I've got a gold chain and a bar tab. What have you got? → Callie "Standards." · 2 I do all my own stunts. → Justin "Is breathing that hard one of them?" · 2 At my age, all night long means I fell asleep during. → Fin "Why would you tell me that?" · 2 Want to see my award? I don't have one. Want to see my room? → Gerald "I want to see a manager." · 1 I was huge in the eighties. → Gerald "You're pretty big now. Around the middle." · 1 My ex-wives said I was unforgettable. → Heather "In court?" · 1 Stick with me, baby. I know this island. → Fin "You got lost in the lobby."
- Rex after fail (to himself): 2 She's into it. · 2 Playing hard to get. They all do. For decades. · 2 Still got it. · 2 That's a maybe.
- Rex → Carmen (take 1 each) → Carmen answer (take 1): We make a good team. → "You broke a vending machine with your face." · After this, you, me, a bottle of wine? → "After this, you, me, your bar bill." · Admit it. You're impressed. → "I'm not impressed. I'm surprised. It's different."

## 10. Deliverables per asset
.blend (keep full-detail source) · .fbx (model+rig+anims) · Unity prefab · .glb · PNG approval renders. Game builds: Windows .exe + Android .apk.

## 11. Build order
1. Specs check + settings (§1). 2. Rex: front/side renders → approve (DONE) → rig, first-person arms, full-body anims for third person. 3. Tourist blob → approve → other 3 blobs. 4. Bottle, towel, knife → approve → other weapons. 5. Island rough map + 3 view renders (hotel/pool from beach, waterfall, ruins) → approve. 6. **First playable:** Rex's room + pool area with fists, bottle, towel, tourist blob, controls, first/third-person camera switch, kill cam, wake-up lines, title + day music. Test on PC and phone. 7. Add one zone at a time: hotel floors, beach, golf + cart, jungle + night, ruins/factory, cave, ship + King Glorp. 8. Accomplishments, photo album, settings, remap, save system.

Reference docs (owner's Claude Docs, optional — this file is complete without them): Rex 23121400-55c3-4730-b5eb-42fbfd6950f4 · Island feae6b9d-a159-4215-9ee5-211f34fc42f4 · Blobs 7ea0681d-7813-4895-b11f-6a1ee072afa2 · Weapons 85266f28-9b28-41ce-bae1-f3f971cab9f0 · Rules 55c80092-9280-4dec-9150-5742a6abc034 · Lines 8db38afa-3e89-411d-8992-bd29e24a4751 (claude.ai/code/artifact/<id>)
