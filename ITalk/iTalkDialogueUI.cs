// Filename: iTalkDialogueUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.EventSystems;

namespace CelestialCyclesSystem
{
    /// <summary>
    /// Displays dialogue output and conversation history through a user interface,
    /// supports both JRPG and MMO styles, and offers expandability for choice-based dialogue interactions.
    /// </summary>
    public class iTalkDialogueUI : MonoBehaviour
    {
        public enum InternalDialogueMode { Undetermined, MMORPG, JRPG }
        public enum InternalJRPGState { Idle, ShowingNPCText, ShowingPlayerInput }

        [Header("UI Configuration")]
        public InternalDialogueMode _currentUIMode = InternalDialogueMode.Undetermined;
        [SerializeField] private InternalJRPGState _jrpgDisplayState = InternalJRPGState.Idle;

        [Header("Core UI Element References")]
        public TextMeshProUGUI npcNameText;
        public TextMeshProUGUI dialogueHistoryText; // MMO: History; JRPG: Current NPC Line
        public TMP_InputField userInputField;
        public Button sendButton;
        public Image npcPortraitImage;
        public GameObject dialoguePanelRoot;

        [Header("MMORPG Style Specific")]
        [Tooltip("Assign for scrolling dialogue history (MMORPG style).")]
        public ScrollRect historyScrollRect;

        [Header("JRPG Style Specific Panel GameObjects")]
        [Tooltip("Parent GameObject for NPC's dialogue display elements in JRPG mode.")]
        public GameObject jrpgNpcDialoguePanelRoot;
        [Tooltip("Button to advance dialogue/switch to player input in JRPG mode.")]
        public Button jrpgAdvanceButton;
        [Tooltip("Parent GameObject for Player's input elements in JRPG mode.")]
        public GameObject jrpgPlayerInputPanelRoot;

        [Header("Events")]
        [HideInInspector] public UnityEvent<string> OnPlayerInputSubmitted = new UnityEvent<string>();
        [HideInInspector] public UnityEvent OnSendButtonClicked = new UnityEvent();
        [HideInInspector] public UnityEvent OnCloseDialogueClicked = new UnityEvent();

        // JRPG state-specific events
        public UnityEvent stateIdle;
        public UnityEvent stateNPCTalk;
        public UnityEvent statePlayerInput;

        [Header("Feedback Elements")]
        public TextMeshProUGUI temporaryMessageText;
        public float defaultTemporaryMessageDuration = 2.0f;

        // Private state management
        private List<string> currentDisplayLines = new List<string>();
        private const int MAX_UI_HISTORY_LINES = 15;
        private Coroutine temporaryMessageCoroutine;

        // Centralized JRPG state property with event triggering
        public InternalJRPGState JRPGDisplayState
        {
            get => _jrpgDisplayState;
            set
            {
                if (_jrpgDisplayState != value)
                {
                    _jrpgDisplayState = value;
                    OnJRPGDisplayStateChanged(value);
                }
            }
        }

        void Awake()
        {
            DetermineUIMode();
            SetupEventListeners();
            InitializeUIState();
        }

        void Update()
        {
            HandleKeyboardInput();
        }

        #region Initialization
        private void DetermineUIMode()
        {
            // Determine UI mode based on assigned components
            if (historyScrollRect != null && dialogueHistoryText != null)
            {
                _currentUIMode = InternalDialogueMode.MMORPG;
            }
            else if (dialogueHistoryText != null && userInputField != null &&
                     jrpgNpcDialoguePanelRoot != null && jrpgPlayerInputPanelRoot != null && jrpgAdvanceButton != null)
            {
                _currentUIMode = InternalDialogueMode.JRPG;
            }
            else
            {
                _currentUIMode = InternalDialogueMode.Undetermined;
                Debug.LogError("[iTalkDialogueUI] UI elements not configured correctly for either MMORPG or JRPG mode.", this);
            }
        }

