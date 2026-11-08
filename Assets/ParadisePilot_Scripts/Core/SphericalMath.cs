using UnityEngine;

namespace ParadisePilot.Core
{
    /// <summary>
    /// Pure mathematical utility for spherical planet kinematics, 
    /// planet-normal gravity, geodesic coordinates, and tangential projections.
    /// Designed for Paradise Pilot (Unity 6 / 2022.3 URP).
    /// </summary>
    public static class SphericalMath
    {
        /// <summary>
        /// Computes the outward surface normal (radial up) from planet center (0,0,0) to world position.
        /// </summary>
        public static Vector3 GetPlanetUp(Vector3 worldPosition, Vector3 planetCenter)
        {
            Vector3 offset = worldPosition - planetCenter;
            return offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.up;
        }

        public static Vector3 GetPlanetUp(Vector3 worldPosition)
        {
            return GetPlanetUp(worldPosition, Vector3.zero);
        }

        /// <summary>
        /// Computes planet gravity direction (radially inward towards planet center).
        /// </summary>
        public static Vector3 GetPlanetGravity(Vector3 worldPosition, Vector3 planetCenter)
        {
            return -GetPlanetUp(worldPosition, planetCenter);
        }

        public static Vector3 GetPlanetGravity(Vector3 worldPosition)
        {
            return -GetPlanetUp(worldPosition, Vector3.zero);
        }

        /// <summary>
        /// Computes altitude above the spherical planet surface.
        /// </summary>
        public static float GetAltitude(Vector3 worldPosition, Vector3 planetCenter, float planetRadius)
        {
            return Vector3.Distance(worldPosition, planetCenter) - planetRadius;
        }

        /// <summary>
        /// Computes altitude above planet center at (0,0,0).
        /// </summary>
        public static float GetAltitude(Vector3 worldPosition, float planetRadius)
        {
            return worldPosition.magnitude - planetRadius;
        }

        /// <summary>
        /// Projects a world position onto the surface of a sphere of radius R.
        /// </summary>
        public static Vector3 ProjectToSphere(Vector3 worldPosition, Vector3 planetCenter, float targetRadius)
        {
            Vector3 direction = GetPlanetUp(worldPosition, planetCenter);
            return planetCenter + direction * targetRadius;
        }

        /// <summary>
        /// Aligns an existing rotation so its 'up' vector aligns with the planet's radial normal,
        /// preserving its current forward heading as closely as possible.
        /// </summary>
        public static Quaternion AlignToPlanetNormal(Quaternion currentRotation, Vector3 planetUp)
        {
            Vector3 currentUp = currentRotation * Vector3.up;
            Quaternion reorientation = Quaternion.FromToRotation(currentUp, planetUp);
            return reorientation * currentRotation;
        }

        /// <summary>
        /// Computes a rotation that points along a tangential forward heading while maintaining planetUp.
        /// </summary>
        public static Quaternion LookRotationOnSphere(Vector3 forwardHeading, Vector3 planetUp)
        {
            Vector3 projectedForward = Vector3.ProjectOnPlane(forwardHeading, planetUp).normalized;
            if (projectedForward.sqrMagnitude < 0.0001f)
            {
                projectedForward = Vector3.Cross(planetUp, Vector3.right).normalized;
                if (projectedForward.sqrMagnitude < 0.0001f)
                {
                    projectedForward = Vector3.Cross(planetUp, Vector3.forward).normalized;
                }
            }
            return Quaternion.LookRotation(projectedForward, planetUp);
        }

        /// <summary>
        /// Converts a unit spherical direction into Latitude and Longitude (in degrees).
        /// </summary>
        public static Vector2 CartesianToLatLong(Vector3 unitPosition)
        {
            float lat = Mathf.Asin(Mathf.Clamp(unitPosition.y, -1f, 1f)) * Mathf.Rad2Deg;
            float lon = Mathf.Atan2(unitPosition.x, -unitPosition.z) * Mathf.Rad2Deg;
            return new Vector2(lat, lon);
        }

        /// <summary>
        /// Converts Latitude and Longitude (degrees) on a sphere of radius R to Cartesian (x, y, z).
        /// </summary>
        public static Vector3 LatLongToCartesian(float latitudeDeg, float longitudeDeg, float radius)
        {
            float latRad = latitudeDeg * Mathf.Deg2Rad;
            float lonRad = longitudeDeg * Mathf.Deg2Rad;

            float cosLat = Mathf.Cos(latRad);
            float x = radius * cosLat * Mathf.Sin(lonRad);
            float y = radius * Mathf.Sin(latRad);
            float z = -radius * cosLat * Mathf.Cos(lonRad);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Maps a 3D unit point on a sphere to equirectangular UV coordinates [0, 1].
        /// </summary>
        public static Vector2 PointToEquirectangularUV(Vector3 unitPosition)
        {
            Vector2 latLon = CartesianToLatLong(unitPosition.normalized);
            float u = (latLon.y / 360f) + 0.5f;
            float v = (latLon.x / 180f) + 0.5f;
            return new Vector2(u, v);
        }

        /// <summary>
        /// Computes the great-circle surface distance between two points on a spherical planet.
        /// </summary>
        public static float GreatCircleDistance(Vector3 pointA, Vector3 pointB, Vector3 planetCenter, float planetRadius)
        {
            Vector3 uA = (pointA - planetCenter).normalized;
            Vector3 uB = (pointB - planetCenter).normalized;
            float dot = Mathf.Clamp(Vector3.Dot(uA, uB), -1f, 1f);
            float centralAngle = Mathf.Acos(dot); // radians
            return planetRadius * centralAngle;
        }

        /// <summary>
        /// Calculates the Ground Effect multiplier based on altitude (m).
        /// Returns 1.0 (no effect) at or above 15m, scaling up to 1.25x (+25% lift bonus) at 0m.
        /// </summary>
        public static float CalculateGroundEffect(float altitudeAboveSurface, float threshold = 15f, float maxBonus = 0.25f)
        {
            if (altitudeAboveSurface >= threshold) return 1f;
            if (altitudeAboveSurface <= 0f) return 1f + maxBonus;

            float factor = 1f - (altitudeAboveSurface / threshold);
            return 1f + (factor * maxBonus);
        }
    }
}
