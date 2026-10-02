using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Rex calls his agent Marty now and then (brief section 9): Marty's greeting first, then Rex's next
    /// voice-mail message, in order, one per call. Only when no blob is close. The next message is saved.
    /// Clips (optional): Resources/Voice/Marty/greeting_01, Resources/Voice/Rex/voicemail_##.
    /// </summary>
    public class MartyPhone : MonoBehaviour
    {
        public float firstCallAfter = 240f;     // seconds of play
        public float callEvery = 420f;
        public float safeDistance = 20f;
        const string Key = "RexMagnum_VoiceMail";

        RexController ctrl;
        float playTime, nextCall;
        bool calling;

        void Awake()
        {
            ctrl = GetComponent<RexController>();
            nextCall = firstCallAfter;
        }

        void Update()
        {
            if (calling || Time.timeScale == 0f || !ctrl.InputEnabled) return;
            if (GameGuide.EventRunning || KingGlorpBoss.Fighting || GameFlow.Busy || EndingSequence.Playing) return;   // no call over an event
            playTime += Time.deltaTime;
            if (playTime < nextCall) return;
            int n = PlayerPrefs.GetInt(Key, 0);
            if (n >= RexVoice.VoiceMail.Length) { enabled = false; return; }
            foreach (var b in Blob.All)
                if (b != null && !b.IsDead && Vector3.Distance(b.transform.position, transform.position) < safeDistance)
                {
                    nextCall = playTime + 20f;     // busy: try again soon
                    return;
                }
            StartCoroutine(Call(n));
        }

        System.Collections.IEnumerator Call(int n)
        {
            calling = true;
            float t = RexVoice.SayLine(gameObject, "MARTY (voice mail)", RexVoice.MartyGreeting, "Voice/Marty/greeting_01");
            yield return new WaitForSeconds(t + 0.4f);
            t = RexVoice.SayLine(gameObject, "REX", RexVoice.VoiceMail[n], $"Voice/Rex/voicemail_{n + 1:00}");
            yield return new WaitForSeconds(t);
            PlayerPrefs.SetInt(Key, n + 1);
            PlayerPrefs.Save();
            Progress.Add("voicemails");
            nextCall = playTime + callEvery;
            calling = false;
        }
    }
}
