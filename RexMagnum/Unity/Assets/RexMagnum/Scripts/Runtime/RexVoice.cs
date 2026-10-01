using System.Collections.Generic;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Rex's one-liners (brief section 9). Plays a clip from Resources/Voice/Rex/&lt;group&gt;_##_takeN
    /// when the ElevenLabs files are added, and always shows a subtitle.
    /// Never the same line twice in a row (brief rule).
    /// </summary>
    public static class RexVoice
    {
        static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            { "Wake", new[] { "Who put the floor so close to my face?", "Room service is loud today.", "Never again. Until tonight.", "Why not. I die, or I trend. Win-win." } },
            { "Fists", new[] { "Talk to the hand. It's the only one still returning my calls.", "These fists are registered weapons. In Guam.", "Knuckle sandwich. Hold the mayo. You ARE the mayo.", "I've punched better. I've punched worse. I've punched a director.", "That's gonna leave a stain." } },
            { "Taunt", new[] { "Read between the lines.", "That's my autograph.", "Count with me. One.", "Here's my head shot.", "This finger's single. Like me. Four times.", "Sit on it and wobble.", "This one's free. Autographs are ten bucks." } },
            { "Photo", new[] { "Smile. Or whatever the hell you do.", "Get my good side. It's this one. No. Other one.", "This one's for the fans. Both of them.", "Say cheese. You smell like it.", "Hold still. My thumb's on the lens. Every damn time." } },
            { "PhotoAlone", new[] { "Just me. Again. Story of my life." } },
            { "Slip", new[] { "I meant to do that.", "My hip! No, it's fine. It's the fake one.", "Stunt fall. Nailed it." } },
            { "Slime", new[] { "Ugh. It's in my mouth. Not the worst thing this week.", "I've had worse on me. Ask housekeeping.", "This shirt is vintage, you snot.", "Great. Now I'm moist." } },
            { "Hurt", new[] { "Not the face! It's all I've got left!", "I've been hit harder by critics.", "Ow. My everything." } },
            { "Death", new[] { "Tell my agent... he's fired.", "Finally. Publicity.", "Bury me... in something slimming." } },
            { "Respawn", new[] { "Huh. Still here. Damn.", "Take two." } },
            { "Bottle", new[] { "This round's on you.", "Last call, snot-rocket.", "Bottle service.", "I don't usually waste a beer on the first date.", "Damn. That one was half full." } },
            { "BottleBroken", new[] { "Last call, snot-rocket.", "Bottle service." } },
            { "Towel", new[] { "Towel service, jackass.", "Snap out of it.", "Locker room rules, baby.", "I've been whipped before. I paid extra.", "Wet T-shirt contest is over. You lost." } },
            { "Knife", new[] { "Well done.", "Stick a fork in you. All I got is the knife.", "Relax. It's just the tip.", "You're leaking. Happens to me too. It's an age thing.", "Medium rare. Mostly rare. Mostly goo." } },
            { "GolfClub", new[] { "Fore... play.", "Hole in one. You're the hole.", "Nice balls. Oh wait. You don't have any.", "I never play fair. Or on the fairway.", "That's a bogey. I don't know what a bogey is." } },
            { "Swordfish", new[] { "Catch of the day.", "Sleep with the fishes. Or get beaten with one. Your call.", "Biggest thing I've swung in years. Second biggest.", "Frozen stiff. Haven't said that since '92.", "Something smells fishy. It's both of us." } },
            { "Pistol", new[] { "Checkout time.", "Chrome, baby. Like my new hip.", "I did my own stunts. You did your last.", "Premature? No. Right on time. For once.", "Bang. That's the noise it makes. I checked." } },
            { "Harpoon", new[] { "Stick around.", "Shaft happens.", "Get the point?", "First thing I've nailed in this hotel.", "Hang in there. No, really. You're stuck." } },
            { "Sniper", new[] { "That's a wrap.", "One take. Like my wedding nights. All four.", "Long-distance relationship. Over.", "Head shot. I used to have one of those. Eight by ten. Glossy.", "I can hit anything from up here. Except rock bottom. Already did." } },
            { "Coconut", new[] { "Go nuts.", "Drinks are on me. And on you. And on the wall.", "Blow me. I'm talking to the coconut.", "Fire in the hole. That's what she said. Then she took the house." } },
            { "Mirror", new[] { "It's not bald. It's aerodynamic.", "Still got it. Most of it. Some of it.", "Hello, handsome. Hello, handsome's father." } },
            { "Door", new[] { "Knock knock. Who's there? Me. Leaving.", "I've kicked down bigger doors. On a sound stage." } },
            { "Idle", new[] { "Hello? I'm being heroic over here.", "Back in '86 I had a trailer. With a door.", "Is this thing on? Is anything on?" } },
            { "Rescue", new[] { "Did you film that? Tag me.", "You're welcome. Cash is fine.", "Tell your friends. Tell a casting director." } },
            { "FlirtFail", new[] { "She's into it.", "Playing hard to get. They all do. For decades.", "Still got it.", "That's a maybe." } },
            { "CartDead", new[] { "I do my own stunts. I don't do my own walking.", "Dead battery. We have so much in common." } },
            { "CartPush", new[] { "Pushing. Great. My heart doctor will hear about this. If I had one." } },
            { "WeaponFound", new[] { "Oh, that'll do.", "Finders keepers. Losers get goo'd." } },
            { "Tourist", new[] { "Vacation's over, booger.", "Three eyes and you still didn't see that coming." } },
        };

        static readonly Dictionary<string, int> last = new Dictionary<string, int>();
        static float nextAllowed;
        public static System.Action<string> Subtitle;   // UI hook

        public static void Say(GameObject speaker, string group, bool force = false)
        {
            if (!Lines.TryGetValue(group, out var lines)) return;
            if (!force && Time.time < nextAllowed) return;
            int i = Random.Range(0, lines.Length);
            if (lines.Length > 1 && last.TryGetValue(group, out int prev) && prev == i) i = (i + 1) % lines.Length;
            last[group] = i;
            nextAllowed = Time.time + 2.5f;
            Subtitle?.Invoke(lines[i]);
            var clip = Resources.Load<AudioClip>($"Voice/Rex/{group.ToLowerInvariant()}_{i + 1:00}");
            var src = speaker != null ? speaker.GetComponent<AudioSource>() : null;
            if (clip != null && src != null) src.PlayOneShot(clip);
        }
    }
}