        private void SetupEventListeners()
        {
            if (sendButton) sendButton.onClick.AddListener(HandleSendButtonClick);
            if (userInputField) userInputField.onSubmit.AddListener(HandleInputSubmission);
            if (jrpgAdvanceButton) jrpgAdvanceButton.onClick.AddListener(HandleJRPGAdvanceButtonClick);
        }

        private void InitializeUIState()
        {
            if (_currentUIMode == InternalDialogueMode.Undetermined)
            {
                if (dialoguePanelRoot) dialoguePanelRoot.SetActive(false);
                return;
            }

            // Set initial panel visibility based on mode
            if (_currentUIMode == InternalDialogueMode.MMORPG)
            {
                SetPanelVisibility(jrpgNpcDialoguePanelRoot, false);
                SetPanelVisibility(jrpgPlayerInputPanelRoot, false);
            }
            else if (_currentUIMode == InternalDialogueMode.JRPG)
            {
                if (historyScrollRect) historyScrollRect.gameObject.SetActive(false);
                SetPanelVisibility(jrpgNpcDialoguePanelRoot, false);
                SetPanelVisibility(jrpgPlayerInputPanelRoot, false);
                JRPGDisplayState = InternalJRPGState.Idle;
            }
        }
        #endregion

        #region Input Handling (Streamlined)
        private void HandleKeyboardInput()
        {
            if (!dialoguePanelRoot || !dialoguePanelRoot.activeSelf || _currentUIMode == InternalDialogueMode.Undetermined)
                return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Avoid double processing if input field is already focused
                if (EventSystem.current?.currentSelectedGameObject == userInputField?.gameObject)
                    return;

                if (_currentUIMode == InternalDialogueMode.MMORPG)
                {
                    FocusInputField();
                }
                else if (_currentUIMode == InternalDialogueMode.JRPG)
                {
                    HandleJRPGKeyboardInput();
                }
            }
        }

        private void HandleJRPGKeyboardInput()
        {
            if (JRPGDisplayState == InternalJRPGState.ShowingNPCText)
            {
                if (jrpgAdvanceButton?.gameObject.activeInHierarchy == true && jrpgAdvanceButton.interactable)
                {
                    HandleJRPGAdvanceButtonClick();
                }
            }
            else if (JRPGDisplayState == InternalJRPGState.ShowingPlayerInput)
            {
                FocusInputField();
            }
        }

        private void HandleInputSubmission(string text)
        {
            if (!userInputField?.interactable == true) return;

            if (!string.IsNullOrWhiteSpace(text))
            {
                OnPlayerInputSubmitted.Invoke(text);
                
                if (_currentUIMode == InternalDialogueMode.JRPG)
                {
                    // Transition JRPG panels after successful submission
                    SetPanelVisibility(jrpgPlayerInputPanelRoot, false);
                    SetPanelVisibility(jrpgNpcDialoguePanelRoot, true);
                }
                
                ClearInputField();
            }
            else if (_currentUIMode == InternalDialogueMode.MMORPG)
            {
                OnSendButtonClicked.Invoke();
            }
        }

        private void HandleSendButtonClick()
        {
            if (sendButton?.interactable == true)
            {
                HandleInputSubmission(GetCurrentInput());
            }
        }

        private void HandleJRPGAdvanceButtonClick()
        {
            if (_currentUIMode != InternalDialogueMode.JRPG || JRPGDisplayState != InternalJRPGState.ShowingNPCText)
                return;

            SetPanelVisibility(jrpgNpcDialoguePanelRoot, false);
            JRPGDisplayState = InternalJRPGState.ShowingPlayerInput;
        }
        #endregion

        #region Public Interface
        public void Show(string characterName = null)
        {
            if (dialoguePanelRoot) dialoguePanelRoot.SetActive(true);
            HideTemporaryMessage();

            if (_currentUIMode == InternalDialogueMode.MMORPG)
            {
                ShowMMORPGElements();
                SetInputActive(true);
            }
            else if (_currentUIMode == InternalDialogueMode.JRPG)
            {
                JRPGDisplayState = InternalJRPGState.ShowingNPCText;
            }
        }

