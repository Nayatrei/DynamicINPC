using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CelestialCyclesSystem
{
    public class Celestial_Gizmo : MonoBehaviour
    {
        public Color gizmoColor = Color.red;
        public float gizmoSize = 0.1f;

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, gizmoSize);
        }
    }
}
