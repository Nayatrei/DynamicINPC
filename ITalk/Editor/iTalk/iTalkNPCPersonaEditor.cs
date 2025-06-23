#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic; // Added for Dictionary

namespace CelestialCyclesSystem
{
    [CustomEditor(typeof(iTalkNPCPersona))]
    public class iTalkNPCPersonaEditor : Editor
    {
        private iTalkNPCPersona _persona;

        private int _selectedTab = 0;
        private readonly string[] _tabs = { "Overview", "Personality & Story", "Dialogue & Voice" };

        // Serialized properties
        private SerializedProperty _portraitSpriteProp;
        private SerializedProperty _characterNameProp;
        private SerializedProperty _jobDescriptionProp;
        private SerializedProperty _uniqueIdProp;
        private SerializedProperty _alignmentProp;
        private SerializedProperty _voiceTypeProp;
        private SerializedProperty _enableTTSProp;
        private SerializedProperty _personalityTraitsProp;
        private SerializedProperty _coreValueProp;
        private SerializedProperty _backgroundStoryProp;
        private SerializedProperty _memoriesProp;
        private SerializedProperty _characterOverrideProp;
        private SerializedProperty _situationalDialogueSOReferenceProp;

        private SerializedProperty _strengthProp;
        private SerializedProperty _dexterityProp;
        private SerializedProperty _constitutionProp;
        private SerializedProperty _intelligenceProp;
        private SerializedProperty _wisdomProp;
        private SerializedProperty _charismaProp;

        

        void OnEnable()
        {
            _persona = (iTalkNPCPersona)target;
            // Find properties
            _portraitSpriteProp = serializedObject.FindProperty("portraitSprite");
            _characterNameProp = serializedObject.FindProperty("characterName");
            _jobDescriptionProp = serializedObject.FindProperty("jobDescription");
            _uniqueIdProp = serializedObject.FindProperty("uniqueId");
            _alignmentProp = serializedObject.FindProperty("alignment");
            _voiceTypeProp = serializedObject.FindProperty("voiceType");
            _enableTTSProp = serializedObject.FindProperty("enableTTS");
            _personalityTraitsProp = serializedObject.FindProperty("personalityTraits");
            _coreValueProp = serializedObject.FindProperty("coreValue");
            _backgroundStoryProp = serializedObject.FindProperty("backgroundStory");
            _memoriesProp = serializedObject.FindProperty("memories");
            _characterOverrideProp = serializedObject.FindProperty("characterOverride");
            _situationalDialogueSOReferenceProp = serializedObject.FindProperty("situationalDialogueSOReference");
            _strengthProp = serializedObject.FindProperty("strength");
            _dexterityProp = serializedObject.FindProperty("dexterity");
            _constitutionProp = serializedObject.FindProperty("constitution");
            _intelligenceProp = serializedObject.FindProperty("intelligence");
            _wisdomProp = serializedObject.FindProperty("wisdom");
            _charismaProp = serializedObject.FindProperty("charisma");


        }



        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField(_persona.name, EditorStyles.boldLabel); // Show asset name
            GUILayout.Space(5);

            if (GUILayout.Button("Build Master Persona Summary & Full Prompt", GUILayout.Height(30)))
            {
                // Provide more relevant context for prompt building if possible, or keep generic for editor test.
                _persona.BuildPrompt("current location", "current situation", "player interaction context");
                EditorUtility.SetDirty(_persona);
                AssetDatabase.SaveAssets(); // Ensure SO is saved
                Debug.Log($"[Persona Editor] Prompt Rebuilt for {_persona.characterName}");
            }
            GUILayout.Space(10);

            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);
            GUILayout.Space(10);

