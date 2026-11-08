using UnityEngine;
using ParadisePilot.Core;

namespace ParadisePilot.Flight
{
    /// <summary>
    /// Arcade Flight Controller for Paradise Pilot.
    /// Implements:
    /// - Dual Altitude Bands (Cruise > 150m vs Skim < 30m)
    /// - 25% Ground Effect Lift & Drag Reduction (< 15m)
    /// - Planet-Normal Spherical Tangential Orientation
    /// - Decoupled Model Banking & Pitch for tactile kinesthetic feel
    /// - Soft Bounce Collision Recovery (Forgiving failsafes, no fatal crashes)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ArcadeFlightController : MonoBehaviour
    {
        [Header("Speed & Throttle")]
        [Tooltip("Cruising speed in the high-altitude Cruise Band (> 150m).")]
        [SerializeField] private float cruiseSpeed = 90f;

        [Tooltip("Agile cruising speed in the low-altitude Skim Band (< 30m).")]
        [SerializeField] private float skimSpeed = 45f;

        [Tooltip("Boost multiplier when holding boost input.")]
        [SerializeField] private float boostMultiplier = 1.6f;

        [Tooltip("Deceleration rate when braking / air-braking.")]
        [SerializeField] private float brakeMultiplier = 0.6f;

        [Tooltip("Acceleration responsiveness (lerp speed).")]
        [SerializeField] private float accelerationLerp = 3.5f;

        [Header("Steering & Aerodynamics")]
        [Tooltip("Yaw turn rate around planet up axis (deg/sec).")]
        [SerializeField] private float yawTurnRate = 50f;

        [Tooltip("Pitch rate for diving and climbing (deg/sec).")]
        [SerializeField] private float pitchRate = 45f;

        [Tooltip("Maximum allowed visual bank angle when turning (deg).")]
        [SerializeField] private float maxVisualBankAngle = 65f;

        [Tooltip("Maximum visual pitch tilt for the child model (deg).")]
        [SerializeField] private float maxVisualPitchAngle = 35f;

        [Tooltip("Smoothing factor for visual bank/pitch transitions.")]
        [SerializeField] private float visualDamping = 8f;

        [Header("Ground Effect (< 15m)")]
        [Tooltip("Max upward cushion velocity applied at 0m altitude.")]
        [SerializeField] private float maxGroundEffectCushion = 12f;

        [Header("Collision & Soft Bounce Resolver")]
        [Tooltip("Collision bounce elasticity (rebound velocity dampening).")]
        [SerializeField] private float bounceDampening = 0.65f;

        [Tooltip("Cooldown in seconds between bounce events to avoid rapid jitter.")]
        [SerializeField] private float bounceCooldown = 0.4f;

        [Header("Visual Model Child")]
        [Tooltip("The visual airplane model that tilts, pitches, and banks.")]
        [SerializeField] private Transform planeModelVisual;

        // Runtime State
        private Rigidbody rb;
        private PlanetDescriptor planet;
        private float currentSpeed;
        private float targetSpeed;
        private float currentAltitude = 100f;
        private bool isInGroundEffect;
        private float groundEffectMultiplier = 1f;

        // Visual attitude
        private float currentVisualRoll;
        private float currentVisualPitch;
        private float lastBounceTime;

        // Public Accessors for UI/Camera/Audio
        public float CurrentSpeed => currentSpeed;
        public float TargetSpeed => targetSpeed;
        public float CurrentAltitude => currentAltitude;
        public bool IsInSkimBand => currentAltitude < (planet != null ? planet.SkimBandMaxAltitude : 30f);
        public bool IsInCruiseBand => currentAltitude > (planet != null ? planet.CruiseBandMinAltitude : 150f);
        public bool IsInGroundEffect => isInGroundEffect;
        public float GroundEffectFactor => groundEffectMultiplier;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            currentSpeed = skimSpeed;
            targetSpeed = skimSpeed;
        }

        private void Start()
        {
            planet = PlanetDescriptor.Instance;
            if (planet == null)
            {
                Debug.LogWarning("[ArcadeFlightController] No PlanetDescriptor found. Ensure one exists in the scene.");
            }
        }

        private void Update()
        {
            ReadInputAndCalculateTargetSpeed();
            UpdateVisualAttitude();
        }

        private void FixedUpdate()
        {
            if (planet == null)
            {
                planet = PlanetDescriptor.Instance;
                if (planet == null) return;
            }

            Vector3 planetUp = planet.GetUpVector(transform.position);

            UpdateAltitudeAndGroundEffect(planetUp);
            ApplySphericalOrientation(planetUp);
            ApplySteering(planetUp);
            ApplyForwardThrust(planetUp);
        }

        private void ReadInputAndCalculateTargetSpeed()
        {
            bool isDiving = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S) || Input.GetAxis("Vertical") < -0.1f;
            bool isBoosting = Input.GetKey(KeyCode.LeftShift) || Input.GetButton("Fire3");
            bool isBraking = Input.GetKey(KeyCode.Space) || Input.GetButton("Fire1");

