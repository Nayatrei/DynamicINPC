using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using System.Linq;
#if UNITY_EDITOR
using System.Threading.Tasks;
#endif

namespace CelestialCyclesSystem
{
    /// <summary>
    /// Centralizes AI and prompt-related functions to minimize redundancy across scripts,
    /// manages global news and situational context, processes LLM and TTS requests,
    /// and mediates NPC responses during both player-NPC and NPC-NPC dialogues.
    /// </summary>
    public static class iTalkUtilities
    {
        #region Prompt Building & Context Management

        /// <summary>
        /// Builds a comprehensive prompt for player-NPC or NPC-NPC dialogues
        /// </summary>
        public static string BuildPrompt(iTalkNPCPersona persona, string userInput, string conversationHistory, 
            List<iTalkNewsItemEntry> worldNews, string globalSituation)
        {
            if (persona == null)
            {
                Debug.LogError("[iTalkUtilities] Cannot build prompt: persona is null");
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            
            // Build persona prompt if not already built
            if (string.IsNullOrEmpty(persona.builtPrompt))
            {
                persona.BuildPrompt("current location", "general interaction", userInput ?? "");
            }
            
            sb.AppendLine(persona.builtPrompt);
            sb.AppendLine();
            sb.AppendLine("--- CURRENT GAME CONTEXT & HISTORY ---");

            // Add global situation
            if (!string.IsNullOrWhiteSpace(globalSituation))
                sb.AppendLine($"Current Global Situation: {globalSituation}");

            // Add recent news
            AppendNewsToPrompt(sb, worldNews);

            // Add conversation history
            if (!string.IsNullOrWhiteSpace(conversationHistory))
            {
                sb.AppendLine("Recent Conversation History:");
                sb.AppendLine(conversationHistory);
            }

            // Add user input
            if (!string.IsNullOrWhiteSpace(userInput))
                sb.AppendLine($"Latest Input: \"{userInput}\"");

            sb.AppendLine("--- END OF CONTEXT ---");
            sb.AppendLine($"Now, as {persona.characterName}, respond based on the above context.");

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Builds prompt specifically for NPC-to-NPC conversations
        /// </summary>
        public static string BuildNPCToNPCPrompt(iTalkNPCPersona speaker, iTalkNPCPersona partner, 
            List<iTalkNewsItemEntry> worldNews, string globalSituation, string conversationContext = "")
        {
            if (speaker == null || partner == null)
            {
                Debug.LogError("[iTalkUtilities] Cannot build NPC-to-NPC prompt: speaker or partner is null");
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            
            // Build speaker persona
            if (string.IsNullOrEmpty(speaker.builtPrompt))
            {
                speaker.BuildPrompt("current location", "NPC conversation", "");
            }
            
            sb.AppendLine(speaker.builtPrompt);
            sb.AppendLine();
            sb.AppendLine("--- NPC-TO-NPC CONVERSATION CONTEXT ---");
            sb.AppendLine($"You are speaking with {partner.characterName} ({partner.jobDescription}).");
            
            if (!string.IsNullOrWhiteSpace(globalSituation))
                sb.AppendLine($"Current Situation: {globalSituation}");

            AppendNewsToPrompt(sb, worldNews);

            if (!string.IsNullOrWhiteSpace(conversationContext))
            {
                sb.AppendLine("Conversation Context:");
                sb.AppendLine(conversationContext);
            }

            sb.AppendLine("--- CONVERSATION INSTRUCTIONS ---");
            sb.AppendLine("Respond naturally as if talking to another NPC. Keep it conversational and brief (1-2 sentences).");
            sb.AppendLine("Consider your relationship, shared experiences, and current events in your response.");

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Builds a contextual prompt for group NPC conversations
        /// </summary>
        public static string BuildGroupConversationPrompt(iTalkNPCPersona speaker, List<iTalkNPCPersona> groupMembers,
            List<iTalkNewsItemEntry> worldNews, string globalSituation, string conversationTopic = "")
        {
            if (speaker == null || groupMembers == null || groupMembers.Count == 0)
            {
                Debug.LogError("[iTalkUtilities] Cannot build group conversation prompt: invalid parameters");
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            
            if (string.IsNullOrEmpty(speaker.builtPrompt))
            {
                speaker.BuildPrompt("current location", "group conversation", "");
            }
            
            sb.AppendLine(speaker.builtPrompt);
            sb.AppendLine();
            sb.AppendLine("--- GROUP CONVERSATION CONTEXT ---");
            sb.AppendLine("You are in a group conversation with:");
            
            foreach (var member in groupMembers)
            {
                if (member != speaker)
                {
                    sb.AppendLine($"- {member.characterName} ({member.jobDescription})");
                }
            }

            if (!string.IsNullOrWhiteSpace(globalSituation))
                sb.AppendLine($"\nCurrent Situation: {globalSituation}");

            AppendNewsToPrompt(sb, worldNews);

            if (!string.IsNullOrWhiteSpace(conversationTopic))
            {
                sb.AppendLine($"Current Topic: {conversationTopic}");
            }

            sb.AppendLine("--- GROUP CONVERSATION INSTRUCTIONS ---");
            sb.AppendLine("Respond as part of a group discussion. Keep responses brief and natural.");
            sb.AppendLine("Consider the group dynamic and your relationships with the other participants.");

            return sb.ToString().Trim();
        }

        private static void AppendNewsToPrompt(StringBuilder sb, List<iTalkNewsItemEntry> worldNews)
        {
            if (worldNews != null && worldNews.Count > 0)
            {
                sb.AppendLine("Recent News Relevant to the World:");
                var allNews = new List<(string text, long timestamp)>();
                
                foreach (var entry in worldNews)
                {
                    if (entry.texts != null && entry.timestamps != null)
                    {
                        int count = Math.Min(entry.texts.Count, entry.timestamps.Count);
                        for (int i = 0; i < count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(entry.texts[i]))
                            {
                                allNews.Add((entry.texts[i], entry.timestamps[i]));
                            }
                        }
                    }
                }

                var topNews = allNews
                    .OrderByDescending(n => n.timestamp)
                    .Take(5);

                foreach (var news in topNews)
                {
                    DateTime newsDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddSeconds(news.timestamp).ToLocalTime();
                    sb.AppendLine($"- ({newsDate:yyyy-MM-dd HH:mm}) {news.text}");
                }
            }
        }

        #endregion

        #region LLM Request Management

        /// <summary>
        /// Central method for all LLM requests with comprehensive error handling and token management
        /// </summary>
        public static IEnumerator SendLLMRequest(string prompt, iTalkApiConfigSO apiConfig, 
            System.Action<string> onSuccess, System.Action<string> onError, string requestId = "")
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                onError?.Invoke("Prompt is empty or null.");
                yield break;
            }

            if (apiConfig == null)
            {
                onError?.Invoke("API configuration is missing.");
                yield break;
            }

            if (string.IsNullOrEmpty(apiConfig.apiKey) || apiConfig.apiKey.Contains("YOUR_API_KEY"))
            {
                onError?.Invoke("API key is not configured properly.");
                yield break;
            }

            // Check token limits before making request
            if (iTalkManager.Instance != null)
            {
                int estimatedTokens = EstimateTokens(prompt, iTalkManager.Instance.estimatedTokensPerCharPrompt);
                if (!iTalkManager.Instance.HasEnoughTokens(prompt.Length))
                {
                    onError?.Invoke("Token quota would be exceeded. Request blocked.");
                    yield break;
                }
            }

            string logPrefix = string.IsNullOrEmpty(requestId) ? "[iTalkUtilities]" : $"[iTalkUtilities:{requestId}]";
            Debug.Log($"{logPrefix} Sending LLM request. Prompt length: {prompt.Length}");

            var requestData = new ChatApiRequest
            {
                model = apiConfig.GetChatModelApiString(),
                messages = new List<ChatApiMessage> { new ChatApiMessage("user", prompt) },
                max_tokens = apiConfig.maxTokens,
                temperature = apiConfig.temperature,
                top_p = apiConfig.topP,
                frequency_penalty = apiConfig.frequencyPenalty,
                presence_penalty = apiConfig.presencePenalty
            };

            string jsonPayload = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            using (var www = new UnityWebRequest(apiConfig.apiUrl, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", $"Bearer {apiConfig.apiKey}");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"API Error ({www.responseCode}): {www.error}";
                    if (!string.IsNullOrEmpty(www.downloadHandler.text))
                    {
                        errorMsg += $" - {www.downloadHandler.text}";
                    }
                    Debug.LogError($"{logPrefix} {errorMsg}");
                    onError?.Invoke(errorMsg);
                    yield break;
                }

                try
                {
                    ChatApiResponse response = JsonUtility.FromJson<ChatApiResponse>(www.downloadHandler.text);
                    if (response?.choices?.Count > 0 && !string.IsNullOrWhiteSpace(response.choices[0].message.content))
                    {
                        string aiResponse = response.choices[0].message.content.Trim();
                        
                        // Log token usage if manager is available
                        if (iTalkManager.Instance != null)
                        {
                            iTalkManager.Instance.LogTokenUsage(prompt, aiResponse);
                        }

                        Debug.Log($"{logPrefix} LLM request successful. Response length: {aiResponse.Length}");
                        onSuccess?.Invoke(aiResponse);
                    }
                    else
                    {
                        onError?.Invoke("Invalid AI response or no content returned.");
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Error parsing AI response: {ex.Message}";
                    Debug.LogError($"{logPrefix} {errorMsg}");
                    onError?.Invoke(errorMsg);
                }
            }
        }

        /// <summary>
        /// Specialized LLM request for player-NPC dialogues
        /// </summary>
        public static IEnumerator RequestPlayerNPCDialogue(iTalk speakerNPC, string userInput, 
            string conversationHistory, System.Action<string> onSuccess, System.Action<string> onError)
        {
            if (speakerNPC?.assignedPersona == null)
            {
                onError?.Invoke("Speaker NPC or persona is null.");
                yield break;
            }

            if (iTalkManager.Instance == null)
            {
                onError?.Invoke("iTalkManager instance not available.");
                yield break;
            }

            string prompt = BuildPrompt(
                speakerNPC.assignedPersona,
                userInput,
                conversationHistory,
                iTalkManager.Instance.GetWorldNews(),
                iTalkManager.Instance.GetCurrentGlobalSituation()
            );

            yield return SendLLMRequest(prompt, iTalkManager.Instance.GetApiConfig(), onSuccess, onError, speakerNPC.EntityName);
        }

        /// <summary>
        /// Specialized LLM request for NPC-to-NPC dialogues
        /// </summary>
        public static IEnumerator RequestNPCToNPCDialogue(iTalk speaker, iTalk partner,
            string conversationContext, System.Action<string> onSuccess, System.Action<string> onError)
        {
            if (speaker?.assignedPersona == null || partner?.assignedPersona == null)
            {
                onError?.Invoke("Speaker or partner NPC/persona is null.");
                yield break;
            }

            if (iTalkManager.Instance == null)
            {
                onError?.Invoke("iTalkManager instance not available.");
                yield break;
            }

            string prompt = BuildNPCToNPCPrompt(
                speaker.assignedPersona,
                partner.assignedPersona,
                iTalkManager.Instance.GetWorldNews(),
                iTalkManager.Instance.GetCurrentGlobalSituation(),
                conversationContext
            );

            yield return SendLLMRequest(prompt, iTalkManager.Instance.GetApiConfig(), onSuccess, onError, 
                $"{speaker.EntityName}->{partner.EntityName}");
        }

        #endregion

        #region TTS Management

        /// <summary>
        /// Central TTS request handler for all dialogue scenarios
        /// </summary>
        public static void RequestTTS(iTalkNPCPersona persona, string text, AudioSource audioSource, 
            System.Action<iTalkNPCPersona, string, AudioSource> onTTSGenerated = null)
        {
            if (persona == null)
            {
                Debug.LogWarning("[iTalkUtilities] TTS request failed: persona is null");
                return;
            }

            if (!persona.enableTTS)
            {
                Debug.Log($"[iTalkUtilities] TTS disabled for {persona.characterName}");
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning($"[iTalkUtilities] TTS request failed for {persona.characterName}: text is empty");
                return;
            }

            if (audioSource == null)
            {
                Debug.LogWarning($"[iTalkUtilities] TTS request failed for {persona.characterName}: audio source is null");
                return;
            }

            Debug.Log($"[iTalkUtilities] Requesting TTS for {persona.characterName}: \"{text.Substring(0, Math.Min(50, text.Length))}...\"");

            // Use callback if provided, otherwise use manager's TTS system
            if (onTTSGenerated != null)
            {
                onTTSGenerated.Invoke(persona, text, audioSource);
            }
            else if (iTalkManager.Instance != null)
            {
                iTalkManager.Instance.RequestTTS(persona, text, audioSource);
            }
            else
            {
                Debug.LogError("[iTalkUtilities] No TTS handler available");
            }
        }

        /// <summary>
        /// Request TTS for player-NPC dialogue responses
        /// </summary>
        public static void RequestDialogueTTS(iTalk speaker, string responseText)
        {
            if (speaker?.assignedPersona != null && speaker.GetAudioSource() != null)
            {
                RequestTTS(speaker.assignedPersona, responseText, speaker.GetAudioSource());
            }
        }

        /// <summary>
        /// Request TTS for situational dialogue lines
        /// </summary>
        public static void RequestSituationalTTS(iTalk speaker, NPCAvailabilityState state)
        {
            if (speaker?.assignedPersona == null) return;

            var (line, audioClip) = speaker.GetSituationalLineAndAudio(state);
            if (!string.IsNullOrWhiteSpace(line))
            {
                // If there's a pre-recorded audio clip, play that instead of TTS
                if (audioClip != null && speaker.GetAudioSource() != null)
                {
                    speaker.GetAudioSource().PlayOneShot(audioClip);
                }
                else
                {
                    RequestTTS(speaker.assignedPersona, line, speaker.GetAudioSource());
                }
            }
        }

        #endregion

        #region Utility Functions

        /// <summary>
        /// Estimates token count for a given text
        /// </summary>
        public static int EstimateTokens(string text, float tokensPerChar = 0.3f)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Mathf.CeilToInt(text.Length * tokensPerChar);
        }

        /// <summary>
        /// Formats conversation history for prompts
        /// </summary>
        public static string FormatHistory(List<string> historyLines, int maxLines = 10)
        {
            if (historyLines == null || historyLines.Count == 0) return string.Empty;
            
            var recentLines = historyLines.Skip(Math.Max(0, historyLines.Count - maxLines));
            return string.Join("\n", recentLines);
        }

        /// <summary>
        /// Validates if an LLM request can be made
        /// </summary>
        public static bool CanMakeLLMRequest(out string reason)
        {
            reason = "";

            if (iTalkManager.Instance == null)
            {
                reason = "iTalkManager instance not available";
                return false;
            }

            var apiConfig = iTalkManager.Instance.GetApiConfig();
            if (apiConfig == null)
            {
                reason = "API configuration is missing";
                return false;
            }

            if (string.IsNullOrEmpty(apiConfig.apiKey) || apiConfig.apiKey.Contains("YOUR_API_KEY"))
            {
                reason = "API key is not configured";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets appropriate fallback dialogue when AI is unavailable
        /// </summary>
        public static (string line, AudioClip clip) GetFallbackDialogue(iTalk npc, NPCAvailabilityState state)
        {
            if (npc == null) return ("...", null);

            // Try to get from situational dialogue first
            var situationalResult = npc.GetSituationalLineAndAudio(state);
            if (!string.IsNullOrWhiteSpace(situationalResult.line))
            {
                return situationalResult;
            }

            // Fallback to hardcoded responses
            return state switch
            {
                NPCAvailabilityState.Available => ("Hello there.", null),
                NPCAvailabilityState.Busy => ("I'm a bit busy right now.", null),
                NPCAvailabilityState.Working => ("Sorry, I'm working.", null),
                NPCAvailabilityState.Sleeping => ("(Sleeping)", null),
                NPCAvailabilityState.Greeting => ("Greetings!", null),
                NPCAvailabilityState.Goodbye => ("Farewell.", null),
                _ => ("...", null)
            };
        }

        #endregion

        #region Editor-Only Independent Async Methods

#if UNITY_EDITOR
        /// <summary>
        /// Independent async LLM request - no dependency on EditorAiContentUtility
        /// </summary>
        public static async Task<string> SendChatRequestAsync(string prompt, iTalkApiConfigSO apiConfig, int maxTokens = 500)
        {
            if (string.IsNullOrWhiteSpace(prompt) || apiConfig == null)
            {
                throw new ArgumentException("Invalid prompt or API configuration");
            }

            if (string.IsNullOrEmpty(apiConfig.apiKey) || apiConfig.apiKey.Contains("YOUR_API_KEY"))
            {
                throw new ArgumentException("API key is not configured properly");
            }

            try
            {
                var requestData = new ChatApiRequest
                {
                    model = apiConfig.GetChatModelApiString(),
                    messages = new List<ChatApiMessage> { new ChatApiMessage("user", prompt) },
                    max_tokens = maxTokens,
                    temperature = apiConfig.temperature,
                    top_p = apiConfig.topP,
                    frequency_penalty = apiConfig.frequencyPenalty,
                    presence_penalty = apiConfig.presencePenalty
                };

                string jsonPayload = JsonUtility.ToJson(requestData);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

                using (var www = new UnityWebRequest(apiConfig.apiUrl, "POST"))
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
                        throw new Exception($"API Error ({www.responseCode}): {www.error} - {www.downloadHandler.text}");
                    }

                    ChatApiResponse response = JsonUtility.FromJson<ChatApiResponse>(www.downloadHandler.text);
                    if (response?.choices?.Count > 0 && !string.IsNullOrWhiteSpace(response.choices[0].message.content))
                    {
                        return response.choices[0].message.content.Trim();
                    }
                    else
                    {
                        throw new Exception("Invalid AI response or no content returned");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[iTalkUtilities] Async LLM request failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Independent async method for generating dialogue content - no EditorAiContentUtility dependency
        /// </summary>
        public static async Task<string> GenerateDialogueContentAsync(iTalkNPCPersona persona, NPCAvailabilityState state, int count = 3)
        {
            if (persona == null) 
                throw new ArgumentException("Persona cannot be null");

            var apiConfig = UnityEngine.Resources.Load<iTalkApiConfigSO>("NPCPersona/ApiConfigSettings");
            if (apiConfig == null) 
                throw new Exception("API configuration not found at Resources/NPCPersona/ApiConfigSettings");

            // Build persona prompt if needed
            if (string.IsNullOrEmpty(persona.builtPrompt))
            {
                persona.BuildPrompt("current location", "dialogue generation", "");
            }

            string prompt = $"Generate {count} different dialogue lines for {persona.characterName} in {state} state.\n\n" +
                           $"Character Background:\n{persona.builtPrompt}\n\n" +
                           $"Requirements:\n" +
                           $"- Generate exactly {count} unique dialogue lines for {state} state\n" +
                           $"- Each line should be 1-2 sentences maximum\n" +
                           $"- Stay in character based on the persona\n" +
                           $"- Make lines appropriate for the {state} situation\n" +
                           $"- Separate each line with '|||'\n\n" +
                           $"Example format: Line 1|||Line 2|||Line 3";

            try
            {
                string response = await SendChatRequestAsync(prompt, apiConfig, 300);
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[iTalkUtilities] Failed to generate dialogue content: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Independent async method for generating voice instruction prompts - no EditorAiContentUtility dependency
        /// </summary>
        public static async Task<string> GenerateVoiceInstructionPromptAsync(iTalkNPCPersona persona)
        {
            if (persona == null) 
                throw new ArgumentException("Persona cannot be null");

            var apiConfig = UnityEngine.Resources.Load<iTalkApiConfigSO>("NPCPersona/ApiConfigSettings");
            if (apiConfig == null) 
                throw new Exception("API configuration not found at Resources/NPCPersona/ApiConfigSettings");

            // Build persona prompt if needed
            if (string.IsNullOrEmpty(persona.builtPrompt))
            {
                persona.BuildPrompt("current location", "voice analysis", "");
            }

            string voiceAnalysisPrompt = 
                "Analyze the following NPC persona and generate voice instruction keywords.\n\n" +
                $"### Persona Description:\n{persona.builtPrompt}\n\n" +
                "### Task:\n" +
                "Based on this persona, generate appropriate voice characteristics using these categories:\n" +
                "- **Affect**: Vocal quality (e.g., deep, high-pitched, resonant, husky, clear, smooth, gravelly, warm, harsh)\n" +
                "- **Tone**: Speaking manner (e.g., formal, informal, authoritative, empathetic, sarcastic, mysterious, playful, serious)\n" +
                "- **Emotion**: Feeling conveyed (e.g., happy, sad, angry, confident, bored, loving, excited, calm)\n" +
                "- **Pronunciation**: Speech clarity (e.g., fast pace, slow pace, clear articulation, mumbled, accented)\n" +
                "- **Phrasing**: Rhythm and pauses (e.g., meaningful pauses, speaking in bursts, short sentences, flowing)\n\n" +
                "### Output Format:\n" +
                "Return exactly 5 lines in this format:\n" +
                "Affect: [keywords]\n" +
                "Tone: [keywords]\n" +
                "Emotion: [keywords]\n" +
                "Pronunciation: [keywords]\n" +
                "Phrasing: [keywords]";

            try
            {
                string rawResponse = await SendChatRequestAsync(voiceAnalysisPrompt, apiConfig, 200);
                
                // Parse and format the response
                var parsedKeywords = new Dictionary<string, string>();
                var lines = rawResponse.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        parsedKeywords[parts[0].Trim()] = parts[1].Trim();
                    }
                }

                // Build formatted voice instruction
                var instructionBuilder = new StringBuilder();
                
                if (parsedKeywords.TryGetValue("Affect", out var affect)) 
                    instructionBuilder.AppendLine($"Affect: {affect.ToLower()} voice.");
                if (parsedKeywords.TryGetValue("Tone", out var tone)) 
                    instructionBuilder.AppendLine($"Tone: {tone.ToLower()}.");
                if (parsedKeywords.TryGetValue("Emotion", out var emotion)) 
                    instructionBuilder.AppendLine($"Emotion: {emotion.ToLower()}.");
                if (parsedKeywords.TryGetValue("Pronunciation", out var pronunciation)) 
                    instructionBuilder.AppendLine($"Pronunciation: {pronunciation.ToLower()}.");
                if (parsedKeywords.TryGetValue("Phrasing", out var phrasing)) 
                    instructionBuilder.AppendLine($"Phrasing: {phrasing.ToLower()}.");

                return instructionBuilder.ToString().Trim();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[iTalkUtilities] Failed to generate voice instruction prompt: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Independent async method for bulk dialogue generation - no EditorAiContentUtility dependency
        /// </summary>
        public static async Task<Dictionary<NPCAvailabilityState, List<string>>> GenerateBulkDialogueAsync(
            iTalkNPCPersona persona, Dictionary<NPCAvailabilityState, int> desiredCounts)
        {
            if (persona == null) 
                throw new ArgumentException("Persona cannot be null");
            if (desiredCounts == null || desiredCounts.Count == 0) 
                throw new ArgumentException("Desired counts cannot be null or empty");

            var apiConfig = UnityEngine.Resources.Load<iTalkApiConfigSO>("NPCPersona/ApiConfigSettings");
            if (apiConfig == null) 
                throw new Exception("API configuration not found");

            // Build persona prompt if needed
            if (string.IsNullOrEmpty(persona.builtPrompt))
            {
                persona.BuildPrompt("current location", "dialogue generation", "");
            }

            // Build bulk generation prompt
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Generate dialogue lines for an NPC based on the following persona:");
            promptBuilder.AppendLine($"---\n{persona.builtPrompt}\n---");
            promptBuilder.AppendLine("\nGenerate dialogue lines for these situations:");

            foreach (var kvp in desiredCounts)
            {
                if (kvp.Value > 0)
                {
                    promptBuilder.AppendLine($"- {kvp.Key}: Generate exactly {kvp.Value} unique dialogue line(s)");
                }
            }

            promptBuilder.AppendLine("\nFormat Requirements:");
            promptBuilder.AppendLine("- Each line should be 1-2 sentences maximum");
            promptBuilder.AppendLine("- Stay in character based on the persona");
            promptBuilder.AppendLine("- Format as: StateName: [Line 1], [Line 2], [Line 3]");
            promptBuilder.AppendLine("- Do not use quotes around individual lines");
            promptBuilder.AppendLine("- Example: Greeting: Hello there, Welcome friend");

            try
            {
                string rawResponse = await SendChatRequestAsync(promptBuilder.ToString(), apiConfig, 1024);
                
                // Parse the response
                var result = new Dictionary<NPCAvailabilityState, List<string>>();
                var lines = rawResponse.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    int colonIndex = line.IndexOf(':');
                    if (colonIndex == -1) continue;

                    string stateStr = line.Substring(0, colonIndex).Trim();
                    string dialogueStr = line.Substring(colonIndex + 1).Trim();

                    if (Enum.TryParse<NPCAvailabilityState>(stateStr, true, out var state))
                    {
                        var dialogueLines = dialogueStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                     .Select(d => d.Trim())
                                                     .Where(d => !string.IsNullOrWhiteSpace(d))
                                                     .ToList();
                        
                        result[state] = dialogueLines;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[iTalkUtilities] Failed to generate bulk dialogue: {ex.Message}");
                throw;
            }
        }
#endif

        #endregion

        #region API Data Structures

        [System.Serializable]
        public class ChatApiRequest
        {
            public string model;
            public List<ChatApiMessage> messages;
            public int max_tokens;
            public float temperature;
            public float top_p;
            public float frequency_penalty;
            public float presence_penalty;
        }

        [System.Serializable]
        public class ChatApiMessage
        {
            public string role;
            public string content;

            public ChatApiMessage(string role, string content)
            {
                this.role = role;
                this.content = content;
            }
        }

        [System.Serializable]
        public class ChatApiResponse
        {
            public List<ChatApiChoice> choices;
        }

        [System.Serializable]
        public class ChatApiChoice
        {
            public ChatApiMessage message;
        }

        #endregion
    }
}