using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

namespace CelestialCyclesSystem
{
    [RequireComponent(typeof(SphereCollider))]
    public class Celestial_Object : MonoBehaviour
    {
        [Header("Shared Attributes")]
        protected SphereCollider objectCollider;
        public bool queueIsMoving;
        public Vector3 usePositionOffset;
        public Vector3 useRotationOffset;
        public float recoverAmount = 1f;
        public float useDuration = 5f;
        public float happyTime = 5f;
        public Celestial_NPC npcInUse; // Base NPC

        [Header("Home / Bed")]
        [Range(1, 20)] public int homeCapacity;
        public bool isBed = true;

        [Header("Chair")]
        public int maxQueueSize = 5;
        [SerializeField] private float queueDirection = 0f;
        public float queueSpacing = 3.0f;
        public float exitDirection = 45f;

        public bool isOccupied = false;

        public List<Celestial_NPC> npcQueue = new(); // Base NPC
        private List<GameObject> lineMarkers = new();

        [Header("Food")]
        public bool isConsumable = false;

        [Header("Events")]
        public UnityEvent<Celestial_Object, Celestial_NPC> OnObjectUsed;
        public UnityEvent<Celestial_Object, Celestial_NPC> OnObjectLeft;

        private void OnDrawGizmos()
        {
            if (isConsumable) return;

            Gizmos.color = Color.green;
            Quaternion rotation = Quaternion.Euler(0, transform.eulerAngles.y + queueDirection, 0);
            Vector3 direction = rotation * Vector3.forward * queueSpacing;
            Gizmos.DrawLine(transform.position, transform.position + direction);

            Gizmos.color = Color.red;
            Quaternion exitRotation = Quaternion.Euler(0, transform.eulerAngles.y + exitDirection, 0);
            Vector3 exitDirectionLine = exitRotation * Vector3.forward * queueSpacing;
            Gizmos.DrawLine(transform.position, transform.position + exitDirectionLine);
        }

        public virtual void Start()
        {
            objectCollider = GetComponent<SphereCollider>();
            objectCollider.isTrigger = true;

            if (!isConsumable)
            {
                SpawnLineMarkers(maxQueueSize);
            }
        }

        private void Update()
        {
            if (isOccupied && npcInUse != null && !IsStillInUse(npcInUse))
            {
                LeaveAction(npcInUse);
            }
            else if (!isOccupied && npcQueue.Count > 0 && npcInUse == null)
            {
                PerformAction(npcQueue[0]);
            }
        }

        private bool IsStillInUse(Celestial_NPC npc)
        {
            return Time.time - npc.startTimeOfUse < useDuration;
        }

        private void SpawnLineMarkers(int count)
        {
            lineMarkers.Clear();
            Quaternion rotation = Quaternion.Euler(0, transform.eulerAngles.y + queueDirection, 0);
            float minimumDistance = objectCollider.radius;

            for (int i = 0; i < count + 1; i++)
            {
                Vector3 positionOffset = i == 0 ? Vector3.zero :
                    (i == 1 ? rotation * Vector3.forward * (minimumDistance + 0.4f) :
                    rotation * Vector3.forward * ((minimumDistance + 0.4f) + queueSpacing * (i - 1)));

                Vector3 markerPosition = transform.position + positionOffset;
                GameObject marker = new($"LineMarker {i + 1}");
                marker.AddComponent<Celestial_Gizmo>();
                marker.transform.position = markerPosition;
                marker.transform.parent = transform;
                lineMarkers.Add(marker);
            }
        }

        private void OnTriggerEnter(Collider col)
        {
            if (!col.CompareTag("Npc")) return;

            if (col.TryGetComponent<Celestial_NPC>(out var npc)) // Works with any child NPC
            {
                if (npc.justFinishedAction)
                {
                    npc.StartCoroutine(npc.DelayedAction(happyTime, () => npc.isSatisfied = false)); // Use generalized coroutine
                }

                if ((npc.stamina.needChair || npc.stamina.needBed || npc.stamina.needFood) && !npc.justFinishedAction)
                {
                    if (isConsumable) PerformAction(npc);
                    else JoinQueue(npc);
                }
            }
        }

