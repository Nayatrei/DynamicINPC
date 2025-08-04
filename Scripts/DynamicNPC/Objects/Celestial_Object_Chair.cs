using System.Collections;
using UnityEngine;

namespace CelestialCyclesSystem
{
    public class Celestial_Object_Chair : Celestial_Object
    {
        public override void PerformAction(Celestial_NPC npc)
        {
            npc.stamina.SetRecoveryAmount(recoverAmount, useDuration);
            npc.ChangeState(Celestial_NPC.NPCState.Sitting);
            base.PerformAction(npc); // Call base to start timer if needed
        }

        public override void LeaveAction(Celestial_NPC npc)
        {
            base.LeaveAction(npc); // Call base to start timer if needed
            TeleportToOutsideObject(npc);
        }
    }
}