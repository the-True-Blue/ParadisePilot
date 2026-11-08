using UnityEngine;
using ParadisePilot.Core;

namespace ParadisePilot.Flight
{
    /// <summary>
    /// Spherical Follow Camera for Paradise Pilot.
    /// Implements:
    /// - Tangential spherical horizon leveling (never disorients at poles)
    /// - Dynamic Dual-Band FOV transitions (Cruise 75° vs Skim 55°)
    /// - Smooth position and attitude damping
    /// - Forward look-ahead offset for arcade responsiveness
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FlightCamera : MonoBehaviour
    {
        [Header("Target & Anchor")]
        [SerializeField] private ArcadeFlightController targetFlightController;

        [Header("Cruise Band Settings (> 150m)")]
        [Tooltip("Follow distance behind plane in high-altitude Cruise.")]
        [SerializeField] private float cruiseDistance = 24f;

        [Tooltip("Height above plane in high-altitude Cruise.")]
        [SerializeField] private float cruiseHeight = 8f;

        [Tooltip("Field of View in high-altitude Cruise.")]
        [SerializeField] private float cruiseFOV = 75f;

        [Header("Skim Band Settings (< 30m)")]
        [Tooltip("Follow distance behind plane in low-altitude Skim.")]
        [SerializeField] private float skimDistance = 14f;

        [Tooltip("Height above plane in low-altitude Skim.")]
        [SerializeField] private float skimHeight = 4.5f;

        [Tooltip("Field of View in low-altitude Skim.")]
        [SerializeField] private float skimFOV = 55f;

        [Header("Camera Dynamics")]
        [Tooltip("Look-ahead distance along plane's forward vector.")]
        [SerializeField] private float lookAheadDistance = 10f;

        [Tooltip("Position follow damping speed.")]
        [SerializeField] private float positionSmoothSpeed = 8f;

        [Tooltip("Rotation follow damping speed.")]
        [SerializeField] private float rotationSmoothSpeed = 10f;

        [Tooltip("FOV transition speed.")]
        [SerializeField] private float fovTransitionSpeed = 2.5f;

        private Camera cam;
        private PlanetDescriptor planet;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            planet = PlanetDescriptor.Instance;
            if (targetFlightController == null)
            {
                targetFlightController = FindAnyObjectByType<ArcadeFlightController>();
            }

            if (cam != null)
            {
                cam.fieldOfView = skimFOV;
            }
        }

        private void LateUpdate()
        {
            if (targetFlightController == null) return;
            if (planet == null)
            {
                planet = PlanetDescriptor.Instance;
                if (planet == null) return;
            }

            Transform target = targetFlightController.transform;
            Vector3 planetUp = planet.GetUpVector(target.position);

            // 1. Calculate band interpolation factor (0 = Skim, 1 = Cruise)
            float alt = targetFlightController.CurrentAltitude;
            float bandFactor = Mathf.InverseLerp(planet.SkimBandMaxAltitude, planet.CruiseBandMinAltitude, alt);

            float targetDistance = Mathf.Lerp(skimDistance, cruiseDistance, bandFactor);
            float targetHeight = Mathf.Lerp(skimHeight, cruiseHeight, bandFactor);
            float targetFOV = Mathf.Lerp(skimFOV, cruiseFOV, bandFactor);

            // 2. Compute camera ideal position relative to spherical coordinates
            Vector3 forwardTangential = Vector3.ProjectOnPlane(target.forward, planetUp).normalized;
            Vector3 targetPosition = target.position - (forwardTangential * targetDistance) + (planetUp * targetHeight);

            // Smooth position
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionSmoothSpeed);

            // 3. Compute camera look rotation
            Vector3 lookTarget = target.position + (target.forward * lookAheadDistance);
            Vector3 lookDirection = (lookTarget - transform.position).normalized;

            // Maintain planetUp as the camera's up reference so the horizon stays spherical
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, planetUp);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);

            // 4. Smooth FOV
            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovTransitionSpeed);
            }
        }
    }
}