        public void Hide()
        {
            if (dialoguePanelRoot) dialoguePanelRoot.SetActive(false);
            if (_currentUIMode == InternalDialogueMode.JRPG)
            {
                JRPGDisplayState = InternalJRPGState.Idle;
            }
            ClearInputField();
        }

        public void SetNPCName(string name)
        {
            if (npcNameText) npcNameText.text = name;
        }

        public void SetNPCPortrait(Sprite portrait)
        {
            if (npcPortraitImage)
            {
                npcPortraitImage.sprite = portrait;
                npcPortraitImage.gameObject.SetActive(portrait != null);
            }
        }

        public void AddDialogueLine(string speaker, string line)
        {
            HideTemporaryMessage();

            if (_currentUIMode == InternalDialogueMode.MMORPG)
            {
                AddToMMORPGHistory(speaker, line);
            }
            else if (_currentUIMode == InternalDialogueMode.JRPG)
            {
                AddToJRPGDisplay(speaker, line);
            }
        }

        public void ClearHistory()
        {
            if (_currentUIMode == InternalDialogueMode.MMORPG)
            {
                currentDisplayLines.Clear();
                if (dialogueHistoryText) dialogueHistoryText.text = "";
            }
            else if (_currentUIMode == InternalDialogueMode.JRPG)
            {
                if (dialogueHistoryText) dialogueHistoryText.text = "";
            }
        }

        public string GetCurrentInput() => userInputField ? userInputField.text : "";
        public IReadOnlyList<string> GetHistoryLines() => currentDisplayLines.AsReadOnly();
        #endregion

