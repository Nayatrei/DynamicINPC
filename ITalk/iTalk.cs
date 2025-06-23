// Filename: iTalk.cs
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
        public iTalkNPCPersona assignedPersona;
        private iTalkSituationDialogueSO personaDialogueSet;

        [Header("Runtime State")]
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

        void Awake() => _audioSource = GetComponent<AudioSource>();

        void Start()
        {
            // Initialize persona dialogue set
            personaDialogueSet = assignedPersona?.situationalDialogueSOReference;
            
            // Register with manager (central registration system)
            if (iTalkManager.Instance != null)
                iTalkManager.Instance.RegisteriTalk(this);
            else
                Debug.LogError($"[iTalk:{EntityName}] iTalkManager.Instance is null. Cannot register.", this);
        }

        void OnDestroy()
        {
            if (iTalkManager.Instance != null)
                iTalkManager.Instance.UnregisteriTalk(this);
        }

        #region State Management & Context-Based Triggering

        /// <summary>
        /// Triggers interaction and appropriate dialogue/events based on current context
        /// </summary>
        public void TriggerInteraction()
        {
            // Find controller through manager's registration system (no manual FindObjectOfType)
            var controllers = iTalkManager.Instance?.GetRegisteredControllers();
            if (controllers?.Count > 0)
            {
                controllers[0].RequestInteraction(this);
                
                // Trigger contextual events based on current state
                TriggerContextualEvents();
            }
            else
            {
                Debug.LogWarning($"[iTalk:{EntityName}] No registered controllers found for interaction.");
            }
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
            // Trigger dialogue for meaningful transitions
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
    }
}