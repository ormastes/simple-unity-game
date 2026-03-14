using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.VFX
{
    /// <summary>
    /// Frost creep visual effect for frozen objects. Applies an animated
    /// frost overlay, spawns ice crystal particles on freeze, and crack
    /// particles on shatter.
    /// </summary>
    public class FrostSpreadVFX : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// Tracks frost state on a single frozen object.
        /// </summary>
        private class FrostEntry
        {
            public Transform target;
            public SpriteRenderer frostOverlay;
            public float freezeProgress; // 0 = none, 1 = fully frozen
            public float startTime;
        }

        #endregion

        #region Serialized Fields

        [Header("Frost Overlay")]
        [SerializeField] private Sprite _frostOverlaySprite;
        [SerializeField] private Material _frostMaterial;
        [SerializeField] private Color _frostTint = new Color(0.7f, 0.9f, 1f, 0.8f);
        [SerializeField] private float _frostSpreadSpeed = 1f;
        [SerializeField] private int _frostSortingOrder = 10;

        [Header("Frost Animation")]
        [SerializeField] private string _frostProgressProperty = "_FrostAmount";
        [SerializeField] private float _frostPulseSpeed = 2f;
        [SerializeField] private float _frostPulseAmplitude = 0.05f;

        [Header("Ice Crystal Particles")]
        [SerializeField] private ParticleSystem _iceCrystalBurstPrefab;
        [SerializeField] private int _crystalBurstCount = 15;
        [SerializeField] private Color _crystalColor = new Color(0.6f, 0.85f, 1f, 1f);

        [Header("Shatter Particles")]
        [SerializeField] private ParticleSystem _shatterParticlePrefab;
        [SerializeField] private int _shatterBurstCount = 25;
        [SerializeField] private Color _shatterColor = new Color(0.8f, 0.95f, 1f, 1f);
        [SerializeField] private AudioClip _shatterSound;

        [Header("Settings")]
        [SerializeField] private float _overlayScalePadding = 1.2f;

        #endregion

        #region Private State

        private readonly List<FrostEntry> _activeEntries = new List<FrostEntry>();
        private readonly Dictionary<Transform, FrostEntry> _entryLookup = new Dictionary<Transform, FrostEntry>();

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            UpdateFrostProgress();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Begins the frost creep effect on the target object.
        /// </summary>
        /// <param name="target">Transform of the object being frozen.</param>
        /// <param name="immediate">If true, sets frost to 100% instantly.</param>
        public void ApplyFrost(Transform target, bool immediate = false)
        {
            if (target == null) return;

            // Already frosted
            if (_entryLookup.ContainsKey(target)) return;

            // Create frost overlay
            SpriteRenderer overlay = CreateFrostOverlay(target);

            FrostEntry entry = new FrostEntry
            {
                target = target,
                frostOverlay = overlay,
                freezeProgress = immediate ? 1f : 0f,
                startTime = Time.time
            };

            _activeEntries.Add(entry);
            _entryLookup[target] = entry;

            // Spawn ice crystal burst
            SpawnIceCrystalBurst(target.position);

            if (immediate && overlay != null)
            {
                Color c = _frostTint;
                c.a = _frostTint.a;
                overlay.color = c;

                if (overlay.material.HasProperty(_frostProgressProperty))
                    overlay.material.SetFloat(_frostProgressProperty, 1f);
            }
        }

        /// <summary>
        /// Removes frost from the target object.
        /// </summary>
        public void RemoveFrost(Transform target)
        {
            if (target == null || !_entryLookup.ContainsKey(target)) return;

            FrostEntry entry = _entryLookup[target];
            CleanupEntry(entry);
            _activeEntries.Remove(entry);
            _entryLookup.Remove(target);
        }

        /// <summary>
        /// Shatters the frozen object with crack/shatter particles.
        /// </summary>
        /// <param name="target">The frozen object to shatter.</param>
        /// <param name="destroyTarget">If true, destroys the target game object after shattering.</param>
        public void ShatterFrost(Transform target, bool destroyTarget = false)
        {
            if (target == null) return;

            SpawnShatterParticles(target.position);

            if (_entryLookup.ContainsKey(target))
            {
                FrostEntry entry = _entryLookup[target];
                CleanupEntry(entry);
                _activeEntries.Remove(entry);
                _entryLookup.Remove(target);
            }

            if (destroyTarget && target != null)
                Destroy(target.gameObject);
        }

        /// <summary>
        /// Returns the current freeze progress (0–1) for the given target.
        /// </summary>
        public float GetFreezeProgress(Transform target)
        {
            if (target == null || !_entryLookup.ContainsKey(target)) return 0f;
            return _entryLookup[target].freezeProgress;
        }

        /// <summary>
        /// Returns whether the given target is fully frozen.
        /// </summary>
        public bool IsFullyFrozen(Transform target)
        {
            return GetFreezeProgress(target) >= 1f;
        }

        /// <summary>
        /// Removes all frost effects.
        /// </summary>
        public void ClearAll()
        {
            foreach (var entry in _activeEntries)
            {
                CleanupEntry(entry);
            }
            _activeEntries.Clear();
            _entryLookup.Clear();
        }

        #endregion

        #region Frost Progress

        private void UpdateFrostProgress()
        {
            for (int i = _activeEntries.Count - 1; i >= 0; i--)
            {
                FrostEntry entry = _activeEntries[i];

                // Target destroyed externally
                if (entry.target == null)
                {
                    CleanupEntry(entry);
                    _activeEntries.RemoveAt(i);
                    continue;
                }

                // Animate frost spread
                if (entry.freezeProgress < 1f)
                {
                    entry.freezeProgress += _frostSpreadSpeed * Time.deltaTime;
                    entry.freezeProgress = Mathf.Clamp01(entry.freezeProgress);
                }

                // Update overlay visual
                if (entry.frostOverlay != null)
                {
                    // Fade in alpha based on progress
                    Color c = _frostTint;
                    c.a = _frostTint.a * entry.freezeProgress;

                    // Pulse when fully frozen
                    if (entry.freezeProgress >= 1f)
                    {
                        float pulse = 1f + Mathf.Sin(Time.time * _frostPulseSpeed) * _frostPulseAmplitude;
                        c.a *= pulse;
                    }

                    entry.frostOverlay.color = c;

                    // Update shader property if available
                    if (entry.frostOverlay.material.HasProperty(_frostProgressProperty))
                        entry.frostOverlay.material.SetFloat(_frostProgressProperty, entry.freezeProgress);
                }
            }
        }

        #endregion

        #region Overlay Creation

        private SpriteRenderer CreateFrostOverlay(Transform target)
        {
            if (_frostOverlaySprite == null) return null;

            GameObject overlayObj = new GameObject("FrostOverlay");
            overlayObj.transform.SetParent(target);
            overlayObj.transform.localPosition = Vector3.zero;
            overlayObj.transform.localRotation = Quaternion.identity;

            // Scale overlay to cover the target with padding
            SpriteRenderer targetSR = target.GetComponent<SpriteRenderer>();
            if (targetSR != null)
            {
                Vector2 targetSize = targetSR.bounds.size;
                overlayObj.transform.localScale = Vector3.one * _overlayScalePadding;
            }
            else
            {
                overlayObj.transform.localScale = Vector3.one * _overlayScalePadding;
            }

            SpriteRenderer sr = overlayObj.AddComponent<SpriteRenderer>();
            sr.sprite = _frostOverlaySprite;
            sr.sortingOrder = _frostSortingOrder;

            if (_frostMaterial != null)
                sr.material = new Material(_frostMaterial);

            Color startColor = _frostTint;
            startColor.a = 0f;
            sr.color = startColor;

            return sr;
        }

        #endregion

        #region Particles

        private void SpawnIceCrystalBurst(Vector3 position)
        {
            if (_iceCrystalBurstPrefab == null) return;

            ParticleSystem ps = Instantiate(_iceCrystalBurstPrefab, position, Quaternion.identity);

            var main = ps.main;
            main.startColor = _crystalColor;

            var burst = new ParticleSystem.Burst(0f, _crystalBurstCount);
            var emission = ps.emission;
            emission.SetBurst(0, burst);

            ps.Play();
            Destroy(ps.gameObject, main.duration + main.startLifetime.constantMax);
        }

        private void SpawnShatterParticles(Vector3 position)
        {
            if (_shatterParticlePrefab == null) return;

            ParticleSystem ps = Instantiate(_shatterParticlePrefab, position, Quaternion.identity);

            var main = ps.main;
            main.startColor = _shatterColor;

            var burst = new ParticleSystem.Burst(0f, _shatterBurstCount);
            var emission = ps.emission;
            emission.SetBurst(0, burst);

            ps.Play();
            Destroy(ps.gameObject, main.duration + main.startLifetime.constantMax);

            // Play shatter sound
            if (_shatterSound != null)
            {
                AudioSource.PlayClipAtPoint(_shatterSound, position);
            }
        }

        #endregion

        #region Cleanup

        private void CleanupEntry(FrostEntry entry)
        {
            if (entry.frostOverlay != null)
                Destroy(entry.frostOverlay.gameObject);

            if (entry.target != null)
                _entryLookup.Remove(entry.target);
        }

        #endregion
    }
}
