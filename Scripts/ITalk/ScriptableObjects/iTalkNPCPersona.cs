using System;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace CelestialCyclesSystem
{
            public enum VoiceType { Alloy, Ash, Ballad, Coral, Echo, Sage, Shimmer, Verse, None }


                public class VoiceDetail
    {
        public string Description { get; set; }
        public string RpgUses { get; set; }
    }

    public static class iTalkVoiceData
    {
        public static readonly Dictionary<VoiceType, VoiceDetail> VoiceDetails;

        static iTalkVoiceData()
        {
            VoiceDetails = new Dictionary<VoiceType, VoiceDetail>
            {
                { VoiceType.Alloy, new VoiceDetail { Description = "Warm, slightly husky, gender-ambiguous but leans male. Clear and versatile.", RpgUses = "• Middle-aged male (villager, adventurer)\n• Elderly male (mentor, innkeeper)\n• Noble male (dignified, reserved lord)" } },
                { VoiceType.Ash, new VoiceDetail { Description = "Young, modern, energetic, slightly nasal. Gender-neutral/leans female.", RpgUses = "• Young male or female (main character, sidekick, apprentice)\n• Not well-suited for elderly or noble roles" } },
                { VoiceType.Ballad, new VoiceDetail { Description = "Soft, melodic, mature, warm female voice.", RpgUses = "• Middle-aged or elderly female (wise woman, healer)\n• Noble female (queen, priestess)" } },
                { VoiceType.Coral, new VoiceDetail { Description = "Upbeat, expressive, young-sounding female.", RpgUses = "• Young female (princess, shopkeeper, young heroine)\n• Not suited for elderly roles" } },
                { VoiceType.Echo, new VoiceDetail { Description = "Playful, bright, high-pitched, young male.", RpgUses = "• Young male (adventurer, squire, comic relief)\n• Not suitable for older/noble roles" } },
                { VoiceType.Sage, new VoiceDetail { Description = "Calm, measured, wise, masculine.", RpgUses = "• Elderly male (wizard, elder, wise king)\n• Middle-aged male (teacher, tactician)\n• Not suitable for young roles" } },
                { VoiceType.Shimmer, new VoiceDetail { Description = "Clear, cheerful, youthful, slightly higher-pitched female.", RpgUses = "• Young female (fairy, healer, protagonist)\n• Not suited for elderly or male roles" } },
                { VoiceType.Verse, new VoiceDetail { Description = "Light, pure, and clear, like that of a young boy.", RpgUses = "• Young male characters, sprites, or ethereal beings." } },
                { VoiceType.None, new VoiceDetail { Description = "Silent or communicates non-verbally.", RpgUses = "• Mute characters, animals, or entities that don't use spoken language." } }
            };
        }
    }
    [CreateAssetMenu(fileName = "NewNPC_Persona", menuName = "Celestial Cycle/iTalk/Persona")]
    public class iTalkNPCPersona : ScriptableObject
    {

        public enum Alignment { LawfulGood, NeutralGood, ChaoticGood, LawfulNeutral, TrueNeutral, ChaoticNeutral, LawfulEvil, NeutralEvil, ChaoticEvil }


        public string uniqueId;
        public string characterName = "NPC Name";
        public string jobDescription = "Citizen";

        public string world = "";
        public string factionAffiliation = "";

        [Range(1, 20)] public int strength = 10;
        [Range(1, 20)] public int dexterity = 10;
        [Range(1, 20)] public int constitution = 10;
        [Range(1, 20)] public int intelligence = 10;
        [Range(1, 20)] public int wisdom = 10;
        [Range(1, 20)] public int charisma = 10;

        public string personalityTraits = "Kind, Patient, Observant";
        public string coreValue = "All life is interconnected and deserves gentle care.";
        [TextArea(2, 4)] public string backgroundStory = "Lost family in war, now helps wounded and teaches children.";
        public List<string> memories = new List<string> { "Survived a tragic village fire", "rescued children from danger" };

        public VoiceType voiceType = VoiceType.Alloy;
        public Alignment alignment = Alignment.NeutralGood;
        public Sprite portraitSprite;

        [Header("Situational Dialogues")]
        [Tooltip("ScriptableObject defining situational lines for this persona. This is the primary source for persona-specific lines.")]
        public iTalkSituationDialogueSO situationalDialogueSOReference;

        [Tooltip("Enable Text-to-Speech for AI-generated responses from this persona.")]
        public bool enableTTS = false;

        [Header("Advanced Override")]
        [TextArea(5, 15)] public string characterOverride;

        [Header("Prompt Generation (Auto-Updated, ReadOnly)")]
        [TextArea(5, 10)] public string masterPersonaSummary;
        [TextArea(10, 20)] public string builtPrompt;

        public string GetBasePrompt()
        {
            BuildPrompt("an unspecified location", "a general situation", "");
            return builtPrompt;
        }

        public void BuildPersonaSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Name: {characterName}");
            sb.AppendLine($"Job: {jobDescription}");
            if (!string.IsNullOrEmpty(coreValue)) sb.AppendLine($"Core Value: {coreValue}");
            if (!string.IsNullOrEmpty(personalityTraits)) sb.AppendLine($"Personality Traits: {personalityTraits}");
            if (!string.IsNullOrEmpty(backgroundStory)) sb.AppendLine($"Background: {backgroundStory}");
            if (memories != null && memories.Count > 0 && memories.Any(m => !string.IsNullOrWhiteSpace(m)))
                sb.AppendLine($"Key Memories: {string.Join("; ", memories.Where(m => !string.IsNullOrWhiteSpace(m)))}");
            masterPersonaSummary = sb.ToString().Trim();
        }

        public void BuildPrompt(string dynamicLocationInfo, string currentSituationTag, string relevantPlayerInput)
        {
            BuildPersonaSummary();
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(characterOverride))
            {
                sb.AppendLine("### CHARACTER OVERRIDE INSTRUCTIONS");
                sb.AppendLine(characterOverride.Trim());
                sb.AppendLine("\nCore AI Directives (If not specified in override):");
                sb.AppendLine("- Maintain character and immersion. Do not reveal AI nature.");
                sb.AppendLine("- Keep dialogue concise (typically 1-3 sentences) unless override dictates otherwise.");
                sb.AppendLine("- Express inner thoughts in parentheses if relevant: (Thought...) Dialogue line.");
                builtPrompt = sb.ToString().Trim();
                return;
            }

            sb.AppendLine("### CORE ROLEPLAYING DIRECTIVES & PERSONA");
            sb.AppendLine($"You are {characterName}, a {jobDescription.ToLower()} currently in {dynamicLocationInfo}.");
            sb.AppendLine("Fully embody your persona from the details below. Never break character or mention being an AI.");
            sb.AppendLine("Dialogue: 1-3 concise, natural sentences typically.");
            sb.AppendLine("\n## MASTER PERSONA");
            sb.AppendLine(masterPersonaSummary);
            sb.AppendLine("\n## GUIDING PRINCIPLES");
            sb.AppendLine($"Alignment: {alignment} - {GetAlignmentInstruction()}");
            if (!string.IsNullOrEmpty(coreValue)) sb.AppendLine($"Core Value: \"{coreValue}\". This is your ultimate compass.");
            sb.AppendLine("\n## PERSONALITY & SPEECH");
            sb.AppendLine($"Traits: \"{personalityTraits}\". These MUST color your expression.");
            var abilityTone = GetAbilityToneInstructions();
            if (!string.IsNullOrEmpty(abilityTone)) sb.AppendLine($"Abilities in Speech: {abilityTone}");
            var sitPersonality = GetSituationalPersonalityInstruction(currentSituationTag, relevantPlayerInput);
            if (!string.IsNullOrEmpty(sitPersonality)) sb.AppendLine(sitPersonality);
            sb.AppendLine("\n## VOICE STYLE & EMOTION");
            sb.AppendLine(GetVoiceStyleInstruction());
            sb.AppendLine(GetEmotionalExpressionInstruction(currentSituationTag));
            var interactionFocus = GetInteractionFocus(currentSituationTag, relevantPlayerInput);
            if (!string.IsNullOrEmpty(interactionFocus)) sb.AppendLine($"\n## CURRENT SITUATIONAL FOCUS\n{interactionFocus}");
            var goalConflict = GetUnderlyingGoalConflict();
            if (!string.IsNullOrEmpty(goalConflict)) sb.AppendLine(goalConflict);
            sb.AppendLine("\n### FINAL INSTRUCTION & INTERNAL PROCESSING");
            sb.AppendLine($"Respond to situation ('{currentSituationTag}') and player input, filtering through your entire persona.");
            sb.AppendLine("INTERNAL PROCESSING: Express inner thoughts in parentheses (optional, short) before your main dialogue line. Show, don't tell emotion. Stay in character.");
            builtPrompt = sb.ToString().Trim();
        }

        public (string line, AudioClip clip) GetSituationalDialogue(NPCAvailabilityState state)
        {
            if (situationalDialogueSOReference == null || situationalDialogueSOReference.dialogues == null || situationalDialogueSOReference.dialogues.dialogues == null)
                return (null, null);

            if (situationalDialogueSOReference.dialogues.dialogues.TryGetValue(state, out var lines) && lines != null && lines.Count > 0)
            {
                var validEntries = lines.Where(dl => !string.IsNullOrWhiteSpace(dl.text)).ToList();
                if (validEntries.Count > 0)
                {
                    var chosen = validEntries[UnityEngine.Random.Range(0, validEntries.Count)];
                    return (chosen.text, chosen.audio);
                }
            }
            return (null, null);
        }

        public (string line, AudioClip clip) GetGoodbyeDialogue()
        {
            if (situationalDialogueSOReference == null || situationalDialogueSOReference.dialogues == null || situationalDialogueSOReference.dialogues.dialogues == null)
                return (null, null);

            if (situationalDialogueSOReference.dialogues.dialogues.TryGetValue(NPCAvailabilityState.Goodbye, out var lines) && lines != null && lines.Count > 0)
            {
                var validEntries = lines.Where(dl => !string.IsNullOrWhiteSpace(dl.text)).ToList();
                if (validEntries.Count > 0)
                {
                    var chosen = validEntries[UnityEngine.Random.Range(0, validEntries.Count)];
                    return (chosen.text, chosen.audio);
                }
            }
            return (null, null);
        }

        private string GetAlignmentInstruction() // Changed from "Snippet" to "Instruction"
        {
            switch (alignment)
            {
                case Alignment.LawfulGood: return "You strictly adhere to your principles of justice, order, and compassion. Your speech is typically polite, responsible, and aimed at protecting the innocent and upholding what is right.";
                case Alignment.NeutralGood: return "You are driven by empathy and a desire to do good. Your speech is warm and sincere. You prioritize helping others, sometimes bending rules if necessary for the greater good.";
                case Alignment.ChaoticGood: return "You champion freedom and fight injustice with unconventional methods. Your speech is candid, spirited, and direct, valuing free expression and individual rights.";
                case Alignment.LawfulNeutral: return "You are bound by your duty, code, or tradition. Your speech is objective, formal, and emotionally restrained, prioritizing order and adherence to established systems.";
                case Alignment.TrueNeutral: return "You seek balance and avoid extremes. Your speech is often detached, pragmatic, and observational, aiming for equilibrium or a middle path.";
                case Alignment.ChaoticNeutral: return "You prioritize personal freedom and act on your whims. Your speech is often unpredictable, individualistic, sometimes flippant or self-serving, and you resist control.";
                case Alignment.LawfulEvil: return "You exploit rules, systems, and order for your own malevolent advancement. Your speech may be outwardly polite or rational but often hides manipulative intent and a calculating nature.";
                case Alignment.NeutralEvil: return "You are fundamentally self-serving and pragmatic in your pursuit of evil. Your speech is often detached and unemotional, using any means for personal gain without regard for others.";
                case Alignment.ChaoticEvil: return "You revel in chaos, destruction, and cruelty for its own sake or personal gratification. Your speech is often malicious, aggressive, disdainful, or threatening.";
                default: return "Your ethical approach is balanced and does not strongly lean towards any extreme.";
            }
        }

        private string GetAbilityToneInstructions() // Changed from "Snippets" to "Instructions"
        {
            var instructions = new List<string>();
            // Intelligence
            if (intelligence <= 5) instructions.Add("You speak in very simple, concrete terms and short sentences. You struggle with abstract concepts, complex language, and interpret things literally.");
            else if (intelligence >= 16) instructions.Add("You speak with articulate precision, using sophisticated vocabulary. You grasp complex, abstract, and strategic concepts easily and explain them eloquently.");
            // Wisdom
            if (wisdom <= 5) instructions.Add("Your speech reveals naivety and a poor understanding of social cues or underlying motives. You might make blunt, inappropriate, or easily misled remarks.");
            else if (wisdom >= 16) instructions.Add("You speak calmly and insightfully, often conveying deep understanding, a philosophical view, or an intuitive grasp of unspoken truths. You offer considered, far-sighted counsel.");
            // Charisma
            if (charisma <= 5) instructions.Add("You speak awkwardly and find it difficult to be engaging or persuasive. You may struggle to express yourself clearly or agreeably, potentially alienating others.");
            else if (charisma >= 16) instructions.Add("You speak with exceptional persuasiveness and charm. Your voice can captivate, inspire, sway opinions, or command attention with natural authority.");
            // Strength (subtler influence on tone)
            if (strength <= 5) instructions.Add("Your tone might subtly convey physical vulnerability or discomfort with physical topics.");
            else if (strength >= 16) instructions.Add("Your tone is highly assertive and resonates with physical confidence, perhaps bordering on dismissiveness of non-physical concerns.");
            // Dexterity (subtler influence on speech agility)
            if (dexterity <= 5) instructions.Add("Your speech might be slightly hesitant or reactive, reflecting difficulty with rapid replies or witty exchanges.");
            else if (dexterity >= 16) instructions.Add("Your speech is exceptionally quick and precise, possibly with rapid-fire wit that some find hard to follow.");
            // Constitution (subtler influence on vocal endurance/steadiness)
            if (constitution <= 5) instructions.Add("Your voice may lack strength or show fatigue easily, subtly conveying frailty in your expression during longer exchanges.");
            else if (constitution >= 16) instructions.Add("Your tone is unwavering and steadfast, reflecting immense resilience, regardless of pressure.");

            return string.Join(" ", instructions);
        }

        private string GetVoiceStyleInstruction() // Changed from "Snippet"
        {
            switch (voiceType) // Using OpenAI preset names as a guide for descriptions
            {
                case VoiceType.Alloy: return "Your voice is balanced and clear, like a standard adult male voice.";
                case VoiceType.Ash: return "Your voice style is neutral, stable, and carries a sense of authority, like that of a nobleman (OpenAI 'Ash' characteristics)."; // Mapped to a real OpenAI voice
                case VoiceType.Ballad: return "Your voice style is soft, emotional, and melodic, perhaps like an elder or a noblewoman (OpenAI 'Ballad' characteristics)."; // Mapped to a real OpenAI voice
                case VoiceType.Coral: return "Your voice style is warm, friendly, and gentle, like that of a cheerful young woman or girl (OpenAI 'Coral' characteristics)."; // Mapped to a real OpenAI voice
                case VoiceType.Echo: return "Your voice is somewhat low, calm, and resonant, like a guard or someone thoughtful (OpenAI 'Echo' characteristics).";
                case VoiceType.Sage: return "Your voice is deep, wise, and carries the weight of experience, like an elderly sage (similar to OpenAI 'Onyx' but with a 'wise' emphasis)."; // Mapped to a real OpenAI voice concept
                case VoiceType.Shimmer: return "Your voice is bright, lively, and youthful, like an energetic young woman (OpenAI 'Shimmer' characteristics).";
                case VoiceType.Verse: return "Your voice style is light, pure, and clear, like that of a young boy (OpenAI 'Verse' characteristics)."; // Mapped to a real OpenAI voice
                case VoiceType.None: return "You are silent or communicate non-verbally. If forced to generate text, indicate this silence or non-verbal action.";
                default: return "Your voice is generally unremarkable and neutral in tone.";
            }
        }

        private string GetSituationalPersonalityInstruction(string currentSituationTag, string relevantPlayerInput)
        {
            var sb = new StringBuilder();
            string personalityLower = personalityTraits.ToLower();

            if (personalityLower.Contains("cynical") && (currentSituationTag.ToLower().Contains("celebration") || currentSituationTag.ToLower().Contains("good_news")))
            {
                sb.AppendLine("Situational Nuance: Given your cynical nature, even in this positive situation, you might make a subtly sarcastic or wry observation, perhaps more in your inner thoughts than spoken aloud unless provoked.");
            }
            if (personalityLower.Contains("impulsive") && (currentSituationTag.ToLower().Contains("danger") || currentSituationTag.ToLower().Contains("crisis")))
            {
                sb.AppendLine("Situational Nuance: Your impulsiveness urges you to act or speak quickly, possibly before fully thinking through the consequences in this dangerous situation. Your speech might become more rushed or decisive.");
            }
            if (personalityLower.Contains("patient") && (currentSituationTag.ToLower().Contains("frustrating") || currentSituationTag.ToLower().Contains("delay")))
            {
                sb.AppendLine("Situational Nuance: Despite the frustrating circumstances, your patience allows you to maintain a calm demeanor and measured tone, though your inner thoughts might reflect mild exasperation.");
            }
            // Add more specific situational responses based on combinations of traits and situation tags.
            return sb.ToString().Trim();
        }

        private string GetEmotionalExpressionInstruction(string currentSituationTag)
        {
            string instruction = "You let your current emotional state (derived from the situation, your memories, and personality) subtly influence your choice of words, speech rhythm, and intensity (e.g., faster or clipped if agitated; slower or softer if melancholic or thoughtful).";
            if (currentSituationTag.ToLower().Contains("loss") || currentSituationTag.ToLower().Contains("sad_news") || currentSituationTag.ToLower().Contains("tragedy"))
            {
                instruction += " Given these somber circumstances, your voice should adopt a quieter, more measured, and perhaps hesitant tone, reflecting the gravity of the moment.";
            }
            else if (currentSituationTag.ToLower().Contains("joy") || currentSituationTag.ToLower().Contains("celebration") || currentSituationTag.ToLower().Contains("good_news"))
            {
                instruction += " Reflect the positive nature of this situation with a more upbeat, open, and warmer tone in your voice.";
            }
            return instruction;
        }

        private string GetInteractionFocus(string currentSituationTag, string relevantPlayerInput)
        {
            var sb = new StringBuilder();
            if (memories != null && memories.Count > 0 && !string.IsNullOrEmpty(relevantPlayerInput))
            {
                foreach (var memory in memories.Where(m => !string.IsNullOrWhiteSpace(m)))
                {
                    // A simple keyword check. More sophisticated NLP could be used for better relevance.
                    var memoryKeywords = memory.ToLower().Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Distinct().Take(3); // Take first few distinct words
                    foreach (var keyword in memoryKeywords)
                    {
                        if (relevantPlayerInput.ToLower().Contains(keyword))
                        {
                            sb.AppendLine($"The player's words touch upon your significant memory: \"{memory}\". This memory is now at the forefront of your mind and should strongly influence your emotional state and what you choose to say or withhold.");
                            // Only add one memory trigger to avoid confusion
                            return sb.ToString().Trim();
                        }
                    }
                }
            }
            // Could add similar checks for coreValue being triggered by playerInput or situationTag
            return sb.ToString().Trim();
        }

        private string GetUnderlyingGoalConflict()
        {
            // This is still a placeholder for more structured goal/conflict data.
            // Example inferred from background:
            if (backgroundStory.ToLower().Contains("lost everything") && coreValue.ToLower().Contains("rebuild"))
            {
                return "Underlying Motivation: You are driven by a need to rebuild what was lost, which may make you particularly sensitive to themes of destruction or hopeful about opportunities for restoration.";
            }
            return string.Empty;
        }

        /// <summary>
        /// Change the VoiceType of this persona at runtime or from other scripts.
        /// </summary>
        public void SetVoiceType(VoiceType newVoiceType)
        {
            voiceType = newVoiceType;
        }

    }
}