            // Base speed adjusts depending on altitude band
            float baseBandSpeed = IsInSkimBand ? skimSpeed : cruiseSpeed;

            if (isDiving && !IsInSkimBand)
            {
                // Smooth dive into skim band
                baseBandSpeed = Mathf.Lerp(cruiseSpeed, skimSpeed, 0.5f);
            }

            if (isBoosting)
            {
                targetSpeed = baseBandSpeed * boostMultiplier;
            }
            else if (isBraking)
            {
                targetSpeed = baseBandSpeed * brakeMultiplier;
            }
            else
            {
                targetSpeed = baseBandSpeed;
            }

            // Ground effect drag reduction (speeds up gliding slightly)
            if (isInGroundEffect)
            {
                targetSpeed *= (1f + (groundEffectMultiplier - 1f) * 0.4f);
            }

            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * accelerationLerp);
        }

        private void UpdateAltitudeAndGroundEffect(Vector3 planetUp)
        {
            if (planet.SampleSurfaceAltitude(transform.position, 200f, out RaycastHit hit, out float sampledAlt))
            {
                currentAltitude = sampledAlt;
            }
            else
            {
                currentAltitude = planet.GetAltitudeAboveOcean(transform.position);
            }

            // Ground effect kicks in below threshold (15m in GDD)
            if (currentAltitude <= planet.GroundEffectThreshold)
            {
                isInGroundEffect = true;
                groundEffectMultiplier = SphericalMath.CalculateGroundEffect(currentAltitude, planet.GroundEffectThreshold, 0.25f);
            }
            else
            {
                isInGroundEffect = false;
                groundEffectMultiplier = 1f;
            }
        }

        private void ApplySphericalOrientation(Vector3 planetUp)
        {
            // Continuously reorient the root body so its 'up' points along planet-normal
            transform.rotation = SphericalMath.AlignToPlanetNormal(transform.rotation, planetUp);
        }

        private void ApplySteering(Vector3 planetUp)
        {
            float rollInput = Input.GetAxis("Horizontal"); // A/D or Left Stick X
            float pitchInput = Input.GetAxis("Vertical");   // W/S or Left Stick Y

            // 1. Yaw rotation around planet normal
            float yawAmount = rollInput * yawTurnRate * Time.fixedDeltaTime;
            transform.Rotate(planetUp, yawAmount, Space.World);

            // 2. Pitch rotation (nose up/down)
            float pitchAmount = -pitchInput * pitchRate * Time.fixedDeltaTime;
            transform.Rotate(transform.right, pitchAmount, Space.World);
        }

        private void ApplyForwardThrust(Vector3 planetUp)
        {
            Vector3 forwardVelocity = transform.forward * currentSpeed;

            // Apply upward cushion when inside ground effect
            if (isInGroundEffect)
            {
                float cushionStrength = (1f - (currentAltitude / planet.GroundEffectThreshold)) * maxGroundEffectCushion;
                forwardVelocity += planetUp * cushionStrength;
            }

            rb.linearVelocity = forwardVelocity;
        }

        private void UpdateVisualAttitude()
        {
            if (planeModelVisual == null) return;

            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // Bank visually into the turn
            float targetRoll = -horizontal * maxVisualBankAngle;
            // Visual pitch response
            float targetPitch = vertical * maxVisualPitchAngle;

            currentVisualRoll = Mathf.Lerp(currentVisualRoll, targetRoll, Time.deltaTime * visualDamping);
            currentVisualPitch = Mathf.Lerp(currentVisualPitch, targetPitch, Time.deltaTime * visualDamping);

            planeModelVisual.localRotation = Quaternion.Euler(currentVisualPitch, 0f, currentVisualRoll);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time - lastBounceTime < bounceCooldown) return;

            // Forgiving Failsafe: Soft Bounce Resolver (GDD v2.0 Section 4)
            ContactPoint contact = collision.contacts[0];
            Vector3 surfaceNormal = contact.normal;

            // Reflect velocity off the surface normal
            Vector3 reflectedVel = Vector3.Reflect(rb.linearVelocity, surfaceNormal) * bounceDampening;

            // Ensure reflected velocity has an outward radial bias so the plane escapes the surface
            Vector3 planetUp = planet != null ? planet.GetUpVector(transform.position) : transform.position.normalized;
            if (Vector3.Dot(reflectedVel, planetUp) < 0.2f)
            {
                reflectedVel += planetUp * (currentSpeed * 0.35f);
            }

            rb.linearVelocity = reflectedVel;
            currentSpeed *= bounceDampening;
            lastBounceTime = Time.time;

            // Align heading with new movement direction
            if (reflectedVel.sqrMagnitude > 1f)
            {
                transform.rotation = Quaternion.LookRotation(reflectedVel.normalized, planetUp);
            }
        }
    }
}
