using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

namespace CelestialCyclesSystem
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(AudioSource))]
    public abstract class Celestial_NPC : MonoBehaviour
    {
        public CelestialTimeManager timeManager;
        public Celestial_Object_Manager objManager;

        public enum NPCState
        {
            Idle, Sitting, Sleeping, Eating, Talking, Roaming, MovingToward, FollowingPath, WaitingInQueue
        }

        public NPCState currentState = NPCState.Roaming;

        [Header("Show Status:")]
        public bool justFinishedAction;
        public bool isSatisfied;
        public bool stopUpdate;

        [Header("Area Settings:")]
        public Celestial_Areas celestialAreas;

        [Header("Movement Attributes:")]
        [HideInInspector] public NavMeshAgent _navComponent;
        private Animator _animator;
        private Rigidbody _rigidBody;
        private CapsuleCollider _capCollider;

        public float movementRestTime = 3f;
        public float movementSpeed = 1f;
        private float saveMovementSpeed = 1f;
        private float pathCheckCooldown = 0f;
        private float pathCheckRate = 2f;
        public Transform queueLocation;

        [Header("Seat / Sleep Attributes:")]
        public float maxSearchDistance = 50f;
        public float wakeTime = 7f;
        public float sleepTime = 20f;

        [HideInInspector] public Celestial_Object_Home assignedHome;

        public float startTimeOfUse;
        public bool isStun = false;

         [HideInInspector]  public bool isSitting;
        [HideInInspector] public bool isSleeping;
        [HideInInspector] public bool isTalking;
        [HideInInspector] public bool isRoaming;
        [HideInInspector] public bool isFollowingPath;
        [HideInInspector] public bool IsMovingToObject;
        [HideInInspector] public bool isWaitingInQueue = false;

        [HideInInspector] public Celestial_NPC_Stamina stamina;

        // Integrated from Celestial_NPC_Talk
        [Header("Interaction Attributes:")]
        private Celestial_NPC talkingPartner;
        private Celestial_NPC_Patrol npcPatrol;
        private bool unableToTalk;
        public float talkStaminaCost = 5f;
        public float talkDistance = 1.5f;
        public float talkTimer = 3f;
        public float talkCooldown = 5f;
        public float audioDistance = 5f;
        private const float talkCooldownPeriod = 30f;

        private float _currentTalkTime = 0f;

        private List<Celestial_NPC> otherNPCs;
        private Dictionary<Celestial_NPC, float> lastTalkTime = new();

        [Header("Audio Attributes:")]
        public AudioClip[] talkAudioClips;
        private AudioSource audioSource;

        protected virtual void Awake()
        {
            timeManager = FindObjectOfType<CelestialTimeManager>();
            objManager = FindObjectOfType<Celestial_Object_Manager>();
            stamina = GetComponent<Celestial_NPC_Stamina>();
            _navComponent = GetComponent<NavMeshAgent>();
            _navComponent.radius = 0.2f;
            _animator = GetComponent<Animator>();
            _capCollider = GetComponent<CapsuleCollider>() ?? gameObject.AddComponent<CapsuleCollider>();
            _capCollider.center = new Vector3(0, 1, 0);
            _capCollider.radius = 0.2f;
            _capCollider.height = 1.8f;
            _rigidBody = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
            _rigidBody.useGravity = false;
            _rigidBody.isKinematic = true;

            // Register with manager
            Celestial_NPC_Manager manager = FindObjectOfType<Celestial_NPC_Manager>();
            if (manager != null)
            {
                manager.allNPCs.Add(this);
                if (this is Celestial_NPC_Merchant merchant)
                {
                    manager.allMerchants.Add(merchant);
                }
            }
        }

        protected virtual void Start()
        {
            _navComponent.speed = movementSpeed;
            saveMovementSpeed = movementSpeed;

            ChangeState(GetInitialState()); // Child classes override GetInitialState
            checkPosition();

            // Talk initialization
            npcPatrol = GetComponent<Celestial_NPC_Patrol>();
            Celestial_NPC_Manager manager = FindObjectOfType<Celestial_NPC_Manager>();
            otherNPCs = new List<Celestial_NPC>(manager.allNPCs);
            otherNPCs.Remove(this);
            audioSource = GetComponent<AudioSource>();
        }

        protected virtual NPCState GetInitialState()
        {
            return NPCState.Roaming; // Default; overridden in children if needed
        }

        private void Update()
        {
            if (isRoaming && stamina.currentStamina > talkStaminaCost)
            {
                HandleNPCInteractions();
            }
            if (isTalking)
            {
                HandleTalking();
                FaceTalkingPartner();
            }
        }

        private void FixedUpdate()
        {
            if (isStun) return;

            switch (currentState)
            {
                case NPCState.Roaming:
                    HandleRoaming();
                    ResetActionBooleans();
                    CheckForStamina();
                    break;
                case NPCState.FollowingPath:
                    HandleFollowingPath(); // Virtual; overridden in Patrol
                    break;
                case NPCState.MovingToward:
                    if (stamina.failedToJoinQueue)
                    {
                        stamina.FindRecoveryObject();
                        _navComponent.avoidancePriority = Random.Range(60, 80);
                        _navComponent.speed = 0.8f;
                    }
                    else
                    {
                        _navComponent.avoidancePriority = Random.Range(60, 80);
                        _navComponent.speed = 0.8f;
                        ResetActionBooleans();
                    }
                    break;
                case NPCState.Idle:
                    _navComponent.speed = 0f;
                    UpdateAnimationWalkSpeed(0f);
                    if (IsOnNavMesh())
                    {
                        HandleIdle();
                        ResetActionBooleans();
                    }
                    else
                    {
                        _rigidBody.useGravity = true;
                        _rigidBody.isKinematic = false;
                    }
                    break;
                case NPCState.WaitingInQueue:
                    if (stamina.currentRecoveryObject != null)
                    {
                        queueLocation = stamina.currentRecoveryObject.GetQueuePosition(this);
                        float distanceToQueuePosition = Vector3.Distance(transform.position, queueLocation.position);
                        if (distanceToQueuePosition > 0.1f)
                        {
                            _navComponent.destination = queueLocation.position;
                            _navComponent.speed = 1f;
                            _animator.SetFloat("Speed", _navComponent.velocity.magnitude);
                            UnfreezeNPC();
                        }
                        else
                        {
                            FreezeNPC(0, false);
                            transform.LookAt(stamina.currentRecoveryObject.transform.position);
                        }
                    }
                    ResetActionBooleans();
                    break;
                case NPCState.Sitting:
                    stamina.VisualizeStaminaChange();
                    if (stamina.currentRecoveryObject != null && !stamina.isRecoveringStamina)
                    {
                        FreezeNPC(stamina.recoveryDuration, false);
                        StartCoroutine(stamina.RecoverStaminaOverTime(stamina.recoveryAmount, stamina.recoveryDuration));
                    }
                    _animator.SetBool("IsSitting", true);
                    break;
                case NPCState.Eating:
                    if (stamina.currentRecoveryObject != null)
                    {
                        FreezeNPC(stamina.currentRecoveryObject.useDuration, true);
                        stamina.RecoverStamina(stamina.currentRecoveryObject.recoverAmount);
                        stamina.VisualizeStaminaChange();
                    }
                    break;
                case NPCState.Sleeping:
                    _animator.SetBool("IsSleeping", true);
                    break;
                case NPCState.Talking:
                    _animator.SetBool("IsTalking", true);
                    break;
                default:
                    ResetActionBooleans();
                    break;
            }
        }

        protected virtual void HandleFollowingPath()
        {
            // Base does nothing; overridden in Patrol
        }

        protected void HandleRoaming()
        {
            _animator.SetFloat("Speed", _navComponent.velocity.magnitude);
            pathCheckCooldown -= Time.deltaTime;

            if (_navComponent.pathPending || _navComponent.remainingDistance <= _navComponent.stoppingDistance)
            {
                if (pathCheckCooldown <= 0)
                {
                    pathCheckCooldown = pathCheckRate;
                    SetNewRoamingDestination();
                }
            }
            else
            {
                _navComponent.speed = movementSpeed;
                UpdateAnimationWalkSpeed(_navComponent.velocity.magnitude);
            }
        }

        protected void SetNewRoamingDestination()
        {
            Vector3 newPosition = celestialAreas.GetRandomPositionWithinBounds();
            if (newPosition != _navComponent.destination)
            {
                _navComponent.SetDestination(newPosition);
            }
        }

        protected void HandleIdle()
        {
            if (timeManager.currentTimeOfDay > sleepTime)
            {
                stamina.FindRecoveryObject();
            }
            if (stamina.currentStamina > stamina.maxStamina * 0.8f)
            {
                ChangeState(NPCState.Roaming);
            }
            StartCoroutine(stamina.RecoverStaminaOverTime(1f, 20f));
        }

        private void checkPosition()
        {
            if (!IsOnNavMesh())
            {
                ChangeState(NPCState.Idle);
                _navComponent.ResetPath();
            }
        }

        private bool IsOnNavMesh()
        {
            NavMeshHit hit;
            return NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas);
        }

        public IEnumerator DelayedAction(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        protected void CheckForStamina()
        {
            stamina.VisualizeStaminaChange();
            if (stamina.NeedStamina())
            {
                if (!justFinishedAction && !isSatisfied)
                {
                    if (stamina.targetRecoveryObject == null)
                    {
                        stamina.FindRecoveryObject();
                    }
                    if (stamina.targetRecoveryObject != null)
                    {
                        ChangeState(NPCState.MovingToward);
                        _navComponent.SetDestination(stamina.targetRecoveryObject.transform.position);
                    }
                }
            }
        }

        public void ChangeState(NPCState newState)
        {
            currentState = newState;
            ResetActionBooleans();
            UpdateAnimationState();
        }

        private void UpdateAnimationState()
        {
            _animator.SetFloat("Speed", currentState == NPCState.Roaming || currentState == NPCState.MovingToward ? _navComponent.velocity.magnitude : 0f);
            _animator.SetBool("IsSitting", currentState == NPCState.Sitting);
            _animator.SetBool("IsSleeping", currentState == NPCState.Sleeping);
            _animator.SetBool("IsTalking", currentState == NPCState.Talking);
            // Add more as needed
        }

        private void ResetActionBooleans()
        {
            isSitting = currentState == NPCState.Sitting;
            isSleeping = currentState == NPCState.Sleeping;
            isTalking = currentState == NPCState.Talking;
            IsMovingToObject = currentState == NPCState.MovingToward;
            isRoaming = currentState == NPCState.Roaming;
            isFollowingPath = currentState == NPCState.FollowingPath;
            isWaitingInQueue = currentState == NPCState.WaitingInQueue;
        }

        public void FreezeNPC(float time, bool useTimer)
        {
            if (_navComponent.enabled)
            {
                stopUpdate = true;
                _navComponent.isStopped = true;
                _animator.applyRootMotion = true;
                _navComponent.enabled = false;
            }
            UpdateAnimationWalkSpeed(0f);
            if (useTimer) StartCoroutine(UnfreezeNPC(time));
        }

        public void UnfreezeNPC()
        {
            stopUpdate = false;
            _navComponent.enabled = true;
            _navComponent.isStopped = false;
            _animator.applyRootMotion = false;
            stamina.ResetNeed();
            UpdateAnimationWalkSpeed(movementSpeed);
        }

        private IEnumerator UnfreezeNPC(float time)
        {
            yield return new WaitForSeconds(time);
            UnfreezeNPC();
            ChangeState(NPCState.Roaming);
        }

        public void UpdateAnimationWalkSpeed(float speed)
        {
            _animator.SetFloat("Speed", speed);
        }

        public void ForcePlayAnimation(string animationStateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
        {
            _animator.Play(animationStateName, layer, normalizedTime);
        }

        public Transform DeepFind(Transform parent, string targetName)
        {
            if (string.Equals(parent.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return parent;

            foreach (Transform child in parent)
            {
                Transform result = DeepFind(child, targetName);
                if (result != null) return result;
            }
            return null;
        }

        // Merchant-specific virtuals can be added here if needed
        protected virtual void HandleMerchantLogic() { } // Override in Merchant child

        // Integrated Talk methods
        private void HandleNPCInteractions()
        {
            foreach (var npc in otherNPCs)
            {
                if (npc == null || npc.unableToTalk || isTalking ||
                    !npc.isRoaming || Vector3.Distance(transform.position, npc.transform.position) > talkDistance)
                    continue;

                if (lastTalkTime.TryGetValue(npc, out float lastTime) && Time.time - lastTime < talkCooldownPeriod)
                    continue;

                StartTalking(npc);
                npc.StartTalking(this);
                lastTalkTime[npc] = Time.time;
                if (!lastTalkTime.ContainsKey(this)) lastTalkTime[this] = Time.time;
                break;
            }
        }

        private void HandleTalking()
        {
            _currentTalkTime -= Time.deltaTime;
            if (_currentTalkTime <= 0)
            {
                StopTalking();
                StartCoroutine(TalkCooldownTimer());
            }
        }

        public void StartTalking(Celestial_NPC partner)
        {
            if (!stamina.TryConsumeStamina(talkStaminaCost) || isTalking) return;

            talkingPartner = partner;
            Talking(partner);
            partner.Talking(this);
        }

        private void Talking(Celestial_NPC partner)
        {
            ChangeState(NPCState.Talking);
            FreezeNPC(talkTimer, true);
            if (npcPatrol != null) npcPatrol.PausePathFollowing();
            _currentTalkTime = talkTimer;

            float distanceToCamera = Vector3.Distance(Camera.main.transform.position, transform.position);
            if (talkAudioClips.Length > 0 && distanceToCamera <= audioDistance)
            {
                int index = Random.Range(0, talkAudioClips.Length);
                audioSource.PlayOneShot(talkAudioClips[index]);
            }
        }

        private IEnumerator TalkCooldownTimer()
        {
            unableToTalk = true;
            yield return new WaitForSeconds(talkCooldown);
            unableToTalk = false;
        }

        private void StopTalking()
        {
            ChangeState(NPCState.Roaming);

            if (talkingPartner != null)
            {
                talkingPartner.StopTalking();
                talkingPartner = null;
            }
        }

        private void FaceTalkingPartner()
        {
            if (talkingPartner == null) return;

            Vector3 directionToPartner = talkingPartner.transform.position - transform.position;
            directionToPartner.y = 0;

            Quaternion lookRotation = Quaternion.LookRotation(directionToPartner);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
        }
    }
}