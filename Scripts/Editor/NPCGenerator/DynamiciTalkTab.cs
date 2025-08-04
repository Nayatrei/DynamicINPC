// DynamiciTalkTab.cs
#if UNITY_EDITOR
using System; 
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CelestialCyclesSystem;


public class DynamiciTalkTab
{
    private DynamicNPCGenerator _editorWindow;

    // --- Constants for SO Paths ---
    private const string PERSONA_SO_PATH = "Assets/CelestialCycle/DynamicAiNpc/Resources/NPCPersona";
    private const string NEWS_SO_FULL_PATH = "Assets/CelestialCycle/DynamicAiNpc/Resources/NewsItem";
    private const string DIALOGUE_SO_FULL_PATH = "Assets/CelestialCycle/DynamicAiNpc/Resources/NPCSituationDialogue";
    private const string VOICE_SO_FULL_PATH = "Assets/CelestialCycle/DynamicAiNpc/Resources/NPCDialogueVoice";
    // API_CONFIG_SO_NAME is managed by DynamiciTalkIntegrationTab

    // --- SubTab State ---
    private int selectedPersonaSubTabIndex = 0;
    // Sub-tabs: "API Model", "Voice Model", "Import/Export JSON" are handled by integrationTabHandler
    private string[] personaSubTabs = new string[] { "Persona List", "News List", "Dialogue SOs", "API Model", "Voice Model", "Import/Export JSON" };
    private Vector2 personaSubTabScrollPos;

    // --- Persona List State ---
    private Vector2 personaListScrollPos;
    private List<iTalkNPCPersona> allPersonaSOs = new List<iTalkNPCPersona>();
    private iTalkNPCPersona selectedPersonaSO;
    private Editor selectedPersonaSOEditor;
    private string newPersonaNameInput = "NewPersona";

    // --- News List State ---
    private Vector2 newsListScrollPos;
    private List<iTalkNewsItemEntry> allNewsSOs = new List<iTalkNewsItemEntry>();
    private iTalkNewsItemEntry selectedNewsSO;
    private Editor selectedNewsSOEditor;
    private string newNewsSOName = "NewWorldNews";

    // --- Dialogue SOs List State ---
    private Vector2 dialogueListScrollPos;
    private List<iTalkSituationDialogueSO> allDialogueSOs = new List<iTalkSituationDialogueSO>();
    private iTalkSituationDialogueSO selectedDialogueSO;
    private Editor selectedDialogueSOEditor;
    private string newDialogueSOName = "NewSituationDialogue";

    // --- Integration Tab Handler ---
    private DynamiciTalkIntegrationTab integrationTabHandler;

private Dictionary<iTalkNPCPersona, bool> _personaPromptPopupStates = new Dictionary<iTalkNPCPersona, bool>();


    public DynamiciTalkTab(DynamicNPCGenerator editorWindow)
    {
        _editorWindow = editorWindow;
        // Pass only the editor window reference, integrationTabHandler manages its own dependencies.
        integrationTabHandler = new DynamiciTalkIntegrationTab(editorWindow);
    }

    public void OnEnable()
    {
        LoadAllPersonaSOs();
        LoadAllNewsSOs();
        LoadAllDialogueSOs();
        integrationTabHandler.OnEnable(); // Propagate OnEnable
    }

    public void OnDestroy()
    {
        if (selectedPersonaSOEditor != null) UnityEngine.Object.DestroyImmediate(selectedPersonaSOEditor);
        if (selectedNewsSOEditor != null) UnityEngine.Object.DestroyImmediate(selectedNewsSOEditor);
        if (selectedDialogueSOEditor != null) UnityEngine.Object.DestroyImmediate(selectedDialogueSOEditor);
        EditorAiContentUtility.StopEditorPreviewAudio(); // Ensure audio is stopped on destroy
        integrationTabHandler.OnDestroy(); // Propagate OnDestroy
    }

