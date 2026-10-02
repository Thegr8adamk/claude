using System.Collections;
using UnityEngine;

namespace RexMagnum
{
    /// <summary>
    /// Big goal 1 (brief section 2): stop the blob factory in the ruins. The core takes damage from every weapon;
    /// it pulses faster as it weakens; at 0 it blows up (slime everywhere), the factory lights die, blobs come
    /// much less from now on (BlobSpawner), and the result is saved (PlayerPrefs "RexMagnum_FactoryStopped").
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class FactoryCore : MonoBehaviour
    {
        public const string SaveKey = "RexMagnum_FactoryStopped";
        public Renderer coreRenderer;
        public Light coreLight;
        public Light[] hallLights;
        Health health;
        MaterialPropertyBlock mpb;
        Color baseEmission = new Color(0.4f, 1f, 0.25f) * 6f;
        public static bool Stopped => PlayerPrefs.GetInt(SaveKey, 0) == 1;

        void Start()
        {
            health = GetComponent<Health>();
            health.maxHealth = 400f;
            health.ResetHealth(400f);
            mpb = new MaterialPropertyBlock();
            health.Died += by => StartCoroutine(Blow(by));
            if (Stopped) Dead(true);
        }

        void Update()
        {
            if (health.IsDead || coreRenderer == null) return;
            float rate = Mathf.Lerp(6f, 1f, health.Fraction);
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * rate);
            coreRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", baseEmission * pulse);
            coreRenderer.SetPropertyBlock(mpb);
            if (coreLight != null) coreLight.intensity = 4f * pulse;
        }

        IEnumerator Blow(GameObject by)
        {
            for (int i = 0; i < 6; i++)
            {
                SlimeFX.Splat(transform.position + Random.insideUnitSphere * 2f, Random.onUnitSphere, new Color(0.3f, 1f, 0.2f), 30);
                Sfx.Play("blob_pop", transform.position);
                yield return new WaitForSeconds(0.15f);
            }
            if (by != null) RexVoice.Say(by, "Factory", true);
            KillCam.Show(transform, by != null ? by.transform.position : transform.position - transform.forward * 5f, false);
            Dead(false);
            PlayerPrefs.SetInt(SaveKey, 1);
            Progress.Set("factory", 1);
            PlayerPrefs.Save();
        }

        void Dead(bool instant)
        {
            if (coreRenderer != null) coreRenderer.gameObject.SetActive(false);
            foreach (var c in GetComponents<Collider>()) c.enabled = false;   // saved as stopped: the core cannot be blown up again
            if (coreLight != null) coreLight.enabled = false;
            if (hallLights != null) foreach (var l in hallLights) if (l != null) l.intensity *= 0.2f;
            foreach (var sp in FindObjectsByType<BlobSpawner>(FindObjectsSortMode.None))
            {
                if (sp.baseMax < 0) sp.baseMax = sp.maxAlive;
                sp.factoryScale = 1f / 3f;          // kept by DayNight (it used to reset maxAlive every frame)
                sp.maxAlive = Mathf.Max(1, sp.maxAlive / 3);
                sp.interval *= 2.5f;
            }
        }
    }
}
