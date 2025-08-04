// iTalkPlayerController.cs (Fixed: Automatic finding/attachment logic; removed INPCBase dependency; null checks)
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace CelestialCyclesSystem
{
    /// <summary>
    /// Handles player input to initiate interactions with NPCs, triggering dialogue via iTalk.
    /// Attached to the player GameObject automatically if not present.
    /// </summary>
    public class iTalkPlayerController : MonoBehaviour
    {
        [Header("Player Settings")]
        [Tooltip("Transform of the player. If null, uses this transform.")]
        [SerializeField] private Transform playerTransform;
        [Tooltip("Key to initiate dialogue with the closest NPC.")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Header("Interaction Settings")]
        [Tooltip("Maximum distance to detect interactable NPCs (should match iTalkManager.maxInteractionDistance).")]
        [SerializeField] private float interactionDistance = 20.0f;

        // Automatic attachment to player
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoAttachToPlayer()
        {
            // Find player object (assume tagged "Player" or by name; customize as needed)
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
            if (playerObject != null && playerObject.GetComponent<iTalkPlayerController>() == null)
            {
                playerObject.AddComponent<iTalkPlayerController>();
                Debug.Log("[iTalkPlayerController] Automatically attached to player object.");
            }
        }

        void Awake()
        {
            if (playerTransform == null)
                playerTransform = transform;
        }

        void Start()
        {
            if (iTalkManager.Instance != null)
            {
                iTalkManager.Instance.OnInteractableNPCsChanged += HandleInteractableNPCsChanged;
            }
            else
            {
                Debug.LogError("[iTalkPlayerController] iTalkManager not found! Player interactions will not work.");
                enabled = false;
                return;
            }
        }

        void OnDestroy()
        {
            if (iTalkManager.Instance != null)
            {
                iTalkManager.Instance.OnInteractableNPCsChanged -= HandleInteractableNPCsChanged;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(interactionKey) && !iTalkManager.Instance.IsInConversation())
            {
                TryInteractWithClosestNPC();
            }
        }

        private void TryInteractWithClosestNPC()
        {
            var interactableNPCs = iTalkManager.Instance.GetCurrentlyInteractableNPCs();
            if (interactableNPCs.Count == 0)
            {
                Debug.Log("[iTalkPlayerController] No interactable NPCs in range.");
                return;
            }

            iTalk closestNPC = null;
            float minDistance = float.MaxValue;
            Vector3 playerPos = playerTransform.position;

            foreach (var npc in interactableNPCs)
            {
                if (npc != null)
                {
                    float distance = Vector3.Distance(playerPos, npc.Position);
                    if (distance < minDistance && distance <= interactionDistance)
                    {
                        minDistance = distance;
                        closestNPC = npc;
                    }
                }
            }

            if (closestNPC != null)
            {
                if (iTalkManager.Instance.TryStartPlayerConversation(closestNPC))
                {
                    // Removed INPCBase dependency; assume interaction is triggered via iTalk
                    closestNPC.TriggerInteraction();
                    Debug.Log($"[iTalkPlayerController] Initiated interaction with {closestNPC.EntityName}.");
                }
                else
                {
                    iTalkManager.Instance.ShowTemporaryMessage($"Cannot start conversation with {closestNPC.EntityName}.", 3f);
                }
            }
        }

        private void HandleInteractableNPCsChanged(IReadOnlyList<iTalk> interactableNPCs)
        {
            if (interactableNPCs.Count > 0 && !iTalkManager.Instance.IsInConversation())
            {
                string npcNames = string.Join(", ", interactableNPCs.Select(n => n.EntityName));
                iTalkManager.Instance.ShowTemporaryMessage($"Nearby NPCs: {npcNames}", 2f);
                Debug.Log($"[iTalkPlayerController] Interactable NPCs: {npcNames}");
            }
        }
    }
}