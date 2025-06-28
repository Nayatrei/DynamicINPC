using UnityEngine;

namespace CelestialCyclesSystem
{
    /// <summary>
    /// This script acts as a bridge between the iTalk dialogue system and the INPC movement/action system.
    /// It listens for conversation events from the iTalkController and translates them into
    /// Engage/Disengage commands for the INPCBase on the same GameObject.
    /// This component should be placed on the same NPC prefab that has INPCBase and iTalk components.
    /// </summary>
    [RequireComponent(typeof(INPCBase))]
    [RequireComponent(typeof(iTalk))]
    public class NPCInteractionBridge : MonoBehaviour
    {
        // References to the core components on this NPC
        private INPCBase _npcBase;
        private iTalk _iTalk;

        // Reference to the scene's player controller for dialogue
        private iTalkController _iTalkController;

        void Awake()
        {
            // Get the components on this same GameObject
            _npcBase = GetComponent<INPCBase>();
            _iTalk = GetComponent<iTalk>();
        }

        void Start()
        {
            // Find the iTalkController in the scene. 
            // This assumes you have one active iTalkController.
            _iTalkController = FindObjectOfType<iTalkController>();

            if (_iTalkController != null)
            {
                // Subscribe to the conversation events.
                // When a conversation starts or ends anywhere, our methods will be called.
                _iTalkController.OnConversationStarted += HandleConversationStarted;
                _iTalkController.OnConversationEnded += HandleConversationEnded;
            }
            else
            {
                Debug.LogError($"[NPCInteractionBridge] No iTalkController found in the scene! The bridge on '{gameObject.name}' will not function.", this);
            }
        }

        void OnDestroy()
        {
            // IMPORTANT: Always unsubscribe from events when this object is destroyed to prevent errors.
            if (_iTalkController != null)
            {
                _iTalkController.OnConversationStarted -= HandleConversationStarted;
                _iTalkController.OnConversationEnded -= HandleConversationEnded;
            }
        }

        /// <summary>
        /// This method is called by the iTalkController's event whenever any conversation starts.
        /// </summary>
        /// <param name="involvedNPC">The iTalk component of the NPC that started the conversation.</param>
        private void HandleConversationStarted(iTalk involvedNPC)
        {
            // We only care if the conversation involves THIS specific NPC.
            if (involvedNPC == _iTalk)
            {
                // Tell our INPCBase to engage, freezing its movement and setting its animation to Talking.
                if (_iTalkController != null)
                {
                    _npcBase.EngageInteraction(_iTalkController.transform, INPCAction.Talking);
                    Debug.Log($"[NPCInteractionBridge] {gameObject.name} is engaging in conversation.", this);
                }
            }
        }

        /// <summary>
        /// This method is called by the iTalkController's event whenever any conversation ends.
        /// </summary>
        /// <param name="involvedNPC">The iTalk component of the NPC that ended the conversation.</param>
        private void HandleConversationEnded(iTalk involvedNPC)
        {
            // We only care if the conversation that ended involved THIS specific NPC.
            if (involvedNPC == _iTalk)
            {
                // Tell our INPCBase to disengage, allowing it to resume its normal activities.
                _npcBase.DisengageInteraction();
                Debug.Log($"[NPCInteractionBridge] {gameObject.name} is disengaging from conversation.", this);
            }
        }
    }
}
