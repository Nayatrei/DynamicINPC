using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Project.Tools.DictionaryHelp;
using System;

namespace CelestialCyclesSystem
{
    public delegate float TimeProvider();

    [System.Serializable] // RoleWorkSchedule removed, but keep [System.Serializable] if used elsewhere
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
            // This function is called in the editor when the script is loaded or a value is changed.
            // It ensures that our work hours dictionary stays in sync with the INPCRole enum.
            var allRoles = Enum.GetValues(typeof(INPCRole));
            foreach (INPCRole role in allRoles)
            {
                if (role == INPCRole.None) continue; // We don't need to schedule the 'None' role.

                if (!roleWorkHours.ContainsKey(role))
                {
                    // If a new role was added to the enum, add it to our dictionary with a default schedule.
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
            return new Vector2(9, 17); // Default if role not found
        }

        private void UpdateNPCSchedules()
        {

        }

        /// <summary>
        /// Checks if the current time falls within the global sleep window.
        /// Accounts for overnight sleep periods (e.g., 22:00 to 07:00).
        /// </summary>
        private bool IsSleepTime(float currentTime)
        {
            // If sleep time is later than wake time (e.g., 22:00 sleep, 7:00 wake)
            if (globalSleepWakeTime.x > globalSleepWakeTime.y)
            {
                return currentTime >= globalSleepWakeTime.x || currentTime < globalSleepWakeTime.y;
            }
            // If sleep time is earlier than wake time (e.g., 10:00 sleep, 18:00 wake - less common but possible)
            else
            {
                return currentTime >= globalSleepWakeTime.x && currentTime < globalSleepWakeTime.y;
            }
        }


        public bool IsWorkHours(INPCBase npc)
        {
            var workHours = GetWorkHoursForRole(npc.role);
            float currentTime = GetCurrentTime();

            // If work start time is later than work end time (e.g., 20:00 start, 04:00 end)
            if (workHours.x > workHours.y)
            {
                return currentTime >= workHours.x || currentTime < workHours.y;
            }
            // If work start time is earlier than work end time (standard daytime shift)
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

        /// <summary>
        /// Registers an NPC with the manager. Called by individual NPCs on their Awake/Start.
        /// </summary>
        public void RegisterNPC(INPCBase npc)
        {
            if (npc != null && !allNPCs.Contains(npc))
            {
                allNPCs.Add(npc);
            }
        }

        /// <summary>
        /// Unregisters an NPC from the manager. Called by individual NPCs on their OnDestroy.
        /// </summary>
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