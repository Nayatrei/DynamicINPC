using UnityEngine;
using System.Collections.Generic;
using System;

namespace CelestialCyclesSystem
{
    // --- Chat API ---

    public enum OpenAIChatModelPreset
    {
        [Tooltip("Fast, affordable small model for focused tasks (e.g., maps to 'gpt-4o-mini')")] //
        Gpt4oMini,
        [Tooltip("Faster, more affordable reasoning model (e.g., maps to 'o4-mini' based on latest OpenAI naming)")] //
        O4Mini, // Potentially maps to "o4-mini"
        [Tooltip("Balanced for intelligence, speed, and cost (e.g., maps to 'gpt-4.1-mini' - hypothetical ID)")] //
        Gpt41Mini,
        [Tooltip("Fastest, most cost-effective GPT-4.1 model (e.g., maps to 'gpt-4.1-nano' - hypothetical ID)")] //
        Gpt41Nano,
        [Tooltip("A small model alternative to o3 (e.g., maps to 'o3-mini' or a specific gpt-3.5-turbo version)")] //
        O3Mini,
        [Tooltip("GPT-4o mini, noting its multimodal capabilities (text generation used here, e.g., maps to 'gpt-4o-mini')")] //
        Gpt4oMiniAudio
    }

    [Serializable]
    public class ChatApiMessage
    {
        public const string RoleSystem = "system";
        public const string RoleUser = "user";
        public const string RoleAssistant = "assistant";

        public string role;
        public string content;

        public ChatApiMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [Serializable]
    public class ChatApiRequest
    {
        public string model;
        public List<ChatApiMessage> messages = new List<ChatApiMessage>();
        public int max_tokens = 150;
        public float temperature = 1.0f;
        public float top_p = 1.0f;
        public int n = 1;
        public bool stream = false;
        public string stop;
        public float presence_penalty = 0f;
        public float frequency_penalty = 0f;
        public string user;
    }

    [Serializable]
    public class ChatApiChoice
    {
        public int index;
        public ChatApiMessage message;
        public string finish_reason;
    }

    [Serializable]
    public class ChatApiUsage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }

    [Serializable]
    public class ChatApiResponse
    {
        public string id;
        public string @object;
        public long created;
        public string model;
        public List<ChatApiChoice> choices = new List<ChatApiChoice>();
        public ChatApiUsage usage;
    }

    // --- Voice (Text-to-Speech) API ---

    public enum OpenAIVoiceModel
    {
        [Tooltip("Text-to-speech model optimized for speed (e.g., tts-1).")] //
        TTS_1, // Changed from tts_1
        [Tooltip("Text-to-speech model optimized for quality (e.g., tts-1-hd).")] //
        TTS_1_HD, // Changed from tts_1_hd
        [Tooltip("Newer text-to-speech model powered by GPT-4o mini (e.g., gpt-4o-mini-tts).")] //
        GPT_4o_Mini_TTS // New
    }

    public enum OpenAIVoicePreset
    {
        alloy,
        ash, // (custom, not OpenAI)
        ballad, // (custom, not OpenAI)
        coral, // (custom, not OpenAI)
        echo,
        fable,
        onyx,
        nova,
        sage, // (custom, not OpenAI)
        shimmer,
        verse, // (custom, not OpenAI)
        none // Silent/nonverbal/special (custom)
    }

    public enum OpenAIAudioResponseFormat
    {
        mp3,
        wav
    }

    [Serializable]
    public class VoiceApiRequest // This class will now be used
    {
        public string model;
        public string input;
        public string voice;
        public string response_format = "mp3";
        public float speed = 1.0f;
        public string instructions; // Optional: Only for models that support it
    }

    [Serializable]
    public class VoiceApiError
    {
        public string error;
        public string message;
    }

    [CreateAssetMenu(fileName = "ApiConfigSettings", menuName = "Celestial NPC/API Configuration")]
    public class iTalkApiConfigSO : ScriptableObject
    {
        [Header("API Credentials & Endpoint")]
        [Tooltip("Your OpenAI API Key. Keep this secure!")]
        public string apiKey = "sk-YOUR_OPENAI_API_KEY_HERE";

