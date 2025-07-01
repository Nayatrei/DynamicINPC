using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System; // Required for Action

namespace CelestialCyclesSystem
{
    /// <summary>
    /// Represents an active NPC-to-NPC conversation with participant tracking and lifecycle management
    /// </summary>
    [System.Serializable]
    public class Conversation : MonoBehaviour
    {
        [SerializeField] private List<iTalk> participants = new List<iTalk>();
        [SerializeField] private iTalkSubManager parentManager;
        [SerializeField] private float conversationStartTime;
        [SerializeField] private bool isActive = false;

        /// <summary>
        /// Initialize the conversation with participants and parent manager
        /// </summary>
        public void Initialize(List<iTalk> conversationParticipants, iTalkSubManager manager)
        {
            participants = new List<iTalk>(conversationParticipants);
            parentManager = manager;
            conversationStartTime = Time.time;
            isActive = true;

            Debug.Log($"[Conversation] Initialized with {participants.Count} participants: {string.Join(", ", participants.Select(p => p.EntityName))}");
        }

        /// <summary>
        /// Get read-only list of conversation participants
        /// </summary>
        public List<iTalk> GetParticipants()
        {
            return new List<iTalk>(participants);
        }

        /// <summary>
        /// End conversation due to player interruption
        /// </summary>
        public void EndByPlayerInterruption()
        {
            if (!isActive) return;

            Debug.Log($"[Conversation] Ending conversation by player interruption after {Time.time - conversationStartTime:F1} seconds");
            
            isActive = false;
            
            // Notify parent manager to handle cleanup
            if (parentManager != null)
            {
                parentManager.EndNPCConversation(this);
            }
        }

        /// <summary>
        /// End conversation naturally
        /// </summary>
        public void EndNaturally()
        {
            if (!isActive) return;

            Debug.Log($"[Conversation] Ending conversation naturally after {Time.time - conversationStartTime:F1} seconds");
            
            isActive = false;
            
            // Notify parent manager to handle cleanup
            if (parentManager != null)
            {
                parentManager.EndNPCConversation(this);
            }
        }

        /// <summary>
        /// Check if conversation is still active
        /// </summary>
        public bool IsActive() => isActive;

        /// <summary>
        /// Get conversation duration in seconds
        /// </summary>
        public float GetDuration() => Time.time - conversationStartTime;

        /// <summary>
        /// Check if an NPC is participating in this conversation
        /// </summary>
        public bool HasParticipant(iTalk npc)
        {
            return participants.Contains(npc);
        }

        /// <summary>
        /// Remove a participant from the conversation (e.g., if they get interrupted)
        /// </summary>
        public void RemoveParticipant(iTalk npc)
        {
            if (participants.Remove(npc))
            {
                Debug.Log($"[Conversation] Removed {npc.EntityName} from conversation. {participants.Count} participants remaining.");
                
                // End conversation if too few participants remain
                if (participants.Count < 2)
                {
                    EndNaturally();
                }
            }
        }
    }

    /// <summary>
    /// Handles ALL NPC-to-NPC conversation logic including periodic dialogues with random cooldowns,
    /// prioritization based on WorldAffinity and FactionAffinity, group conversations, 
    /// post-dialogue cooldowns, and full conversation lifecycle management.
    /// </summary>
    public class iTalkSubManager : MonoBehaviour
    {
        // --- NEW: Events for the bridge to listen to ---
        /// <summary>
        /// Fired when an NPC-to-NPC conversation begins. Passes the two participants.
        /// </summary>
        public event Action<iTalk, iTalk> OnNPCConversationStarted;
        /// <summary>
        /// Fired when an NPC-to-NPC conversation ends. Passes the two participants.
        /// </summary>
        public event Action<iTalk, iTalk> OnNPCConversationEnded;


        [Header("NPC-to-NPC Dialogue Settings")]
        [SerializeField] private float minDialogueCooldown = 30f;
        [SerializeField] private float maxDialogueCooldown = 90f;
        [SerializeField] private float postDialogueCooldown = 120f; // Cooldown after dialogue ends
        [SerializeField] private float maxConversationDistance = 8f;
        
        [Header("Group Conversation Settings")]
        [SerializeField] private bool enableGroupConversations = true;
        [SerializeField] private int maxGroupSize = 4;
        [SerializeField] private float groupFormationRadius = 5f;

        [Header("Affinity-Based Prioritization")]
        [SerializeField] private bool prioritizeByAffinity = true;
        [SerializeField] private float worldAffinityWeight = 0.6f;
        [SerializeField] private float factionAffinityWeight = 0.4f;

        [Header("Conversation Management")]
        [SerializeField] private GameObject conversationPrefab; // Changed from Conversation to GameObject

