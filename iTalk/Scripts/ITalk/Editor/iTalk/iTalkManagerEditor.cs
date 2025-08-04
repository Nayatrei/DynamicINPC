#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace CelestialCyclesSystem
{
    [CustomEditor(typeof(iTalkManager))]
    public class iTalkManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            iTalkManager manager = (iTalkManager)target;
            if (manager.interactableLayer.value == 0)
            {
                EditorGUILayout.HelpBox("Interactable Layer is not set! NPCs won't be detected.", MessageType.Warning);
            }
            EditorGUILayout.LabelField("Interactable NPCs", EditorStyles.boldLabel);
            var interactables = manager.GetCurrentlyInteractableNPCs();
            if (interactables.Count > 0)
            {
                foreach (var npc in interactables)
                {
                    EditorGUILayout.LabelField($"- {npc.EntityName} (State: {npc.GetInternalAvailability()})");
                }
            }
            else
            {
                EditorGUILayout.LabelField("No interactable NPCs detected.");
            }

            // Add section for registered iTalk components
            EditorGUILayout.LabelField("Registered iTalk Components", EditorStyles.boldLabel);
            var registeredComponents = manager.GetRegisteredTalkComponents(); // Fixed method name
            if (registeredComponents.Count > 0)
            {
                foreach (var component in registeredComponents)
                {
                    EditorGUILayout.LabelField($"- {component.EntityName}");
                }
            }
            else
            {
                EditorGUILayout.LabelField("No registered iTalk components.");
            }

            if (Application.isPlaying) EditorUtility.SetDirty(manager);
        }
    }
}
#endif