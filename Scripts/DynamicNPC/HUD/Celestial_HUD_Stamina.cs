using UnityEngine;

namespace CelestialCyclesSystem
{
    public class Celestial_HUD_Stamina : MonoBehaviour
    {
        private Material staminaMaterial;

        private void Awake()
        {
            Renderer staminaRenderer = GetComponent<Renderer>();
            if (staminaRenderer != null)
            {
                staminaMaterial = staminaRenderer.material;
            }
        }

        public void UpdateStamina(float ratio, bool alarm)
        {
            if (staminaMaterial != null)
            {
                staminaMaterial.SetFloat("_Amount", ratio);
                staminaMaterial.SetFloat("_Alarm", alarm ? 1f : 0f);
            }
        }
    }
}