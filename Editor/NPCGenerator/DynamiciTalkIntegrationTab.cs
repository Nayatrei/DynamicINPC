#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using CelestialCyclesSystem; //

// JSON data structures remain here as they are specific to this tab's import functionality
[System.Serializable]
public class NPCProfileJson //
{
    public string id; //
    public string name; //
    public string job; //
    public string personality; //
    public string coreValue; //
    public string background; //
    public List<string> memories; //
    public int strength; //
    public int dexterity; //
    public int constitution; //
    public int intelligence; //
    public int wisdom; //
    public int charisma; //
    public string alignment; //
    public string voiceType; //
    public bool enableTTS; //
}

[System.Serializable]
public class RootJsonData //
{
    public string openai_token; //
    public NPCProfileJson[] profiles; //
}

public class DynamiciTalkIntegrationTab
{
    private DynamicNPCGenerator _editorWindow; //

    // --- Constants ---
    private const string PERSONA_SO_PATH = "Assets/CelestialCycle/DynamicAiNpc/Resources/NPCPersona"; //
    private const string API_CONFIG_SO_NAME = "ApiConfigSettings.asset"; //

    // --- State for API Config, Voice, and Import ---
    private iTalkApiConfigSO currentApiConfigSO; //
    private Editor currentApiConfigSOEditor; //
    private Vector2 scrollPos; //

    private string apiKeyInput = ""; //
    private string sampleDialogueInput = "Hello, this is a test dialogue."; //

public iTalkApiConfigSO GetApiConfigSO() => currentApiConfigSO;

#if UNITY_EDITOR // Keep editor-only fields within this directive
    private static AudioSource _editorPreviewAudioSource;
    private static GameObject _editorPreviewAudioGameObject;
#endif

    public DynamiciTalkIntegrationTab(DynamicNPCGenerator editorWindow)
    {
        _editorWindow = editorWindow; //
    }

    public void OnEnable()
    {
        LoadApiConfigSO(); //
    }

    public void OnDestroy()
    {
        if (currentApiConfigSOEditor != null) UnityEngine.Object.DestroyImmediate(currentApiConfigSOEditor); //
    }

    public void LoadApiConfigSO()
    {
        string path = Path.Combine(PERSONA_SO_PATH, API_CONFIG_SO_NAME); //
        currentApiConfigSO = AssetDatabase.LoadAssetAtPath<iTalkApiConfigSO>(path); //
        if (currentApiConfigSOEditor != null && (currentApiConfigSO == null || currentApiConfigSOEditor.target != currentApiConfigSO)) //
        {
            UnityEngine.Object.DestroyImmediate(currentApiConfigSOEditor); //
            currentApiConfigSOEditor = null; //
        }
    }

