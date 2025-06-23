using UnityEngine;
using UnityEditor; // This namespace is essential for Editor scripting

// This attribute links this custom editor to the INPCBase script.
// It tells Unity to use this class to draw the Inspector for INPCBase components.
[CustomEditor(typeof(INPCBase))]
public class INPCBaseEditor : Editor
{
    // This method is called by Unity whenever the Inspector for an INPCBase object needs to be drawn.
    public override void OnInspectorGUI()
    {

        base.OnInspectorGUI();

        INPCBase npcBase = (INPCBase)target;


        EditorGUILayout.Space(); // Add some vertical space for better readability
        EditorGUILayout.LabelField("Live NPC Action", EditorStyles.boldLabel); // A bold label for emphasis


        EditorGUI.BeginDisabledGroup(true); // Start a disabled group (makes fields read-only)
        EditorGUILayout.EnumPopup("Current Action", npcBase.currentAction); // Display the enum

        if (npcBase.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
        {
            EditorGUILayout.FloatField("Current Nav Speed", npcBase.GetComponent<UnityEngine.AI.NavMeshAgent>().velocity.magnitude);
        }
        EditorGUI.EndDisabledGroup(); // End the disabled group
        serializedObject.ApplyModifiedProperties();
    }
}