        // Core state management
        private Dictionary<iTalk, float> npcDialogueCooldowns = new Dictionary<iTalk, float>();
        private List<iTalk> availableNPCs = new List<iTalk>();
        private List<iTalk> busyNPCs = new List<iTalk>();
        private readonly List<Conversation> activeNPCConversations = new List<Conversation>();

        void Start()
        {
            if (iTalkManager.Instance != null)
            {
                iTalkManager.Instance.OniTalkRegistered += OnNPCRegistered;
                iTalkManager.Instance.OniTalkUnregistered += OnNPCUnregistered;
                
                // Register this SubManager with the main Manager
                iTalkManager.Instance.SetSubManager(this);
            }
            StartCoroutine(NPCDialogueRoutine());
        }

        void OnDestroy()
        {
            if (iTalkManager.Instance != null)
            {
                iTalkManager.Instance.OniTalkRegistered -= OnNPCRegistered;
                iTalkManager.Instance.OniTalkUnregistered -= OnNPCUnregistered;
            }
        }

        private void OnNPCRegistered(iTalk npc)
        {
            if (!npcDialogueCooldowns.ContainsKey(npc))
            {
                npcDialogueCooldowns[npc] = 0f;
            }
        }

        private void OnNPCUnregistered(iTalk npc)
        {
            npcDialogueCooldowns.Remove(npc);
            availableNPCs.Remove(npc);
            busyNPCs.Remove(npc);
            
            // Clean up any conversations involving this NPC
            var conversationsToEnd = activeNPCConversations.Where(c => c.GetParticipants().Contains(npc)).ToList();
            foreach (var conversation in conversationsToEnd)
            {
                conversation.RemoveParticipant(npc);
            }
        }

        private IEnumerator NPCDialogueRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(minDialogueCooldown, maxDialogueCooldown));
                
