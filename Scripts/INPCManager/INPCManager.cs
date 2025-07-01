using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Project.Tools.DictionaryHelp;
using System;

namespace CelestialCyclesSystem
{
    public delegate float TimeProvider();

    [System.Serializable]
    public class INPCManager : MonoBehaviour
    {
        [Header("NPC Management")]
        public List<INPCBase> allNPCs = new List<INPCBase>();

        [Header("Time Settings")]
        [SerializeField] private float internalTimeSpeed = 10f; // Minutes per second
        private TimeProvider timeProvider;
        private float internalTimeOfDay = 8f; // Start at 8 AM

        [Header("Global Schedules")]
        [Tooltip("Global sleep and wake times for all NPCs (X=Sleep, Y=Wake).")]
        public Vector2 globalSleepWakeTime = new Vector2(22, 7);

        [Header("Role Work Schedules")]
        [Tooltip("Define work start and end times for each NPC role. New roles from the INPCRole enum will be added automatically.")]
        [SerializeField] private SerializableDictionary<INPCRole, Vector2> roleWorkHours = new();


        public static INPCManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupTimeProvider();
            RefreshNPCs();
        }

        private void OnValidate()
        {
            var allRoles = Enum.GetValues(typeof(INPCRole));
            foreach (INPCRole role in allRoles)
            {
                if (role == INPCRole.None) continue; 

                if (!roleWorkHours.ContainsKey(role))
                {
                    roleWorkHours[role] = new Vector2(9, 17);
                }
            }
        }

        void Update()
        {
            if (timeProvider == GetInternalTime)
            {
                internalTimeOfDay += Time.deltaTime * (internalTimeSpeed / 60f);
                internalTimeOfDay %= 24f;
            }

            UpdateNPCSchedules();
        }

        #region Time Management

        private void SetupTimeProvider()
        {
            var timeManager = FindObjectOfType<CelestialTimeManager>();
            if (timeManager != null)
            {
                timeProvider = () => timeManager.currentTimeOfDay;
                Debug.Log("Using CelestialTimeManager for time.");
            }
            else
            {
                timeProvider = GetInternalTime;
                Debug.LogWarning("No CelestialTimeManager found. Using internal clock.");
            }
        }

        private float GetInternalTime()
        {
            return internalTimeOfDay;
        }

        public float GetCurrentTime()
        {
            return timeProvider != null ? timeProvider() : internalTimeOfDay;
        }

        public void SetCustomTimeProvider(TimeProvider provider)
        {
            if (provider != null)
            {
                timeProvider = provider;
                Debug.Log("Custom time provider set.");
            }
        }

        #endregion

        #region Scheduling

        public Vector2 GetWorkHoursForRole(INPCRole role)
        {
            if (roleWorkHours.TryGetValue(role, out Vector2 workHours))
            {
                return workHours;
            }

            Debug.LogWarning($"Work hours not defined for role '{role}' in INPCManager. Returning default values.");
            return new Vector2(9, 17);
        }

        /// <summary>
        /// This method now runs once per frame and updates the state of all NPCs.
        /// </summary>
        private void UpdateNPCSchedules()
        {
            // Loop through every registered NPC
            foreach (INPCBase npc in allNPCs)
            {
                if (npc == null) continue;

                // Determine if this NPC should be working right now
                bool shouldBeWorking = IsWorkHours(npc);

                // If the NPC's current state doesn't match what it should be, update it.
                if (npc.isWorking != shouldBeWorking)
                {
                    npc.isWorking = shouldBeWorking;
                    // Optional: Log the state change for debugging
                    // Debug.Log($"{npc.name} is now {(shouldBeWorking ? "working" : "not working")}.");
                }
            }
        }

        private bool IsSleepTime(float currentTime)
        {
            if (globalSleepWakeTime.x > globalSleepWakeTime.y)
            {
                return currentTime >= globalSleepWakeTime.x || currentTime < globalSleepWakeTime.y;
            }
            else
            {
                return currentTime >= globalSleepWakeTime.x && currentTime < globalSleepWakeTime.y;
            }
        }


        public bool IsWorkHours(INPCBase npc)
        {
            var workHours = GetWorkHoursForRole(npc.role);
            float currentTime = GetCurrentTime();

            if (workHours.x > workHours.y)
            {
                return currentTime >= workHours.x || currentTime < workHours.y;
            }
            else
            {
                return currentTime >= workHours.x && currentTime < workHours.y;
            }
        }

        #endregion

        #region NPC Management

        public void RefreshNPCs()
        {
            allNPCs.Clear();
            allNPCs = FindObjectsOfType<INPCBase>().ToList();
            Debug.Log($"Refreshed NPC list. Found {allNPCs.Count} NPCs.");
        }

        public void RegisterNPC(INPCBase npc)
        {
            if (npc != null && !allNPCs.Contains(npc))
            {
                allNPCs.Add(npc);
            }
        }

        public void UnregisterNPC(INPCBase npc)
        {
            if (npc != null && allNPCs.Contains(npc))
            {
                allNPCs.Remove(npc);
            }
        }

        #endregion
    }
}
