using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CelestialCyclesSystem
{
    public class Celestial_NPC_Stamina : MonoBehaviour
    {
        public float maxStamina = 100f;
        public float currentStamina = 100f;
        public float actionStaminaCost = 10f;
        public float roamingStaminaCost = 1f;
        public bool isRecoveringStamina = false;
        public bool displayStamina = true;
        public string staminaHudType = "None"; // String to select HUD prefab
        public Transform headTransform;
        private GameObject staminaObjectInstance;
        private Celestial_HUD_Stamina hudStamina;

        [Range(0, 1)] public float lookForStaminaBelow = 0.5f;
        public Celestial_Object targetRecoveryObject;
        public Celestial_Object currentRecoveryObject;
        internal float recoveryDuration;
        internal float recoveryAmount;
        public HashSet<Celestial_Object> unusableObjects = new HashSet<Celestial_Object>();
        public bool failedToJoinQueue;
        public bool needFood;
        public bool needChair;
        public bool needBed;

        private CelestialTimeManager timeManager;
        private Celestial_Object_Manager objManager;
        private Celestial_NPC npc;

        private void Awake()
        {
            npc = GetComponent<Celestial_NPC>();
            timeManager = FindObjectOfType<CelestialTimeManager>();
            objManager = FindObjectOfType<Celestial_Object_Manager>();
        }

        private void Start()
        {
            if (displayStamina && staminaHudType != "None")
            {
                headTransform = npc.DeepFind(transform, "head") ?? transform;
                GameObject prefab = Resources.Load<GameObject>($"Status/{staminaHudType}");
                if (prefab != null)
                {
                    staminaObjectInstance = Instantiate(prefab, headTransform);
                    hudStamina = staminaObjectInstance.GetComponent<Celestial_HUD_Stamina>() ?? staminaObjectInstance.AddComponent<Celestial_HUD_Stamina>();
                }
                else
                {
                    Debug.LogWarning($"Stamina HUD prefab '{staminaHudType}' not found in Resources/Status folder.", gameObject);
                }
            }
            currentStamina = maxStamina;
        }

        private void Update()
        {
            if (npc.isRoaming && !npc.isStun)
            {
                DepleteStaminaOverTime();
            }
        }

        private void DepleteStaminaOverTime()
        {
            currentStamina = Mathf.Clamp(currentStamina - roamingStaminaCost * Time.deltaTime, 0, maxStamina);
            VisualizeStaminaChange();
        }

        public bool TryConsumeStamina(float amount)
        {
            if (currentStamina >= amount)
            {
                currentStamina -= amount;
                VisualizeStaminaChange();
                return true;
            }
            return false;
        }

        public void RecoverStamina(float amount)
        {
            currentStamina = Mathf.Clamp(currentStamina + amount, 0, maxStamina);
            VisualizeStaminaChange();
        }

        public IEnumerator RecoverStaminaOverTime(float amount, float duration)
        {
            isRecoveringStamina = true;
            float elapsedTime = 0;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                currentStamina = Mathf.Clamp(currentStamina + amount * Time.deltaTime, 0, maxStamina);
                VisualizeStaminaChange();
                yield return null;
            }
            isRecoveringStamina = false;
            VisualizeStaminaChange();
        }

        public void VisualizeStaminaChange()
        {
            if (displayStamina && hudStamina != null)
            {
                float staminaRatio = currentStamina / maxStamina;
                bool alarmState = staminaRatio < 0.2f;
                hudStamina.UpdateStamina(staminaRatio, alarmState);
            }
        }

        public bool NeedStamina()
        {
            float currentTime = timeManager.currentTimeOfDay;
            if (IsMealTime() && currentStamina < maxStamina * lookForStaminaBelow)
            {
                needFood = true;
                return true;
            }
            else if (currentTime > npc.sleepTime)
            {
                needBed = true;
                return true;
            }
            else if (!IsMealTime() && currentStamina < maxStamina * lookForStaminaBelow)
            {
                needChair = true;
                return true;
            }
            ResetNeed();
            return false;
        }

        public bool InNeed()
        {
            return needBed || needChair || needFood;
        }

        private bool IsMealTime()
        {
            float currentTime = timeManager.currentTimeOfDay;
            return (currentTime >= 6f && currentTime <= 9f) || (currentTime >= 12f && currentTime <= 14f) || (currentTime >= 18f && currentTime <= 20f);
        }

        public void FindRecoveryObject()
        {
            if (currentRecoveryObject != null) return;
            if (needFood) FindRestaurant();
            else if (needBed) FindBed();
            else if (needChair) FindBestChair();
            else npc.ChangeState(Celestial_NPC.NPCState.Idle);
        }

        private void FindBed()
        {
            float shortestDistance = float.MaxValue;
            Celestial_Object targetBed = null;
            foreach (var obj in objManager.celestialBeds)
            {
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < shortestDistance)
                {
                    targetBed = obj;
                    shortestDistance = distance;
                }
            }
            targetRecoveryObject = targetBed ?? npc.assignedHome;
        }

        public void FindBestChair()
        {
            float shortestDistance = float.MaxValue;
            Celestial_Object bestChair = null;
            foreach (var obj in objManager.celestialChairs)
            {
                if (!unusableObjects.Contains(obj))
                {
                    float distance = Vector3.Distance(transform.position, obj.transform.position);
                    if (distance < shortestDistance)
                    {
                        bestChair = obj;
                        shortestDistance = distance;
                    }
                }
            }
            targetRecoveryObject = bestChair;
            if (bestChair != null) npc._navComponent.SetDestination(targetRecoveryObject.transform.position);
        }

        public void FindRestaurant()
        {
            float shortestDistance = float.MaxValue;
            Celestial_Object closeRestaurant = null;
            foreach (var obj in objManager.celestialFoodStands)
            {
                if (!unusableObjects.Contains(obj))
                {
                    float distance = Vector3.Distance(transform.position, obj.transform.position);
                    if (distance < shortestDistance)
                    {
                        closeRestaurant = obj;
                        shortestDistance = distance;
                    }
                }
            }
            targetRecoveryObject = closeRestaurant;
            if (closeRestaurant != null) npc._navComponent.SetDestination(targetRecoveryObject.transform.position);
        }

        public void MarkObjectAsUnusable(Celestial_Object obj)
        {
            if (obj != null) unusableObjects.Add(obj);
        }

        public void MarkObjectAsUsable(Celestial_Object obj)
        {
            unusableObjects.Remove(obj);
        }

        public void SetRecoveryAmount(float amount, float duration)
        {
            recoveryAmount = amount;
            recoveryDuration = duration;
        }

        public void ResetNeed()
        {
            needBed = false;
            needChair = false;
            needFood = false;
        }
    }
}