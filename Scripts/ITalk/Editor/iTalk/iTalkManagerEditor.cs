#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
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
            if (manager == null) return;

            GUILayout.Space(10);
            EditorGUILayout.LabelField("News Management", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh News from Resources"))
            {
                manager.LoadNewsItemsFromResources();
                EditorUtility.SetDirty(manager); // Mark dirty to save changes if any news was loaded
            }
            
            if (manager.GetWorldNews() != null && manager.GetWorldNews().Count > 0)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField($"Loaded News Items: {manager.GetWorldNews().Count}", EditorStyles.miniLabel);
                
                // Display up to 3 news entries with proper handling of iTalkNewsItemEntry structure
                int displayCount = Mathf.Min(3, manager.GetWorldNews().Count);
                for (int i = 0; i < displayCount; i++)
                {
                    var newsItem = manager.GetWorldNews()[i];
                    
                    // Handle iTalkNewsItemEntry's list-based structure
                    if (newsItem.texts != null && newsItem.texts.Count > 0)
                    {
                        // Get the most recent news text from this entry
                        string newsText = newsItem.texts[0]; // First text entry
                        
                        // Get corresponding timestamp if available
                        string timestampInfo = "";
                        if (newsItem.timestamps != null && newsItem.timestamps.Count > 0)
                        {
                            var timestamp = newsItem.timestamps[0];
                            System.DateTime newsDate = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
                                .AddSeconds(timestamp).ToLocalTime();
                            timestampInfo = $" ({newsDate:MM/dd HH:mm})";
                        }
                        
                        // Truncate display text if too long
                        string displayText = newsText.Length > 45 ? newsText.Substring(0, 45) + "..." : newsText;
                        EditorGUILayout.LabelField($"- {displayText}{timestampInfo}", EditorStyles.miniLabel);
                        
                        // Show additional texts from this entry if any
                        if (newsItem.texts.Count > 1)
                        {
                            EditorGUILayout.LabelField($"  (+{newsItem.texts.Count - 1} more texts in this entry)", EditorStyles.miniLabel);
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"- {newsItem.name} (No text content)", EditorStyles.miniLabel);
                    }
                }
                
                // Show total text count across all entries
                int totalTexts = manager.GetWorldNews().Sum(entry => entry.texts?.Count ?? 0);
                if (totalTexts > displayCount)
                {
                    EditorGUILayout.LabelField($"  Total news texts: {totalTexts}", EditorStyles.miniLabel);
                }
            }
            else
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("No news items currently loaded.", EditorStyles.miniLabel);
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Token Management", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Current Token Usage (Est.): {manager.GetCurrentTokenUsage()} / {(manager.tokenQuotaPeriod > 0 ? manager.tokenQuotaPeriod.ToString() : "Unlimited")}");
            if (GUILayout.Button("Reset Token Quota Usage"))
            {
                manager.ResetTokenQuota();
                EditorUtility.SetDirty(manager);
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime Diagnostics", EditorStyles.boldLabel);
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField($"Registered iTalks: {manager.GetRegisteredTalkComponents().Count(c => c != null)}"); // Count non-null
                EditorGUILayout.LabelField($"Registered Controllers: {manager.GetRegisteredControllers().Count(c => c != null)}");
                EditorGUILayout.LabelField($"Currently Interactable NPCs: {manager.GetCurrentlyInteractableNPCs().Count}");
                if (GUILayout.Button("Force Update Interactable NPCs")) manager.UpdateInteractableNPCsList();

                if (manager.GetCurrentlyInteractableNPCs().Count > 0)
                {
                    EditorGUILayout.LabelField("Interactable NPCs:", EditorStyles.boldLabel);
                    foreach (var npc in manager.GetCurrentlyInteractableNPCs())
                    {
                        if (npc != null) EditorGUILayout.LabelField($"- {npc.EntityName} (State: {npc.GetInternalAvailability()})");
                    }
                }
                
                // Add SubManager diagnostics if available
                GUILayout.Space(5);
                var subManager = FindObjectOfType<iTalkNPCDialogueCoordinator>();
                if (subManager != null)
                {
                    EditorGUILayout.LabelField("NPC Conversations:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Available NPCs: {subManager.GetAvailableNPCCount()}");
                    EditorGUILayout.LabelField($"Busy NPCs: {subManager.GetBusyNPCCount()}");
                    EditorGUILayout.LabelField($"Active NPC Conversations: {subManager.GetActiveNPCConversationCount()}");
                    
                    if (GUILayout.Button("Force NPC Dialogue Check"))
                    {
                        subManager.ForceNPCDialogueCheck();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("SubManager: Not found in scene", EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Runtime diagnostics available in Play Mode.", MessageType.Info);
            }

            // Repaint inspector if values change frequently during play mode
            if (Application.isPlaying) Repaint();
        }
    }
}
#endif