        protected IEnumerator ObjectUseTimer(Celestial_NPC npc)
        {
            isOccupied = true;
            float elapsedTime = 0;

            while (elapsedTime < useDuration || npc.stamina.currentStamina < npc.stamina.maxStamina * 0.99f)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            LeaveAction(npc);
            isOccupied = false;
            queueIsMoving = true;
            yield return new WaitForSeconds(1f);
            queueIsMoving = false;
        }

        public virtual void PerformAction(Celestial_NPC npc)
        {
            TeleportToObject(npc);
            StartCoroutine(ObjectUseTimer(npc));
            npcInUse = npc;
            npc.startTimeOfUse = Time.time;
            OnObjectUsed.Invoke(this, npc); // Event for decoupling (e.g., iTalk listen)
        }

        public virtual void LeaveAction(Celestial_NPC npc)
        {
            isOccupied = false;
            npcInUse = null;
            npcQueue.RemoveAt(0);
            npc.UnfreezeNPC();
            npc.stamina.ResetNeed();
            npc.ChangeState(Celestial_NPC.NPCState.Roaming);
            npc.StartCoroutine(npc.DelayedAction(happyTime, () => npc.isSatisfied = false)); // Generalized
            npc.stamina.targetRecoveryObject = null;
            npc.stamina.currentRecoveryObject = null;
            npc.isWaitingInQueue = false;
            OnObjectLeft.Invoke(this, npc); // Event
        }

        public virtual void JoinQueue(Celestial_NPC npc)
        {
            if (npcQueue.Count < maxQueueSize && !npcQueue.Contains(npc))
            {
                npc.ChangeState(Celestial_NPC.NPCState.WaitingInQueue);
                npc.stamina.currentRecoveryObject = this;
                npc.stamina.targetRecoveryObject = null;
                npc.stamina.failedToJoinQueue = false;
                npcQueue.Add(npc);
            }
            else
            {
                npc.stamina.MarkObjectAsUnusable(this);
                StartCoroutine(ResetUsability(npc, this));
                npc.stamina.failedToJoinQueue = true;
                npc.StartCoroutine(npc.DelayedAction(1f, () => npc.stamina.FindRecoveryObject()));
            }
            UpdateQueuePriorities();
        }

        private IEnumerator ResetUsability(Celestial_NPC npc, Celestial_Object target)
        {
            yield return new WaitForSeconds(20f);
            npc.stamina.MarkObjectAsUsable(target);
        }

        protected void TeleportToObject(Celestial_NPC npc)
        {
            Vector3 worldOffset = transform.TransformPoint(usePositionOffset) - transform.position;
            npc.transform.position = transform.position + worldOffset;
            npc.transform.rotation = Quaternion.Euler(transform.eulerAngles + useRotationOffset);
        }

        public void TeleportToOutsideObject(Celestial_NPC npc)
        {
            Quaternion rotation = Quaternion.Euler(0, transform.eulerAngles.y + exitDirection, 0);
            Vector3 positionOffset = rotation * Vector3.forward * (queueSpacing + 2);
            npc._navComponent.destination = transform.position + positionOffset;
        }

        public Transform GetQueuePosition(Celestial_NPC npc)
        {
            int index = npcQueue.IndexOf(npc);
            return (index != -1 && index < lineMarkers.Count) ? lineMarkers[index].transform : null;
        }

        public Transform GetNextQueuePosition(Celestial_NPC npc)
        {
            int currentIndex = npcQueue.IndexOf(npc);
            return currentIndex > 0 ? lineMarkers[currentIndex - 1].transform : null;
        }

        private void UpdateQueuePriorities()
        {
            for (int i = 0; i < npcQueue.Count; i++)
            {
                if (npcQueue[i]._navComponent != null)
                {
                    npcQueue[i]._navComponent.avoidancePriority = i * 10;
                }
            }
        }
    }
}