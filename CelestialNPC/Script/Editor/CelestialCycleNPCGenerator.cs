using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;

namespace CelestialCyclesSystem
{
    public class CelestialCycleNPCGenerator : EditorWindow
    {
        private GameObject managerObj;
        Celestial_NPC_Manager generator;
        Celestial_Object_Manager objectManager;
        public List<Celestial_Areas> allBounds;
        public List<Celestial_Areas> allPaths;
        private Editor npcManager;
        private Vector2 scrollPosition;
        private int selectedMainTabIndex = 0;
        private int selectedNPCTabIndex = 0;
        private int selectedPatrolTabIndex = 0;
        private string[] mainTabs = new string[] { "NPCs", "Patrol", "Object" };
        private string[] objectTabs = new string[] { "Chair", "Foods", "FoodStand", "Bed" };
        private int selectedTabIndex = 0;
        private int selectedObjectTabIndex = 0;
        private string[] boundNames;
        private string[] pathNames;
        private GameObject roamerPrefab;
        private GameObject merchantPrefab;
        private GameObject patrolPrefab;
        private RuntimeAnimatorController npcAnimator;
        private int npcCount = 1;
        private string newAreaName = "";
        private int selectedNPCType = 0; // 0: Roamer, 1: Merchant for Bound areas
        private string selectedStaminaHudType = "None";
        private List<string> staminaHudNames = new List<string>();
        private List<GameObject> staminaHudObjects = new List<GameObject>();

        [MenuItem("Window/CelestialCycle/Eco Life Manager")]
        static public void ShowWindow()
        {
            CelestialCycleNPCGenerator window = EditorWindow.GetWindow<CelestialCycleNPCGenerator>();
            window.titleContent.text = "Eco Life Manager";
        }

        void OnEnable()
        {
            RefreshEditors();
            LoadStaminaHudTypes();
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical("box");
            {
                DrawHeader();
            }
            GUILayout.EndVertical();

            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.5f, 0.5f, 0.75f, 1f);
            headerStyle.margin = new RectOffset(0, 0, 10, 10);
            GUILayout.BeginVertical("box");
            {
                GUILayout.Label("Celestial Cycle Eco Life Manager", headerStyle);
            }
            GUILayout.EndVertical();

            generator = FindAnyObjectByType<Celestial_NPC_Manager>();
            if (generator == null)
            {
                GUILayout.Label("Eco Life Manager GameObject not found.");
                if (GUILayout.Button("Create Eco Life Manager"))
                {
                    GameObject ecomManager = Resources.Load<GameObject>("Celestial Eco Life Manager");
                    managerObj = GameObject.Instantiate(ecomManager);
                }
                return;
            }

            objectManager = FindAnyObjectByType<Celestial_Object_Manager>();
            if (objectManager == null)
            {
                GUILayout.Label("Celestial Object Manager GameObject not found.");
                return;
            }

            EditorGUILayout.BeginHorizontal();
            newAreaName = EditorGUILayout.TextField("New Area Name:", newAreaName);
            if (GUILayout.Button("Spawn Area", GUILayout.ExpandWidth(false)))
            {
                SpawnNewArea(newAreaName, false);
                UpdateAreaNames(generator);
            }
            if (GUILayout.Button("Spawn Patrol Path", GUILayout.ExpandWidth(false)))
            {
                SpawnNewArea(newAreaName, true);
                UpdateAreaNames(generator);
            }
            if (GUILayout.Button("Refresh", GUILayout.ExpandWidth(false)))
            {
                UpdateAreaNames(generator);
                LoadStaminaHudTypes();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            int selectedStatusIndex = staminaHudNames.IndexOf(selectedStaminaHudType);
            int newStatusIndex = EditorGUILayout.Popup("Stamina HUD", selectedStatusIndex, staminaHudNames.ToArray());
            if (newStatusIndex != selectedStatusIndex)
            {
                selectedStaminaHudType = staminaHudNames[newStatusIndex];
                SetStatusForAll(selectedStaminaHudType);
            }
            EditorGUILayout.EndHorizontal();

            selectedMainTabIndex = GUILayout.Toolbar(selectedMainTabIndex, mainTabs);
            switch (selectedMainTabIndex)
            {
                case 0: // NPCs
                    DrawNPCTab();
                    break;
                case 1: // Patrol
                    DrawPatrolTab();
                    break;
                case 2: // Objects
                    DrawObjectTab();
                    break;
            }
        }

