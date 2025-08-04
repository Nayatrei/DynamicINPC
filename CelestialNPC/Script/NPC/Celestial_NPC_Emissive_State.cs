using UnityEngine;
namespace CelestialCyclesSystem
{
    public class Celestial_NPC_Emissive_State : MonoBehaviour
    {
        private Celestial_NPC thisNPC;
        [ColorUsage(true, true)] // Enables HDR color picker in the Inspector
        public Color movingToObjectColor = Color.yellow;
        [ColorUsage(true, true)] // Enables HDR color picker in the Inspector
        public Color roamingColor = Color.green;
        [ColorUsage(true, true)] // Enables HDR color picker in the Inspector
        public Color waitingInQueueColor = Color.cyan;
        [ColorUsage(true, true)] // Enables HDR color picker in the Inspector
        public Color stopUpdateColor = Color.red;
        [ColorUsage(true, true)] // Enables HDR color picker in the Inspector
        public Color isSatisfiedColor = Color.blue;
        [ColorUsage(true, true)] // Enables HDR color picker in the Inspector
        public Color isRecovering = Color.magenta;
        private void Start()
        {

                thisNPC = GetComponent<Celestial_NPC>();
                    
        }

        private void Update()
        {
            if (!thisNPC._navComponent.enabled)
            {
                ChangeEmissiveColor(isRecovering);
            }
            if (thisNPC.IsMovingToObject)
            {
                ChangeEmissiveColor(movingToObjectColor);
            }
            if (thisNPC.isRoaming)
            {
                ChangeEmissiveColor(roamingColor);
            }
            if (thisNPC.isWaitingInQueue)
            {
                ChangeEmissiveColor(waitingInQueueColor);
            }
            if(thisNPC.stopUpdate)
            {
                ChangeEmissiveColor(stopUpdateColor);
            }
            if (thisNPC.isSatisfied)
            {
                ChangeEmissiveColor(isSatisfiedColor);
            }
        }

        // Call this method to change the emissive color
        public void ChangeEmissiveColor(Color newColor)
        {
            // Find the Skinned Mesh Renderer component in child objects
            SkinnedMeshRenderer skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null)
            {
                // Ensure the material has an "_Emissive" property
                if (skinnedMeshRenderer.material.HasProperty("_Emissive"))
                {
                    skinnedMeshRenderer.material.SetColor("_Emissive", newColor);
                }
            }

        }
    }
}