        #region Input Field Management (Centralized)
        public void SetInputActive(bool isActive)
        {
            if (userInputField) userInputField.interactable = isActive;

            // Send button logic based on mode and state
            if (sendButton)
            {
                if (_currentUIMode == InternalDialogueMode.MMORPG)
                {
                    sendButton.interactable = isActive;
                }
                else if (_currentUIMode == InternalDialogueMode.JRPG)
                {
                    sendButton.interactable = isActive && IsJRPGPlayerInputVisible();
                }
            }

            // Focus input field if activating and appropriate
            if (isActive && ShouldFocusInputField())
            {
                StartCoroutine(FocusInputFieldDelayed());
            }
            else if (!isActive && EventSystem.current?.currentSelectedGameObject == userInputField?.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void ClearInputField()
        {
            if (userInputField)
            {
                userInputField.text = "";
                
                // Re-focus for MMORPG mode after clearing
                if (_currentUIMode == InternalDialogueMode.MMORPG && 
                    userInputField.interactable && 
                    dialoguePanelRoot?.activeSelf == true)
                {
                    StartCoroutine(FocusInputFieldDelayed());
                }
            }
        }

        private void FocusInputField()
        {
            if (userInputField?.interactable == true && userInputField.gameObject.activeInHierarchy)
            {
                StartCoroutine(FocusInputFieldDelayed());
            }
        }

        private IEnumerator FocusInputFieldDelayed()
        {
            yield return null; // Wait one frame for UI updates
            
            if (userInputField?.interactable == true && userInputField.gameObject.activeInHierarchy)
            {
                userInputField.ActivateInputField();
                userInputField.Select();
                EventSystem.current?.SetSelectedGameObject(userInputField.gameObject);
            }
        }

        private bool ShouldFocusInputField()
        {
            if (!userInputField?.gameObject.activeInHierarchy == true) return false;
            if (EventSystem.current?.currentSelectedGameObject == userInputField.gameObject) return false;

            return _currentUIMode switch
            {
                InternalDialogueMode.MMORPG => true,
                InternalDialogueMode.JRPG => JRPGDisplayState == InternalJRPGState.ShowingPlayerInput && IsJRPGPlayerInputVisible(),
                _ => false
            };
        }
        #endregion

        #region Temporary Messages
        public void ShowTemporaryMessage(string message, float duration = 0)
        {
            if (!temporaryMessageText) return;
            
            if (duration <= 0) duration = defaultTemporaryMessageDuration;
            
            temporaryMessageText.text = message;
            temporaryMessageText.gameObject.SetActive(true);
            
            if (temporaryMessageCoroutine != null) StopCoroutine(temporaryMessageCoroutine);
            temporaryMessageCoroutine = StartCoroutine(HideTemporaryMessageAfterDelay(duration));
        }

        public void HideTemporaryMessage()
        {
            if (temporaryMessageCoroutine != null) StopCoroutine(temporaryMessageCoroutine);
            if (temporaryMessageText) temporaryMessageText.gameObject.SetActive(false);
            temporaryMessageCoroutine = null;
        }

        private IEnumerator HideTemporaryMessageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideTemporaryMessage();
        }
        #endregion

        #region Mode-Specific Display Logic
        private void AddToMMORPGHistory(string speaker, string line)
        {
            currentDisplayLines.Add($"{speaker}: {line}");
            if (currentDisplayLines.Count > MAX_UI_HISTORY_LINES) 
                currentDisplayLines.RemoveAt(0);
                
            if (dialogueHistoryText) 
                dialogueHistoryText.text = string.Join("\n\n", currentDisplayLines);
                
            ScrollToBottom();
        }

        private void AddToJRPGDisplay(string speaker, string line)
        {
            bool isPlayerLine = speaker.Equals("Player", System.StringComparison.OrdinalIgnoreCase);
            
            if (!isPlayerLine) // NPC or System message
            {
                if (dialogueHistoryText) dialogueHistoryText.text = line;
                JRPGDisplayState = InternalJRPGState.ShowingNPCText;
            }
        }

        private void ShowMMORPGElements()
        {
            SetGameObjectActive(dialogueHistoryText?.gameObject, true);
            SetGameObjectActive(userInputField?.gameObject, true);
            SetGameObjectActive(sendButton?.gameObject, true);
            SetPanelVisibility(jrpgNpcDialoguePanelRoot, false);
            SetPanelVisibility(jrpgPlayerInputPanelRoot, false);
        }

        private void ScrollToBottom()
        {
            if (_currentUIMode == InternalDialogueMode.MMORPG && historyScrollRect?.gameObject.activeInHierarchy == true)
            {
                StartCoroutine(ScrollToBottomDelayed());
            }
        }

        private IEnumerator ScrollToBottomDelayed()
        {
            yield return new WaitForEndOfFrame();
            if (historyScrollRect) historyScrollRect.verticalNormalizedPosition = 0f;
        }
        #endregion

        #region JRPG State Management
        private void OnJRPGDisplayStateChanged(InternalJRPGState newState)
        {
            if (_currentUIMode != InternalDialogueMode.JRPG) return;

            // Update panel visibility based on state
            SetPanelVisibility(jrpgNpcDialoguePanelRoot, newState == InternalJRPGState.ShowingNPCText);
            SetPanelVisibility(jrpgPlayerInputPanelRoot, newState == InternalJRPGState.ShowingPlayerInput);

            // Trigger state-specific events and input management
            switch (newState)
            {
                case InternalJRPGState.Idle:
                    stateIdle?.Invoke();
                    SetInputActive(false);
                    break;
                    
                case InternalJRPGState.ShowingNPCText:
                    stateNPCTalk?.Invoke();
                    SetInputActive(false);
                    break;
                    
                case InternalJRPGState.ShowingPlayerInput:
                    statePlayerInput?.Invoke();
                    SetInputActive(true);
                    break;
            }
        }

        private bool IsJRPGPlayerInputVisible() => jrpgPlayerInputPanelRoot?.activeSelf == true;
        #endregion

        #region Utility Methods
        private void SetPanelVisibility(GameObject panel, bool visible)
        {
            if (panel) panel.SetActive(visible);
        }

        private void SetGameObjectActive(GameObject obj, bool active)
        {
            if (obj) obj.SetActive(active);
        }
        #endregion
    }
}