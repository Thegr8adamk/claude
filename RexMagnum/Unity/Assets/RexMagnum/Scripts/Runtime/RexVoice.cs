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
