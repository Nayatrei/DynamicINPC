// iTalkManager.cs (Fixed: Added null checks; ensured LLM disable uses presets first; modularity improvements)
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using System.Text;

namespace CelestialCyclesSystem
{
    /// <summary>
    /// Acts as the central controller for the iTalk system by managing registration and deregistration of all NPCs,
    /// updating the list of interactable NPCs, and overseeing dialogue flow for player-initiated conversations only.
    /// NPC-to-NPC conversations are handled by iTalkNPCDialogueCoordinator.
    /// Now also manages player dialogue coordination and UI.
    /// </summary>
    public interface ITalkManager
    {
        void RegisteriTalk(iTalk talkComponent);
        void UnregisteriTalk(iTalk talkComponent);
        bool IsNPCInteractable(iTalk npc, Vector3 playerPosition, out string reason);
        bool TryStartPlayerConversation(iTalk npc);
        void RequestDialogueFromAI(iTalk speakeriTalk, string userInput, string conversationHistory, Action<string> onSuccess, Action<string> onError);
        void RequestTTS(iTalkNPCPersona speakerPersona, string textToSpeak, AudioSource targetAudioSource);
        iTalkApiConfigSO GetApiConfig();
        List<iTalkNewsItemEntry> GetWorldNews();
        string GetCurrentGlobalSituation();
        IReadOnlyList<iTalk> SimulateInteractableCheck(Vector3 playerPos);
    }

    public class iTalkManager : MonoBehaviour, ITalkManager
    {
        public static iTalkManager Instance { get; private set; }

        [Header("API Configuration")]
        [Tooltip("ScriptableObject containing API settings for LLM and TTS requests.")]
        [SerializeField] private iTalkApiConfigSO apiConfig;

        [Header("World Context")]
        [Tooltip("Description of the current global situation affecting NPC dialogue context.")]
        [SerializeField] private string currentGlobalSituation = "A typical day in the realm.";
        [Tooltip("List of recent news items influencing NPC conversations.")]
        [SerializeField] private List<iTalkNewsItemEntry> worldNews = new List<iTalkNewsItemEntry>();
        private const int MAX_NEWS_IN_PROMPT = 5;

        [Header("Interaction Settings")]
        [Tooltip("Radius (in units) around the player where NPCs can engage in dynamic behaviors (e.g., NPC-to-NPC conversations).")]
        public float maxInteractionDistance = 20.0f;
        [Tooltip("LayerMask defining which layers are checked for interactable NPCs.")]
        public LayerMask interactableLayer;

        [Header("Token Management")]
        [Tooltip("Estimated tokens per character for LLM prompt inputs.")]
        public float estimatedTokensPerCharPrompt = 0.3f;
        [Tooltip("Estimated tokens per character for LLM response outputs.")]
        public float estimatedTokensPerCharResponse = 0.4f;
        [Tooltip("Maximum tokens allowed per LLM request to prevent exceeding quotas.")]
        public int maxTokensPerRequestSafety = 1000;
        [Tooltip("Total token quota for the system over a period.")]
        public int tokenQuotaPeriod = 100000;
        [Tooltip("Current token usage within the quota period.")]
        [SerializeField] private int currentTokenUsage = 0;
        [Tooltip("Disable LLM requests and force fallback to presets.")]
        [SerializeField] private bool disableLLM = true;

        [Header("Debugging")]
        [Tooltip("Enable drawing of gizmos in the Scene view to visualize detection radii and interactable NPCs.")]
        [SerializeField] private bool enableDetectionGizmos = true;

        // Registration management - core responsibility
        private readonly List<iTalk> registeredTalkComponents = new List<iTalk>();
        private readonly List<iTalk> currentlyInteractableNPCs = new List<iTalk>();

        // Player transform (replaces primaryPlayerTransform)
        [Header("Player Reference")]
        [Tooltip("Transform of the player. If null, uses this transform.")]
        public Transform playerTransform;

        // State tracking
        private bool isApiRequestInProgress = false;

        // Events for registration and interactable changes
        public event Action<iTalk> OniTalkRegistered;
        public event Action<iTalk> OniTalkUnregistered;
        public event Action<IReadOnlyList<iTalk>> OnInteractableNPCsChanged;

