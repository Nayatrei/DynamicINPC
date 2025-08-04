using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
namespace CelestialCyclesSystem
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(SplineContainer))]
    public class Celestial_Areas : MonoBehaviour
    {
        public enum AreaType { Bound, Path }
        public AreaType areaType;
        private Bounds areaBounds;
        public float pathWidth = 4f;
        public SplineContainer currentSpline;

        // Lists to hold calculated boundary points
        private List<Vector3> leftBoundaryPoints = new List<Vector3>();
        private List<Vector3> rightBoundaryPoints = new List<Vector3>();

        [Header("Manage Npc")]
        [HideInInspector]
        public GameObject tempStatus;
        private GameObject pTempStatus;

        public List<Celestial_NPC> celestialNPCs = new List<Celestial_NPC>();

        void Awake()
        {
            currentSpline = GetComponent<SplineContainer>();
            //Bounds bounds = CalculateBoundsFromKnots(currentSpline);
            if (areaType == AreaType.Bound)
            {
                CalculateAreaBounds(); // Initialize bounds for each area
            }
            if (areaType == AreaType.Path)
            {
                CalculateBoundary();
            }

        }

        public void ChangeAll()
        {
            celestialNPCs.Clear();
            FindNPCInChildren(transform);

        }



        [ContextMenu("UpdateGizmo")]
        public void UpdateGizmo()
        {
            if (areaType == AreaType.Bound)
            {
                CalculateAreaBounds(); // Initialize bounds for each area
            }
            if (areaType == AreaType.Path)
            {
                CalculateBoundary();
            }
        }
        private void FindNPCInChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                Celestial_NPC celestialNPC = child.GetComponent<Celestial_NPC>();
                if (celestialNPC != null)
                {
                    celestialNPCs.Add(celestialNPC);
                }
            }
        }

        public void CalculateAreaBounds()
        {

            if (currentSpline == null)
            {
                Debug.LogError("SplineContainer component not found on GameObject.", this);
                return;
            }
            areaBounds = CalculateBoundsFromKnots(currentSpline);
        }

        void CalculateBoundary()
        {
            leftBoundaryPoints.Clear();
            rightBoundaryPoints.Clear();

            if (currentSpline == null || currentSpline.Spline.Count == 0) return;

            Spline spline = currentSpline.Spline;
            for (float t = 0; t <= 1; t += 0.01f) // Increment t to move along the spline
            {
                Vector3 position = spline.EvaluatePosition(t);
                Vector3 worldPosition = transform.TransformPoint(position);
                Vector3 tangent = spline.EvaluateTangent(t);
                Vector3 normal = Vector3.Cross(tangent, Vector3.up).normalized; // Calculate normal to the tangent for width calculation

                Vector3 leftBoundary = worldPosition + normal * (pathWidth / 2);
                Vector3 rightBoundary = worldPosition - normal * (pathWidth / 2);

                leftBoundaryPoints.Add(leftBoundary);
                rightBoundaryPoints.Add(rightBoundary);
            }
        }


        // Checks if the position is within the bounds of the area
        public bool IsPositionWithinArea(Vector3 position)
        {
            return areaBounds.Contains(position);
        }

        public Bounds GetSplineBounds()
        {
            return areaBounds;
        }

        public Vector3 GetRandomPositionWithinBounds()
        {
            Bounds bounds = CalculateBoundsFromKnots(currentSpline);

            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.min.y, // This would set it to the bottom of the bounds; adjust as needed
                Random.Range(bounds.min.z, bounds.max.z)
            );
        }

        Bounds CalculateBoundsFromKnots(SplineContainer container)
        {
            if (container == null || container.Spline.Count == 0) return new Bounds(transform.position, Vector3.zero); ;

            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;

            foreach (var knot in container.Spline)
            {
                Vector3 worldPosition = transform.TransformPoint(knot.Position);
                min = Vector3.Min(min, worldPosition);
                max = Vector3.Max(max, worldPosition);
            }

            Vector3 size = max - min;
            Vector3 center = min + size * 0.5f;
            return new Bounds(center, size);
        }
        /// Use Path 
        /// 

        void OnDrawGizmos()
        {
            switch (areaType)
            {
                case Celestial_Areas.AreaType.Bound:
                    Gizmos.color = Color.cyan;

                    Bounds bounds = CalculateBoundsFromKnots(currentSpline); // Assuming this gets your bounds

                    // Bottom
                    Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z), new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
                    Gizmos.DrawLine(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z), new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
                    Gizmos.DrawLine(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z), new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
                    Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z), new Vector3(bounds.min.x, bounds.min.y, bounds.min.z));

                    // Top
                    Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z), new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
                    Gizmos.DrawLine(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z), new Vector3(bounds.max.x, bounds.max.y, bounds.max.z));
                    Gizmos.DrawLine(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z), new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));
                    Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z), new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));

                    // Sides
                    Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z), new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
                    Gizmos.DrawLine(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z), new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
                    Gizmos.DrawLine(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z), new Vector3(bounds.max.x, bounds.max.y, bounds.max.z));
                    Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z), new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));
                    break;

                case Celestial_Areas.AreaType.Path:
                    // Example Gizmo drawing for visualizing the path or boundary
                    if (areaType == AreaType.Path && currentSpline != null)
                    {
                        // Draw the path with width

                        Gizmos.color = Color.blue; // Set the Gizmo color to yellow for visibility
                        for (int i = 0; i < leftBoundaryPoints.Count - 1; i++)
                        {
                            // Draw left boundary
                            Gizmos.DrawLine(leftBoundaryPoints[i], leftBoundaryPoints[i + 1]);

                            // Draw right boundary
                            Gizmos.DrawLine(rightBoundaryPoints[i], rightBoundaryPoints[i + 1]);
                        }
                    }
                        
                    
                    break;
            }
            

        }
        }



    }
