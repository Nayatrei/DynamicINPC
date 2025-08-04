using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CelestialCyclesSystem
{
    public class Celestial_NPC_Roamer : Celestial_NPC
    {
        protected override NPCState GetInitialState()
        {
            return NPCState.Roaming;
        }
    }
}