// iTalk.cs (Fixed: Added null checks; removed any implicit dependencies; ensured modularity)
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

namespace CelestialCyclesSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class iTalk : MonoBehaviour
    {
        [Header("Persona & Dialogue Configuration")]
        [Tooltip("The NPC persona defining character traits and dialogue settings.")]
        public iTalkNPCPersona assignedPersona;
        private iTalkSituationDialogueSO personaDialogueSet;

        public enum InteractionMode { Everyone, OnlyNPCs, OnlyPlayers }
        [Header("Interaction Type")]
        [Tooltip("Determines who this NPC can interact with: Everyone, only other NPCs, or only players.")]
        [SerializeField] private InteractionMode interactionMode;

        [Header("Runtime State")]
        [Tooltip("Current availability state of the NPC (e.g., Available, Busy, Working).")]
        [SerializeField]
        private NPCAvailabilityState currentInternalAvailability = NPCAvailabilityState.Available;

        // Conversation state tracking
        private bool _isCurrentlyInConversation = false;

        // Context-based event system
        public event Action<iTalk, NPCAvailabilityState> OnInternalAvailabilityChanged;
        public event Action<iTalk, string> OnDialogueTriggered;
        public event Action<iTalk, NPCAvailabilityState> OnContextualEventTriggered;

        // Public accessors aligned with goal
        public string EntityName => assignedPersona ? assignedPersona.characterName : gameObject.name;
        public Vector3 Position => transform.position;

        private AudioSource _audioSource;

        [Header("Debugging")]
        [Tooltip("Enable to draw gizmos in the Scene view showing the NPC's collider and state (green for available, red for unavailable).")]
        [SerializeField] private bool enableNPCGizmos = true;
        private Color gizmoColor = Color.green; // Default: Available

        private static int DefaultInteractableLayer = -1;
        private static bool isLayerInitialized = false;

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            InitializeDefaultLayer();
            EnsureColliderAndLayer();
        }

        void Start()
        {
            // Initialize persona dialogue set
            personaDialogueSet = assignedPersona?.situationalDialogueSOReference;

            // Register with manager (central registration system)
            if (iTalkManager.Instance != null)
                iTalkManager.Instance.RegisteriTalk(this);
            else
                Debug.LogError($"[iTalk:{EntityName}] iTalkManager.Instance is null. Cannot register.", this);

            OnInternalAvailabilityChanged += UpdateGizmoColor; // Subscribe to state changes
            UpdateGizmoColor(this, currentInternalAvailability); // Initial update
        }

        void OnDestroy()
        {
            if (iTalkManager.Instance != null)
                iTalkManager.Instance.UnregisteriTalk(this);

            OnInternalAvailabilityChanged -= UpdateGizmoColor;
        }

        private void InitializeDefaultLayer()
        {
            if (!isLayerInitialized)
            {
                DefaultInteractableLayer = LayerMask.NameToLayer("Interactable");
                if (DefaultInteractableLayer == -1)
                {
                    Debug.LogError("[iTalk] Default 'Interactable' layer not found in project settings! Please add it in Layer settings.");
                }
                isLayerInitialized = true;
            }
        }

        private void EnsureColliderAndLayer()
        {
            // Ensure collider exists
            if (!GetComponent<Collider>())
            {
                gameObject.AddComponent<SphereCollider>().isTrigger = true; // Add default trigger collider
                Debug.LogWarning($"[iTalk:{EntityName}] Added default SphereCollider for interaction detection.");
            }
            // Set layer if not in manager's interactableLayer
            if (iTalkManager.Instance != null && (iTalkManager.Instance.interactableLayer.value & (1 << gameObject.layer)) == 0)
            {
                if (DefaultInteractableLayer == -1)
                {
                    Debug.LogError($"[iTalk:{EntityName}] Default 'Interactable' layer not found in project settings!");
                }
                else
                {
                    gameObject.layer = DefaultInteractableLayer;
                    Debug.Log($"[iTalk:{EntityName}] Set layer to 'Interactable' for detection by iTalkManager.");
                }
            }
        }

        #region State Management & Context-Based Triggering

        /// <summary>
        /// Triggers interaction and appropriate dialogue/events based on current context
        /// </summary>
        public void TriggerInteraction()
        {
            // Directly request interaction via manager (since coordinator merged)
            iTalkManager.Instance?.RequestInteraction(this);

            // Trigger contextual events based on current state
            TriggerContextualEvents();
        }

        /// <summary>
        /// Triggers appropriate events based on current context and state
        /// </summary>
        private void TriggerContextualEvents()
        {
            NPCAvailabilityState currentState = GetInternalAvailability();

            // Trigger context-based events
            OnContextualEventTriggered?.Invoke(this, currentState);

            // Get and potentially play situational dialogue based on context
            var (contextLine, contextClip) = GetSituationalLineAndAudio(currentState);
            if (!string.IsNullOrWhiteSpace(contextLine))
            {
                OnDialogueTriggered?.Invoke(this, contextLine);

                // Use iTalkUtilities for TTS (proper delegation)
                iTalkUtilities.RequestSituationalTTS(this, currentState);
            }
        }

        public bool IsInternallyAvailableForDialogue() => currentInternalAvailability == NPCAvailabilityState.Available;
        public NPCAvailabilityState GetInternalAvailability() => currentInternalAvailability;
        public AudioSource GetAudioSource() => _audioSource;

        /// <summary>
        /// Sets internal availability and triggers appropriate context-based events
        /// </summary>
        public void SetInternalAvailability(NPCAvailabilityState newState)
        {
            if (currentInternalAvailability != newState)
            {
                NPCAvailabilityState previousState = currentInternalAvailability;
                currentInternalAvailability = newState;

                // Trigger availability change event
                OnInternalAvailabilityChanged?.Invoke(this, newState);

                // Trigger contextual events for state transitions
                TriggerStateTransitionEvents(previousState, newState);
            }
        }

        /// <summary>
        /// Triggers appropriate events when NPC state transitions occur
        /// </summary>
        private void TriggerStateTransitionEvents(NPCAvailabilityState fromState, NPCAvailabilityState toState)
        {
            // Log significant state changes
            Debug.Log($"[iTalk:{EntityName}] State changed from {fromState} to {toState}");

            // Trigger contextual dialogue for certain transitions
            if (ShouldTriggerDialogueForTransition(fromState, toState))
            {
                var (transitionLine, transitionClip) = GetSituationalLineAndAudio(toState);
                if (!string.IsNullOrWhiteSpace(transitionLine))
                {
                    OnDialogueTriggered?.Invoke(this, transitionLine);
                    iTalkUtilities.RequestSituationalTTS(this, toState);
                }
            }

            // Trigger context-based events
            OnContextualEventTriggered?.Invoke(this, toState);
        }

        /// <summary>
        /// Determines if dialogue should be triggered for specific state transitions
        /// </summary>
        private bool ShouldTriggerDialogueForTransition(NPCAvailabilityState fromState, NPCAvailabilityState toState)
        {
            return toState switch
            {
                NPCAvailabilityState.Busy when fromState == NPCAvailabilityState.Available => true,
                NPCAvailabilityState.Working when fromState == NPCAvailabilityState.Available => true,
                NPCAvailabilityState.Available when fromState != NPCAvailabilityState.Available => true,
                _ => false
            };
        }

        #endregion

        #region Conversation State Management

        /// <summary>
        /// Returns whether this NPC is currently in a conversation.
        /// </summary>
        public bool IsCurrentlyInConversation() => _isCurrentlyInConversation;

        /// <summary>
        /// Sets the conversation state and triggers appropriate contextual events
        /// </summary>
        public void SetConversationState(bool inConversation)
        {
            if (_isCurrentlyInConversation != inConversation)
            {
                _isCurrentlyInConversation = inConversation;

                // Trigger contextual events when conversation state changes
                if (inConversation)
                {
                    OnContextualEventTriggered?.Invoke(this, NPCAvailabilityState.Busy);
                }
                else
                {
                    OnContextualEventTriggered?.Invoke(this, currentInternalAvailability);
                }
            }
        }

        #endregion

        #region Persona & Dialogue Management

        public string GetPersonaPromptBase()
        {
            if (assignedPersona == null) return string.Empty;
            assignedPersona.BuildPrompt(transform.position.ToString(), "general interaction", "");
            return assignedPersona.builtPrompt;
        }

        /// <summary>
        /// Returns a contextual dialogue line and audio for the given state, prioritizing persona data
        /// </summary>
        public (string line, AudioClip clip) GetSituationalLineAndAudio(NPCAvailabilityState forState)
        {
            // Priority 1: Attempt to get from Persona's assigned SO
            if (assignedPersona != null)
            {
                var personaResult = assignedPersona.GetSituationalDialogue(forState);
                if (!string.IsNullOrEmpty(personaResult.line))
                {
                    return personaResult;
                }
            }

            // Priority 2: Check persona dialogue set
            if (personaDialogueSet?.dialogues?.dialogues != null)
            {
                if (personaDialogueSet.dialogues.dialogues.TryGetValue(forState, out var lines) && lines?.Count > 0)
                {
                    var validEntries = lines.Where(dl => !string.IsNullOrWhiteSpace(dl.text)).ToList();
                    if (validEntries.Count > 0)
                    {
                        var chosen = validEntries[UnityEngine.Random.Range(0, validEntries.Count)];
                        return (chosen.text, chosen.audio);
                    }
                }
            }

            // Priority 3: Use iTalkUtilities fallback (centralized fallback system)
            return iTalkUtilities.GetFallbackDialogue(this, forState);
        }

        /// <summary>
        /// Returns a contextual goodbye line and audio using persona data
        /// </summary>
        public (string line, AudioClip clip) GetGoodbyeLineAndAudio()
        {
            // Priority 1: Attempt to get from Persona's assigned SO
            if (assignedPersona != null)
            {
                var personaResult = assignedPersona.GetGoodbyeDialogue();
                if (!string.IsNullOrEmpty(personaResult.line))
                {
                    return personaResult;
                }
            }

            // Priority 2: Check persona dialogue set for goodbye state
            if (personaDialogueSet?.dialogues?.dialogues != null)
            {
                if (personaDialogueSet.dialogues.dialogues.TryGetValue(NPCAvailabilityState.Goodbye, out var lines) && lines?.Count > 0)
                {
                    var validEntries = lines.Where(dl => !string.IsNullOrWhiteSpace(dl.text)).ToList();
                    if (validEntries.Count > 0)
                    {
                        var chosen = validEntries[UnityEngine.Random.Range(0, validEntries.Count)];
                        return (chosen.text, chosen.audio);
                    }
                }
            }

            // Priority 3: Use iTalkUtilities fallback
            return iTalkUtilities.GetFallbackDialogue(this, NPCAvailabilityState.Goodbye);
        }

        #endregion

        #region Context-Based Public Interface

        /// <summary>
        /// Triggers dialogue based on current context and situation
        /// </summary>
        public void TriggerContextBasedDialogue(NPCAvailabilityState contextState)
        {
            var (dialogueLine, audioClip) = GetSituationalLineAndAudio(contextState);
            if (!string.IsNullOrWhiteSpace(dialogueLine))
            {
                OnDialogueTriggered?.Invoke(this, dialogueLine);

                // Play audio with proper prioritization (clip > TTS)
                if (audioClip != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(audioClip);
                }
                else
                {
                    iTalkUtilities.RequestSituationalTTS(this, contextState);
                }
            }
        }

        /// <summary>
        /// Updates NPC state based on external context (e.g., time of day, events)
        /// </summary>
        public void UpdateStateFromContext(NPCAvailabilityState newContextState, string contextReason = "")
        {
            if (currentInternalAvailability != newContextState)
            {
                Debug.Log($"[iTalk:{EntityName}] Context update: {contextReason}");
                SetInternalAvailability(newContextState);
            }
        }

        #endregion

        private void UpdateGizmoColor(iTalk sender, NPCAvailabilityState state)
        {
            gizmoColor = state == NPCAvailabilityState.Available ? Color.green : Color.red;
        }

        private void OnDrawGizmosSelected()
        {
            if (!enableNPCGizmos) return;
            Collider collider = GetComponent<Collider>();
            if (collider == null) return;
            Gizmos.color = gizmoColor;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            if (collider is BoxCollider box)
            {
                Gizmos.DrawWireCube(Vector3.zero, box.size);
            }
            else if (collider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(Vector3.zero, sphere.radius);
            }
        }
    }
}