        // TTS event for external systems
        public delegate void TTSRequestHandler(iTalkNPCPersona speakerPersona, string textToSpeak, AudioSource targetAudioSource);
        public event TTSRequestHandler OnRequestTTSGenerated;

        // Reference to SubManager for NPC conversation delegation
        private iTalkNPCDialogueCoordinator npcDialogueCoordinator;

        // Merged from Coordinator: UI and Conversation Management
        [Header("UI Configuration")]
        [Tooltip("Prefab for the dialogue UI. Instantiated if no UI is found in the scene.")]
        [SerializeField] private iTalkDialogueUI dialogueUIPrefab;
        private iTalkDialogueUI activeDialogueUI;

        [Header("Interaction Queue Settings")]
        [Tooltip("Enable queuing of NPC interactions when another conversation is active.")]
        [SerializeField] private bool enableInteractionQueue = true;
        [Tooltip("Duration (in seconds) to show temporary UI messages (e.g., queue notifications).")]
        [SerializeField] private float queueNotificationDuration = 2f;

        private iTalk _activeConversationTarget;
        private readonly Queue<iTalk> _interactionQueue = new Queue<iTalk>();
        private bool _isProcessingQueue = false;
        private List<string> _conversationHistory = new List<string>();

        public event Action<iTalk> OnConversationStarted;
        public event Action<iTalk> OnConversationEnded;
        public event Action<string> OnPlayerResponseReceived;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadInitialConfiguration();
            }
            else
            {
                Destroy(gameObject);
            }