    public void DrawTab()
    {
        EditorGUILayout.LabelField("iTalk Data Management", _editorWindow.GetCenteredHeaderStyle(16, Color.black));
        EditorGUILayout.HelpBox("Manage NPC Personas, World News, Situational Dialogues, API configurations, and batch import/export.", MessageType.Info);
        EditorGUILayout.Space();

        selectedPersonaSubTabIndex = GUILayout.Toolbar(selectedPersonaSubTabIndex, personaSubTabs, GUILayout.Height(25));
        EditorGUILayout.Space(10);

        personaSubTabScrollPos = EditorGUILayout.BeginScrollView(personaSubTabScrollPos, GUILayout.ExpandHeight(true));

        switch (selectedPersonaSubTabIndex)
        {
            case 0: DrawPersonaListSubTab(); break;
            case 1: DrawNewsListSubTab(); break;
            case 2: DrawSituationDialogueListSubTab(); break;
            case 3: integrationTabHandler.DrawApiConfigSection(); break; // Delegated to integrationTabHandler
            case 4: // Voice Model - Delegated to integrationTabHandler
                integrationTabHandler.DrawAPIVoiceSection();
                EditorGUILayout.Space(10); // Space between voice config and test
                integrationTabHandler.DrawVoiceTestSection();
                break;
            case 5: integrationTabHandler.DrawImportExportJsonSubTab(); break; // Delegated
            default: EditorGUILayout.LabelField("Select a sub-tab."); break;
        }

        EditorGUILayout.EndScrollView();
    }
    public void UpdateManagers()
    {
        // This method can be used to update references to other managers if needed.
        // Currently, DynamiciTalkTab does not require external manager references.
    }
    public void RequestDataRefresh()
    {
        LoadAllPersonaSOs();
        // Potentially LoadAllNewsSOs() and LoadAllDialogueSOs() if JSON import affects them.
        // For now, assuming JSON import primarily affects Personas and API Config.
    }

    // --- Persona Management ---
    private void DrawPersonaListSubTab()
    {
        EditorGUILayout.LabelField("Persona List & Editor (iTalkNPCPersona)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.BeginVertical(GUILayout.Width(300)); // Left Panel: List
        DrawPersonaListPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)); // Right Panel: Details
        if (selectedPersonaSO != null)
        {
            CelestialEditorUtility.DrawSOInspector(selectedPersonaSO, ref selectedPersonaSOEditor, $"Details for: {selectedPersonaSO.name}");
        }
        else
        {
            EditorGUILayout.HelpBox("Select a Persona from the list to view/edit its details, or create a new one.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPersonaListPanel()
    {
        if (GUILayout.Button("Refresh Personas List")) LoadAllPersonaSOs();
        EditorGUILayout.Space();
        /*
  newPersonaNameInput = EditorGUILayout.TextField("New Persona Name:", newPersonaNameInput);

  if (GUILayout.Button("Create New Persona SO") && !string.IsNullOrWhiteSpace(newPersonaNameInput))
  {
      CreateNewPersonaSO(newPersonaNameInput);
      newPersonaNameInput = "NewPersona"; // Reset
  }
  */
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Available Personas:", EditorStyles.miniBoldLabel);
        personaListScrollPos = EditorGUILayout.BeginScrollView(personaListScrollPos, "box");
        iTalkNPCPersona personaToDelete = null;
        foreach (var persona in allPersonaSOs)
        {
            EditorGUILayout.BeginHorizontal();
            bool isSelected = selectedPersonaSO == persona;
            if (GUILayout.Toggle(isSelected, persona.name, EditorStyles.radioButton) != isSelected)
            {
                selectedPersonaSO = persona;
                selectedPersonaSOEditor = null; // Force redraw of inspector
            }
            if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(22)))
            {
                 if (EditorUtility.DisplayDialog("Delete Persona SO?", $"Are you sure you want to delete '{persona.name}'? This cannot be undone.", "Delete", "Cancel"))
                 {
                    personaToDelete = persona;
                 }
            }
            EditorGUILayout.EndHorizontal();
        }
         if(personaToDelete != null)
        {
            DeleteSelectedPersonaSO(personaToDelete);
        }
        EditorGUILayout.EndScrollView();
    }
    
