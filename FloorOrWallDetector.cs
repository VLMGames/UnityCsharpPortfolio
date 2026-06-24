using UnityEngine;

namespace BuildSystem
{
    public class FloorOrWallDetector : MonoBehaviour
    {
        [Tooltip("Threshold for floor detection (closer to 1, more horizontal)")]
        [Range(0.5f, 1f)]
        [SerializeField] private float floorDotThreshold = 0.85f;

        [Tooltip("Threshold for wall detection (closer to 0, more vertical)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float wallDotThreshold = 0.25f;

        public PlacementType GetPlacementType(Vector3 normal)
        {
            float dot = Vector3.Dot(normal, Vector3.up);

            if (dot >= floorDotThreshold) return PlacementType.Floor;
            if (Mathf.Abs(dot) <= wallDotThreshold) return PlacementType.Wall;

            return PlacementType.Free;
        }
    }
}