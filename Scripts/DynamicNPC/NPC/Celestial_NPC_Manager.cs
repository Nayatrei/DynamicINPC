using System.Collections.Generic;
using UnityEngine;

namespace CelestialCyclesSystem
{
    public class Celestial_NPC_Manager : MonoBehaviour
    {
        public List<Celestial_Areas> areaManager = new();
        public List<Celestial_Areas> boundArea = new();
        public List<Celestial_Areas> pathArea = new();
        public List<Celestial_NPC> allNPCs = new(); // NPCs register themselves
        public List<Celestial_NPC_Merchant> allMerchants = new(); // Merchants register themselves

        private void Awake()
        {
            UpdateAll();
        }

        [ContextMenu("Update")]
        public void UpdateAll()
        {
            areaManager.Clear();
            boundArea.Clear();
            pathArea.Clear();
            // allNPCs and allMerchants are populated via registration, so no clear here to avoid runtime issues
            FindInChildren(transform);
        }

        private void FindInChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.TryGetComponent<Celestial_Areas>(out var area))
                {
                    areaManager.Add(area);
                    if (area.areaType == Celestial_Areas.AreaType.Bound) boundArea.Add(area);
                    if (area.areaType == Celestial_Areas.AreaType.Path) pathArea.Add(area);
                }

                FindInChildren(child);
            }
        }
    }
}