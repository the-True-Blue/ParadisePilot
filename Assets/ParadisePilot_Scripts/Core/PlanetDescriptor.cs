using UnityEngine;

namespace ParadisePilot.Core
{
    /// <summary>
    /// Defines global planetary properties for the stylized celestial sphere in Paradise Pilot.
    /// Acts as the central anchor for flight physics, ground-effect raycasting, and camera tracking.
    /// </summary>
    public class PlanetDescriptor : MonoBehaviour
    {
        public static PlanetDescriptor Instance { get; private set; }

        [Header("Planetary Geometry")]
        [Tooltip("Radius of the stylized ocean sphere in Unity meters.")]
        [SerializeField] private float oceanRadius = 500f;

        [Tooltip("World space center of the planet.")]
        [SerializeField] private Vector3 center = Vector3.zero;

        [Header("Atmosphere & Altitude Bands (GDD v2.0)")]
        [Tooltip("Altitude threshold above terrain/ocean entering the Skim Band (< 30m).")]
        [SerializeField] private float skimBandMaxAltitude = 30f;

        [Tooltip("Altitude threshold above terrain/ocean entering the Cruise Band (> 150m).")]
        [SerializeField] private float cruiseBandMinAltitude = 150f;

        [Tooltip("Maximum ground effect interaction ceiling (15m in GDD).")]
        [SerializeField] private float groundEffectThreshold = 15f;

        [Header("Layer Masks")]
        [Tooltip("Layer mask for solid terrain (islands, rocks, ruins, bridges).")]
        [SerializeField] private LayerMask terrainLayer = ~0;

        [Tooltip("Layer mask for water surfaces.")]
        [SerializeField] private LayerMask waterLayer = ~0;

        public float OceanRadius => oceanRadius;
        public Vector3 Center => center;
        public float SkimBandMaxAltitude => skimBandMaxAltitude;
        public float CruiseBandMinAltitude => cruiseBandMinAltitude;
        public float GroundEffectThreshold => groundEffectThreshold;
        public LayerMask TerrainLayer => terrainLayer;
        public LayerMask WaterLayer => waterLayer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Returns the radial outward normal vector at a given position.
        /// </summary>
        public Vector3 GetUpVector(Vector3 worldPosition)
        {
            return SphericalMath.GetPlanetUp(worldPosition, center);
        }

        /// <summary>
        /// Returns the inward gravity vector at a given position.
        /// </summary>
        public Vector3 GetGravityVector(Vector3 worldPosition)
        {
            return SphericalMath.GetPlanetGravity(worldPosition, center);
        }

        /// <summary>
        /// Gets raw altitude above the baseline ocean sphere.
        /// </summary>
        public float GetAltitudeAboveOcean(Vector3 worldPosition)
        {
            return SphericalMath.GetAltitude(worldPosition, center, oceanRadius);
        }

        /// <summary>
        /// Raycasts downward toward the planet core to find true physical altitude above terrain or ocean.
        /// </summary>
        public bool SampleSurfaceAltitude(Vector3 worldPosition, float maxDistance, out RaycastHit hitInfo, out float altitude)
        {
            Vector3 down = GetGravityVector(worldPosition);
            LayerMask combinedMask = terrainLayer | waterLayer;

            if (Physics.Raycast(worldPosition, down, out hitInfo, maxDistance, combinedMask))
            {
                altitude = hitInfo.distance;
                return true;
            }

            // Fallback to ocean geometric radius if nothing hit
            altitude = GetAltitudeAboveOcean(worldPosition);
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, oceanRadius);

            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.15f);
            Gizmos.DrawWireSphere(center, oceanRadius + skimBandMaxAltitude);

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.15f);
            Gizmos.DrawWireSphere(center, oceanRadius + cruiseBandMinAltitude);
        }
    }
}
