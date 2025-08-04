using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Project.Tools.DictionaryHelp; // Add this for SerializableDictionary

namespace CelestialCyclesSystem
{
    [CustomEditor(typeof(iTalkSituationDialogueSO))]
    public class iTalkSituationDialogueSOEditor : Editor
    {
        private NPCAvailabilityState? _selectedState = null;
        private Vector2 _leftPanelScrollPosition;
        private Vector2 _rightPanelScrollPosition;


        public override void OnInspectorGUI()
        {
            var so = (iTalkSituationDialogueSO)target;
            serializedObject.Update();


            // Add a TextField for audioPrompt at the top of the inspector

            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            EditorGUILayout.LabelField("Audio Prompts", EditorStyles.boldLabel, GUILayout.Width(160));
            EditorGUILayout.EndVertical();
                       EditorGUILayout.BeginVertical();
            string defaultPrompt =
            @"Affect: 
Tone: 
Emotion: 
Pronunciation: 
Phrasing: 
";
            string promptToShow = string.IsNullOrEmpty(so.voiceInstructionPrompt) ? defaultPrompt : so.voiceInstructionPrompt;
            EditorGUILayout.BeginHorizontal();
            string newAudioPrompt = EditorGUILayout.TextArea(promptToShow ,GUILayout.Width(550),GUILayout.MinHeight(90));

            EditorGUILayout.EndHorizontal();
            if (newAudioPrompt != so.voiceInstructionPrompt)
            {
                Undo.RecordObject(so, "Edit Audio Prompt");
                so.voiceInstructionPrompt = newAudioPrompt;
                EditorUtility.SetDirty(so);
            }
            EditorGUILayout.EndVertical();


            EditorGUILayout.BeginHorizontal();
            // --- Left Column ---
            // --- Left Column (Fixed Width) ---
            EditorGUILayout.BeginVertical(GUILayout.Width(180), GUILayout.ExpandHeight(true));
            _leftPanelScrollPosition = EditorGUILayout.BeginScrollView(_leftPanelScrollPosition, GUILayout.ExpandHeight(true));
            DrawDialogueStatePanel(so);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            // --- Right Column (Takes All Remaining Space) ---
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _rightPanelScrollPosition = EditorGUILayout.BeginScrollView(_rightPanelScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_selectedState.HasValue)
            {
                DrawDialogue(so, _selectedState.Value);
            }
            else
            {
                EditorGUILayout.HelpBox("Select a state from the left panel to view and edit its dialogue lines.", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawDialogueStatePanel(iTalkSituationDialogueSO so)
        {
            EditorGUILayout.LabelField("States & Line Counts", EditorStyles.boldLabel, GUILayout.Width(160));
            // Robustness check for dictionaries
            if (so.desiredLineCounts == null) so.desiredLineCounts = new SerializableDictionary<NPCAvailabilityState, int>();
            if (so.dialogues == null) so.dialogues = new iTalkSituationDialogueBundle();
            if (so.dialogues.dialogues == null) so.dialogues.dialogues = new SerializableDictionary<NPCAvailabilityState, List<DialogueLine>>();

            var allStates = System.Enum.GetValues(typeof(NPCAvailabilityState));
            bool anySelected = _selectedState.HasValue;

            foreach (NPCAvailabilityState stateEnum in allStates)
            {
                EditorGUILayout.BeginHorizontal();

                // Select the first state by default if none is selected
                if (!anySelected)
                {
                    _selectedState = stateEnum;
                    anySelected = true;
                }

                bool isSelected = _selectedState.HasValue && _selectedState.Value == stateEnum;
                if (GUILayout.Toggle(isSelected, stateEnum.ToString(), EditorStyles.radioButton, GUILayout.Width(130)) && !isSelected)
                {
                    _selectedState = stateEnum;
                    GUI.FocusControl(null);
                }

                // Desired Line Count IntField
                int currentCount = so.desiredLineCounts.ContainsKey(stateEnum) ? so.desiredLineCounts[stateEnum] : 0;
                int newCount = EditorGUILayout.IntField(currentCount, GUILayout.Width(30));
                if (newCount < 0) newCount = 0;

                // --- AUTOMATIC SYNC LOGIC ---
                // If the count has changed, update the model and automatically sync the list.
                if (newCount != currentCount)
                {
                    Undo.RecordObject(so, $"Change Desired Line Count for {stateEnum}");
                    so.desiredLineCounts[stateEnum] = newCount;

                    // Ensure the list for this state exists
                    if (!so.dialogues.dialogues.ContainsKey(stateEnum))
                    {
                        so.dialogues.dialogues[stateEnum] = new List<DialogueLine>();
                    }
                    var list = so.dialogues.dialogues[stateEnum];

                    // Add or remove items to match the new count immediately
                    while (list.Count < newCount) list.Add(new DialogueLine { text = "", audio = null });
                    while (list.Count > newCount) list.RemoveAt(list.Count - 1);

                    EditorUtility.SetDirty(so); // Mark the object as changed to ensure it gets saved
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }

        void DrawDialogue(iTalkSituationDialogueSO so, NPCAvailabilityState stateToDraw)
        {
            EditorGUILayout.LabelField($"{stateToDraw} Dialogues", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // This check should now rarely fail because DrawDialogueStatePanel is more robust, but it's good practice.
            if (so.dialogues == null || so.dialogues.dialogues == null || !so.dialogues.dialogues.ContainsKey(stateToDraw))
            {
                EditorGUILayout.HelpBox("Dialogue list not initialized. Try changing a line count to sync.", MessageType.Warning);
                EditorGUI.indentLevel--;
                return;
            }

            List<DialogueLine> lines = so.dialogues.dialogues[stateToDraw];

            if (lines.Count == 0)
            {
                EditorGUILayout.HelpBox($"No dialogue lines for {stateToDraw}. Adjust the count on the left to add lines.", MessageType.Info);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                DialogueLine line = lines[i];

                EditorGUILayout.LabelField($"Line {i + 1}", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;

                // Text Area for dialogue
                string newText = EditorGUILayout.TextArea(line.text,GUILayout.Width(370), GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
                if (newText != line.text)
                {
                    Undo.RecordObject(so, $"Edit Dialogue Text ({stateToDraw} Line {i + 1})");
                    line.text = newText;
                    lines[i] = line;
                    EditorUtility.SetDirty(so);
                }

                // Audio Clip field and player
                EditorGUILayout.BeginHorizontal();
                AudioClip newAudio = (AudioClip)EditorGUILayout.ObjectField("Audio", line.audio, typeof(AudioClip), false,GUILayout.Width(320));
                if (newAudio != line.audio)
                {
                    Undo.RecordObject(so, $"Edit Dialogue Audio ({stateToDraw} Line {i + 1})");
                    line.audio = newAudio;
                    lines[i] = line;
                    EditorUtility.SetDirty(so);
                }
                if (GUILayout.Button("Play", GUILayout.Width(50)))
                {
                    // Using the centralized utility to play the clip
                    EditorAiContentUtility.PlayClipInEditor(line.audio);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }
            EditorGUI.indentLevel--;
        }

    }
}