    public void LoadAllPersonaSOs()
    {
        allPersonaSOs.Clear();
        if (!Directory.Exists(PERSONA_SO_PATH)) Directory.CreateDirectory(PERSONA_SO_PATH);
        string[] guids = AssetDatabase.FindAssets("t:iTalkNPCPersona", new[] { PERSONA_SO_PATH });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            allPersonaSOs.Add(AssetDatabase.LoadAssetAtPath<iTalkNPCPersona>(path));
        }
        allPersonaSOs = allPersonaSOs.OrderBy(p => p.name).ToList();
        if (selectedPersonaSO != null && !allPersonaSOs.Contains(selectedPersonaSO)) selectedPersonaSO = null; // Deselect if deleted
    }

    private void CreateNewPersonaSO(string soName)
    {
        iTalkNPCPersona newPersona = ScriptableObject.CreateInstance<iTalkNPCPersona>();
        newPersona.characterName = soName; 

        string path = Path.Combine(PERSONA_SO_PATH, $"{soName.Replace(" ", "_")}.asset");
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        AssetDatabase.CreateAsset(newPersona, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LoadAllPersonaSOs();
        selectedPersonaSO = newPersona; 
        EditorUtility.FocusProjectWindow();
    }

    private void DeleteSelectedPersonaSO(iTalkNPCPersona personaSO)
    {
        if (personaSO == null) return;
        string path = AssetDatabase.GetAssetPath(personaSO);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (selectedPersonaSO == personaSO) selectedPersonaSO = null;
        LoadAllPersonaSOs(); 
    }

    // --- News Management ---
    private void DrawNewsListSubTab()
    {
        EditorGUILayout.LabelField("World News List & Editor (iTalkNewsItemEntry)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(300)); 
        DrawNewsListPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)); 
        if (selectedNewsSO != null)
        {
            CelestialEditorUtility.DrawSOInspector(selectedNewsSO, ref selectedNewsSOEditor, $"Details for: {selectedNewsSO.name}");
        }
        else
        {
            EditorGUILayout.HelpBox("Select a News SO from the list to view/edit its details, or create a new one.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawNewsListPanel()
    {
        if (GUILayout.Button("Refresh News List")) LoadAllNewsSOs();
        EditorGUILayout.Space();

        newNewsSOName = EditorGUILayout.TextField("New News Item Name:", newNewsSOName);
        if (GUILayout.Button("Create New News SO") && !string.IsNullOrWhiteSpace(newNewsSOName))
        {
            CreateNewNewsSO(newNewsSOName);
            newNewsSOName = "NewWorldNews"; 
        }
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Available News Items:", EditorStyles.miniBoldLabel);
        newsListScrollPos = EditorGUILayout.BeginScrollView(newsListScrollPos, "box", GUILayout.MinHeight(100), GUILayout.ExpandHeight(true));
        iTalkNewsItemEntry newsToDelete = null; 
        foreach (var newsItem in allNewsSOs)
        {
            EditorGUILayout.BeginHorizontal();
            bool isSelected = selectedNewsSO == newsItem;
            if (GUILayout.Toggle(isSelected, newsItem.name, EditorStyles.radioButton) != isSelected)
            {
                selectedNewsSO = newsItem;
                selectedNewsSOEditor = null;
            }
             if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(22)))
            {
                if (EditorUtility.DisplayDialog("Delete News SO?", $"Are you sure you want to delete '{newsItem.name}'?", "Delete", "Cancel"))
                {
                    newsToDelete = newsItem;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        if(newsToDelete != null) DeleteSelectedNewsSO(newsToDelete);
        EditorGUILayout.EndScrollView();
    }

    public void LoadAllNewsSOs()
    {
        allNewsSOs.Clear();
        if (!Directory.Exists(NEWS_SO_FULL_PATH)) Directory.CreateDirectory(NEWS_SO_FULL_PATH);
        string[] guids = AssetDatabase.FindAssets("t:iTalkNewsItemEntry", new[] { NEWS_SO_FULL_PATH }); 
        foreach (string guid in guids)
        {
            allNewsSOs.Add(AssetDatabase.LoadAssetAtPath<iTalkNewsItemEntry>(AssetDatabase.GUIDToAssetPath(guid))); 
        }
        allNewsSOs = allNewsSOs.OrderBy(n => n.name).ToList();
        if (selectedNewsSO != null && !allNewsSOs.Contains(selectedNewsSO)) selectedNewsSO = null;
    }

    private void CreateNewNewsSO(string soName)
    {
        iTalkNewsItemEntry newNews = ScriptableObject.CreateInstance<iTalkNewsItemEntry>(); 
        string path = Path.Combine(NEWS_SO_FULL_PATH, $"{soName.Replace(" ", "_")}.asset");
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        AssetDatabase.CreateAsset(newNews, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LoadAllNewsSOs();
        selectedNewsSO = newNews;
        EditorUtility.FocusProjectWindow();
    }

    private void DeleteSelectedNewsSO(iTalkNewsItemEntry newsSO)
    {
        if (newsSO == null) return;
        string path = AssetDatabase.GetAssetPath(newsSO);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if(selectedNewsSO == newsSO) selectedNewsSO = null;
        LoadAllNewsSOs();
    }

    // --- Dialogue SO Management ---
    private void DrawSituationDialogueListSubTab()
    {
        EditorGUILayout.LabelField("Situational Dialogue List & Editor (iTalkSituationDialogueSO)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(300)); 
        DrawDialogueListPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();
        if (selectedPersonaSO != null)
        {
            EditorGUILayout.BeginVertical("box",GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // --- Mini button for builtPrompt popup ---
            // Static variable to track popup state per persona
            if (_personaPromptPopupStates == null)
                _personaPromptPopupStates = new Dictionary<iTalkNPCPersona, bool>();
            if (!_personaPromptPopupStates.ContainsKey(selectedPersonaSO))
                _personaPromptPopupStates[selectedPersonaSO] = false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{selectedPersonaSO.characterName} Dialogue & Voice", titleStyle,GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (GUILayout.Button("Show Prompt", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                _personaPromptPopupStates[selectedPersonaSO] = !_personaPromptPopupStates[selectedPersonaSO];
            }
            EditorGUILayout.EndHorizontal();

            // Show popup if toggled
            if (_personaPromptPopupStates[selectedPersonaSO])
            {
                EditorGUILayout.HelpBox(selectedPersonaSO.builtPrompt, MessageType.None);
                if (GUILayout.Button("Close Prompt", EditorStyles.miniButton, GUILayout.Width(90)))
                {
                    _personaPromptPopupStates[selectedPersonaSO] = false;
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        if (selectedPersonaSO != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newVoiceType = (VoiceType)EditorGUILayout.EnumPopup(
            new GUIContent(selectedPersonaSO.characterName + "'s Voice Model:"),
            selectedPersonaSO.voiceType
            );
            if (EditorGUI.EndChangeCheck())
            {
            Undo.RecordObject(selectedPersonaSO, "Change Voice Type");
            selectedPersonaSO.voiceType = newVoiceType;
            EditorUtility.SetDirty(selectedPersonaSO);
            }
        if (GUILayout.Button("Play Sample")) EditorAiContentUtility.PlayVoiceTypeSample(selectedPersonaSO.voiceType);

            EditorGUILayout.EndHorizontal();

        }

        if (selectedPersonaSO != null && selectedPersonaSO.situationalDialogueSOReference != null)
        {
            DrawDialogueGenerater();
            CelestialEditorUtility.DrawSOInspector(
                selectedPersonaSO.situationalDialogueSOReference,
                ref selectedDialogueSOEditor,
                $""
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Select a Persona with an assigned Dialogue SO to view/edit its details.",
                MessageType.Info
            );
        }

        EditorGUILayout.EndVertical();
      
        EditorGUILayout.EndHorizontal();
    
    }

    private void DrawDialogueListPanel()
    {
        if (GUILayout.Button("Refresh Personas List")) LoadAllPersonaSOs();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Personas & Dialogue Assignment:", EditorStyles.miniBoldLabel);
        personaListScrollPos = EditorGUILayout.BeginScrollView(personaListScrollPos, "box");

        foreach (var persona in allPersonaSOs)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            bool isSelected = selectedPersonaSO == persona;
            if (GUILayout.Toggle(isSelected, persona.name, EditorStyles.toolbarButton,GUILayout.Width(200)) != isSelected)
            {
                selectedPersonaSO = persona;
                selectedPersonaSOEditor = null; // Force redraw
            }


            // ����� DialogueSO�� ���� ��� + ��ư ����
            if (persona.situationalDialogueSOReference == null)
            {
                if (GUILayout.Button($"Create",EditorStyles.miniButtonRight, GUILayout.Width(50)))
                {
                    string safePersonaName = persona.characterName.Replace(" ", "_");
                    string assetName = $"{safePersonaName}_Dialog.asset";
                    string assetPath = Path.Combine(DIALOGUE_SO_FULL_PATH, assetName);
                    assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                    var newSO = ScriptableObject.CreateInstance<iTalkSituationDialogueSO>();
                    AssetDatabase.CreateAsset(newSO, assetPath);
                    AssetDatabase.SaveAssets();
                    persona.situationalDialogueSOReference = newSO;
                    EditorUtility.SetDirty(persona);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
  
                persona.situationalDialogueSOReference = (iTalkSituationDialogueSO)EditorGUILayout.ObjectField(
                    persona.situationalDialogueSOReference, typeof(iTalkSituationDialogueSO), false, GUILayout.Width(50)
                );
                EditorGUILayout.EndHorizontal();

            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawDialogueGenerater()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("AI Generate Dialogue")) GenerateDialogueForSelectedPersona();
        if (GUILayout.Button("Generate Voice Prompt")) GenerateVoicePromptForSelectedPersona();
        if (GUILayout.Button("Generate Audio")) GenerateDialogueAudio();
        EditorGUILayout.EndHorizontal();

    }

     private async void GenerateDialogueForSelectedPersona()
    {
        if (selectedPersonaSO == null || selectedPersonaSO.situationalDialogueSOReference == null)
        {
            Debug.LogError("No Persona or Situational Dialogue SO selected.");
            EditorUtility.DisplayDialog("Error", "No Persona or Situational Dialogue SO selected.", "OK");
            return;
        }

        // Build persona prompt based on available fields in iTalkNPCPersona
        string personaPrompt = selectedPersonaSO.builtPrompt;

        // Progress callback
        EditorAiContentUtility.AiProgressCallback progressCallback = (message, progress) =>
        {
            EditorUtility.DisplayProgressBar("Generating Dialogue", message, progress);
            Debug.Log($"Progress: {message} ({progress * 100}%)");
        };

        try
        {
            await EditorAiContentUtility.PopulateSituationDialogueSOAsync(
                dialogueSO: selectedPersonaSO.situationalDialogueSOReference,
                personaFullPrompt: personaPrompt,
                progressCallback: progressCallback
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"Dialogue generation failed: {ex.Message}");
            EditorUtility.DisplayDialog("Error", $"Dialogue generation failed: {ex.Message}", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"Dialogue generation completed for {selectedPersonaSO.characterName}.");
        EditorUtility.DisplayDialog("Success", $"Dialogue text generation completed for {selectedPersonaSO.characterName}. Use 'Generate Audio' to create audio for these lines.", "OK");
    }
    private async void GenerateVoicePromptForSelectedPersona()
    {
        if (selectedPersonaSO == null)
        {
            EditorUtility.DisplayDialog("Error", "You must select a Persona first.", "OK");
            return;
        }

        EditorAiContentUtility.AiProgressCallback progressCallback = (message, progress) =>
        {
            EditorUtility.DisplayProgressBar("Generating Voice Prompt", message, progress);
            Debug.Log($"Progress: {message} ({progress * 100}%)");
        };

        try
        {
            string generatedPrompt = await EditorAiContentUtility.GenerateVoiceInstructionPromptAsync(selectedPersonaSO, progressCallback);

            if (!string.IsNullOrEmpty(generatedPrompt))
            {
                // Assign the result to the persona and mark it for saving
                Undo.RecordObject(selectedPersonaSO, "Generate Voice Prompt");
                selectedPersonaSO.situationalDialogueSOReference.voiceInstructionPrompt = generatedPrompt;
                EditorUtility.SetDirty(selectedPersonaSO);
                Debug.Log($"Generated voice prompt for {selectedPersonaSO.characterName}: {generatedPrompt}");
                EditorUtility.DisplayDialog("Success", "A voice instruction prompt has been generated for the selected persona. You can now review and edit it in the persona's inspector.", "OK");
            }
            else
            {
                Debug.LogError($"Failed to generate voice prompt for {selectedPersonaSO.characterName}.");
                EditorUtility.DisplayDialog("Error", $"Failed to generate voice prompt for {selectedPersonaSO.characterName}.", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Voice prompt generation failed: {ex.Message}");
            EditorUtility.DisplayDialog("Error", $"Voice prompt generation failed: {ex.Message}", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

   private async void GenerateDialogueAudio()
    {
        if (selectedPersonaSO == null || selectedPersonaSO.situationalDialogueSOReference == null)
        {
            Debug.LogError("No Persona or Situational Dialogue SO selected.");
            EditorUtility.DisplayDialog("Error", "No Persona or Situational Dialogue SO selected.", "OK");
            return;
        }

        iTalkSituationDialogueSO dialogueSO = selectedPersonaSO.situationalDialogueSOReference;
        // Check if there are any dialogue lines to process
        int totalLines = dialogueSO.dialogues.dialogues.Values.Sum(list => list.Count(line => !string.IsNullOrWhiteSpace(line.text)));
        if (totalLines == 0)
        {
            Debug.LogWarning("No dialogue lines available to generate audio for.");
            EditorUtility.DisplayDialog("Warning", "No dialogue lines available to generate audio for. Generate dialogue text first.", "OK");
            return;
        }

        // Define output directory for audio files
        string safePersonaName = selectedPersonaSO.characterName.Replace(" ", "_");
        string outputDirectory = Path.Combine(DIALOGUE_SO_FULL_PATH, "Audio", safePersonaName);
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Progress callback
        EditorAiContentUtility.AiProgressCallback progressCallback = (message, progress) =>
        {
            EditorUtility.DisplayProgressBar("Generating Audio", message, progress);
            Debug.Log($"Progress: {message} ({progress * 100}%)");
        };

        try
        {
            int processedLines = 0;
            int linesToProcess = dialogueSO.dialogues.dialogues.Values.Sum(list => list.Count(line => !string.IsNullOrWhiteSpace(line.text) && line.audio == null));

            if (linesToProcess == 0)
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("All dialogue lines already have audio.");
                EditorUtility.DisplayDialog("Info", "All dialogue lines already have audio. No new audio generated.", "OK");
                return;
            }

            foreach (var state in System.Enum.GetValues(typeof(NPCAvailabilityState)).Cast<NPCAvailabilityState>())
            {
                if (!dialogueSO.dialogues.dialogues.ContainsKey(state)) continue;
                var lines = dialogueSO.dialogues.dialogues[state];
                for (int i = 0; i < lines.Count; i++)
                {
                    DialogueLine line = lines[i];
                    if (string.IsNullOrWhiteSpace(line.text)) continue;

                    // Skip if audio already exists
                    if (line.audio != null) continue;

                    string category = state.ToString();
                    string fileName = $"{category}_{i}";
                    AudioClip audio = await EditorAiContentUtility.GenerateAndSaveAudioClipAsync(
                        textToSpeak: line.text,
                        voiceInstruction: selectedPersonaSO.situationalDialogueSOReference.voiceInstructionPrompt, // Use the generated voice instruction
                        voicePresetName: selectedPersonaSO.voiceType.ToString().ToLower(), // Use persona's voice type
                        outputDirectory: Path.Combine(outputDirectory, category),
                        outputFileNameWithoutExtension: fileName,
                        progressCallback: progressCallback
                    );

                    if (audio != null)
                    {
                        line.audio = audio;
                        lines[i] = line; // Update the struct in the list
                    }

                    processedLines++;
                    progressCallback?.Invoke($"Processed {processedLines}/{linesToProcess} lines", (float)processedLines / linesToProcess);
                }
            }

            EditorUtility.SetDirty(dialogueSO);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Audio generation failed: {ex.Message}");
            EditorUtility.DisplayDialog("Error", $"Audio generation failed: {ex.Message}", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"Audio generation completed for {selectedPersonaSO.characterName}.");
        EditorUtility.DisplayDialog("Success", $"Audio generation completed for {selectedPersonaSO.characterName}.", "OK");
    }

    public void LoadAllDialogueSOs()
    {
        allDialogueSOs.Clear();
        if (!Directory.Exists(DIALOGUE_SO_FULL_PATH)) Directory.CreateDirectory(DIALOGUE_SO_FULL_PATH);
        string[] guids = AssetDatabase.FindAssets("t:iTalkSituationDialogueSO", new[] { DIALOGUE_SO_FULL_PATH });
        foreach (string guid in guids)
        {
            allDialogueSOs.Add(AssetDatabase.LoadAssetAtPath<iTalkSituationDialogueSO>(AssetDatabase.GUIDToAssetPath(guid)));
        }
        allDialogueSOs = allDialogueSOs.OrderBy(d => d.name).ToList();
        if (selectedDialogueSO != null && !allDialogueSOs.Contains(selectedDialogueSO)) selectedDialogueSO = null;
    }

    private void CreateNewDialogueSO(string soName)
    {
        iTalkSituationDialogueSO newDialogue = ScriptableObject.CreateInstance<iTalkSituationDialogueSO>();
        string path = Path.Combine(DIALOGUE_SO_FULL_PATH, $"{soName.Replace(" ", "_")}.asset");
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        AssetDatabase.CreateAsset(newDialogue, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LoadAllDialogueSOs();
        selectedDialogueSO = newDialogue;
        EditorUtility.FocusProjectWindow();
    }

    private void DeleteSelectedDialogueSO(iTalkSituationDialogueSO dialogueSO)
    {
        if (dialogueSO == null) return;
        string path = AssetDatabase.GetAssetPath(dialogueSO);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if(selectedDialogueSO == dialogueSO) selectedDialogueSO = null;
        LoadAllDialogueSOs();
    }
}
#endif