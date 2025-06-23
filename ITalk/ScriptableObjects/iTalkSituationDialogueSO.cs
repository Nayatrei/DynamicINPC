using UnityEngine;
using System.Collections.Generic;
using Project.Tools.DictionaryHelp;

namespace CelestialCyclesSystem
{
    // NPCAvailabilityState enum is crucial for iTalk, iTalkNPCPersona, and iTalkController.
    // It defines the different states an NPC can be in regarding their availability for dialogue.
    public enum NPCAvailabilityState
    {
        Greeting, // Initial greeting state when the player approaches
        Available,  // NPC is free to talk
        Busy,       // NPC is occupied with a task but might give a short response
        Sleeping,   // NPC is asleep
        Working,    // NPC is engaged in work
        InCutscene, // NPC is part of a cutscene or scripted event
        Other,    // Any other state not covered by the above
        Goodbye    // Final goodbye state when the player is leaving
    }

    [System.Serializable]
    public struct DialogueLine
    {
        [TextArea(2, 5)] public string text;
        public AudioClip audio;
    }

    [System.Serializable]
    public class iTalkSituationDialogueBundle
    {
        public SerializableDictionary<NPCAvailabilityState, List<DialogueLine>> dialogues = new SerializableDictionary<NPCAvailabilityState, List<DialogueLine>>();

      public iTalkSituationDialogueBundle()
    {
        // Initialize the dialogues dictionary to prevent null errors.
        foreach (NPCAvailabilityState state in System.Enum.GetValues(typeof(NPCAvailabilityState)))
        {
            if (dialogues == null)
            {
                dialogues = new SerializableDictionary<NPCAvailabilityState, List<DialogueLine>>();
            }
            dialogues[state] = new List<DialogueLine>();
        }
        // The logic that set default counts in the nested dictionary has been removed.
    }
    }

    [CreateAssetMenu(fileName = "NewSituationDialogueSet", menuName = "Celestial Cycle/iTalk/Situation Dialogue Set")]
    public class iTalkSituationDialogueSO : ScriptableObject
    {
        [Header("Desired Line Counts (for AI Generation)")]
        [Tooltip("Desired number of dialogue lines for each state. Use the 'Sync' button below to apply.")]
        public SerializableDictionary<NPCAvailabilityState, int> desiredLineCounts = new SerializableDictionary<NPCAvailabilityState, int>();

        [Header("Dialogue Lines")]
        public iTalkSituationDialogueBundle dialogues = new iTalkSituationDialogueBundle();
        [TextArea(3,6)] public string voiceInstructionPrompt;
        private void Reset()
        {
            if (desiredLineCounts == null)
                desiredLineCounts = new SerializableDictionary<NPCAvailabilityState, int>();

            if (dialogues == null)
                dialogues = new iTalkSituationDialogueBundle();
        }
    }
}
