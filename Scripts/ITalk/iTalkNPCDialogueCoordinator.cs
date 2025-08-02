using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

namespace CelestialCyclesSystem
{
    /// <summary>
    /// Handles ALL NPC-to-NPC conversation logic including periodic dialogues,
    /// prioritization, group conversations, and lifecycle management.
    /// It no longer defines what a conversation is, only manages them.
    /// </summary>
    public class iTalkNPCDialogueCoordinator : MonoBehaviour
    {
        public event Action<iTalk, iTalk> OnNPCConversationStarted;
        public event Action<iTalk, iTalk> OnNPCConversationEnded;

        [Header("NPC-to-NPC Dialogue Settings")]
        [SerializeField] private float minDialogueCooldown = 30f;
        [SerializeField] private float maxDialogueCooldown = 90f;
        [SerializeField] private float postDialogueCooldown = 120f;
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
        [SerializeField] private GameObject conversationPrefab;

        [Header("Debugging")]
        [Tooltip("Enable to draw visual gizmos in the Scene view.")]
        public bool enableGizmos = true;
        private List<(iTalk, iTalk)> _consideredPairsForGizmos = new List<(iTalk, iTalk)>();

        // Core state management
        private Dictionary<iTalk, float> npcDialogueCooldowns = new Dictionary<iTalk, float>();
        private List<iTalk> availableNPCs = new List<iTalk>();
        private List<iTalk> busyNPCs = new List<iTalk>();
        // MODIFIED: This now uses the external iTalkNPCConversation class
        private readonly List<iTalkNPCConversation> activeNPCConversations = new List<iTalkNPCConversation>();