        [Tooltip("The API endpoint URL for chat completions.")]
        public string apiUrl = "https://api.openai.com/v1/chat/completions";

        [Tooltip("The API endpoint URL for text-to-speech (voice) synthesis.")]
        public string voiceApiUrl = "https://api.openai.com/v1/audio/speech";

        [Header("Default Chat API Parameters")]
        [Tooltip("Select the AI model preset to use.")]
        public OpenAIChatModelPreset modelPreset = OpenAIChatModelPreset.Gpt4oMini;

        [Tooltip("Maximum number of tokens to generate in the chat completion.")]
        public int maxTokens = 150;

        [Tooltip("Controls randomness (0.0 to 2.0). Lower is more focused.")]
        [Range(0f, 2f)]
        public float temperature = 0.7f;

        [Tooltip("Nucleus sampling (0.0 to 1.0).")]
        [Range(0f, 1f)]
        public float topP = 1.0f;

        [Tooltip("Reduces repetition (-2.0 to 2.0).")]
        [Range(-2f, 2f)]
        public float frequencyPenalty = 0.3f;

        [Tooltip("Encourages new topics (-2.0 to 2.0).")]
        [Range(-2f, 2f)]
        public float presencePenalty = 0.2f;

        [Header("Prompting & Behavior")]
        [TextArea(3, 5)]
        [Tooltip("Optional global system message prepended to AI requests (e.g., 'You are an AI character in a medieval fantasy game.').")]
        public string globalSystemMessage = "You are an AI character. Embody your given persona and respond naturally within the context of a game world interaction.";

        [Tooltip("Optional delay in seconds before sending a request to the AI, after GenerateDialogueResponse is called. Can help prevent overly rapid responses.")]
        [Range(0f, 5f)]
        public float initialRequestDelay = 0.1f;

        [Header("Default Voice API Parameters")]
        [Tooltip("Default voice model for TTS.")]
        public OpenAIVoiceModel voiceModel = OpenAIVoiceModel.TTS_1;

        [Tooltip("Default voice preset.")]
        public OpenAIVoicePreset voicePreset = OpenAIVoicePreset.alloy;

        [Tooltip("Default audio response format for TTS.")]
        public OpenAIAudioResponseFormat audioResponseFormat = OpenAIAudioResponseFormat.mp3;

        [Tooltip("Default TTS speed (1.0 = normal, 0.25-4.0 allowed).")]
        [Range(0.25f, 4.0f)]
        public float voiceSpeed = 1.0f;

        // Helper to get the correct API string for the selected chat model preset
        public string GetChatModelApiString()
        {
            switch (modelPreset)
            {
                case OpenAIChatModelPreset.Gpt4oMini:
                    return "gpt-4o-mini"; //
                case OpenAIChatModelPreset.O4Mini:
                    return "o4-mini"; // Verify with OpenAI documentation; was "gpt-4-mini"
                case OpenAIChatModelPreset.Gpt41Mini:
                    return "gpt-4.1-mini"; // Verify if this is an actual/available ID
                case OpenAIChatModelPreset.Gpt41Nano:
                    return "gpt-4.1-nano"; // Verify if this is an actual/available ID
                case OpenAIChatModelPreset.O3Mini:
                    return "o3-mini"; // Verify with OpenAI documentation; was "gpt-3.5-turbo-0125"
                case OpenAIChatModelPreset.Gpt4oMiniAudio:
                    return "gpt-4o-mini"; //
                default:
                    Debug.LogWarning($"Unknown model preset: {modelPreset}. Defaulting to gpt-4o-mini.");
                    return "gpt-4o-mini";
            }
        }

        // Helper to get the correct API string for the selected voice model
        public string GetVoiceModelApiString()
        {
            switch (voiceModel)
            {
                case OpenAIVoiceModel.TTS_1:
                    return "tts-1"; //
                case OpenAIVoiceModel.TTS_1_HD:
                    return "tts-1-hd"; //
                case OpenAIVoiceModel.GPT_4o_Mini_TTS:
                    return "gpt-4o-mini-tts"; // Based on image
                default:
                    Debug.LogWarning($"Unknown voice model: {voiceModel}. Defaulting to tts-1.");
                    return "tts-1";
            }
        }
    }
}