                UpdateCooldowns();
                CheckForNPCToNPCDialogues();
            }
        }

        private void UpdateCooldowns()
        {
            var keys = npcDialogueCooldowns.Keys.ToList();
            foreach (var npc in keys)
            {
                if (npc == null) continue;
                
                if (npcDialogueCooldowns[npc] > 0)
                {
                    npcDialogueCooldowns[npc] -= Time.deltaTime;
                }
            }
        }

        private void CheckForNPCToNPCDialogues()
        {
            Debug.Log("[iTalkSubManager] Checking for NPC-to-NPC dialogues..."); // DEBUG LOG

            UpdateAvailableNPCs();
            
            if (availableNPCs.Count < 2) return;

            // Try group conversation first if enabled
            if (enableGroupConversations && availableNPCs.Count >= 3)
            {
                var group = FormConversationGroup();
                if (group.Count >= 3)
                {
                    InitiateGroupDialogue(group);
                    return;
                }
            }

            // Fall back to one-on-one conversation
            var pair = SelectConversationPair();
            if (pair.Item1 != null && pair.Item2 != null)
            {
                InitiateOneOnOneDialogue(pair.Item1, pair.Item2);
            }
        }

        private void UpdateAvailableNPCs()
        {
            availableNPCs.Clear();
            busyNPCs.Clear();

            if (iTalkManager.Instance == null) return;

            foreach (var npc in iTalkManager.Instance.GetRegisteredTalkComponents())
            {
                if (npc == null) continue;

                bool isOnCooldown = npcDialogueCooldowns.ContainsKey(npc) && npcDialogueCooldowns[npc] > 0;
                bool isAvailable = npc.GetInternalAvailability() == NPCAvailabilityState.Available;
                bool isNotInConversation = !npc.IsCurrentlyInConversation();

                if (isAvailable && isNotInConversation && !isOnCooldown)
                {
                    availableNPCs.Add(npc);
                }
                else if (npc.IsCurrentlyInConversation())
                {
                    busyNPCs.Add(npc);
                }
            }
        }

        private (iTalk, iTalk) SelectConversationPair()
        {
            if (availableNPCs.Count < 2) return (null, null);

            iTalk bestNpc1 = null;
            iTalk bestNpc2 = null;
            float bestAffinityScore = -1f;

            for (int i = 0; i < availableNPCs.Count; i++)
            {
                for (int j = i + 1; j < availableNPCs.Count; j++)
                {
                    iTalk npc1 = availableNPCs[i];
                    iTalk npc2 = availableNPCs[j];

                    if (!AreNPCsInRange(npc1, npc2, maxConversationDistance)) continue;

                    float affinityScore = prioritizeByAffinity ? CalculateAffinityScore(npc1, npc2) : UnityEngine.Random.Range(0f, 1f);
                    
                    if (affinityScore > bestAffinityScore)
                    {
                        bestAffinityScore = affinityScore;
                        bestNpc1 = npc1;
                        bestNpc2 = npc2;
                    }
                }
            }

            return (bestNpc1, bestNpc2);
        }

        private List<iTalk> FormConversationGroup()
        {
            var group = new List<iTalk>();
            if (availableNPCs.Count == 0) return group;

            // Start with a random NPC as the group center
            iTalk centerNpc = availableNPCs[UnityEngine.Random.Range(0, availableNPCs.Count)];
            group.Add(centerNpc);

            // Find nearby NPCs to form a group
            foreach (var npc in availableNPCs)
            {
                if (npc == centerNpc || group.Count >= maxGroupSize) continue;
                
                if (AreNPCsInRange(centerNpc, npc, groupFormationRadius))
                {
                    group.Add(npc);
                }
            }

            return group;
        }

        private bool AreNPCsInRange(iTalk npc1, iTalk npc2, float maxDistance)
        {
            return Vector3.Distance(npc1.Position, npc2.Position) <= maxDistance;
        }

        private float CalculateAffinityScore(iTalk npc1, iTalk npc2)
        {
            if (npc1?.assignedPersona == null || npc2?.assignedPersona == null) return 0f;

            // Calculate WorldAffinity compatibility
            float worldAffinityScore = 0f;
            if (npc1.assignedPersona.world != null && npc2.assignedPersona.world != null)
            {
                worldAffinityScore = UnityEngine.Random.Range(0.2f, 1f); // Placeholder
            }

            // Calculate FactionAffinity compatibility  
            float factionAffinityScore = 0f;
            if (npc1.assignedPersona.factionAffiliation != null && npc2.assignedPersona.factionAffiliation != null)
            {
                factionAffinityScore = UnityEngine.Random.Range(0.2f, 1f); // Placeholder
            }
            
            return (worldAffinityScore * worldAffinityWeight) + (factionAffinityScore * factionAffinityWeight);
        }

        #region NPC-to-NPC Conversation Management (Centralized)
        
        public void RequestNPCToNPCConversation(iTalk initiator, iTalk partner)
        {
            if (initiator == null || partner == null) return;
            if (initiator.IsCurrentlyInConversation() || partner.IsCurrentlyInConversation()) return;

            Debug.Log($"[iTalkSubManager] Starting NPC conversation between {initiator.EntityName} and {partner.EntityName}");

            Conversation newConversation = CreateConversationObject();
            if (newConversation != null)
            {
                newConversation.Initialize(new List<iTalk> { initiator, partner }, this);
                activeNPCConversations.Add(newConversation);
            }

            initiator.SetConversationState(true);
            partner.SetConversationState(true);

            if (iTalkManager.Instance?.GetDialogueStateChannel() != null)
            {
                iTalkManager.Instance.GetDialogueStateChannel().Raise(initiator, true);
            }
        }

        private Conversation CreateConversationObject()
        {
            GameObject conversationObj;
            
            if (conversationPrefab != null)
            {
                conversationObj = Instantiate(conversationPrefab);
            }
            else
            {
                conversationObj = new GameObject("NPC Conversation");
            }

            Conversation conversation = conversationObj.GetComponent<Conversation>();
            if (conversation == null)
            {
                conversation = conversationObj.AddComponent<Conversation>();
            }

            return conversation;
        }

        public void EndNPCConversation(Conversation conversation)
        {
            if (conversation == null || !activeNPCConversations.Contains(conversation)) return;

            Debug.Log($"[iTalkSubManager] Ending NPC conversation");

            foreach (var participant in conversation.GetParticipants())
            {
                if (participant != null)
                {
                    participant.SetConversationState(false);
                    
                    if (iTalkManager.Instance?.GetDialogueStateChannel() != null)
                    {
                        iTalkManager.Instance.GetDialogueStateChannel().Raise(participant, false);
                    }
                }
            }

            activeNPCConversations.Remove(conversation);
            if (conversation.gameObject != null)
            {
                Destroy(conversation.gameObject);
            }
        }

        public bool TryInterruptNPCConversationForPlayer(iTalk npc)
        {
            var existingConversation = activeNPCConversations.FirstOrDefault(c => c.GetParticipants().Contains(npc));
            if (existingConversation != null)
            {
                Debug.Log($"[iTalkSubManager] Player interrupting NPC conversation involving {npc.EntityName}");
                existingConversation.EndByPlayerInterruption();
                return true;
            }
            return false;
        }

        #endregion

        private void InitiateOneOnOneDialogue(iTalk npc1, iTalk npc2)
        {
            Debug.Log($"[iTalkSubManager] Found a pair! Initiating dialogue between {npc1.EntityName} and {npc2.EntityName}"); // DEBUG LOG
            
            SetNPCBusyState(npc1, true);
            SetNPCBusyState(npc2, true);

            RequestNPCToNPCConversation(npc1, npc2);

            // --- NEW: Fire the event for the bridge ---
            OnNPCConversationStarted?.Invoke(npc1, npc2);

            StartCoroutine(iTalkUtilities.RequestNPCToNPCDialogue(
                npc1, npc2, "",
                (response) => {
                    Debug.Log($"[iTalkSubManager] {npc1.EntityName}: {response}");
                    iTalkUtilities.RequestDialogueTTS(npc1, response);
                    
                    StartCoroutine(HandlePostDialogueCooldown(new List<iTalk> { npc1, npc2 }));
                },
                (error) => {
                    Debug.LogError($"[iTalkSubManager] NPC dialogue error: {error}");
                    StartCoroutine(HandlePostDialogueCooldown(new List<iTalk> { npc1, npc2 }));
                }
            ));
        }

        private void InitiateGroupDialogue(List<iTalk> group)
        {
            Debug.Log($"[iTalkSubManager] Initiating group dialogue with {group.Count} NPCs: {string.Join(", ", group.Select(n => n.EntityName))}");
            
            foreach (var npc in group)
            {
                SetNPCBusyState(npc, true);
            }

            if (group.Count >= 2)
            {
                Conversation newConversation = CreateConversationObject();
                if (newConversation != null)
                {
                    newConversation.Initialize(group, this);
                    activeNPCConversations.Add(newConversation);

                    foreach (var npc in group)
                    {
                        npc.SetConversationState(true);
                    }
                    
                    // --- NEW: Fire the event for the bridge ---
                    // We fire it for the first two members of the group as the main participants
                    OnNPCConversationStarted?.Invoke(group[0], group[1]);
                }
            }

            if (group.Count >= 2)
            {
                iTalk speaker = group[0];
                List<iTalkNPCPersona> groupPersonas = group.Select(npc => npc.assignedPersona).Where(p => p != null).ToList();
                
                string groupPrompt = iTalkUtilities.BuildGroupConversationPrompt(
                    speaker.assignedPersona,
                    groupPersonas,
                    iTalkManager.Instance?.GetWorldNews(),
                    iTalkManager.Instance?.GetCurrentGlobalSituation(),
                    "casual group conversation"
                );

                StartCoroutine(iTalkUtilities.SendLLMRequest(
                    groupPrompt,
                    iTalkManager.Instance?.GetApiConfig(),
                    (response) => {
                        Debug.Log($"[iTalkSubManager] Group conversation - {speaker.EntityName}: {response}");
                        iTalkUtilities.RequestDialogueTTS(speaker, response);
                        
                        StartCoroutine(HandlePostDialogueCooldown(group));
                    },
                    (error) => {
                        Debug.LogError($"[iTalkSubManager] Group dialogue error: {error}");
                        StartCoroutine(HandlePostDialogueCooldown(group));
                    },
                    $"GroupConversation-{group.Count}NPCs"
                ));
            }
        }

        private void SetNPCBusyState(iTalk npc, bool isBusy)
        {
            if (npc == null) return;
            
            npc.SetInternalAvailability(isBusy ? NPCAvailabilityState.Busy : NPCAvailabilityState.Available);
            npc.SetConversationState(isBusy);
        }

        private IEnumerator HandlePostDialogueCooldown(List<iTalk> participants)
        {
            float dialogueDuration = UnityEngine.Random.Range(10f, 30f);
            yield return new WaitForSeconds(dialogueDuration);

            // --- NEW: Fire the end event before resetting state ---
            if (participants.Count >= 2)
            {
                OnNPCConversationEnded?.Invoke(participants[0], participants[1]);
            }

            foreach (var npc in participants)
            {
                if (npc != null)
                {
                    SetNPCBusyState(npc, false);
                    npcDialogueCooldowns[npc] = postDialogueCooldown;
                }
            }

            var conversation = activeNPCConversations.FirstOrDefault(c => 
                participants.All(p => c.GetParticipants().Contains(p)));
            if (conversation != null)
            {
                EndNPCConversation(conversation);
            }

            Debug.Log($"[iTalkSubManager] Dialogue ended. Applied {postDialogueCooldown}s cooldown to {participants.Count} NPCs.");
        }

        #region Public Interface
        public void SetDialogueCooldownRange(float min, float max)
        {
            minDialogueCooldown = min;
            maxDialogueCooldown = max;
        }

        public void SetPostDialogueCooldown(float cooldown)
        {
            postDialogueCooldown = cooldown;
        }

        public void ForceNPCDialogueCheck()
        {
            CheckForNPCToNPCDialogues();
        }

        public int GetAvailableNPCCount() => availableNPCs.Count;
        public int GetBusyNPCCount() => busyNPCs.Count;
        public int GetActiveNPCConversationCount() => activeNPCConversations.Count;
        public IReadOnlyList<Conversation> GetActiveNPCConversations() => activeNPCConversations.AsReadOnly();
        
        public void SetConversationPrefab(GameObject prefab)
        {
            conversationPrefab = prefab;
        }
        #endregion
    }
}
