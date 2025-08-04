using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace CelestialCyclesSystem
{
    [RequireComponent(typeof(Collider))]

    public class Celestial_Object_Ball : MonoBehaviour
    {
        [SerializeField] float stunDuration = 2f;
        private GameObject hitTarget;

        void Start()
        {
            transform.GetComponent<Collider>().isTrigger = true;
        }


        public void OnTriggerEnter(Collider col)
        {
            if (col.CompareTag("Npc"))
            {
                hitTarget = col.GetComponent<GameObject>();
                col.GetComponent<Celestial_NPC>().isStun = true;
                col.GetComponent<NavMeshAgent>().enabled = false;
                col.GetComponent<Animator>().SetFloat("Speed", 0f);
            }
        }

        //---------------------------------------------------------------------------------------------------
        public void OnTriggerExit(Collider col)
        {
            if (col.CompareTag("Npc"))
            {
                hitTarget = col.GetComponent<GameObject>();
                col.GetComponent<Animator>().Play("Fall", -1, 0);
                StartCoroutine(Recover(hitTarget));
            }
        }

        IEnumerator Recover(GameObject target)
        {
            yield return new WaitForSeconds(stunDuration);
            target.GetComponent<Celestial_NPC>().enabled = false;
            target.GetComponent<NavMeshAgent>().enabled = true;
            target.GetComponent<Rigidbody>().useGravity = false;
        }
    }
}
