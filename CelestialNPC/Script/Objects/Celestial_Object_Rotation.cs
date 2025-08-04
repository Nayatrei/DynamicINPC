using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CelestialCyclesSystem
{
    public class Celestial_Object_Rotation : MonoBehaviour
    {
        [SerializeField]
        private Vector3 fixedRotationEulers = new Vector3(-90, 0, 0); // Default fixed rotation

        private Quaternion fixedRotation;

        void Start()
        {
            // Convert the Euler angles to a Quaternion based on the specified fixed rotation
            fixedRotation = Quaternion.Euler(fixedRotationEulers);
        }

        void LateUpdate()
        {
            // Override the GameObject's rotation with the fixed rotation
            transform.rotation = fixedRotation;
        }
    }
}