    public void DrawApiConfigSection()
    {
        EditorGUILayout.LabelField("Chat API Configuration", EditorStyles.boldLabel); //

        if (currentApiConfigSO != null) //
        {
            if (!string.IsNullOrEmpty(currentApiConfigSO.apiKey)) //
            {
                EditorGUILayout.HelpBox("API Key is Active.", MessageType.Info); //
                if (GUILayout.Button("Remove API Key")) //
                {
                    currentApiConfigSO.apiKey = ""; //
                    EditorUtility.SetDirty(currentApiConfigSO); //
                    AssetDatabase.SaveAssets(); //
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal(); //
                apiKeyInput = EditorGUILayout.PasswordField("API Key:", apiKeyInput); //
                if (GUILayout.Button("Assign", GUILayout.Width(70))) //
                {
                    currentApiConfigSO.apiKey = apiKeyInput; //
                    EditorUtility.SetDirty(currentApiConfigSO); //
                    AssetDatabase.SaveAssets(); //
                    apiKeyInput = ""; // Clear after assignment
                }
                EditorGUILayout.EndHorizontal(); //
            }

            EditorGUILayout.Space(); //
            EditorGUILayout.LabelField("Chat API Parameters", EditorStyles.boldLabel); //
            currentApiConfigSO.apiUrl = EditorGUILayout.TextField("Chat API URL", currentApiConfigSO.apiUrl); //
            currentApiConfigSO.modelPreset = (OpenAIChatModelPreset)EditorGUILayout.EnumPopup("Model Preset", currentApiConfigSO.modelPreset); //
            currentApiConfigSO.maxTokens = EditorGUILayout.IntField("Max Tokens", currentApiConfigSO.maxTokens); //
            currentApiConfigSO.temperature = EditorGUILayout.Slider("Temperature", currentApiConfigSO.temperature, 0f, 2f); //
            currentApiConfigSO.topP = EditorGUILayout.Slider("Top P", currentApiConfigSO.topP, 0f, 1f); //
            currentApiConfigSO.frequencyPenalty = EditorGUILayout.Slider("Frequency Penalty", currentApiConfigSO.frequencyPenalty, -2f, 2f); //
            currentApiConfigSO.presencePenalty = EditorGUILayout.Slider("Presence Penalty", currentApiConfigSO.presencePenalty, -2f, 2f); //
            // In iTalkApiConfigSO, globalSystemMessage is a string, not a TextArea. Correcting the call here.
            EditorGUILayout.LabelField("Global System Message:"); // Added label for clarity
            currentApiConfigSO.globalSystemMessage = EditorGUILayout.TextArea(currentApiConfigSO.globalSystemMessage, GUILayout.MinHeight(60)); //
            currentApiConfigSO.initialRequestDelay = EditorGUILayout.Slider("Initial Request Delay (s)", currentApiConfigSO.initialRequestDelay, 0f, 5f); //


            if (GUILayout.Button("Save Chat API Settings")) //
            {
                EditorUtility.SetDirty(currentApiConfigSO); //
                AssetDatabase.SaveAssets(); //
                EditorGUILayout.HelpBox("Chat API Settings Saved!", MessageType.Info); //
            }
        }
        else
        {
            EditorGUILayout.HelpBox($"ApiConfigSO '{API_CONFIG_SO_NAME}' not found at '{PERSONA_SO_PATH}'. It can be created via JSON import or manually.", MessageType.Warning); //
            if (GUILayout.Button("Attempt to Reload API Config SO")) LoadApiConfigSO(); //
        }
    }

    public void DrawAPIVoiceSection()
    {
        EditorGUILayout.LabelField("Voice API Configuration", EditorStyles.boldLabel); //
        EditorGUILayout.HelpBox("Configure settings for Text-to-Speech (TTS) services. The same API Key from 'API Model' tab is used if required by the service.", MessageType.Info); //

        if (currentApiConfigSO != null) //
        {
            if (string.IsNullOrEmpty(currentApiConfigSO.apiKey)) //
            {
                 EditorGUILayout.HelpBox("API Key is not set. Please set it in the 'API Model' tab if your voice service requires it.", MessageType.Warning); //
            } else {
                 EditorGUILayout.HelpBox("Using API Key defined in 'API Model' tab.", MessageType.Info); //
            }

            EditorGUILayout.Space(); //
            EditorGUILayout.LabelField("Voice API Parameters", EditorStyles.boldLabel); //
            currentApiConfigSO.voiceApiUrl = EditorGUILayout.TextField("Voice API URL", currentApiConfigSO.voiceApiUrl); //
            currentApiConfigSO.voiceModel = (OpenAIVoiceModel)EditorGUILayout.EnumPopup("Voice Model", currentApiConfigSO.voiceModel); //
            currentApiConfigSO.voicePreset = (OpenAIVoicePreset)EditorGUILayout.EnumPopup("Voice Preset", currentApiConfigSO.voicePreset); //
            currentApiConfigSO.audioResponseFormat = (OpenAIAudioResponseFormat)EditorGUILayout.EnumPopup("Audio Format", currentApiConfigSO.audioResponseFormat); //
            currentApiConfigSO.voiceSpeed = EditorGUILayout.Slider("Voice Speed", currentApiConfigSO.voiceSpeed, 0.25f, 4f); //

            if (GUILayout.Button("Save Voice API Settings")) //
            {
                EditorUtility.SetDirty(currentApiConfigSO); //
                AssetDatabase.SaveAssets(); //
                EditorGUILayout.HelpBox("Voice API Settings Saved!", MessageType.Info); //
            }
        }
        else
        {
            EditorGUILayout.HelpBox($"ApiConfigSO '{API_CONFIG_SO_NAME}' not found. Voice settings cannot be configured.", MessageType.Warning); //
        }
    }

    public void DrawVoiceTestSection()
    {
        EditorGUILayout.LabelField("Voice Test", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Test the current Voice API settings by speaking a sample dialogue.", MessageType.Info);

        sampleDialogueInput = EditorGUILayout.TextField("Sample Dialogue:", sampleDialogueInput);

        bool canTestVoice = currentApiConfigSO != null &&
                            !string.IsNullOrEmpty(currentApiConfigSO.apiKey) &&
                            !string.IsNullOrEmpty(currentApiConfigSO.voiceApiUrl);

        GUI.enabled = canTestVoice;

        if (GUILayout.Button("Speak Sample"))
        {
             var overallStopwatch = System.Diagnostics.Stopwatch.StartNew(); // Start overall timer
            System.Diagnostics.Stopwatch stepStopwatch = new System.Diagnostics.Stopwatch();

            string endpoint = currentApiConfigSO.voiceApiUrl;
            if (endpoint.EndsWith("/"))
            {
                endpoint = endpoint.TrimEnd('/');
            }
            string apiKey = currentApiConfigSO.apiKey;
            
            string voiceModelId = currentApiConfigSO.GetVoiceModelApiString(); // From iTalkApiConfigSO
            string voicePresetName = currentApiConfigSO.voicePreset.ToString().ToLower();
            
            List<string> supportedOpenAiVoices = new List<string> { "alloy", "echo", "fable", "onyx", "nova", "shimmer" };

            if (!supportedOpenAiVoices.Contains(voicePresetName) && voiceModelId.StartsWith("tts"))
            {
                Debug.LogWarning($"Voice preset '{voicePresetName}' is not a standard OpenAI voice for the selected TTS model. Test might fail or use a default voice.");
            }
            
            // --- ALWAYS Use VoiceApiRequest class for the request body ---
            VoiceApiRequest requestData = new VoiceApiRequest
            {
                model = voiceModelId, // Set the model ID
                input = sampleDialogueInput,
                voice = voicePresetName,
                response_format = currentApiConfigSO.audioResponseFormat.ToString().ToLower(),
                speed = currentApiConfigSO.voiceSpeed,
                instructions = null // Initialize instructions to null
            };

            // Conditionally set instructions only for models that support it
            if (currentApiConfigSO.voiceModel == OpenAIVoiceModel.GPT_4o_Mini_TTS)
            {
                // You can make this instruction string configurable in the UI if needed
                requestData.instructions = "Speak in a cheerful and positive tone."; 
            }
            
            // Now, always serialize the 'requestData' object (VoiceApiRequest type)
            string jsonPayload = JsonUtility.ToJson(requestData); 

           Debug.Log($"[TTS Profile] Testing format: {requestData.response_format}");
            Debug.Log($"[TTS Profile] Selected Voice Model Enum: {currentApiConfigSO.voiceModel}");
            Debug.Log($"[TTS Profile] voiceModelId being used for API call: '{requestData.model}'");
            Debug.Log($"[TTS Profile] Full JSON Payload being sent: {jsonPayload}");
            
            
            // Send HTTP POST
            try
            {
                stepStopwatch.Restart(); // Time the network request
                using (var www = new UnityEngine.Networking.UnityWebRequest(endpoint, "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                    www.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                    www.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");
                    www.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                    var asyncOp = www.SendWebRequest();
                    while (!asyncOp.isDone) { } 
                    
                    stepStopwatch.Stop();
                    Debug.Log($"[TTS Profile] Network Download Time ({requestData.response_format}): {stepStopwatch.ElapsedMilliseconds} ms");

#if UNITY_2020_1_OR_NEWER
                    if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
#else
                    if (www.isNetworkError || www.isHttpError)
#endif
                    {
                        Debug.LogError($"TTS request failed: {www.error}\nResponse: {www.downloadHandler.text}");
                        EditorUtility.DisplayDialog("Voice Test Error", $"TTS request failed: {www.error}\n{www.downloadHandler.text}", "OK");
                    }
                    else
                    {
                        byte[] audioData = www.downloadHandler.data;
                        string requestedFormat = requestData.response_format.ToLower();

                        if (requestedFormat == "wav")
                        {
                            stepStopwatch.Restart(); // Time the WAV fixing process
                            try
                            {
                                audioData = iTalkWaveFixer.FixWavHeader(audioData);
                                stepStopwatch.Stop();
                                Debug.Log($"[TTS Profile] WAV Header Fix Time: {stepStopwatch.ElapsedMilliseconds} ms");
                            }
                            catch (System.Exception e)
                            {
                                stepStopwatch.Stop(); // Stop stopwatch even if error
                                Debug.LogError($"[TTS Profile] Error fixing WAV header (Time: {stepStopwatch.ElapsedMilliseconds} ms): {e.Message}. Proceeding with original data.");
                            }
                        }
                        
                        string tempPath = Path.Combine(Application.temporaryCachePath, $"openai_tts_test.{requestedFormat}");
                        File.WriteAllBytes(tempPath, audioData); // Not timing this as it's usually very fast
                        
                        AudioType audioType;
                        if (requestedFormat == "wav") audioType = AudioType.WAV;
                        else if (requestedFormat == "mp3") audioType = AudioType.MPEG;
                        else {
                            Debug.LogError($"Unsupported audio format '{requestedFormat}' for profiling.");
                            overallStopwatch.Stop(); // Stop overall timer before exiting
                            return; 
                        }

                        stepStopwatch.Restart(); // Time loading AudioClip from disk
                        using (var wwwAudio = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, audioType))
                        {
                            var audioOp = wwwAudio.SendWebRequest();
                            while (!audioOp.isDone) { }

                            stepStopwatch.Stop();
                            Debug.Log($"[TTS Profile] AudioClip Load/Decode from Disk Time ({requestedFormat}): {stepStopwatch.ElapsedMilliseconds} ms");

#if UNITY_2020_1_OR_NEWER
                            if (wwwAudio.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
#else
                            if (wwwAudio.isNetworkError || wwwAudio.isHttpError)
#endif
                            {
                                Debug.LogError($"Audio load failed: {wwwAudio.error} (Path: {tempPath})");
                            }
                            else
                            {
                                AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(wwwAudio);
                                if (clip != null && clip.loadState == AudioDataLoadState.Loaded && clip.length > 0) // Added clip.length > 0
                                {
                                    PlayClipInEditor(clip);
                                    // Delay the dialog as discussed previously
                                    EditorApplication.delayCall += () => {
                                        if (_editorWindow != null) EditorUtility.DisplayDialog("Voice Test", "TTS request succeeded and audio should have played.", "OK");
                                    };
                                }
                                else
                                {
                                     Debug.LogError($"AudioClip for {requestedFormat} is null, not loaded, or has zero length after GetContent. Path: {tempPath}, LoadState: {clip?.loadState}, Length: {clip?.length}");
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Exception during TTS: {ex}");
            }
            finally // Ensure overall timer always stops
            {
                overallStopwatch.Stop();
                Debug.Log($"[TTS Profile] TOTAL Measured Time for {requestData.response_format} path (until clip ready): {overallStopwatch.ElapsedMilliseconds} ms");
            }
        }
        GUI.enabled = true;
        if (!canTestVoice)
        {
            EditorGUILayout.HelpBox("API Key or Voice API URL is missing in ApiConfigSO. Please configure them to enable voice test.", MessageType.Warning);
        }
    }

 private void PlayClipInEditor(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("PlayClipInEditor: AudioClip is null.");
            return;
        }

        // Stop and clean up any previously playing clip from this utility
        StopEditorPreviewAudio();

        // Create a temporary GameObject with an AudioSource
        _editorPreviewAudioGameObject = new GameObject($"_EditorAudioPlayer_{clip.name}");
        // Hide it from hierarchy and prevent saving to scene
        _editorPreviewAudioGameObject.hideFlags = HideFlags.HideAndDontSave; 

        _editorPreviewAudioSource = _editorPreviewAudioGameObject.AddComponent<AudioSource>();
        _editorPreviewAudioSource.clip = clip;
        _editorPreviewAudioSource.Play();

        // Register a method to monitor playback and clean up
        EditorApplication.update -= MonitorEditorPreviewAudioPlayback; // Ensure it's not added multiple times
        EditorApplication.update += MonitorEditorPreviewAudioPlayback;
    }

    private void MonitorEditorPreviewAudioPlayback()
    {
        // This method will be called by EditorApplication.update
        if (_editorPreviewAudioSource == null || 
            _editorPreviewAudioGameObject == null || 
            !_editorPreviewAudioSource.isPlaying)
        {
            StopEditorPreviewAudio();
        }
    }

    private void StopEditorPreviewAudio()
    {
        EditorApplication.update -= MonitorEditorPreviewAudioPlayback; // Always try to unregister

        if (_editorPreviewAudioSource != null) // Check if the source exists
        {
            if (_editorPreviewAudioSource.isPlaying)
            {
                _editorPreviewAudioSource.Stop();
            }
        }

        if (_editorPreviewAudioGameObject != null)
        {
            // Use DestroyImmediate for objects created and managed by editor scripts
            UnityEngine.Object.DestroyImmediate(_editorPreviewAudioGameObject); 
            _editorPreviewAudioGameObject = null; // Clear reference
            _editorPreviewAudioSource = null; // Clear reference
        }
    }

    public void DrawImportExportJsonSubTab()
    {
        EditorGUILayout.LabelField("Batch Persona & API Key Import (from JSON)", EditorStyles.boldLabel); //
        EditorGUILayout.HelpBox( //
            "Import multiple NPC Personas and API key from 'all_npc_personas.json' located in a 'Resources/NPCPersona' folder. " + //
            "This will also update the API key in the 'ApiConfigSO'.", //
            MessageType.Info); //

        if (GUILayout.Button("Import/Update Personas & API Key from JSON", GUILayout.Height(40))) //
        {
            if (EditorUtility.DisplayDialog("Confirm Full Import", //
                "This will process 'all_npc_personas.json' from 'Resources/NPCPersona':\n" + //
                "- Create/Update iTalkNPCPersona ScriptableObjects.\n" + //
                "- Create/Update ApiConfigSO with the 'openai_token' from the JSON.\n\n" + //
                "JSON data will overwrite existing SO values. Proceed?", //
                "Yes, Import All", "Cancel")) //
            {
                ProcessFullJsonFile(); //
            }
        }
        EditorGUILayout.Space(); //
        
        EditorGUILayout.LabelField("Current API Key (for reference from ApiConfigSO)", EditorStyles.miniBoldLabel); //
        if (currentApiConfigSO != null && !string.IsNullOrEmpty(currentApiConfigSO.apiKey)) //
        {
            EditorGUILayout.TextField("Loaded API Key:", currentApiConfigSO.apiKey, EditorStyles.textField); //
        }
        else if (currentApiConfigSO != null && string.IsNullOrEmpty(currentApiConfigSO.apiKey)) //
        {
             EditorGUILayout.HelpBox("ApiConfigSO is loaded, but the API Key is empty. Import from JSON or set it in the 'API Model' tab.", MessageType.Warning); //
        }
        else
        {
            EditorGUILayout.HelpBox("ApiConfigSO not loaded. Check 'API Model' tab or run JSON import.", MessageType.Warning); //
        }
    }
    
    private T ParseEnum<T>(string value, T defaultValue) where T : struct, Enum //
    {
        if (string.IsNullOrEmpty(value)) return defaultValue; //
        if (Enum.TryParse<T>(value, true, out T result)) // true for case-insensitive
        {
            return result; //
        }
        Debug.LogWarning($"Failed to parse enum {typeof(T).Name} from value '{value}'. Using default '{defaultValue}'."); //
        return defaultValue; //
    }

    private void ProcessFullJsonFile() //
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(Path.Combine("NPCPersona", "all_npc_personas")); //
        if (jsonFile == null) //
        {
            Debug.LogError("Failed to load 'all_npc_personas.json'. Make sure it's in a 'Resources/NPCPersona' folder."); //
            EditorUtility.DisplayDialog("Error", "Could not find 'all_npc_personas.json' in any 'Resources/NPCPersona' folder.", "OK"); //
            return; //
        }

        RootJsonData rootData; //
        try //
        {
            rootData = JsonUtility.FromJson<RootJsonData>(jsonFile.text); //
        }
        catch (Exception e) //
        {
            Debug.LogError($"Failed to parse JSON data from 'all_npc_personas.json'. Error: {e.Message}. Check its format."); //
            EditorUtility.DisplayDialog("Error", "Failed to parse JSON data. Check console for details.", "OK"); //
            return; //
        }

        if (rootData == null) //
        {
            Debug.LogError("Parsed JSON data is null. Check 'all_npc_personas.json'."); //
            EditorUtility.DisplayDialog("Error", "Parsed JSON data is null. Check file and console.", "OK"); //
            return; //
        }

        if (!Directory.Exists(PERSONA_SO_PATH)) //
        {
            Directory.CreateDirectory(PERSONA_SO_PATH); //
        }
        string apiConfigFullPath = Path.Combine(PERSONA_SO_PATH, API_CONFIG_SO_NAME); //

        if (currentApiConfigSO == null)  //
        {
            currentApiConfigSO = AssetDatabase.LoadAssetAtPath<iTalkApiConfigSO>(apiConfigFullPath); //
        }

        if (currentApiConfigSO == null) //
        {
            currentApiConfigSO = ScriptableObject.CreateInstance<iTalkApiConfigSO>(); //
            AssetDatabase.CreateAsset(currentApiConfigSO, apiConfigFullPath); //
            Debug.Log($"Created ApiConfigSO at {apiConfigFullPath}"); //
        }
        currentApiConfigSO.apiKey = rootData.openai_token;  //
        EditorUtility.SetDirty(currentApiConfigSO); //

        if (rootData.profiles != null) //
        {
            foreach (NPCProfileJson profile in rootData.profiles) //
            {
                if (string.IsNullOrWhiteSpace(profile.name)) //
                {
                    Debug.LogWarning("Skipping profile with empty name from JSON."); //
                    continue; //
                }
                string safeName = string.Join("_", profile.name.Split(Path.GetInvalidFileNameChars())); //
                string soName = $"Persona_{safeName}.asset"; // Prefixing to avoid conflicts //
                string soPath = Path.Combine(PERSONA_SO_PATH, soName); //
                iTalkNPCPersona personaSO = AssetDatabase.LoadAssetAtPath<iTalkNPCPersona>(soPath); //

                if (personaSO == null) //
                {
                    personaSO = ScriptableObject.CreateInstance<iTalkNPCPersona>(); //
                    AssetDatabase.CreateAsset(personaSO, soPath); //
                }

                // Assuming iTalkNPCPersona has these fields. If not, these lines will error.
                // Ensure iTalkNPCPersona class definition matches these assignments.
                personaSO.uniqueId = profile.id; //
                personaSO.characterName = profile.name; //
                personaSO.jobDescription = profile.job; //
                personaSO.personalityTraits = profile.personality;  //
                personaSO.coreValue = profile.coreValue; //
                personaSO.backgroundStory = profile.background; //
                personaSO.memories = profile.memories != null ? new List<string>(profile.memories) : new List<string>(); //
                personaSO.strength = profile.strength; //
                personaSO.dexterity = profile.dexterity; //
                personaSO.constitution = profile.constitution; //
                personaSO.intelligence = profile.intelligence; //
                personaSO.wisdom = profile.wisdom; //
                personaSO.charisma = profile.charisma; //
                

                personaSO.enableTTS = profile.enableTTS; //

                EditorUtility.SetDirty(personaSO); //
            }
        }

        AssetDatabase.SaveAssets(); //
        AssetDatabase.Refresh(); //
        Debug.Log("Successfully imported/updated Personas and API Key from JSON."); //
        EditorUtility.DisplayDialog("Import Complete", "Personas and API Key have been imported/updated.", "OK"); //
        
        LoadApiConfigSO(); //
    }
}
#endif