// Celestial_Object_Manager.cs (Refactored - Unified finding, base compatibility)
using System.Collections.Generic;
using UnityEngine;

namespace CelestialCyclesSystem
{
    public class Celestial_Object_Manager : MonoBehaviour
    {
        public List<Celestial_Object_Chair> celestialChairs = new();
        public List<Celestial_Object_FoodStand> celestialFoodStands = new();
        public List<Celestial_Object_Home> celestialBeds = new();
        public List<Celestial_Object_Food> celestialFoods = new();

        private void Start()
        {
            UpdateAll();
        }

        [ContextMenu("Update")]
        public void UpdateAll()
        {
            celestialChairs.Clear();
            celestialFoodStands.Clear();
            celestialBeds.Clear();
            celestialFoods.Clear();
            FindAllObjects();
        }

        private void FindAllObjects()
        {
            celestialChairs.AddRange(FindObjectsOfType<Celestial_Object_Chair>());
            celestialFoodStands.AddRange(FindObjectsOfType<Celestial_Object_FoodStand>());
            celestialBeds.AddRange(FindObjectsOfType<Celestial_Object_Home>());
            celestialFoods.AddRange(FindObjectsOfType<Celestial_Object_Food>());
        }
    }
}