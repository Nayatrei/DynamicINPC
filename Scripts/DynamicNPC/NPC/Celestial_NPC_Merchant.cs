using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CelestialCyclesSystem
{
    public class Celestial_NPC_Merchant : Celestial_NPC
    {
        // Add merchant-specific fields, e.g., List<Item> inventory;
        // Override methods as needed, e.g., for Talking state to open shop

        protected override void HandleMerchantLogic()
        {
            // Example: If in Talking, open shop UI via event
            if (currentState == NPCState.Talking)
            {
                // Raise event or open UI
            }
        }

        protected override void Start()
        {
            base.Start();
            // Merchant-specific init, e.g., load inventory
        }
    }
}