// EditorAiContentUtility.cs
#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using UnityEngine.Networking;

namespace CelestialCyclesSystem
{
public static class EditorAiContentUtility
{
    #region Audio Player
    
    private static AudioSource _editorPreviewAudioSource;
    private static GameObject _editorPreviewAudioGameObject;

    private static VoiceType _selectedVoiceTypeSample = VoiceType.Alloy;
    private const string VOICE_SAMPLE_FULL_PATH = "Assets/CelestialCycle/DynamicAiNpc/Resources/NPCSituationDialogue/Audio/Sample";
    private const string API_CONFIG_SO_PATH = "Assets/CelestialCycle/DynamicAiNpc/Resources/NPCPersona/ApiConfigSettings.asset";


   public static void DrawVoiceTypeSamplePlayer(VoiceType type)
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(170));
        EditorGUILayout.LabelField("VoiceType Sample Player", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _selectedVoiceTypeSample = (VoiceType)EditorGUILayout.EnumPopup("", type, GUILayout.Width(80));

        if (GUILayout.Button($"Play", GUILayout.Width(80)))
        {
            PlayVoiceTypeSample(_selectedVoiceTypeSample);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    public static void PlayVoiceTypeSample(VoiceType type)
    {
        string sampleName = $"{type}-Sample";
        string assetPath = $"{VOICE_SAMPLE_FULL_PATH}/{sampleName}.wav";
        AudioClip sampleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        if (sampleClip != null)
        {
            PlayClipInEditor(sampleClip);
        }
        else
        {
            Debug.LogWarning($"Sample not found at: {assetPath}");
        }
    }

    public static void PlayClipInEditor(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("PlayClipInEditor: AudioClip is null.");
            return;
        }

        StopEditorPreviewAudio();

        _editorPreviewAudioGameObject = new UnityEngine.GameObject($"_EditorAudioPlayer_{clip.name}");
        _editorPreviewAudioGameObject.hideFlags = HideFlags.HideAndDontSave;

        _editorPreviewAudioSource = _editorPreviewAudioGameObject.AddComponent<AudioSource>();
        _editorPreviewAudioSource.clip = clip;
        _editorPreviewAudioSource.Play();

        EditorApplication.update -= MonitorEditorPreviewAudioPlayback;
        EditorApplication.update += MonitorEditorPreviewAudioPlayback;
    }

    private static void MonitorEditorPreviewAudioPlayback()
    {
        if (_editorPreviewAudioSource == null ||
            _editorPreviewAudioGameObject == null ||
            !_editorPreviewAudioSource.isPlaying)
        {
            StopEditorPreviewAudio();
        }
    }

    public static void StopEditorPreviewAudio()
    {
        EditorApplication.update -= MonitorEditorPreviewAudioPlayback;

        if (_editorPreviewAudioSource != null)
        {
            if (_editorPreviewAudioSource.isPlaying)
            {
                _editorPreviewAudioSource.Stop();
            }
        }

        if (_editorPreviewAudioGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_editorPreviewAudioGameObject);
            _editorPreviewAudioGameObject = null;
            _editorPreviewAudioSource = null;
        }
    }
    #endregion

    public delegate void AiProgressCallback(string message, float progress);