        void Start()
        {
            if (iTalkManager.Instance != null)
            {
                iTalkManager.Instance.OniTalkRegistered += OnNPCRegistered;
                iTalkManager.Instance.OniTalkUnregistered += OnNPCUnregistered;
                iTalkManager.Instance.SetNPCDialogueCoordinator(this);
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
            
            var conversationsToEnd = activeNPCConversations.Where(c => c.HasParticipant(npc)).ToList();
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
            UpdateAvailableNPCs();
            
            if (availableNPCs.Count < 2) return;

            if (enableGroupConversations && availableNPCs.Count >= 3)
            {
                var group = FormConversationGroup();
                if (group.Count >= 3)
                {
                    InitiateGroupDialogue(group);
                    return;
                }
            }

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
            _consideredPairsForGizmos.Clear(); 
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

                    _consideredPairsForGizmos.Add((npc1, npc2));

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

            iTalk centerNpc = availableNPCs[UnityEngine.Random.Range(0, availableNPCs.Count)];
            group.Add(centerNpc);

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
            float worldAffinityScore = 0f;
            if (npc1.assignedPersona.world != null && npc2.assignedPersona.world != null)
            {
                worldAffinityScore = UnityEngine.Random.Range(0.2f, 1f);
            }
            float factionAffinityScore = 0f;
            if (npc1.assignedPersona.factionAffiliation != null && npc2.assignedPersona.factionAffiliation != null)
            {
                factionAffinityScore = UnityEngine.Random.Range(0.2f, 1f);
            }
            return (worldAffinityScore * worldAffinityWeight) + (factionAffinityScore * factionAffinityWeight);
        }

        #region Conversation Lifecycle
        
        public void RequestNPCToNPCConversation(iTalk initiator, iTalk partner)
        {
            if (initiator == null || partner == null || initiator.IsCurrentlyInConversation() || partner.IsCurrentlyInConversation()) return;

            iTalkNPCConversation newConversation = CreateConversationObject();
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

        // MODIFIED: This now returns the new iTalkNPCConversation type
        private iTalkNPCConversation CreateConversationObject()
        {
            GameObject conversationObj = conversationPrefab != null ? Instantiate(conversationPrefab) : new GameObject("NPC Conversation");
            iTalkNPCConversation conversation = conversationObj.GetComponent<iTalkNPCConversation>() ?? conversationObj.AddComponent<iTalkNPCConversation>();
            return conversation;
        }

        // MODIFIED: This now accepts the new iTalkNPCConversation type
        public void EndNPCConversation(iTalkNPCConversation conversation)
        {
            if (conversation == null || !activeNPCConversations.Contains(conversation)) return;

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
            if (conversation.gameObject != null) Destroy(conversation.gameObject);
        }

        public bool TryInterruptNPCConversationForPlayer(iTalk npc)
        {
            var existingConversation = activeNPCConversations.FirstOrDefault(c => c.HasParticipant(npc));
            if (existingConversation != null)
            {
                existingConversation.EndByPlayerInterruption();
                return true;
            }
            return false;
        }

        private void InitiateOneOnOneDialogue(iTalk npc1, iTalk npc2)
        {
            SetNPCBusyState(npc1, true);
            SetNPCBusyState(npc2, true);
            RequestNPCToNPCConversation(npc1, npc2);
            OnNPCConversationStarted?.Invoke(npc1, npc2);

            StartCoroutine(iTalkUtilities.RequestNPCToNPCDialogue(npc1, npc2, "",
                (response) => {
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
            foreach (var npc in group) SetNPCBusyState(npc, true);

            if (group.Count >= 2)
            {
                iTalkNPCConversation newConversation = CreateConversationObject();
                if (newConversation != null)
                {
                    newConversation.Initialize(group, this);
                    activeNPCConversations.Add(newConversation);
                    foreach (var npc in group) npc.SetConversationState(true);
                    OnNPCConversationStarted?.Invoke(group[0], group[1]);
                }
            }

            if (group.Count < 2) return;
            
            iTalk speaker = group[0];
            List<iTalkNPCPersona> groupPersonas = group.Select(npc => npc.assignedPersona).Where(p => p != null).ToList();
            string groupPrompt = iTalkUtilities.BuildGroupConversationPrompt(speaker.assignedPersona, groupPersonas,
                iTalkManager.Instance?.GetWorldNews(), iTalkManager.Instance?.GetCurrentGlobalSituation(), "casual group conversation");

            StartCoroutine(iTalkUtilities.SendLLMRequest(groupPrompt, iTalkManager.Instance?.GetApiConfig(),
                (response) => {
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

        private void SetNPCBusyState(iTalk npc, bool isBusy)
        {
            if (npc == null) return;
            npc.SetInternalAvailability(isBusy ? NPCAvailabilityState.Busy : NPCAvailabilityState.Available);
            npc.SetConversationState(isBusy);
        }

        private IEnumerator HandlePostDialogueCooldown(List<iTalk> participants)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(10f, 30f));

            if (participants.Count >= 2) OnNPCConversationEnded?.Invoke(participants[0], participants[1]);

            foreach (var npc in participants)
            {
                if (npc != null)
                {
                    SetNPCBusyState(npc, false);
                    npcDialogueCooldowns[npc] = postDialogueCooldown;
                }
            }

            var conversation = activeNPCConversations.FirstOrDefault(c => participants.All(p => c.HasParticipant(p)));
            if (conversation != null) EndNPCConversation(conversation);
        }
        #endregion

        #region Public Interface
        public void ForceNPCDialogueCheck() => CheckForNPCToNPCDialogues();
        public void SetDialogueCooldownRange(float min, float max)
        {
            minDialogueCooldown = min;
            maxDialogueCooldown = max;
        }
        public void SetPostDialogueCooldown(float cooldown) => postDialogueCooldown = cooldown;
        public int GetAvailableNPCCount() => availableNPCs.Count;
        public int GetBusyNPCCount() => busyNPCs.Count;
        public int GetActiveNPCConversationCount() => activeNPCConversations.Count;
        public IReadOnlyList<iTalkNPCConversation> GetActiveNPCConversations() => activeNPCConversations.AsReadOnly();
        public void SetConversationPrefab(GameObject prefab) => conversationPrefab = prefab;
        #endregion

        #region Editor Gizmos
        private void OnDrawGizmos()
        {
            if (!enableGizmos || !Application.isPlaying) return;

            Gizmos.color = new Color(0, 1, 1, 0.25f); 
            foreach (var npc in availableNPCs)
            {
                if (npc != null) Gizmos.DrawSphere(npc.transform.position, maxConversationDistance);
            }

            Gizmos.color = Color.yellow;
            foreach (var pair in _consideredPairsForGizmos)
            {
                if (pair.Item1 != null && pair.Item2 != null)
                {
                    Gizmos.DrawLine(pair.Item1.transform.position, pair.Item2.transform.position);
                }
            }

            Gizmos.color = Color.green;
            foreach (var conversation in activeNPCConversations)
            {
                var participants = conversation.GetParticipants();
                for (int i = 0; i < participants.Count; i++)
                {
                    for (int j = i + 1; j < participants.Count; j++)
                    {
                        if (participants[i] != null && participants[j] != null)
                        {
                            DrawThickGizmoLine(participants[i].transform.position, participants[j].transform.position, 5);
                        }
                    }
                }
            }
        }

        private void DrawThickGizmoLine(Vector3 start, Vector3 end, int thickness)
        {
            Camera cam = Camera.current;
            if (cam == null) return;

            for (int i = 0; i < thickness; i++)
            {
                Vector3 offset = cam.transform.right * (i - (thickness - 1) * 0.5f) * 0.01f;
                Gizmos.DrawLine(start + offset, end + offset);
            }
        }
        #endregion
    }
}
