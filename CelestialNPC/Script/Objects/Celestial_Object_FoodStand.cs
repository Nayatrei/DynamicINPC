// Celestial_Object_FoodStand.cs (Refactored - Minor cleanup, base compatibility)
using UnityEngine;

namespace CelestialCyclesSystem
{
    public class Celestial_Object_FoodStand : Celestial_Object
    {
        public override void PerformAction(Celestial_NPC npc)
        {
            npc.ChangeState(Celestial_NPC.NPCState.Eating);
            npc.ForcePlayAnimation("PickUp");
            base.PerformAction(npc);
        }
    }
}