    // --- NEW: Reusable Web Request Module for AI Chat ---
    /// <summary>
    /// A generic and reusable function to send any text prompt to the AI chat model.
    /// This contains all the boilerplate web request logic.
    /// </summary>
    /// <param name="prompt">The text prompt to send to the AI.</param>
    /// <param name="apiConfig">The API configuration SO.</param>
    /// <param name="maxTokens">Max tokens for the response.</param>
    /// <returns>The string content of the AI's response, or null on failure.</returns>
    private static async Task<string> SendChatRequestAsync(string prompt, iTalkApiConfigSO apiConfig, int maxTokens = 500)
    {
        if (string.IsNullOrWhiteSpace(prompt) || apiConfig == null || string.IsNullOrEmpty(apiConfig.apiKey))
        {
            Debug.LogError("SendChatRequestAsync: Prompt or API configuration is missing.");
            return null;
        }

        try
        {
            var requestData = new ChatApiRequest
            {
                model = apiConfig.GetChatModelApiString(),
                messages = new List<ChatApiMessage> { new ChatApiMessage(ChatApiMessage.RoleUser, prompt) },
                max_tokens = maxTokens,
                temperature = apiConfig.temperature
            };
            string jsonPayload = JsonUtility.ToJson(requestData);

            using (var www = new UnityWebRequest(apiConfig.apiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", $"Bearer {apiConfig.apiKey}");

                var asyncOp = www.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"API Error ({www.responseCode}): {www.error} - {www.downloadHandler.text}");
                }
                
                ChatApiResponse response = JsonUtility.FromJson<ChatApiResponse>(www.downloadHandler.text);
                if (response == null || response.choices == null || response.choices.Count == 0)
                {
                    throw new Exception("Failed to parse LLM response or no choices returned.");
                }

                return response.choices[0].message.content;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during AI request: {ex.Message}");
            return null;
        }
    }
    
    // --- Dialogue Text Generation ---
    public static string GenerateDialogueMetaPrompt(iTalkSituationDialogueSO dialogueSO, string personaFullPrompt)
    {
        var promptLines = new StringBuilder();
        foreach (var kvp in dialogueSO.desiredLineCounts)
        {
            if (kvp.Value > 0)
            {
                promptLines.AppendLine($"- {kvp.Key}: Generate exactly {kvp.Value} unique dialogue line(s).");
            }
        }

        return $"You are an NPC in a role-playing game. Based on the following persona:\n---\n{personaFullPrompt}\n---\n" +
               $"Generate unique dialogue lines for the following situations. Adhere strictly to the requested number of lines for each situation:\n" +
               promptLines.ToString() +
               "Format the response strictly as:\nGreeting: [Line 1], [Line 2]\nAvailable: [Line 1]\n...and so on.\n" +
               "Do not use quotes around individual lines. For example, write [Hello, friend], [Welcome] not [\"Hello, friend\"], [\"Welcome\"].";
    }

    public static async Task PopulateSituationDialogueSOAsync(iTalkSituationDialogueSO dialogueSO, string personaFullPrompt, AiProgressCallback progressCallback = null)
    {
        iTalkApiConfigSO apiConfig = AssetDatabase.LoadAssetAtPath<iTalkApiConfigSO>(API_CONFIG_SO_PATH);
        if (apiConfig == null)
        {
            Debug.LogError("[EditorAiContentUtility] PopulateSituationDialogueSOAsync: API Config SO not found at " + API_CONFIG_SO_PATH);
            progressCallback?.Invoke("Error: API Config not found.", 1f);
            return;
        }

        progressCallback?.Invoke("Preparing dialogue request...", 0.1f);
        
        string metaPrompt = GenerateDialogueMetaPrompt(dialogueSO, personaFullPrompt);

        progressCallback?.Invoke("Requesting dialogue from AI...", 0.3f);
        string rawResponse = await SendChatRequestAsync(metaPrompt, apiConfig, 1024);

        if (string.IsNullOrEmpty(rawResponse))
        {
            progressCallback?.Invoke("Failed to get response from AI.", 1f);
            return;
        }

        progressCallback?.Invoke("Parsing dialogue response...", 0.8f);
        var dialogueLinesByState = new Dictionary<NPCAvailabilityState, List<string>>();
        foreach (NPCAvailabilityState state in Enum.GetValues(typeof(NPCAvailabilityState)))
        {
            dialogueLinesByState[state] = new List<string>();
        }

        string[] lines = rawResponse.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex == -1) continue;

            string stateStr = line.Substring(0, colonIndex).Trim();
            string dialogueStr = line.Substring(colonIndex + 1).Trim();

