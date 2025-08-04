// CameraViewController.cs (Refactored for new NPC system)
using UnityEngine;
using Cinemachine;
using System.Collections.Generic;
using TMPro;

namespace CelestialCyclesSystem
{
    public class CameraViewController : MonoBehaviour
    {
        public CinemachineVirtualCamera closeViewCamera;
        public CinemachineVirtualCamera fullViewCamera;
        public CinemachineVirtualCamera worldViewCamera;
        public TextMeshProUGUI charName;
        public Celestial_NPC_Manager npcManager;

        private List<Transform> combinedCharacters = new();
        private Stack<Transform> selectionHistory = new();

        private void Start()
        {
            npcManager = FindObjectOfType<Celestial_NPC_Manager>();
            UpdateCombinedCharactersList();

            if (combinedCharacters.Count > 0)
            {
                ChangeFollowTarget(combinedCharacters[0]);
                selectionHistory.Push(combinedCharacters[0]);
            }

            closeViewCamera.Priority = 10;
            fullViewCamera.Priority = 1;
            worldViewCamera.Priority = 1;
            if (closeViewCamera.LookAt != null) charName.text = closeViewCamera.LookAt.name;
        }

        private void UpdateCombinedCharactersList()
        {
            combinedCharacters.Clear();
            foreach (var npc in npcManager.allNPCs) // allNPCs includes all subtypes via base class
            {
                if (npc != null) combinedCharacters.Add(npc.transform);
            }
        }

        public void ChangeCloseViewCamera()
        {
            closeViewCamera.Priority = 10;
            fullViewCamera.Priority = 1;
            worldViewCamera.Priority = 1;
        }

        public void ChangeFullViewCamera()
        {
            closeViewCamera.Priority = 1;
            fullViewCamera.Priority = 10;
            worldViewCamera.Priority = 1;
        }

        public void ChangeWorldViewCamera()
        {
            closeViewCamera.Priority = 1;
            fullViewCamera.Priority = 1;
            worldViewCamera.Priority = 10;
        }

        public void NextCharacter()
        {
            if (combinedCharacters.Count == 0) return;

            Transform nextCharacter = GetRandomCharacterDifferentFromCurrent();
            ChangeFollowTarget(nextCharacter);
            selectionHistory.Push(nextCharacter);
        }

        public void PreviousCharacter()
        {
            if (selectionHistory.Count <= 1) return;

            selectionHistory.Pop(); // Remove current
            Transform previousCharacter = selectionHistory.Peek();
            ChangeFollowTarget(previousCharacter);
        }

        private Transform GetRandomCharacterDifferentFromCurrent()
        {
            if (selectionHistory.Count == 0) return combinedCharacters[Random.Range(0, combinedCharacters.Count)];

            Transform currentCharacter = selectionHistory.Peek();
            Transform nextCharacter;
            do
            {
                nextCharacter = combinedCharacters[Random.Range(0, combinedCharacters.Count)];
            } while (nextCharacter == currentCharacter && combinedCharacters.Count > 1);

            return nextCharacter;
        }

        private void ChangeFollowTarget(Transform target)
        {
            string characterType = "NPC"; // Default

            // Check subtypes
            if (target.TryGetComponent<Celestial_NPC_Patrol>(out _))
            {
                characterType = "Patrol";
            }
            else if (target.TryGetComponent<Celestial_NPC_Merchant>(out _))
            {
                characterType = "Merchant";
            }
            // Roamer or base NPC remains "NPC"

            closeViewCamera.Follow = target;
            closeViewCamera.LookAt = target;
            fullViewCamera.Follow = target;
            fullViewCamera.LookAt = target;
            worldViewCamera.Follow = target;
            worldViewCamera.LookAt = target;

            charName.text = $"{target.name} ({characterType})";
        }
    }
}