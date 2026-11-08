using UnityEngine;
using System;
using ParadisePilot.Core;
using ParadisePilot.Flight;

namespace ParadisePilot.Discovery
{
    public enum InsightStream
    {
        Cultural,   // Architecture, folklore, traditions
        Ecology,    // Wildlife, flora, geothermal
        Recreation, // Surf bays, hiking ridges
        Community   // Harbors, natural spring water
    }

    /// <summary>
    /// Wii Sports Resort inspired Shimmer Ring (i-Point) for Paradise Pilot.
    /// Floats above POIs and hidden landmarks on the stylized planet.
    /// Approaching causes the dashboard radar to chirp at a quickening tempo.
    /// Flying through triggers the resonant chime, shutter snap, and B-roll photo capture.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ShimmerRing : MonoBehaviour
    {
        [Header("Landmark Metadata")]
        [SerializeField] private string landmarkID = "poi_aegean_stack";
        [SerializeField] private string locationTitle = "The Sunken Spires";
        [TextArea(2, 4)]
        [SerializeField] private string historicalLore = "Ancient limestone sea stacks where migratory gulls rest amidst the Mediterranean breeze.";
        [SerializeField] private InsightStream streamType = InsightStream.Cultural;

        [Header("Ring Visuals & Audio")]
        [SerializeField] private float ringRadius = 8f;
        [SerializeField] private Color ringColor = new Color(0.3f, 0.85f, 1f, 0.9f);
        [SerializeField] private AudioClip proximityChirpSound;
        [SerializeField] private AudioClip ringCollectSound;

        [Header("Camera Framing Target")]
        [Tooltip("Transform that the camera will frame when the photo snapshot is taken.")]
        [SerializeField] private Transform photoSubjectTarget;

        [Header("Radar Detection")]
        [Tooltip("Distance at which the cockpit radar starts chirping.")]
        [SerializeField] private float radarDetectionRange = 350f;

        // Events
        public static event Action<ShimmerRing, PhotoScoreData> OnRingCollected;

        private bool isCollected;
        private AudioSource audioSource;
        private float nextRadarChirpTime;

        public string LandmarkID => landmarkID;
        public string LocationTitle => locationTitle;
        public string HistoricalLore => historicalLore;
        public InsightStream StreamType => streamType;
        public Transform PhotoSubjectTarget => photoSubjectTarget != null ? photoSubjectTarget : transform;
        public bool IsCollected => isCollected;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f; // 3D sound
                audioSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            // Auto-align ring so its 'up' points along planet-normal
            if (PlanetDescriptor.Instance != null)
            {
                Vector3 planetUp = PlanetDescriptor.Instance.GetUpVector(transform.position);
                transform.rotation = SphericalMath.AlignToPlanetNormal(transform.rotation, planetUp);
            }
        }

        private void Update()
        {
            if (isCollected) return;

            // Subtle rotating shimmer effect
            transform.Rotate(Vector3.forward, 45f * Time.deltaTime, Space.Self);
        }

        /// <summary>
        /// Evaluates radar chirp frequency when plane is approaching.
        /// Called by cockpit radar or player proximity.
        /// </summary>
        public void ProcessRadarPing(Vector3 aircraftPosition)
        {
            if (isCollected) return;

            float distance = Vector3.Distance(aircraftPosition, transform.position);
            if (distance > radarDetectionRange) return;

            // Radar chirp tempo quickens as distance decreases (0.1s at closest to 1.2s at max range)
            float normalizedDist = distance / radarDetectionRange;
            float chirpInterval = Mathf.Lerp(0.12f, 1.2f, normalizedDist);

            if (Time.time >= nextRadarChirpTime)
            {
                nextRadarChirpTime = Time.time + chirpInterval;
                if (proximityChirpSound != null && audioSource != null)
                {
                    audioSource.pitch = Mathf.Lerp(1.4f, 0.9f, normalizedDist);
                    audioSource.PlayOneShot(proximityChirpSound, 0.4f);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCollected) return;

            ArcadeFlightController flight = other.GetComponentInParent<ArcadeFlightController>();
            if (flight != null)
            {
                CollectRing(flight);
            }
        }

        private void CollectRing(ArcadeFlightController flight)
        {
            isCollected = true;

            // 1. Play tactile resonant chime
            if (ringCollectSound != null && audioSource != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(ringCollectSound, 1.0f);
            }

            // 2. Calculate B-Roll Score based on GDD v2.0 criteria:
            // Bank angle, Speed, and Golden-Hour / approach alignment
            PhotoScoreData scoreData = CalculateBRollScore(flight);

            // 3. Dispatch global collection event for Postcard UI and Photo Capture
            OnRingCollected?.Invoke(this, scoreData);

            // 4. Fade ring visual
            gameObject.SetActive(false);
        }

        private PhotoScoreData CalculateBRollScore(ArcadeFlightController flight)
        {
            PhotoScoreData data = new PhotoScoreData();
            data.LandmarkTitle = locationTitle;
            data.Lore = historicalLore;
            data.Stream = streamType;

            // Rating Criterion 1: Bank Angle (Daring knife-edge or high bank yields higher score)
            float rollAngle = Mathf.Abs(flight.transform.eulerAngles.z);
            if (rollAngle > 180f) rollAngle = 360f - rollAngle;
            bool highBank = rollAngle > 30f;

            // Rating Criterion 2: Speed (High speed through ring)
            bool highSpeed = flight.CurrentSpeed > 60f;

            // Rating Criterion 3: Alignment with ring normal
            float approachAngle = Mathf.Abs(Vector3.Dot(flight.transform.forward, transform.forward));
            bool cleanAlignment = approachAngle > 0.6f;

            int stars = 1;
            if (highBank || highSpeed) stars++;
            if (highBank && highSpeed && cleanAlignment) stars = 3;

            data.StarRating = stars;
            data.Airspeed = flight.CurrentSpeed;
            data.BankAngle = rollAngle;

            return data;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = ringColor;
            Gizmos.DrawWireSphere(transform.position, ringRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 4f);
        }
    }

    [System.Serializable]
    public struct PhotoScoreData
    {
        public string LandmarkTitle;
        public string Lore;
        public InsightStream Stream;
        public int StarRating; // 1 to 3 Stars
        public float Airspeed;
        public float BankAngle;
    }
}
