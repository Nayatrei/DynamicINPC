// Filename: iTalkDialogueStateSO.cs
using UnityEngine;
using UnityEngine.Events;

namespace CelestialCyclesSystem
{
    [CreateAssetMenu(menuName = "Celestial Cycle/iTalk/Dialogue State Channel")]
    public class iTalkDialogueStateSO : ScriptableObject
    {
        /// <summary>
        /// Action that broadcasts the iTalk component of the involved NPC and whether the conversation is starting (true) or ending (false).
        /// Any script can listen to this to react to conversation state changes.
        /// </summary>
        public UnityAction<iTalk, bool> OnEventRaised;

        /// <summary>
        /// Called by iTalkController to broadcast that a conversation state has changed.
        /// </summary>
        /// <param name="talkInstance">The iTalk component of the NPC starting or ending the conversation.</param>
        /// <param name="isStarting">True if the conversation is beginning, false if it's ending.</param>
        public void Raise(iTalk talkInstance, bool isStarting)
        {
            OnEventRaised?.Invoke(talkInstance, isStarting);
        }
    }
}