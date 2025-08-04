using System.Collections;
using UnityEngine;

namespace CelestialCyclesSystem
{
    public class Celestial_Object_Home : Celestial_Object
    {
        private void Start()
        {
            base.Start();
            maxQueueSize = 1;
        }

        public override void PerformAction(Celestial_NPC npc)
        {
            base.PerformAction(npc);
            npc.ChangeState(Celestial_NPC.NPCState.Sleeping);
            npc.FreezeNPC(100f, false);
            npc.stamina.RecoverStamina(npc.stamina.maxStamina); // Use stamina component
            StartCoroutine(WaitUntilWakeTimeAndUnfreeze(npc));
        }

        private IEnumerator WaitUntilWakeTimeAndUnfreeze(Celestial_NPC npc)
        {
            while (npc.timeManager.currentTimeOfDay < npc.wakeTime || npc.timeManager.currentTimeOfDay > npc.sleepTime)
            {
                yield return null;
            }
            npc.UnfreezeNPC();
            npc.stamina.ResetNeed(); // Reset needs via stamina component
            npc.ChangeState(Celestial_NPC.NPCState.Roaming); // Ensure state reset
        }
    }
}