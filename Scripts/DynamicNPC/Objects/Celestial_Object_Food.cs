using System.Collections;
using UnityEngine;

namespace CelestialCyclesSystem
{
    public class Celestial_Object_Food : Celestial_Object
    {
        private bool hasBeenUsed = false;
        public float respawnTimer = 20.0f;
        private bool isRespawning = false;
        public float respawnHeight = 10f;
        private Vector3 originalPosition;

        private void Start()
        {
            base.Start();
            maxQueueSize = 1;
            isConsumable = true;
            originalPosition = transform.position;
        }

        public override void PerformAction(Celestial_NPC npc)
        {
            if (hasBeenUsed || npc.stamina.currentStamina >= npc.stamina.maxStamina * 0.9f) return;

            hasBeenUsed = true;
            UseObject();
            npc.ChangeState(Celestial_NPC.NPCState.Eating);
            npc.ForcePlayAnimation("PickUp");
            npc.FreezeNPC(useDuration, true);
            npc.stamina.RecoverStamina(recoverAmount);
            base.PerformAction(npc);
        }

        private void UseObject()
        {
            if (isRespawning) return;

            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
            isRespawning = true;
            StartCoroutine(RespawnCoroutine());
        }

        private IEnumerator RespawnCoroutine()
        {
            yield return new WaitForSeconds(respawnTimer);
            transform.position = originalPosition + Vector3.up * respawnHeight;
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(true);
            }
            isRespawning = false;
            hasBeenUsed = false; // Reset for reuse
        }
    }
}