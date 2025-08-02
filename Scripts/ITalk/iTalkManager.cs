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
    /// </summary>
    ///   
    public interface ITalkManager
    {
        void RegisteriTalk(iTalk talkComponent);
        void UnregisteriTalk(iTalk talkComponent);
        void RegisterController(iTalkPlayerDialogueCoordinator controller);
        void UnregisterController(iTalkPlayerDialogueCoordinator controller);
        bool IsNPCInteractable(iTalk npc, Vector3 playerPosition, out string reason);
        bool TryStartPlayerConversation(iTalk npc);
        void RequestDialogueFromAI(iTalk speakeriTalk, string userInput, string conversationHistory, Action<string> onSuccess, Action<string> onError);
        void RequestTTS(iTalkNPCPersona speakerPersona, string textToSpeak, AudioSource targetAudioSource);
        iTalkApiConfigSO GetApiConfig();
        List<iTalkNewsItemEntry> GetWorldNews();
        string GetCurrentGlobalSituation();
    }
    public class iTalkManager : MonoBehaviour, ITalkManager
    {
        public static iTalkManager Instance { get; private set; }

        [Header("API Configuration")]
        [SerializeField] private iTalkApiConfigSO apiConfig;

        [Header("System Channels")]
        [SerializeField] private iTalkDialogueStateSO dialogueStateChannel;

        [Header("World Context")]
        [SerializeField] private string currentGlobalSituation = "A typical day in the realm.";
        [SerializeField] private List<iTalkNewsItemEntry> worldNews = new List<iTalkNewsItemEntry>();
        private const int MAX_NEWS_IN_PROMPT = 5;

        [Header("Interaction Settings")]
        public float maxInteractionDistance = 5.0f;
        public LayerMask interactableLayer; // MODIFIED: Replaced interval timer with a LayerMask

        [Header("Token Management")]
        public float estimatedTokensPerCharPrompt = 0.3f;
        public float estimatedTokensPerCharResponse = 0.4f;
        public int maxTokensPerRequestSafety = 1000;
        public int tokenQuotaPeriod = 100000;
        [SerializeField] private int currentTokenUsage = 0;

        // Registration management - core responsibility
        private readonly List<iTalk> registeredTalkComponents = new List<iTalk>();
        private readonly List<iTalkPlayerDialogueCoordinator> registeredControllers = new List<iTalkPlayerDialogueCoordinator>();
        private readonly List<iTalk> currentlyInteractableNPCs = new List<iTalk>();

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
        }

        void Start()
        {
            // Find SubManager for NPC conversation delegation
            npcDialogueCoordinator = FindObjectOfType<iTalkNPCDialogueCoordinator>();
            if (npcDialogueCoordinator == null)
            {
                Debug.LogWarning("[iTalkManager] iTalkNPCDialogueCoordinator not found. NPC-to-NPC conversation interruptions may not work.");
            }
        }

        // MODIFIED: Update is now simpler and calls the checking method every frame.
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
            // Sort by latest timestamp
            worldNews = worldNews
                .OrderByDescending(entry => entry.timestamps.Count > 0 ? entry.timestamps.Max() : 0)
                .ToList();
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

        public void RegisterController(iTalkPlayerDialogueCoordinator controller)
        {
            if (controller != null && !registeredControllers.Contains(controller))
                registeredControllers.Add(controller);
        }

        public void UnregisterController(iTalkPlayerDialogueCoordinator controller)
        {
            if (controller != null) registeredControllers.Remove(controller);
        }
        #endregion

        #region Interactable NPCs Management
        // MODIFIED: This method now uses Physics.OverlapSphere for high performance.
        public void UpdateInteractableNPCsList()
        {
            Transform primaryPlayerTransform = GetPrimaryPlayerTransform();
            if (primaryPlayerTransform == null)
            {
                if (currentlyInteractableNPCs.Count > 0)
                {
                    currentlyInteractableNPCs.Clear();
                    OnInteractableNPCsChanged?.Invoke(currentlyInteractableNPCs.AsReadOnly());
                }
                return;
            }

            // Find all nearby colliders on the specified layer.
            Collider[] hitColliders = Physics.OverlapSphere(primaryPlayerTransform.position, maxInteractionDistance, interactableLayer);

            // Create a new list from the results.
            List<iTalk> newInteractableNPCs = new List<iTalk>();
            foreach (var hitCollider in hitColliders)
            {
                // Get the iTalk component and check if it's available for dialogue.
                if (hitCollider.TryGetComponent<iTalk>(out var npc) && npc.IsInternallyAvailableForDialogue())
                {
                    newInteractableNPCs.Add(npc);
                }
            }

            // Check if the list of interactable NPCs has actually changed before invoking the event.
            if (newInteractableNPCs.Count != currentlyInteractableNPCs.Count || !newInteractableNPCs.All(currentlyInteractableNPCs.Contains))
            {
                currentlyInteractableNPCs.Clear();
                currentlyInteractableNPCs.AddRange(newInteractableNPCs);
                OnInteractableNPCsChanged?.Invoke(currentlyInteractableNPCs.AsReadOnly());
            }
        }

        private Transform GetPrimaryPlayerTransform()
        {
            if (registeredControllers.Count > 0 && registeredControllers[0] != null)
            {
                return registeredControllers[0].playerTransform ?? registeredControllers[0].transform;
            }
            return null;
        }

        public IReadOnlyList<iTalk> GetCurrentlyInteractableNPCs() => currentlyInteractableNPCs.AsReadOnly();

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

        #region Player Conversation Support (Delegation to npcDialogueCoordinator)
        /// <summary>
        /// Attempts to start a player conversation with an NPC, handling interruption of NPC-to-NPC conversations
        /// Delegates NPC conversation interruption to iTalkNPCDialogueCoordinator
        /// </summary>
        public bool TryStartPlayerConversation(iTalk npc)
        {
            if (npc == null) return false;

            // Delegate NPC conversation interruption to SubManager
            if (npcDialogueCoordinator != null)
            {
                bool wasInterrupted = npcDialogueCoordinator.TryInterruptNPCConversationForPlayer(npc);
                if (wasInterrupted)
                {
                    Debug.Log($"[iTalkManager] Interrupted NPC conversation for player interaction with {npc.EntityName}");
                }
            }

            // Player conversations don't need conversation objects - handled by iTalkPlayerDialogueCoordinator
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
            worldNews = worldNews
                .OrderByDescending(entry => entry.timestamps.Count > 0 ? entry.timestamps.Max() : 0)
                .ToList();
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
        /// <summary>
        /// Requests AI dialogue response for player-NPC conversations
        /// Delegates to iTalkUtilities for centralized processing
        /// </summary>
        public void RequestDialogueFromAI(iTalk speakeriTalk, string userInput, string conversationHistory,
            Action<string> onSuccess, Action<string> onError)
        {
            // Validate inputs
            if (speakeriTalk?.assignedPersona == null)
            {
                onError?.Invoke("Speaker NPC or persona is null.");
                return;
            }

            if (isApiRequestInProgress)
            {
                onError?.Invoke("An AI request is already in progress. Please wait.");
                return;
            }

            // Set request in progress to prevent multiple simultaneous requests
            isApiRequestInProgress = true;

            // Delegate to iTalkUtilities for centralized LLM handling
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

        /// <summary>
        /// Requests TTS for dialogue responses
        /// Delegates to iTalkUtilities for centralized TTS handling
        /// </summary>
        public void RequestTTS(iTalkNPCPersona speakerPersona, string textToSpeak, AudioSource targetAudioSource)
        {
            // Delegate to iTalkUtilities for centralized TTS handling
            iTalkUtilities.RequestTTS(speakerPersona, textToSpeak, targetAudioSource);
        }
        #endregion

        #region Public Accessors
        public iTalkApiConfigSO GetApiConfig() => apiConfig;
        public iTalkDialogueStateSO GetDialogueStateChannel() => dialogueStateChannel;
        public List<iTalk> GetRegisteredTalkComponents() => new List<iTalk>(registeredTalkComponents);
        public List<iTalkPlayerDialogueCoordinator> GetRegisteredControllers() => new List<iTalkPlayerDialogueCoordinator>(registeredControllers);

        public void CheckRegistrationConsistency()
        {
            int nullTalks = registeredTalkComponents.RemoveAll(item => item == null);
            int nullControllers = registeredControllers.RemoveAll(item => item == null);
            if (nullTalks > 0 || nullControllers > 0)
                Debug.LogWarning($"[iTalkManager] Cleaned up {nullTalks} null iTalk and {nullControllers} null controller references.");
        }

        /// <summary>
        /// Sets reference to NPCDialogueCoordinator for NPC conversation delegation
        /// </summary>
        public void SetNPCDialogueCoordinator(iTalkNPCDialogueCoordinator manager)
        {
            npcDialogueCoordinator = manager;
        }
        #endregion
    }
}