            if (playerTransform == null)
                playerTransform = transform;
        }

        void Start()
        {
            npcDialogueCoordinator = FindObjectOfType<iTalkNPCDialogueCoordinator>();
            if (npcDialogueCoordinator == null)
            {
                Debug.LogWarning("[iTalkManager] iTalkNPCDialogueCoordinator not found. NPC-to-NPC conversation interruptions may not work.");
            }

            InitializeUI();
        }

        void Update()
        {
            UpdateInteractableNPCsList();
        }

        private void LoadInitialConfiguration()
        {
            if (apiConfig == null)
            {
                apiConfig = Resources.Load<iTalkApiConfigSO>("NPCPersona/ApiConfigSettings");
                if (apiConfig == null) Debug.LogError("[iTalkManager] iTalkApiConfigSO not found!");
            }

            LoadNewsItemsFromResources();
        }

        public void LoadNewsItemsFromResources()
        {
            var newsEntries = Resources.LoadAll<iTalkNewsItemEntry>("NewsItem");
            worldNews.Clear();
            worldNews.AddRange(newsEntries);
            worldNews = worldNews.OrderByDescending(entry => entry.timestamps.Count > 0 ? entry.timestamps.Max() : 0).ToList();
            Debug.Log($"[iTalkManager] Loaded {worldNews.Count} news items.");
        }

        #region NPC Registration Management
        public void RegisteriTalk(iTalk talkComponent)
        {
            if (talkComponent == null) return;
            if (!registeredTalkComponents.Contains(talkComponent))
            {
                registeredTalkComponents.Add(talkComponent);
                OniTalkRegistered?.Invoke(talkComponent);
                Debug.Log($"[iTalkManager] Registered iTalk: {talkComponent.EntityName}");
            }
        }

        public void UnregisteriTalk(iTalk talkComponent)
        {
            if (talkComponent == null) return;
            if (registeredTalkComponents.Remove(talkComponent))
            {
                bool removedFromInteractable = currentlyInteractableNPCs.Remove(talkComponent);
                OniTalkUnregistered?.Invoke(talkComponent);
                if (removedFromInteractable) OnInteractableNPCsChanged?.Invoke(currentlyInteractableNPCs.AsReadOnly());
                Debug.Log($"[iTalkManager] Unregistered iTalk: {talkComponent.EntityName}");
            }
        }
        #endregion

        #region Interactable NPCs Management
        public void UpdateInteractableNPCsList()
        {
            if (playerTransform == null)
            {
                if (currentlyInteractableNPCs.Count > 0)
                {
                    currentlyInteractableNPCs.Clear();
                    OnInteractableNPCsChanged?.Invoke(currentlyInteractableNPCs.AsReadOnly());
                    Debug.Log("[iTalkManager] No player transform found. Cleared interactable NPCs.");
                }
                return;
            }
            Collider[] hitColliders = Physics.OverlapSphere(playerTransform.position, maxInteractionDistance, interactableLayer);
            List<iTalk> newInteractableNPCs = new List<iTalk>();
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.TryGetComponent<iTalk>(out var npc) && npc.IsInternallyAvailableForDialogue())
                {
                    newInteractableNPCs.Add(npc);
                }
            }
            if (newInteractableNPCs.Count != currentlyInteractableNPCs.Count || !newInteractableNPCs.All(currentlyInteractableNPCs.Contains))
            {
                currentlyInteractableNPCs.Clear();
                currentlyInteractableNPCs.AddRange(newInteractableNPCs);
                Debug.Log($"[iTalkManager] Interactable NPCs updated: {string.Join(", ", currentlyInteractableNPCs.Select(n => n.EntityName))}");
                OnInteractableNPCsChanged?.Invoke(currentlyInteractableNPCs.AsReadOnly());
            }
        }

        public IReadOnlyList<iTalk> GetCurrentlyInteractableNPCs() => currentlyInteractableNPCs.AsReadOnly();

        public IReadOnlyList<iTalk> SimulateInteractableCheck(Vector3 playerPos)
        {
            List<iTalk> simulatedInteractables = new List<iTalk>();
            Collider[] hitColliders = Physics.OverlapSphere(playerPos, maxInteractionDistance, interactableLayer);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.TryGetComponent<iTalk>(out var npc) && npc.IsInternallyAvailableForDialogue())
                {
                    simulatedInteractables.Add(npc);
                }
            }
            Debug.Log($"[iTalkManager] Simulated detection at {playerPos}: {string.Join(", ", simulatedInteractables.Select(n => n.EntityName))}");
            return simulatedInteractables.AsReadOnly();
        }

        public bool IsNPCInteractable(iTalk npc, Vector3 playerPosition, out string reason)
        {
            reason = "";
            if (npc == null) { reason = "NPC reference is null."; return false; }

            NPCAvailabilityState internalState = npc.GetInternalAvailability();
            if (internalState != NPCAvailabilityState.Available)
            {
                (string line, _) = npc.GetSituationalLineAndAudio(internalState);
                reason = !string.IsNullOrWhiteSpace(line) ? line : $"{npc.EntityName} is currently unavailable.";
                return false;
            }

            float distance = Vector3.Distance(playerPosition, npc.Position);
            if (distance > maxInteractionDistance)
            {
                reason = $"{npc.EntityName} is too far away.";
                return false;
            }

            reason = "Available";
            return true;
        }
        #endregion

        #region Player Conversation Support
        public bool TryStartPlayerConversation(iTalk npc)
        {
            if (npc == null) return false;

            if (npcDialogueCoordinator != null)
            {
                bool wasInterrupted = npcDialogueCoordinator.TryInterruptNPCConversationForPlayer(npc);
                if (wasInterrupted)
                {
                    Debug.Log($"[iTalkManager] Interrupted NPC conversation for player interaction with {npc.EntityName}");
                }
            }
            return true;
        }
        #endregion

        #region World Context Management
        public void SetCurrentGlobalSituation(string situation) => currentGlobalSituation = situation;
        public string GetCurrentGlobalSituation() => currentGlobalSituation;

        public void AddNewsItem(iTalkNewsItemEntry item)
        {
            if (item == null) return;
            worldNews.Add(item);
            worldNews = worldNews.OrderByDescending(entry => entry.timestamps.Count > 0 ? entry.timestamps.Max() : 0).ToList();
            if (worldNews.Count > 20) worldNews.RemoveRange(20, worldNews.Count - 20);
        }

        public List<iTalkNewsItemEntry> GetWorldNews() => new List<iTalkNewsItemEntry>(worldNews);
        #endregion

        #region Token Management
        public bool HasEnoughTokens(int estimatedInputChars)
        {
            if (tokenQuotaPeriod <= 0) return true;
            int estimatedPromptTokens = Mathf.CeilToInt(estimatedInputChars * estimatedTokensPerCharPrompt);
            return (currentTokenUsage + estimatedPromptTokens + (apiConfig?.maxTokens ?? 150)) < tokenQuotaPeriod;
        }

        public void LogTokenUsage(string prompt, string response)
        {
            if (tokenQuotaPeriod <= 0 || apiConfig == null) return;
            int promptTokens = Mathf.CeilToInt(prompt.Length * estimatedTokensPerCharPrompt);
            int responseTokens = Mathf.CeilToInt(response.Length * estimatedTokensPerCharResponse);
            currentTokenUsage += promptTokens + responseTokens;
        }

        public void ResetTokenQuota()
        {
            currentTokenUsage = 0;
            Debug.Log("[iTalkManager] Token quota reset.");
        }

        public int GetCurrentTokenUsage() => currentTokenUsage;
        #endregion

        #region AI Request Management
        public void RequestDialogueFromAI(iTalk speakeriTalk, string userInput, string conversationHistory,
            Action<string> onSuccess, Action<string> onError)
        {
            if (speakeriTalk?.assignedPersona == null)
            {
                onError?.Invoke("Speaker NPC or persona is null.");
                return;
            }

            if (disableLLM)
            {
                var (fallback, _) = iTalkUtilities.GetFallbackDialogue(speakeriTalk, NPCAvailabilityState.Available);
                onSuccess?.Invoke(fallback ?? "Preset response here.");
                return;
            }

            if (isApiRequestInProgress)
            {
                onError?.Invoke("An AI request is already in progress. Please wait.");
                return;
            }
            isApiRequestInProgress = true;
            StartCoroutine(iTalkUtilities.RequestPlayerNPCDialogue(
                speakeriTalk,
                userInput,
                conversationHistory,
                (response) => {
                    isApiRequestInProgress = false;
                    onSuccess?.Invoke(response);
                },
                (error) => {
                    isApiRequestInProgress = false;
                    onError?.Invoke(error);
                }
            ));
        }

        public void RequestTTS(iTalkNPCPersona speakerPersona, string textToSpeak, AudioSource targetAudioSource)
        {
            iTalkUtilities.RequestTTS(speakerPersona, textToSpeak, targetAudioSource);
        }
        #endregion

        #region Public Accessors
        public iTalkApiConfigSO GetApiConfig() => apiConfig;
        public List<iTalk> GetRegisteredTalkComponents() => new List<iTalk>(registeredTalkComponents);

        public void CheckRegistrationConsistency()
        {
            int nullTalks = registeredTalkComponents.RemoveAll(item => item == null);
            if (nullTalks > 0)
                Debug.LogWarning($"[iTalkManager] Cleaned up {nullTalks} null iTalk references.");
        }

        public void SetNPCDialogueCoordinator(iTalkNPCDialogueCoordinator manager)
        {
            npcDialogueCoordinator = manager;
        }
        #endregion

        #region Debugging
        private void OnDrawGizmos()
        {
            if (!enableDetectionGizmos || !Application.isPlaying) return;
            if (playerTransform == null) return;
            Gizmos.color = new Color(0, 1, 0, 0.3f); // Green for world active radius
            Gizmos.DrawWireSphere(playerTransform.position, maxInteractionDistance);
            Gizmos.color = Color.yellow; // Yellow lines to detected NPCs
            foreach (var npc in currentlyInteractableNPCs)
            {
                if (npc != null)
                {
                    Gizmos.DrawLine(playerTransform.position, npc.Position);
                }
            }
        }
        #endregion

        #region Merged UI Initialization from Coordinator
        private void InitializeUI()
        {
            activeDialogueUI = FindObjectOfType<iTalkDialogueUI>();
            if (activeDialogueUI == null && dialogueUIPrefab != null)
            {
                activeDialogueUI = Instantiate(dialogueUIPrefab);
                Debug.Log("[iTalkManager] Created dialogue UI from prefab.");
            }

            if (activeDialogueUI != null)
            {
                activeDialogueUI.OnPlayerInputSubmitted.AddListener(HandlePlayerInputFromUI);
                activeDialogueUI.OnSendButtonClicked.AddListener(HandleSendButtonClicked);
                activeDialogueUI.Hide();
            }
            else
            {
                Debug.LogError("[iTalkManager] No dialogue UI available! Player dialogues will not work.");
            }
        }
        #endregion

        #region Merged Player Conversation Management from Coordinator
        public void RequestInteraction(iTalk targetNpc)
        {
            if (targetNpc == null)
            {
                Debug.LogWarning("[iTalkManager] Cannot request interaction with null NPC.");
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

        public void EndCurrentConversation()
        {
            if (_activeConversationTarget != null)
            {
                EndConversationInternal(_activeConversationTarget);
                ProcessNextInQueue();
            }
        }

        public void ClearInteractionQueue()
        {
            _interactionQueue.Clear();
            Debug.Log("[iTalkManager] Interaction queue cleared.");
        }

        private void HandleQueuedInteraction(iTalk targetNpc)
        {
            if (!enableInteractionQueue)
            {
                ShowTemporaryMessage($"{targetNpc.EntityName} is busy. Try again later.", queueNotificationDuration);
                return;
            }

            if (!_interactionQueue.Contains(targetNpc))
            {
                _interactionQueue.Enqueue(targetNpc);
                ShowTemporaryMessage(
                    $"{targetNpc.EntityName} will talk to you next. Queue position: {_interactionQueue.Count}",
                    queueNotificationDuration);
                Debug.Log($"[iTalkManager] Added {targetNpc.EntityName} to interaction queue. Position: {_interactionQueue.Count}");
            }
            else
            {
                ShowTemporaryMessage($"{targetNpc.EntityName} is already in the queue.", queueNotificationDuration);
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
            yield return new WaitForSeconds(0.5f);

            while (_interactionQueue.Count > 0)
            {
                iTalk nextNpc = _interactionQueue.Dequeue();

                if (nextNpc == null) continue;

                string reason;
                if (IsNPCInteractable(nextNpc, playerTransform.position, out reason))
                {
                    AttemptInitiateConversation(nextNpc);
                    break;
                }
                else
                {
                    Debug.Log($"[iTalkManager] Skipping queued NPC {nextNpc.EntityName}: {reason}");
                }
            }

            _isProcessingQueue = false;
        }

        private void AttemptInitiateConversation(iTalk targetNpc)
        {
            string reason;
            if (!IsNPCInteractable(targetNpc, playerTransform.position, out reason))
            {
                ShowTemporaryMessage(reason, 3f);
                return;
            }

            if (!TryStartPlayerConversation(targetNpc))
            {
                ShowTemporaryMessage($"Cannot start conversation with {targetNpc.EntityName}.", 3f);
                return;
            }

            StartConversationInternal(targetNpc);
        }

        private void StartConversationInternal(iTalk targetNpc)
        {
            _activeConversationTarget = targetNpc;
            _conversationHistory.Clear();

            activeDialogueUI.SetNPCName(targetNpc.EntityName);
            if (targetNpc.assignedPersona?.portraitSprite != null)
            {
                activeDialogueUI.SetNPCPortrait(targetNpc.assignedPersona.portraitSprite);
            }
            activeDialogueUI.ClearHistory();
            activeDialogueUI.Show();

            (string greetingLine, AudioClip greetingClip) = targetNpc.GetSituationalLineAndAudio(NPCAvailabilityState.Greeting);
            if (string.IsNullOrWhiteSpace(greetingLine))
            {
                (greetingLine, greetingClip) = targetNpc.GetSituationalLineAndAudio(NPCAvailabilityState.Available);
            }
            if (string.IsNullOrWhiteSpace(greetingLine))
            {
                greetingLine = "Hello there.";
            }

            activeDialogueUI.AddDialogueLine(targetNpc.EntityName, greetingLine);
            _conversationHistory.Add($"{targetNpc.EntityName}: {greetingLine}");

            targetNpc.TriggerContextBasedDialogue(NPCAvailabilityState.Greeting);

            OnConversationStarted?.Invoke(targetNpc);
            Debug.Log($"[iTalkManager] Started conversation with {targetNpc.EntityName}");
        }

        private void EndConversationInternal(iTalk targetNpc)
        {
            (string goodbyeLine, AudioClip goodbyeClip) = targetNpc.GetSituationalLineAndAudio(NPCAvailabilityState.Goodbye);
            if (!string.IsNullOrWhiteSpace(goodbyeLine))
            {
                activeDialogueUI.AddDialogueLine(targetNpc.EntityName, goodbyeLine);
                _conversationHistory.Add($"{targetNpc.EntityName}: {goodbyeLine}");
                targetNpc.TriggerContextBasedDialogue(NPCAvailabilityState.Goodbye);
            }

            activeDialogueUI.Hide();
            _activeConversationTarget = null;

            OnConversationEnded?.Invoke(targetNpc);
            Debug.Log($"[iTalkManager] Ended conversation with {targetNpc.EntityName}");
        }

        private void HandlePlayerInputFromUI(string userInput)
        {
            if (_activeConversationTarget == null || string.IsNullOrWhiteSpace(userInput))
                return;

            _conversationHistory.Add($"Player: {userInput}");
            if (_conversationHistory.Count > 10)
            {
                _conversationHistory.RemoveAt(0);
            }

            OnPlayerResponseReceived?.Invoke(userInput);
            RequestAIResponse(userInput);
        }

        private void HandleSendButtonClicked()
        {
            // Optional custom logic if needed
        }

        private void RequestAIResponse(string userInput)
        {
            if (_activeConversationTarget?.assignedPersona == null)
            {
                Debug.LogError("[iTalkManager] Cannot request AI response: No active conversation or persona.");
                return;
            }

            activeDialogueUI.ShowTemporaryMessage("...", 0.5f);

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

            activeDialogueUI.HideTemporaryMessage();
            activeDialogueUI.AddDialogueLine(_activeConversationTarget.EntityName, aiResponse);
            _conversationHistory.Add($"{_activeConversationTarget.EntityName}: {aiResponse}");

            iTalkUtilities.RequestDialogueTTS(_activeConversationTarget, aiResponse);
        }

        private void OnAIResponseError(string errorMessage)
        {
            activeDialogueUI.HideTemporaryMessage();
            activeDialogueUI.ShowTemporaryMessage($"AI Error: {errorMessage}", 5f);
            Debug.LogError($"[iTalkManager] AI Response Error: {errorMessage}");

            if (_activeConversationTarget != null)
            {
                var (fallbackLine, fallbackClip) = iTalkUtilities.GetFallbackDialogue(
                    _activeConversationTarget,
                    NPCAvailabilityState.Available
                );

                if (!string.IsNullOrWhiteSpace(fallbackLine))
                {
                    activeDialogueUI.AddDialogueLine(_activeConversationTarget.EntityName, fallbackLine);
                    _conversationHistory.Add($"{_activeConversationTarget.EntityName}: {fallbackLine}");
                    _activeConversationTarget.TriggerContextBasedDialogue(NPCAvailabilityState.Available);
                }
            }
        }

        public void ShowTemporaryMessage(string message, float duration)
        {
            activeDialogueUI?.ShowTemporaryMessage(message, duration);
        }

        public iTalk GetActiveConversationTarget() => _activeConversationTarget;
        public bool IsInConversation() => _activeConversationTarget != null;
        public int GetQueueLength() => _interactionQueue.Count;
        public List<string> GetConversationHistory() => new List<string>(_conversationHistory);
        #endregion

#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(iTalkManager))]
        public class iTalkManagerEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                iTalkManager manager = (iTalkManager)target;
                if (manager.interactableLayer.value == 0)
                {
                    UnityEditor.EditorGUILayout.HelpBox("Interactable Layer is not set! NPCs won't be detected.", UnityEditor.MessageType.Warning);
                }
                UnityEditor.EditorGUILayout.LabelField("Interactable NPCs", UnityEditor.EditorStyles.boldLabel);
                var interactables = manager.GetCurrentlyInteractableNPCs();
                if (interactables.Count > 0)
                {
                    foreach (var npc in interactables)
                    {
                        UnityEditor.EditorGUILayout.LabelField($"- {npc.EntityName} (State: {npc.GetInternalAvailability()})");
                    }
                }
                else
                {
                    UnityEditor.EditorGUILayout.LabelField("No interactable NPCs detected.");
                }
                if (Application.isPlaying) UnityEditor.EditorUtility.SetDirty(manager);
            }
        }
#endif
    }
}