using UnityEngine;
using System.Collections;
using ParadisePilot.Flight;

namespace ParadisePilot.Discovery
{
    /// <summary>
    /// Handles in-engine documentary B-roll photo capture for Paradise Pilot (GDD v2.0 Section 5).
    /// Subscribes to ShimmerRing events, triggers the subtle camera viewfinder shutter flash,
    /// captures the screenshot framing the landmark, and exposes data for the slide-out postcard.
    /// </summary>
    public class BRollPhotoCapture : MonoBehaviour
    {
        [Header("Shutter VFX & Audio")]
        [SerializeField] private AudioClip shutterClickAudio;
        [SerializeField] private CanvasGroup viewfinderFlashOverlay;
        [SerializeField] private float flashDuration = 0.25f;

        [Header("Snapshot Capture")]
        [Tooltip("Resolution width of the captured postcard photo.")]
        [SerializeField] private int captureWidth = 640;
        [Tooltip("Resolution height of the captured postcard photo.")]
        [SerializeField] private int captureHeight = 360;

        [Header("Postcard UI Trigger")]
        [SerializeField] private PostcardCardUI postcardUI;

        private AudioSource audioSource;
        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            if (viewfinderFlashOverlay != null)
            {
                viewfinderFlashOverlay.alpha = 0f;
            }
        }

        private void OnEnable()
        {
            ShimmerRing.OnRingCollected += HandleRingCollected;
        }

        private void OnDisable()
        {
            ShimmerRing.OnRingCollected -= HandleRingCollected;
        }

        private void HandleRingCollected(ShimmerRing ring, PhotoScoreData scoreData)
        {
            StartCoroutine(CaptureSequence(ring, scoreData));
        }

        private IEnumerator CaptureSequence(ShimmerRing ring, PhotoScoreData scoreData)
        {
            // 1. Play Shutter Click Sound
            if (shutterClickAudio != null && audioSource != null)
            {
                audioSource.PlayOneShot(shutterClickAudio, 0.8f);
            }

            // 2. Viewfinder Shutter White Flash
            if (viewfinderFlashOverlay != null)
            {
                StartCoroutine(TriggerFlashRoutine());
            }

            // Wait until end of frame to capture screenshot cleanly from backbuffer
            yield return new WaitForEndOfFrame();

            // 3. Capture In-Engine Snapshot
            Texture2D snapshot = CaptureFrame(captureWidth, captureHeight);

            // 4. Slide out the Postcard UI card with photo, title, lore, and stars
            if (postcardUI != null)
            {
                postcardUI.DisplayPostcard(snapshot, scoreData);
            }
            else
            {
                Debug.Log($"[BRollPhotoCapture] Postcard captured: {scoreData.LandmarkTitle} | {scoreData.StarRating} Stars | Speed: {scoreData.Airspeed:F1} m/s");
            }
        }

        private IEnumerator TriggerFlashRoutine()
        {
            viewfinderFlashOverlay.alpha = 0.85f;
            float elapsed = 0f;

            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                viewfinderFlashOverlay.alpha = Mathf.Lerp(0.85f, 0f, elapsed / flashDuration);
                yield return null;
            }

            viewfinderFlashOverlay.alpha = 0f;
        }

        private Texture2D CaptureFrame(int width, int height)
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevRT = RenderTexture.active;

            if (mainCamera == null) mainCamera = Camera.main;

            if (mainCamera != null)
            {
                mainCamera.targetTexture = rt;
                mainCamera.Render();
                mainCamera.targetTexture = null;
            }

            RenderTexture.active = rt;
            Texture2D photo = new Texture2D(width, height, TextureFormat.RGB24, false);
            photo.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            photo.Apply();

            RenderTexture.active = prevRT;
            Destroy(rt);

            return photo;
        }
    }

    /// <summary>
    /// Minimal interface/class for the slide-out Postcard UI.
    /// Can be wired up to a standard Unity UGUI or UI Toolkit canvas.
    /// </summary>
    public abstract class PostcardCardUI : MonoBehaviour
    {
        public abstract void DisplayPostcard(Texture2D photoTexture, PhotoScoreData data);
    }
}
