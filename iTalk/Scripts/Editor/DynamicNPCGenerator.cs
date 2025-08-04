#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CelestialCyclesSystem;

public class DynamicNPCGenerator : EditorWindow
{
    // --- Core Manager References ---
    private Celestial_NPC_Manager npcManagerInstance;
    private Celestial_Object_Manager objectManagerInstance;

    // --- UI State ---
    private Vector2 mainScrollPosition;
    private int selectedMainTabIndex = 0;
    private string[] mainTabs = new string[] { "Dynamic NPCs", "iTalk Persona", "Setup & Settings" };

    // --- Tab Handler Instances ---
    private DynamicNPCTab dynamicNpcTabHandler;
    private DynamiciTalkTab dynamiciTalkTabHandler;
    private DynamicNPCSetupTab dynamicNPCSetupTabHandler;

    [MenuItem("Tools/Celestial Cycle/NPC Generator")]
    public static void ShowWindow()
    {
        DynamicNPCGenerator window = GetWindow<DynamicNPCGenerator>(false, "NPC System Mgr V2", true);
        window.minSize = new Vector2(800, 600);
    }

    void OnEnable()
    {
        FindCoreManagers();

        // Instantiate and initialize tab handlers
        dynamicNpcTabHandler = new DynamicNPCTab(this, npcManagerInstance, objectManagerInstance);
        dynamiciTalkTabHandler = new DynamiciTalkTab(this);
        dynamicNPCSetupTabHandler = new DynamicNPCSetupTab(this);

        dynamicNpcTabHandler.OnEnable();
        dynamiciTalkTabHandler.OnEnable();
        dynamicNPCSetupTabHandler.OnEnable();
    }

    void OnDestroy()
    {
        // Cleanup tab handlers
        dynamicNpcTabHandler?.OnDestroy();
        dynamiciTalkTabHandler?.OnDestroy();
        dynamicNPCSetupTabHandler?.OnDestroy();
    }

    public void FindCoreManagers()
    {
        npcManagerInstance = FindObjectOfType<Celestial_NPC_Manager>();
        objectManagerInstance = FindObjectOfType<Celestial_Object_Manager>();

        if (dynamicNpcTabHandler != null) dynamicNpcTabHandler.UpdateManagers(npcManagerInstance, objectManagerInstance);
        if (dynamiciTalkTabHandler != null) dynamiciTalkTabHandler.UpdateManagers();
        if (dynamicNPCSetupTabHandler != null) dynamicNPCSetupTabHandler.UpdateManagers();
    }

    private void OnGUI()
    {
        // --- Top Header Buttons ---
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Asset Store", GUILayout.Width(EditorGUIUtility.currentViewWidth / 4f)))
            Application.OpenURL("https://assetstore.unity.com/publishers/41999");
        if (GUILayout.Button("Documentation", GUILayout.Width(EditorGUIUtility.currentViewWidth / 4f)))
            Application.OpenURL("https://docs.google.com/document/d/your_doc_id/edit");
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // --- Tab Navigation ---
        selectedMainTabIndex = GUILayout.Toolbar(selectedMainTabIndex, mainTabs);

        GUILayout.Space(8);

        // --- Main Content Area ---
        GUILayout.BeginVertical("box");
        GUILayout.Label(mainTabs[selectedMainTabIndex], EditorStyles.boldLabel);
        mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition, GUILayout.ExpandHeight(true));
        switch (selectedMainTabIndex)
        {
            case 0:
                dynamicNpcTabHandler?.DrawTab();
                break;
            case 1:
                dynamiciTalkTabHandler?.DrawTab();
                break;
            case 2:
                dynamicNPCSetupTabHandler?.DrawTab();
                break;
            default:
                GUILayout.Label("Select a tab.", EditorStyles.label);
                break;
        }
        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    // --- Helper Drawing Functions ---
    public GUIStyle GetCenteredHeaderStyle(int fontSize, Color textColor, bool bold = true)
    {
        return new GUIStyle(EditorStyles.label)
        {
            fontSize = fontSize,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            margin = new RectOffset(0, 0, 10, 15),
            normal = { textColor = textColor }
        };
    }

    public void DrawManagerMissingWarning(string managerName, string tabName, System.Action createAction = null)
    {
        EditorGUILayout.HelpBox($"{managerName} not found in scene. The '{tabName}' tab requires it for full functionality.", MessageType.Error);
        if (createAction != null)
        {
            if (GUILayout.Button($"Attempt to Create {managerName} Object"))
            {
                createAction.Invoke();
            }
        }
    }

    public void AttemptCreateNPCManager()
    {
        CreateManagerObject("Celestial Eco Life Manager", ref npcManagerInstance);
    }

    public void AttemptCreateObjectManager()
    {
        CreateManagerObject("Celestial Object Manager", ref objectManagerInstance);
    }

    public void CreateManagerObject<T>(string prefabNameInResources, ref T managerField) where T : MonoBehaviour
    {
        GameObject managerPrefab = Resources.Load<GameObject>(prefabNameInResources);
        if (managerPrefab != null)
        {
            GameObject managerGO = Instantiate(managerPrefab);
            managerGO.name = prefabNameInResources;
            Undo.RegisterCreatedObjectUndo(managerGO, $"Create {prefabNameInResources}");
            managerField = managerGO.GetComponent<T>();
            if (managerField == null)
            {
                Debug.LogError($"{prefabNameInResources} prefab does not contain the {typeof(T).Name} component!");
                DestroyImmediate(managerGO);
                return;
            }
            FindCoreManagers();
            if (npcManagerInstance != null && managerField is Celestial_NPC_Manager cnm)
            {
                // Placeholder for future area update logic
            }
            Repaint();
        }
        else
        {
            Debug.LogError($"'{prefabNameInResources}' prefab not found in any Resources folder.");
            EditorUtility.DisplayDialog("Prefab Not Found", $"Prefab '{prefabNameInResources}' not found in Resources.", "OK");
        }
    }
}

// Static utility class for shared editor functions
public static class CelestialEditorUtility
{
    public static void DrawSOInspector(Object so, ref Editor editor, string title = null)
    {
        if (so == null) return;

        if (string.IsNullOrEmpty(title)) title = so.name;
        GUILayout.Label(title, EditorStyles.boldLabel);

        if (editor == null || editor.target != so)
        {
            if (editor != null) Object.DestroyImmediate(editor);
            editor = Editor.CreateEditor(so);
        }
        editor.OnInspectorGUI();
        EditorGUILayout.Space();
    }
}
#endif