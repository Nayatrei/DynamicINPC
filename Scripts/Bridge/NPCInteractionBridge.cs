using UnityEngine;

namespace CelestialCyclesSystem
{
    /// <summary>
    /// This script acts as a bridge between the iTalk dialogue system and the INPC movement/action system.
    /// It listens for conversation events from BOTH the iTalkPlayerCoordinator (player) and iTalkSubManager (NPC-to-NPC)
    /// and translates them into Engage/Disengage commands for the INPCBase on the same GameObject.
    /// This component should be placed on the same NPC prefab that has INPCBase and iTalk components.
    /// </summary>
    [RequireComponent(typeof(INPCBase))]
    [RequireComponent(typeof(iTalk))]
    public class NPCInteractionBridge : MonoBehaviour
    {
        // References to the core components on this NPC
        private INPCBase _npcBase;
        private iTalk _iTalk;

        // References to the scene's iTalk managers
        private iTalkPlayerDialogueCoordinator _iTalkPlayerCoordinator;
        private iTalkNPCDialogueCoordinator _iTalkNPCCoordinator;

        void Awake()
        {
            // Get the components on this same GameObject
            _npcBase = GetComponent<INPCBase>();
            _iTalk = GetComponent<iTalk>();
        }

        void Start()
        {
            // --- Find and Subscribe to Player Conversation Events ---
            _iTalkPlayerCoordinator = FindObjectOfType<iTalkPlayerDialogueCoordinator>();
            if (_iTalkPlayerCoordinator != null)
            {
                _iTalkPlayerCoordinator.OnConversationStarted += HandlePlayerConversationStarted;
                _iTalkPlayerCoordinator.OnConversationEnded += HandlePlayerConversationEnded;
            }
            else
            {
                Debug.LogWarning($"[NPCInteractionBridge] No _iTalkPlayerCoordinator found. Player interactions may not trigger INPC actions on '{gameObject.name}'.", this);
            }

            // --- Find and Subscribe to NPC-to-NPC Conversation Events ---
            _iTalkNPCCoordinator = FindObjectOfType<iTalkNPCDialogueCoordinator>();
            if (_iTalkNPCCoordinator != null)
            {
                _iTalkNPCCoordinator.OnNPCConversationStarted += HandleNPCConversationStarted;
                _iTalkNPCCoordinator.OnNPCConversationEnded += HandleNPCConversationEnded;
            }
            else
            {
                Debug.LogWarning($"[NPCInteractionBridge] No iTalkNPCDialogueCoordinator found. NPC-to-NPC interactions may not trigger INPC actions on '{gameObject.name}'.", this);
            }
        }

        void OnDestroy()
        {
            // IMPORTANT: Always unsubscribe from events to prevent errors.
            if (_iTalkPlayerCoordinator != null)
            {
                _iTalkPlayerCoordinator.OnConversationStarted -= HandlePlayerConversationStarted;
                _iTalkPlayerCoordinator.OnConversationEnded -= HandlePlayerConversationEnded;
            }
            if (_iTalkNPCCoordinator != null)
            {
                _iTalkNPCCoordinator.OnNPCConversationStarted -= HandleNPCConversationStarted;
                _iTalkNPCCoordinator.OnNPCConversationEnded -= HandleNPCConversationEnded;
            }
        }

        #region Player Conversation Handlers

        private void HandlePlayerConversationStarted(iTalk involvedNPC)
        {
            if (involvedNPC == _iTalk && _iTalkPlayerCoordinator != null)
            {
                _npcBase.EngageInteraction(_iTalkPlayerCoordinator.playerTransform, INPCAction.Talking);
            }
        }

        private void HandlePlayerConversationEnded(iTalk involvedNPC)
        {
            if (involvedNPC == _iTalk)
            {
                _npcBase.DisengageInteraction();
            }
        }

        #endregion

        #region NPC-to-NPC Conversation Handlers

        private void HandleNPCConversationStarted(iTalk npc1, iTalk npc2)
        {
            // Check if this NPC is one of the two participants.
            if (_iTalk == npc1)
            {
                // If I am npc1, I need to face npc2.
                _npcBase.EngageInteraction(npc2.transform, INPCAction.Talking);
            }
            else if (_iTalk == npc2)
            {
                // If I am npc2, I need to face npc1.
                _npcBase.EngageInteraction(npc1.transform, INPCAction.Talking);
            }
        }

        private void HandleNPCConversationEnded(iTalk npc1, iTalk npc2)
        {
            // If this NPC was part of the conversation that just ended, disengage.
            if (_iTalk == npc1 || _iTalk == npc2)
            {
                _npcBase.DisengageInteraction();
            }
        }

        #endregion
    }
}
