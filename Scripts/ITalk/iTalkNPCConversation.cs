using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace CelestialCyclesSystem
{
    /// <summary>
    /// Represents an active NPC-to-NPC conversation with participant tracking and lifecycle management.
    /// This component is dynamically added to a GameObject when a conversation starts.
    /// </summary>
    [System.Serializable]
    public class iTalkNPCConversation : MonoBehaviour
    {
        [SerializeField] private List<iTalk> participants = new List<iTalk>();
        [SerializeField] private iTalkSubManager parentManager;
        [SerializeField] private float conversationStartTime;
        [SerializeField] private bool isActive = false;

        /// <summary>
        /// Initialize the conversation with participants and parent manager.
        /// </summary>
        public void Initialize(List<iTalk> conversationParticipants, iTalkSubManager manager)
        {
            participants = new List<iTalk>(conversationParticipants);
            parentManager = manager;
            conversationStartTime = Time.time;
            isActive = true;
        }

        /// <summary>
        /// Get a read-only list of conversation participants.
        /// </summary>
        public List<iTalk> GetParticipants()
        {
            return new List<iTalk>(participants);
        }

        /// <summary>
        /// End the conversation due to player interruption.
        /// </summary>
        public void EndByPlayerInterruption()
        {
            if (!isActive) return;
            isActive = false;
            if (parentManager != null) parentManager.EndNPCConversation(this);
        }

        /// <summary>
        /// End the conversation naturally.
        /// </summary>
        public void EndNaturally()
        {
            if (!isActive) return;
            isActive = false;
            if (parentManager != null) parentManager.EndNPCConversation(this);
        }

        /// <summary>
        /// Check if the conversation is still active.
        /// </summary>
        public bool IsActive() => isActive;

        /// <summary>
        /// Get the duration of the conversation in seconds.
        /// </summary>
        public float GetDuration() => Time.time - conversationStartTime;

        /// <summary>
        /// Check if a specific NPC is participating in this conversation.
        /// </summary>
        public bool HasParticipant(iTalk npc) => participants.Contains(npc);

        /// <summary>
        /// Remove a participant from the conversation (e.g., if they get interrupted by the player).
        /// </summary>
        public void RemoveParticipant(iTalk npc)
        {
            if (participants.Remove(npc) && participants.Count < 2)
            {
                // If removing a participant leaves fewer than two, the conversation ends.
                EndNaturally();
            }
        }
    }
}