            switch (_selectedTab)
            {
                case 0: DrawOverviewTab(); break;
                case 1: DrawPersonalityStoryTab(); break;
                case 2: DrawDialogueVoiceTab(); break;
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                 // If properties changed, rebuild prompt automatically or remind user
                // _persona.BuildPrompt(...); // Auto-rebuild (can be intensive)
                // EditorUtility.SetDirty(_persona);
                // AssetDatabase.SaveAssets();
                // For now, let user click the button to control rebuilds explicitly.
            }
        }

        private void DrawOverviewTab()
        {
            EditorGUILayout.LabelField("Core Identity & Abilities", EditorStyles.largeLabel);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // --- Left: Portrait ---
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Portrait", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.PropertyField(_portraitSpriteProp, GUIContent.none, GUILayout.Width(200), GUILayout.Height(20));
            Rect previewRect = GUILayoutUtility.GetAspectRect(1.0f / 1.2f, GUILayout.Width(200));
            if (_persona.portraitSprite != null && _persona.portraitSprite.texture != null)
                EditorGUI.DrawPreviewTexture(previewRect, _persona.portraitSprite.texture, null, ScaleMode.StretchToFill);
            else
                EditorGUI.HelpBox(previewRect, "No Portrait", MessageType.None);
            EditorGUILayout.EndVertical();

            GUILayout.Space(20);

            // --- Right: Info Block ---
            EditorGUILayout.BeginVertical();

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
            CenteredLabelField("Overview");

            CenteredTextField(_characterNameProp, "Character Name");
            CenteredTextField(_jobDescriptionProp, "Job Description");
            CenteredTextField(_uniqueIdProp, "Unique ID");
            CenteredEnumPopup(_alignmentProp, "Alignment");

            GUILayout.Space(15);
            CenteredLabelField("Ability Scores");
            GUILayout.Space(5);

            // --- Ability Row (centered single-line row) ---
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawAbility("STR", _strengthProp);
            DrawAbility("DEX", _dexterityProp);
            DrawAbility("CON", _constitutionProp);
            DrawAbility("INT", _intelligenceProp);
            DrawAbility("WIS", _wisdomProp);
            DrawAbility("CHA", _charismaProp);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();



            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                !string.IsNullOrWhiteSpace(_persona.masterPersonaSummary)
                    ? _persona.masterPersonaSummary
                    : "Summary not built. Click 'Build Master Persona Summary & Full Prompt'.",
                MessageType.Info);

            GUILayout.Space(5);
            EditorGUILayout.HelpBox(
                !string.IsNullOrWhiteSpace(_persona.builtPrompt)
                    ? (_persona.builtPrompt.Length > 500 ? _persona.builtPrompt.Substring(0, 500) + "..." : _persona.builtPrompt)
                    : "Prompt not built. Click 'Build Master Persona Summary & Full Prompt'.",
                MessageType.Info);
        }
        private void DrawAbility(string label, SerializedProperty prop)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(40));
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(40));
            EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.Width(40));
            EditorGUILayout.EndVertical();
        }

        private void CenteredLabelField(string label)
        {
            GUIStyle centeredLabel = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField(label, centeredLabel, GUILayout.ExpandWidth(true));
        }

        private void CenteredTextField(SerializedProperty prop, string label)
        {
            GUIStyle centeredLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter };
            GUIStyle centeredText = new GUIStyle(EditorStyles.textField) { alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField(label, centeredLabel, GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (prop.propertyType == SerializedPropertyType.String)
            {
                prop.stringValue = EditorGUILayout.TextField(prop.stringValue, centeredText, GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));
            }
            else
            {
                EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // Helper for centered enum popup - REMOVED width parameter
        private void CenteredEnumPopup(SerializedProperty prop, string label)
        {
            GUIStyle centeredLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField(label, centeredLabel, GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }


        private void DrawPersonalityStoryTab()
        {
            EditorGUILayout.LabelField("Character Depth & Nuances", EditorStyles.largeLabel);
            GUILayout.Space(5);

            EditorGUILayout.LabelField("Character Override (Advanced)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("If filled, this text will be used as the primary instruction for the AI, largely overriding other persona fields below for prompt generation. The AI will still follow general directives like staying in character.", MessageType.Info);
            _characterOverrideProp.stringValue = EditorGUILayout.TextArea(_characterOverrideProp.stringValue, GUILayout.MinHeight(100), GUILayout.ExpandHeight(true));
            
            GUILayout.Space(10);
            // --- Add HelpBox if Override is used ---
            if (!string.IsNullOrWhiteSpace(_characterOverrideProp.stringValue))
            {
                EditorGUILayout.HelpBox("Character Override is active. The fields below (Personality Traits, Core Value, Background, Memories) will generally be bypassed for AI prompt generation. Ensure your override contains all necessary character details.", MessageType.Warning);
            }
            GUILayout.Space(5);

            EditorGUILayout.PropertyField(_personalityTraitsProp, new GUIContent("Personality Traits", "Comma-separated, max 5 (e.g. Kind, Patient, Witty)"));
            EditorGUILayout.PropertyField(_coreValueProp, new GUIContent("Core Value / Motto", "A guiding principle for the character."));
            
            EditorGUILayout.LabelField("Background Story", EditorStyles.boldLabel);
            _backgroundStoryProp.stringValue = EditorGUILayout.TextArea(_backgroundStoryProp.stringValue, GUILayout.MinHeight(60), GUILayout.ExpandHeight(true));
            
            EditorGUILayout.LabelField("Key Memories / Traumas", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_memoriesProp, true); // 'true' to allow editing children if it's a list of complex types
        }

        private void DrawDialogueVoiceTab()
        {
            EditorGUILayout.LabelField("Voice & Situational Dialogue", EditorStyles.largeLabel);
            GUILayout.Space(5);

            EditorGUILayout.PropertyField(_voiceTypeProp, new GUIContent("Voice Type (for TTS)"));
            
VoiceType currentVoiceType = (VoiceType)_voiceTypeProp.enumValueIndex;
if (iTalkVoiceData.VoiceDetails.TryGetValue(currentVoiceType, out VoiceDetail detail))
{
    EditorGUILayout.HelpBox($"Description: {detail.Description}\n\nTypical RPG Uses:\n{detail.RpgUses}", MessageType.Info);
}
else
{
    EditorGUILayout.HelpBox("Select a voice type to see its description and typical uses.", MessageType.None);
}

            EditorGUILayout.PropertyField(_enableTTSProp, new GUIContent("Enable TTS for AI Responses"));
            
            GUILayout.Space(15);
            EditorGUILayout.Separator();
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Situational Dialogue Scriptable Object", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Create Dialogue an iTalkSituationDialogueSO in Dialogue List Tab).", MessageType.Info);
            EditorGUILayout.PropertyField(_situationalDialogueSOReferenceProp, new GUIContent("Dialogue SO Reference"));
            

        }

        private bool _showSituationSOFoldout = true; // Keep it expanded by default

        private void DrawSituationDialogueSOInspector()
        {
            var so = _persona.situationalDialogueSOReference; // Direct reference from the persona instance

            if (so == null)
            {
                _showSituationSOFoldout = EditorGUILayout.Foldout(_showSituationSOFoldout, "Assign or Create Dialogue SO", true, EditorStyles.foldoutHeader);
                if (!_showSituationSOFoldout) return;

                EditorGUILayout.HelpBox("No Situation Dialogue SO assigned. Assign one or click below to auto-create.", MessageType.Warning);
                if (GUILayout.Button("Create & Assign New Situation Dialogue SO"))
                {
                    CreateAndAssignSituationSO();
                }
            }
            else
            {
                _showSituationSOFoldout = EditorGUILayout.Foldout(_showSituationSOFoldout, $"Edit '{so.name}' Details", true, EditorStyles.foldoutHeader);
                if (!_showSituationSOFoldout) return;

                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);
                // Create a new editor for the SO and draw it.
                Editor.CreateCachedEditor(so, null, ref _dialogueSOEditor);
                if (_dialogueSOEditor != null)
                {
                    _dialogueSOEditor.OnInspectorGUI();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        private Editor _dialogueSOEditor; // Cache for the SO editor

        private void CreateAndAssignSituationSO()
        {
            string personaNameSanitized = string.Join("_", _persona.characterName.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(personaNameSanitized)) personaNameSanitized = "NewNPC";

            string folderPath = "Assets/Resources/NPCSituationDialogue"; // Using Resources for runtime loading if needed
            if (!Directory.Exists(Path.Combine(Application.dataPath, folderPath.Replace("Assets/", ""))))
            {
                 // Ensure parent folders exist
                string resourcesFolder = Path.Combine(Application.dataPath, "Resources");
                if (!Directory.Exists(resourcesFolder)) Directory.CreateDirectory(resourcesFolder);
                
                string fullFolderPath = Path.Combine(Application.dataPath, folderPath.Replace("Assets/", ""));
                Directory.CreateDirectory(fullFolderPath);
                AssetDatabase.Refresh(); // Refresh to show new folder in Unity editor
            }

            string assetName = $"{personaNameSanitized}_SituationDialogue.asset";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, assetName));
            
            var newSO = ScriptableObject.CreateInstance<iTalkSituationDialogueSO>();
            AssetDatabase.CreateAsset(newSO, assetPath);
            AssetDatabase.SaveAssets();
            // AssetDatabase.Refresh(); // Done by SetDirty and SaveAssets usually

            _situationalDialogueSOReferenceProp.objectReferenceValue = newSO; // Assign via SerializedProperty
            // _persona.situationalDialogueSOReference = newSO; // Also set direct reference for immediate use if needed before ApplyModifiedProperties
            
            EditorUtility.SetDirty(_persona); // Mark persona as dirty to save the new SO reference
            AssetDatabase.SaveAssets(); // Save the persona asset

            Debug.Log($"Created new Situation Dialogue SO at {assetPath} and assigned to persona '{_persona.characterName}'.");
            Selection.activeObject = newSO; // Select the newly created SO
        }

        // DrawSOArray and DrawSOField are not needed if using embedded editor for the SO.
        // The embedded editor will handle drawing its own properties.
        void OnDisable()
        {
             // Clean up the cached editor when this editor is disabled or destroyed
            if (_dialogueSOEditor != null)
            {
                DestroyImmediate(_dialogueSOEditor);
                _dialogueSOEditor = null;
            }
        }

        // Helper for centered text fields - REMOVED width parameter

    }
}
#endif