            if (Enum.TryParse<NPCAvailabilityState>(stateStr, true, out var state))
            {
                if (dialogueStr.StartsWith("[") && dialogueStr.EndsWith("]"))
                {
                    dialogueStr = dialogueStr.Substring(1, dialogueStr.Length - 2);
                    dialogueLinesByState[state] = dialogueStr.Split(new[] { "], [" }, StringSplitOptions.RemoveEmptyEntries)
                                                             .Select(d => d.Trim()).ToList();
                }
            }
        }

        foreach (var state in Enum.GetValues(typeof(NPCAvailabilityState)).Cast<NPCAvailabilityState>())
        {
            var linesList = dialogueLinesByState[state];
            var dialogueLines = new List<DialogueLine>();
            for (int i = 0; i < linesList.Count; i++)
            {
                dialogueLines.Add(new DialogueLine { text = linesList[i], audio = null });
            }
            dialogueSO.dialogues.dialogues[state] = dialogueLines;
        }
        
        progressCallback?.Invoke("Dialogue generated.", 1f);
        EditorUtility.SetDirty(dialogueSO);
    }
    
    // --- Voice Prompt Generation ---
    public static async Task<string> GenerateVoiceInstructionPromptAsync(iTalkNPCPersona persona, AiProgressCallback progressCallback = null)
    {
        iTalkApiConfigSO apiConfig = AssetDatabase.LoadAssetAtPath<iTalkApiConfigSO>(API_CONFIG_SO_PATH);
        if (apiConfig == null)
        {
            Debug.LogError("[EditorAiContentUtility] GenerateVoiceInstructionPromptAsync: API Config SO not found at " + API_CONFIG_SO_PATH);
            progressCallback?.Invoke("Error: API Config not found.", 1f);
            return null;
        }

        progressCallback?.Invoke("Analyzing persona for voice traits...", 0.1f);
        
        string personaAnalysisPrompt = 
            "Analyze the following NPC persona description. Based on this description, and using the provided keyword framework, generate a set of voice instructions.\n\n" +
            "### Persona Description:\n---\n" +
            $"{persona.builtPrompt}\n" +
            "---\n\n" +
            "### Keyword Framework:\n" +
            "- **Affect**: The inherent vocal quality (e.g., deep, high-pitched, resonant, husky, clear, smooth, gravelly, warm, harsh).\n" +
            "- **Tone**: The manner of speaking and attitude (e.g., formal, informal, authoritative, empathetic, sarcastic, mysterious, playful, serious, urgent, calm).\n" +
            "- **Emotion**: The specific feeling being conveyed (e.g., happy, sad, angry, confident, bored, loving).\n" +
            "- **Pronunciation**: Clarity, speed, and accent (e.g., fast pace, slow pace, clear articulation, mumbled, British accent).\n" +
            "- **Phrasing**: Rhythm and pauses (e.g., meaningful pauses, speaking in bursts, short sentences, flowing rhythm).\n\n" +
            "Your task is to return ONLY the following five lines, filled in with the most appropriate keywords from the framework based on the persona:\n\n" +
            "Affect: [Generated Affect Keywords]\n" +
            "Tone: [Generated Tone Keywords]\n" +
            "Emotion: [Generated Emotion Keywords]\n" +
            "Pronunciation: [Generated Pronunciation Keywords]\n" +
            "Phrasing: [Generated Phrasing Keywords]";

        progressCallback?.Invoke("Requesting voice analysis from AI...", 0.3f);
        string rawKeywordResponse = await SendChatRequestAsync(personaAnalysisPrompt, apiConfig, 200);

        if (string.IsNullOrEmpty(rawKeywordResponse))
        {
            progressCallback?.Invoke("Failed to get voice analysis from AI.", 1f);
            return null;
        }

        progressCallback?.Invoke("Assembling voice prompt...", 0.8f);
        var parsedKeywords = new Dictionary<string, string>();
        var lines = rawKeywordResponse.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(new[] { ':' }, 2);
            if (parts.Length == 2)
            {
                parsedKeywords[parts[0].Trim()] = parts[1].Trim();
            }
        }
        
        var promptBuilder = new StringBuilder("");
        if (parsedKeywords.TryGetValue("Affect", out var affect)) promptBuilder.AppendLine($"Affect: {affect.ToLower()} voice.");
        if (parsedKeywords.TryGetValue("Tone", out var tone)) promptBuilder.AppendLine($"Tone: {tone.ToLower()}.");
        if (parsedKeywords.TryGetValue("Emotion", out var emotion)) promptBuilder.AppendLine($"Emotion: {emotion.ToLower()}.");
        if (parsedKeywords.TryGetValue("Pronunciation", out var pronunciation)) promptBuilder.AppendLine($"Pronunciation: {pronunciation.ToLower()}.");
        if (parsedKeywords.TryGetValue("Phrasing", out var phrasing)) promptBuilder.AppendLine($"Phrasing: {phrasing.ToLower()}.");

        return promptBuilder.ToString();
    }
    
    // --- Audio Generation (TTS) ---
    public static async Task<AudioClip> GenerateAndSaveAudioClipAsync(
        string textToSpeak,
        string voiceInstruction,
        string voicePresetName,
        string outputDirectory,
        string outputFileNameWithoutExtension,
        AiProgressCallback progressCallback = null)
    {
        iTalkApiConfigSO apiConfig = AssetDatabase.LoadAssetAtPath<iTalkApiConfigSO>(API_CONFIG_SO_PATH);
        if (apiConfig == null)
        {
            Debug.LogError("[EditorAiContentUtility] GenerateAndSaveAudioClipAsync: API Config SO not found at " + API_CONFIG_SO_PATH);
            progressCallback?.Invoke("Error: API Config not found.", 1f);
            return null;
        }

        if (string.IsNullOrWhiteSpace(textToSpeak) || string.IsNullOrEmpty(apiConfig.apiKey) || string.IsNullOrEmpty(apiConfig.voiceApiUrl))
        {
            Debug.LogError("[EditorAiContentUtility] TTS: Missing text, API key, or Voice API URL.");
            return null;
        }

        string modelForRequest = apiConfig.GetVoiceModelApiString(); // Use the model from apiConfig
        string audioFormat = apiConfig.audioResponseFormat.ToString().ToLower(); // Use the format from apiConfig
        progressCallback?.Invoke($"TTS Request: {outputFileNameWithoutExtension}", 0.1f);

        VoiceApiRequest requestData = new VoiceApiRequest
        {
            model = modelForRequest,
            input = textToSpeak,
            voice = voicePresetName,
            response_format = audioFormat,
            speed = apiConfig.voiceSpeed, // Use speed from apiConfig
            instructions = voiceInstruction
        };
        string jsonPayload = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        byte[] audioData = null;

        using (var www = new UnityWebRequest(apiConfig.voiceApiUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {apiConfig.apiKey}");

            var asyncOp = www.SendWebRequest();
            while (!asyncOp.isDone)
            {
                await Task.Yield();
            }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[EditorAiContentUtility] TTS request failed for '{textToSpeak.Substring(0, Mathf.Min(20, textToSpeak.Length))}...': {www.error}\nResponse: {www.downloadHandler.text}");
                return null;
            }
            audioData = www.downloadHandler.data;
        }

        if (audioData == null || audioData.Length == 0) return null;

        progressCallback?.Invoke($"Processing: {outputFileNameWithoutExtension}", 0.7f);
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        string fullAssetPath = Path.Combine(outputDirectory, $"{outputFileNameWithoutExtension}.{audioFormat}");

        File.WriteAllBytes(fullAssetPath, audioData);
        AssetDatabase.ImportAsset(fullAssetPath, ImportAssetOptions.ForceUpdate);

        progressCallback?.Invoke($"Importing: {outputFileNameWithoutExtension}", 0.9f);
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(fullAssetPath);

        if (clip == null)
        {
            Debug.LogError($"[EditorAiContentUtility] Failed to load AudioClip at {fullAssetPath}.");
        }
        progressCallback?.Invoke($"Done: {outputFileNameWithoutExtension}", 1f);
        return clip;
    }
}
}
#endif