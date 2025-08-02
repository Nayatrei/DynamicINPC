using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace CelestialCyclesSystem
{
    /// <summary>
    /// Processes player inputs for dialogue requests, manages the dialogue UI through iTalkDialogueUI, 
    /// and organizes the interaction queue to ensure sequential and orderly NPC conversations.
    /// </summary>
    public class iTalkPlayerDialogueCoordinator : MonoBehaviour
    {
        [Header("Player Reference")]
        [Tooltip("Transform of the player. If null, will use this transform.")]
        public Transform playerTransform;

        [Header("UI Configuration")]
        [SerializeField] private iTalkDialogueUI dialogueUIPrefab;
        private iTalkDialogueUI activeDialogueUI;

        [Header("Interaction Queue Settings")]
        [SerializeField] private bool enableInteractionQueue = true;
        [SerializeField] private float queueNotificationDuration = 2f;

        // Core state management - focused on controller responsibilities
        private iTalk _activeConversationTarget;
        private readonly Queue<iTalk> _interactionQueue = new Queue<iTalk>();
        private bool _isProcessingQueue = false;
        private List<string> _conversationHistory = new List<string>();

        // Events for controller-specific functionality
        public event Action<iTalk> OnConversationStarted;
        public event Action<iTalk> OnConversationEnded;
        public event Action<string> OnPlayerResponseReceived;

        void Awake()
        {
            if (playerTransform == null)
                playerTransform = transform;

            InitializeUI();
            RegisterWithManager();
        }

        void Start()
        {
            if (activeDialogueUI == null)
            {
                Debug.LogError("[iTalkPlayerDialogueCoordinator] No dialogue UI available! Player dialogues will not work.");
                enabled = false;
            }
        }

        void OnDestroy()
        {
            UnregisterFromManager();
        }

        private void InitializeUI()
        {
            // Try to find existing UI first
            activeDialogueUI = FindObjectOfType<iTalkDialogueUI>();
            
            // Create from prefab if not found
            if (activeDialogueUI == null && dialogueUIPrefab != null)
            {
                activeDialogueUI = Instantiate(dialogueUIPrefab);
                Debug.Log("[iTalkPlayerDialogueCoordinator] Created dialogue UI from prefab.");
            }

            if (activeDialogueUI != null)
            {
                activeDialogueUI.OnPlayerInputSubmitted.AddListener(HandlePlayerInputFromUI);
                activeDialogueUI.OnSendButtonClicked.AddListener(HandleSendButtonClicked);
                activeDialogueUI.Hide();
            }
        }

        private void RegisterWithManager()
        {
            iTalkManager.Instance?.RegisterController(this);
        }

        private void UnregisterFromManager()
        {
            iTalkManager.Instance?.UnregisterController(this);
        }

        #region Public Interaction Interface
        /// <summary>
        /// Main entry point for requesting dialogue with an NPC
        /// </summary>
        public void RequestInteraction(iTalk targetNpc)
        {
            if (targetNpc == null)
            {
                Debug.LogWarning("[iTalkPlayerDialogueCoordinator] Cannot request interaction with null NPC.");
                return;
            }

            if (_activeConversationTarget != null)
            {
                HandleQueuedInteraction(targetNpc);
            }
            else
            {
                AttemptInitiateConversation(targetNpc);
            }
        }

        /// <summary>
        /// Force end current conversation
        /// </summary>
        public void EndCurrentConversation()
        {
            if (_activeConversationTarget != null)
            {
                EndConversationInternal(_activeConversationTarget);
                ProcessNextInQueue();
            }
        }

        /// <summary>
        /// Clear the interaction queue
        /// </summary>
        public void ClearInteractionQueue()
        {
            _interactionQueue.Clear();
            Debug.Log("[iTalkPlayerDialogueCoordinator] Interaction queue cleared.");
        }
        #endregion

        #region Queue Management - Core Controller Responsibility
        private void HandleQueuedInteraction(iTalk targetNpc)
        {
            if (!enableInteractionQueue)
            {
                activeDialogueUI?.ShowTemporaryMessage($"{targetNpc.EntityName} is busy. Try again later.", queueNotificationDuration);
                return;
            }

            if (!_interactionQueue.Contains(targetNpc))
            {
                _interactionQueue.Enqueue(targetNpc);
                activeDialogueUI?.ShowTemporaryMessage(
                    $"{targetNpc.EntityName} will talk to you next. Queue position: {_interactionQueue.Count}", 
                    queueNotificationDuration);
                Debug.Log($"[iTalkPlayerDialogueCoordinator] Added {targetNpc.EntityName} to interaction queue. Position: {_interactionQueue.Count}");
            }
            else
            {
                activeDialogueUI?.ShowTemporaryMessage($"{targetNpc.EntityName} is already in the queue.", queueNotificationDuration);
            }
        }

        private void ProcessNextInQueue()
        {
            if (_isProcessingQueue || _interactionQueue.Count == 0) return;

            _isProcessingQueue = true;
            StartCoroutine(ProcessQueueCoroutine());
        }

        private IEnumerator ProcessQueueCoroutine()
        {
            yield return new WaitForSeconds(0.5f); // Brief pause between conversations

            while (_interactionQueue.Count > 0)
            {
                iTalk nextNpc = _interactionQueue.Dequeue();
                
                if (nextNpc == null) continue;

                // Check if NPC is still available
                string reason;
                if (iTalkManager.Instance.IsNPCInteractable(nextNpc, playerTransform.position, out reason))
                {
                    AttemptInitiateConversation(nextNpc);
                    break; // Start conversation and stop processing queue
                }
                else
                {
                    Debug.Log($"[iTalkController] Skipping queued NPC {nextNpc.EntityName}: {reason}");
                    // Continue to next NPC in queue
                }
            }

            _isProcessingQueue = false;
        }
        #endregion

        #region Conversation Management - UI and Flow Control Only
        private void AttemptInitiateConversation(iTalk targetNpc)
        {
            if (targetNpc == null) return;

            // Check if player can interact with this NPC
            string reason;
            if (!iTalkManager.Instance.IsNPCInteractable(targetNpc, playerTransform.position, out reason))
            {
                activeDialogueUI?.ShowTemporaryMessage(reason, 3f);
                return;
            }

            // Try to start player conversation (this handles interrupting NPC-NPC conversations)
            if (!iTalkManager.Instance.TryStartPlayerConversation(targetNpc))
            {
                activeDialogueUI?.ShowTemporaryMessage($"Cannot start conversation with {targetNpc.EntityName}.", 3f);
                return;
            }

            StartConversationInternal(targetNpc);
        }

        private void StartConversationInternal(iTalk targetNpc)
        {
            _activeConversationTarget = targetNpc;
            _conversationHistory.Clear();

            // Set up UI - core controller responsibility
            activeDialogueUI.SetNPCName(targetNpc.EntityName);
            if (targetNpc.assignedPersona?.portraitSprite != null)
            {
                activeDialogueUI.SetNPCPortrait(targetNpc.assignedPersona.portraitSprite);
            }
            activeDialogueUI.ClearHistory();
            activeDialogueUI.Show();

            // Get initial greeting and display - delegate audio handling to iTalk
            (string greetingLine, AudioClip greetingClip) = targetNpc.GetSituationalLineAndAudio(NPCAvailabilityState.Greeting);
            
            if (string.IsNullOrWhiteSpace(greetingLine))
            {
                (greetingLine, greetingClip) = targetNpc.GetSituationalLineAndAudio(NPCAvailabilityState.Available);
            }
            
            if (string.IsNullOrWhiteSpace(greetingLine))
            {
                greetingLine = "Hello there."; // Final fallback
            }

            // UI management only - let iTalk handle all audio/TTS
            activeDialogueUI.AddDialogueLine(targetNpc.EntityName, greetingLine);
            _conversationHistory.Add($"{targetNpc.EntityName}: {greetingLine}");

            // Delegate all audio handling to iTalk's context-based system
            targetNpc.TriggerContextBasedDialogue(NPCAvailabilityState.Greeting);

            OnConversationStarted?.Invoke(targetNpc);
            Debug.Log($"[iTalkController] Started conversation with {targetNpc.EntityName}");
        }

        private void EndConversationInternal(iTalk targetNpc)
        {
            if (targetNpc == null) return;

            // Get goodbye line for UI display only
            (string goodbyeLine, AudioClip goodbyeClip) = targetNpc.GetSituationalLineAndAudio(NPCAvailabilityState.Goodbye);
            if (!string.IsNullOrWhiteSpace(goodbyeLine))
            {
                // UI management only
                activeDialogueUI.AddDialogueLine(targetNpc.EntityName, goodbyeLine);
                _conversationHistory.Add($"{targetNpc.EntityName}: {goodbyeLine}");

                // Delegate all audio handling to iTalk's context-based system
                targetNpc.TriggerContextBasedDialogue(NPCAvailabilityState.Goodbye);
            }

            // UI management - core controller responsibility
            activeDialogueUI.Hide();
            _activeConversationTarget = null;
            
            OnConversationEnded?.Invoke(targetNpc);
            Debug.Log($"[iTalkController] Ended conversation with {targetNpc.EntityName}");
        }
        #endregion

        #region UI Event Handlers - Core Controller Responsibility
        private void HandlePlayerInputFromUI(string userInput)
        {
            if (_activeConversationTarget == null || string.IsNullOrWhiteSpace(userInput))
                return;

            // Process player input - core controller functionality
            _conversationHistory.Add($"Player: {userInput}");
            
            // Limit history size for performance
            if (_conversationHistory.Count > 10)
            {
                _conversationHistory.RemoveAt(0);
            }

            OnPlayerResponseReceived?.Invoke(userInput);

            // Delegate AI processing to utilities
            RequestAIResponse(userInput);
        }

        private void HandleSendButtonClicked()
        {
            // UI event handling - could be extended for special send button behavior
        }
        #endregion

        #region AI Integration - Properly Delegated to iTalkUtilities
        private void RequestAIResponse(string userInput)
        {
            if (_activeConversationTarget?.assignedPersona == null)
            {
                Debug.LogError("[iTalkController] Cannot request AI response: No active conversation or persona.");
                return;
            }

            // UI feedback management
            activeDialogueUI.ShowTemporaryMessage("...", 0.5f);

            // Delegate all AI processing to iTalkUtilities
            string conversationHistoryString = iTalkUtilities.FormatHistory(_conversationHistory);

            StartCoroutine(iTalkUtilities.RequestPlayerNPCDialogue(
                _activeConversationTarget,
                userInput,
                conversationHistoryString,
                OnAIResponseSuccess,
                OnAIResponseError
            ));
        }

        private void OnAIResponseSuccess(string aiResponse)
        {
            if (_activeConversationTarget == null) return;

            // UI management only
            activeDialogueUI.HideTemporaryMessage();
            activeDialogueUI.AddDialogueLine(_activeConversationTarget.EntityName, aiResponse);
            _conversationHistory.Add($"{_activeConversationTarget.EntityName}: {aiResponse}");

            // Delegate all TTS to iTalkUtilities - no redundant handling
            iTalkUtilities.RequestDialogueTTS(_activeConversationTarget, aiResponse);
        }

        private void OnAIResponseError(string errorMessage)
        {
            // UI management for error display
            activeDialogueUI.HideTemporaryMessage();
            activeDialogueUI.ShowTemporaryMessage($"AI Error: {errorMessage}", 5f);
            Debug.LogError($"[iTalkController] AI Response Error: {errorMessage}");

            // Use centralized fallback system - no redundant logic
            if (_activeConversationTarget != null)
            {
                var (fallbackLine, fallbackClip) = iTalkUtilities.GetFallbackDialogue(
                    _activeConversationTarget, 
                    NPCAvailabilityState.Available
                );
                
                if (!string.IsNullOrWhiteSpace(fallbackLine))
                {
                    // UI management only
                    activeDialogueUI.AddDialogueLine(_activeConversationTarget.EntityName, fallbackLine);
                    _conversationHistory.Add($"{_activeConversationTarget.EntityName}: {fallbackLine}");
                    
                    // Delegate all audio handling to iTalk's context system
                    _activeConversationTarget.TriggerContextBasedDialogue(NPCAvailabilityState.Available);
                }
            }
        }
        #endregion

        #region Public Accessors - Controller State Information
        public iTalk GetActiveConversationTarget() => _activeConversationTarget;
        public bool IsInConversation() => _activeConversationTarget != null;
        public int GetQueueLength() => _interactionQueue.Count;
        public List<string> GetConversationHistory() => new List<string>(_conversationHistory);
        #endregion
    }
}