        private void UpdateAreaNames(Celestial_NPC_Manager generator)
        {
            allBounds = generator.boundArea;
            boundNames = generator.boundArea.Select(area => area.name).ToArray();
            allPaths = generator.pathArea;
            pathNames = generator.pathArea.Select(area => area.name).ToArray();
        }

        private void DrawNPCTab()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            string[] boundAreaNames = allBounds.Select(area => area.name).ToArray();
            selectedNPCTabIndex = GUILayout.Toolbar(selectedNPCTabIndex, boundAreaNames);

            int areaIndex = selectedNPCTabIndex;
            if (areaIndex >= 0 && areaIndex < boundAreaNames.Length)
            {
                Celestial_Areas selectedArea = generator.boundArea.FirstOrDefault(area => area.name == boundNames[areaIndex]);
                if (selectedArea != null)
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                    EditorGUILayout.BeginVertical("box");
                    GUILayout.Label("Add Celestial NPC", headerStyle);
                    DrawNPCPrefabManagementArea(selectedArea);
                    EditorGUILayout.EndVertical();
                    DrawNPCsForArea(selectedArea);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawPatrolTab()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            string[] pathAreaNames = allPaths.Select(area => area.name).ToArray();
            selectedPatrolTabIndex = GUILayout.Toolbar(selectedPatrolTabIndex, pathAreaNames);

            int areaIndex = selectedPatrolTabIndex;
            if (areaIndex >= 0 && areaIndex < pathAreaNames.Length)
            {
                Celestial_Areas selectedArea = generator.pathArea.FirstOrDefault(area => area.name == pathNames[areaIndex]);
                if (selectedArea != null)
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                    EditorGUILayout.BeginVertical("box");
                    GUILayout.Label("Add Celestial Patrol", headerStyle);
                    DrawNPCPrefabManagementArea(selectedArea);
                    EditorGUILayout.EndVertical();
                    DrawNPCsForArea(selectedArea);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawObjectTab()
        {
            selectedObjectTabIndex = GUILayout.Toolbar(selectedObjectTabIndex, objectTabs);
            switch (selectedObjectTabIndex)
            {
                case 0: // Chairs
                    DrawAllChairs();
                    break;
                case 1: // Foods
                    DrawAllFoods();
                    break;
                case 2: // FoodStands
                    DrawAllFoodstands();
                    break;
                case 3: // Beds
                    DrawAllBeds();
                    break;
            }
        }

        private void SetStatusForAll(string hudType)
        {
            foreach (Celestial_NPC npc in generator.allNPCs)
            {
                Celestial_NPC_Stamina stamina = npc.GetComponent<Celestial_NPC_Stamina>();
                if (stamina != null)
                {
                    stamina.staminaHudType = hudType;
                }
            }
        }

        private void DrawNPCsForArea(Celestial_Areas area)
        {
            string npcParentName = area.name;
            GameObject npcParentObject = GameObject.Find(npcParentName);

            if (npcParentObject == null)
            {
                EditorGUILayout.HelpBox("No NPC parent object found for " + area.name, MessageType.Info);
                return;
            }

            Transform[] allChildren = npcParentObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child == npcParentObject.transform) continue;

                Celestial_NPC npc = child.GetComponent<Celestial_NPC>();
                if (npc != null)
                {
                    Celestial_NPC_Stamina stamina = npc.GetComponent<Celestial_NPC_Stamina>();
                    if (stamina == null) continue;

                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginVertical("box");
                    if (GUILayout.Button(npc.name, EditorStyles.linkLabel))
                    {
                        Selection.activeGameObject = npc.gameObject;
                    }

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    float newMovementSpeed = EditorGUILayout.FloatField("Movement Speed", npc.movementSpeed);
                    EditorGUILayout.EnumFlagsField(npc.currentState);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    float newMaxStamina = EditorGUILayout.FloatField("Max Stamina", stamina.maxStamina);
                    float newCurrentStamina = EditorGUILayout.FloatField("Current Stamina", stamina.currentStamina);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    int selectedHudIndex = staminaHudNames.IndexOf(stamina.staminaHudType);
                    int newHudIndex = EditorGUILayout.Popup("Stamina HUD", selectedHudIndex, staminaHudNames.ToArray());
                    string newHudType = staminaHudNames[newHudIndex];
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Label($"Current Stamina: {newCurrentStamina} / {newMaxStamina}");
                    Rect rect = GUILayoutUtility.GetRect(50, 20);
                    EditorGUI.ProgressBar(rect, newCurrentStamina / newMaxStamina, "Stamina");
                    if (GUI.changed) Repaint();

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(npc, "NPC Property Change");
                        Undo.RecordObject(stamina, "Stamina Property Change");
                        npc.movementSpeed = newMovementSpeed;
                        stamina.maxStamina = newMaxStamina;
                        stamina.currentStamina = newCurrentStamina;
                        stamina.staminaHudType = newHudType;
                        EditorUtility.SetDirty(npc);
                        EditorUtility.SetDirty(stamina);
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawAllChairs()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical("box");
            if (Selection.activeGameObject != null)
            {
                EditorGUILayout.BeginHorizontal();
                GameObject obj = Selection.activeGameObject;
                GUILayout.Label("Conversion Tool: " + obj.name + " -> To Chair", headerStyle);
                if (obj.GetComponent<Celestial_Object_Chair>() == null)
                {
                    if (GUILayout.Button("Convert", GUILayout.Width(200), GUILayout.Height(30)))
                    {
                        Undo.AddComponent<Celestial_Object_Chair>(obj);
                        EditorUtility.SetDirty(obj);
                        Debug.Log($"{obj.name} has been changed to Celestial Chair.", obj);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"{obj.name} is already a Celestial Chair.", MessageType.Info);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Please select a GameObject in the scene.", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();

            foreach (Celestial_Object_Chair chair in objectManager.celestialChairs)
            {
                EditorGUILayout.BeginVertical("box");
                if (GUILayout.Button(chair.name, GUILayout.Width(200)))
                {
                    Selection.activeGameObject = chair.gameObject;
                }

                EditorGUI.BeginChangeCheck();
                SphereCollider objCollider = chair.GetComponent<SphereCollider>();
                EditorGUILayout.BeginHorizontal();
                float newUseDuration = EditorGUILayout.FloatField("Use Duration", chair.useDuration);
                float newRadius = EditorGUILayout.FloatField("Collider Radius", objCollider.radius);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                int newQueueSize = EditorGUILayout.IntField("Max Queue Size", chair.maxQueueSize);
                float newStamPS = EditorGUILayout.FloatField("Stamina Recover/Sec", chair.recoverAmount);
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(chair, "Object Property Change");
                    chair.useDuration = newUseDuration;
                    objCollider.radius = newRadius;
                    chair.maxQueueSize = newQueueSize;
                    chair.recoverAmount = newStamPS;
                    EditorUtility.SetDirty(chair);
                    EditorUtility.SetDirty(objCollider);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawAllFoods()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical("box");
            if (Selection.activeGameObject != null)
            {
                EditorGUILayout.BeginHorizontal();
                GameObject obj = Selection.activeGameObject;
                GUILayout.Label("Conversion Tool: " + obj.name + " -> To Food", headerStyle);
                if (obj.GetComponent<Celestial_Object_Food>() == null)
                {
                    if (GUILayout.Button("Convert", GUILayout.Width(200), GUILayout.Height(30)))
                    {
                        Undo.AddComponent<Celestial_Object_Food>(obj);
                        EditorUtility.SetDirty(obj);
                        Debug.Log($"{obj.name} has been changed to Celestial Food.", obj);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"{obj.name} is already a Celestial Food.", MessageType.Info);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Please select a GameObject in the scene.", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();

            foreach (Celestial_Object_Food food in objectManager.celestialFoods)
            {
                EditorGUILayout.BeginVertical("box");
                if (GUILayout.Button(food.name, GUILayout.Width(200)))
                {
                    Selection.activeGameObject = food.gameObject;
                }

                EditorGUI.BeginChangeCheck();
                SphereCollider objCollider = food.GetComponent<SphereCollider>();
                EditorGUILayout.BeginHorizontal();
                float newUseDuration = EditorGUILayout.FloatField("Use Duration", food.useDuration);
                float newRadius = EditorGUILayout.FloatField("Collider Radius", objCollider.radius);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                float newStamI = EditorGUILayout.FloatField("Stamina Recover", food.recoverAmount);
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(food, "Object Property Change");
                    food.useDuration = newUseDuration;
                    objCollider.radius = newRadius;
                    food.recoverAmount = newStamI;
                    EditorUtility.SetDirty(food);
                    EditorUtility.SetDirty(objCollider);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawAllFoodstands()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical("box");
            if (Selection.activeGameObject != null)
            {
                EditorGUILayout.BeginHorizontal();
                GameObject obj = Selection.activeGameObject;
                GUILayout.Label("Conversion Tool: " + obj.name + " -> To Food Stand", headerStyle);
                if (obj.GetComponent<Celestial_Object_FoodStand>() == null)
                {
                    if (GUILayout.Button("Convert", GUILayout.Width(200), GUILayout.Height(30)))
                    {
                        Undo.AddComponent<Celestial_Object_FoodStand>(obj);
                        EditorUtility.SetDirty(obj);
                        Debug.Log($"{obj.name} has been changed to Celestial Food Stand.", obj);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"{obj.name} is already a Celestial Food Stand.", MessageType.Info);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Please select a GameObject in the scene.", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();

            foreach (Celestial_Object_FoodStand obj in objectManager.celestialFoodStands)
            {
                EditorGUILayout.BeginVertical("box");
                if (GUILayout.Button(obj.name, GUILayout.Width(200)))
                {
                    Selection.activeGameObject = obj.gameObject;
                }

                EditorGUI.BeginChangeCheck();
                SphereCollider objCollider = obj.GetComponent<SphereCollider>();
                EditorGUILayout.BeginHorizontal();
                float newUseDuration = EditorGUILayout.FloatField("Use Duration", obj.useDuration);
                float newRadius = EditorGUILayout.FloatField("Collider Radius", objCollider.radius);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                int newQueueSize = EditorGUILayout.IntField("Max Queue Size", obj.maxQueueSize);
                float newStamI = EditorGUILayout.FloatField("Stamina Recover", obj.recoverAmount);
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(obj, "Object Property Change");
                    obj.useDuration = newUseDuration;
                    objCollider.radius = newRadius;
                    obj.maxQueueSize = newQueueSize;
                    obj.recoverAmount = newStamI;
                    EditorUtility.SetDirty(obj);
                    EditorUtility.SetDirty(objCollider);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawAllBeds()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical("box");
            if (Selection.activeGameObject != null)
            {
                EditorGUILayout.BeginHorizontal();
                GameObject obj = Selection.activeGameObject;
                GUILayout.Label("Conversion Tool: " + obj.name + " -> To Bed", headerStyle);
                if (obj.GetComponent<Celestial_Object_Home>() == null)
                {
                    if (GUILayout.Button("Convert", GUILayout.Width(200), GUILayout.Height(30)))
                    {
                        Undo.AddComponent<Celestial_Object_Home>(obj);
                        EditorUtility.SetDirty(obj);
                        Debug.Log($"{obj.name} has been changed to Celestial Bed.", obj);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"{obj.name} is already a Celestial Bed.", MessageType.Info);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Please select a GameObject in the scene.", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();

            foreach (Celestial_Object_Home obj in objectManager.celestialBeds)
            {
                EditorGUILayout.BeginVertical("box");
                if (GUILayout.Button(obj.name, GUILayout.Width(200)))
                {
                    Selection.activeGameObject = obj.gameObject;
                }

                EditorGUI.BeginChangeCheck();
                SphereCollider objCollider = obj.GetComponent<SphereCollider>();
                EditorGUILayout.BeginHorizontal();
                float newRadius = EditorGUILayout.FloatField("Collider Radius", objCollider.radius);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                int newQueueSize = EditorGUILayout.IntField("Max Queue Size", obj.maxQueueSize);
                float newStamPS = EditorGUILayout.FloatField("Stamina Recover/Sec", obj.recoverAmount);
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(obj, "Object Property Change");
                    objCollider.radius = newRadius;
                    obj.maxQueueSize = newQueueSize;
                    obj.recoverAmount = newStamPS;
                    EditorUtility.SetDirty(obj);
                    EditorUtility.SetDirty(objCollider);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void GetNPCsInChildren(Transform parent, List<Celestial_NPC> npcsList)
        {
            foreach (Transform child in parent)
            {
                Celestial_NPC npc = child.GetComponent<Celestial_NPC>();
                if (npc != null)
                {
                    npcsList.Add(npc);
                }
                if (child.childCount > 0)
                {
                    GetNPCsInChildren(child, npcsList);
                }
            }
        }

        private void DrawNPCPrefabManagementArea(Celestial_Areas selectedArea)
        {
            npcAnimator = (RuntimeAnimatorController)EditorGUILayout.ObjectField("Animator Controller", npcAnimator, typeof(RuntimeAnimatorController), false);
            npcCount = EditorGUILayout.IntSlider("Number of NPCs", npcCount, 1, 20);

            GameObject prefabToDisplay = null;
            string label = "NPC Prefab";

            if (selectedArea.areaType == Celestial_Areas.AreaType.Bound)
            {
                string[] npcTypes = { "Roamer", "Merchant" };
                selectedNPCType = EditorGUILayout.Popup("NPC Type", selectedNPCType, npcTypes);
                if (selectedNPCType == 0)
                {
                    label = "Roamer Prefab";
                    prefabToDisplay = roamerPrefab;
                }
                else
                {
                    label = "Merchant Prefab";
                    prefabToDisplay = merchantPrefab;
                }
            }
            else
            {
                label = "Patrol Prefab";
                prefabToDisplay = patrolPrefab;
            }

            GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(label, prefabToDisplay, typeof(GameObject), false);
            if (newPrefab != prefabToDisplay)
            {
                if (selectedArea.areaType == Celestial_Areas.AreaType.Bound)
                {
                    if (selectedNPCType == 0) roamerPrefab = newPrefab;
                    else merchantPrefab = newPrefab;
                }
                else
                {
                    patrolPrefab = newPrefab;
                }
            }

            if (GUILayout.Button("Spawn NPCs"))
            {
                SpawnNPCsInArea(selectedArea);
            }
        }

        private void SpawnNPCsInArea(Celestial_Areas selectedArea)
        {
            GameObject prefabToUse;
            if (selectedArea.areaType == Celestial_Areas.AreaType.Bound)
            {
                prefabToUse = selectedNPCType == 0 ? roamerPrefab : merchantPrefab;
            }
            else
            {
                prefabToUse = patrolPrefab;
            }

            if (prefabToUse == null)
            {
                Debug.LogWarning("NPC Prefab is not assigned.");
                return;
            }

            Undo.SetCurrentGroupName("Spawn NPCs");
            int group = Undo.GetCurrentGroup();
            HashSet<Vector3> usedPositions = new HashSet<Vector3>();
            for (int i = 0; i < npcCount; i++)
            {
                Vector3 spawnPosition;
                int safetyCounter = 0;
                bool positionFound = false;
                do
                {
                    spawnPosition = selectedArea.GetRandomPositionWithinBounds();
                    spawnPosition += new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));

                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(spawnPosition, out hit, 1.0f, NavMesh.AllAreas))
                    {
                        spawnPosition = hit.position;
                        positionFound = true;
                    }

                    if (++safetyCounter > 100)
                    {
                        Debug.LogWarning("Unable to find a unique spawn position for NPC on the NavMesh.");
                        break;
                    }
                } while (!positionFound || usedPositions.Contains(spawnPosition));

                if (!positionFound) continue;

                usedPositions.Add(spawnPosition);

                GameObject newNPC = PrefabUtility.InstantiatePrefab(prefabToUse) as GameObject;
                if (newNPC != null)
                {
                    newNPC.transform.position = spawnPosition;
                    newNPC.transform.SetParent(selectedArea.transform, true);
                    Celestial_NPC npcComponent = newNPC.GetComponent<Celestial_NPC>();
                    Celestial_NPC_Stamina staminaComponent = newNPC.GetComponent<Celestial_NPC_Stamina>() ?? Undo.AddComponent<Celestial_NPC_Stamina>(newNPC);
                    Rigidbody rigid = newNPC.GetComponent<Rigidbody>() ?? Undo.AddComponent<Rigidbody>(newNPC);
                    CapsuleCollider capsuleCollider = newNPC.GetComponent<CapsuleCollider>() ?? Undo.AddComponent<CapsuleCollider>(newNPC);
                    Animator newAnimator = newNPC.GetComponent<Animator>() ?? Undo.AddComponent<Animator>(newNPC);

                    newAnimator.runtimeAnimatorController = npcAnimator;
                    npcComponent.celestialAreas = selectedArea;
                    staminaComponent.staminaHudType = selectedStaminaHudType; // Assign selected HUD type
                    Undo.RegisterCreatedObjectUndo(newNPC, "Create NPC");
                }
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log($"{npcCount} NPCs spawned in {selectedArea.name}.");
        }

        void RefreshEditors()
        {
            CelestialTimeManager environmentManager = FindObjectOfType<CelestialTimeManager>();
            if (environmentManager != null)
            {
                if (npcManager != null)
                {
                    DestroyImmediate(npcManager);
                }
                npcManager = Editor.CreateEditor(environmentManager);
            }
        }

        private void SpawnNewArea(string areaName, bool isPath)
        {
            if (string.IsNullOrEmpty(areaName))
            {
                Debug.LogWarning("Area name cannot be empty.");
                return;
            }

            Transform areasParent = managerObj.transform.Find("Areas");
            GameObject newArea = new GameObject(areaName);
            Undo.RegisterCreatedObjectUndo(newArea, "Create New Area");
            newArea.transform.SetParent(areasParent);

            Celestial_Areas celestialAreaComponent = newArea.AddComponent<Celestial_Areas>();
            if (isPath)
            {
                celestialAreaComponent.areaType = Celestial_Areas.AreaType.Path;
            }
            Debug.Log($"New area '{areaName}' spawned under Areas.");
        }

        public static void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Asset Store", EditorStyles.miniButton, GUILayout.Width(120)))
            {
                Application.OpenURL("https://assetstore.unity.com/publishers/41999");
            }
            if (GUILayout.Button("Documentation", EditorStyles.miniButton))
            {
                Application.OpenURL("https://docs.google.com/document/d/1RwEpgVMH7uIRCWsu2b3Bi4yRK1iiSg-8dByGHPyc7hM/edit");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void LoadStaminaHudTypes()
        {
            staminaHudNames.Clear();
            staminaHudObjects.Clear();

            staminaHudNames.Add("None");
            staminaHudObjects.Add(null);

            var statuses = Resources.LoadAll<GameObject>("Status");
            foreach (var status in statuses)
            {
                if (status.GetComponent<Celestial_HUD_Stamina>() != null)
                {
                    staminaHudNames.Add(status.name);
                    staminaHudObjects.Add(status);
                }
            }
        }
    }
}