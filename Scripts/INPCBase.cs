using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class INPCBase : MonoBehaviour
{
    [Header("NPC Attributes")]
    public INPCRole role;
    public float roamRadius = 10f;

    [Header("Current State")]
    public INPCAction currentAction = INPCAction.None;
    public bool isWorking;

    private bool _interacting = false;
    private NavMeshAgent _navComponent;
    public Animator _animator;
    private Vector3 _spawnPosition;
    private bool _roaming = false;
    private Transform _player;
    private float _roamDelay = 2f;
    private float _roamTimer = 0f;

    void Awake()
    {
        _navComponent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        if (_navComponent == null || _animator == null)
        {
            Debug.LogError("[INPCBase] Missing required components!", this);
            this.enabled = false;
            return;
        }

    }

    void Start()
    {
        _spawnPosition = transform.position;
        Debug.Log($"[INPCBase] Initializing with currentAction: {currentAction}");
        SetAction(currentAction);
    }

    void Update()
    {
        if (_interacting)
        {
            if (currentAction != INPCAction.Talking)
            {
                SetAction(INPCAction.Talking);
            }
            StopMoving();
        }
        else if (role == INPCRole.None)
        {
            StopMoving();
        }
        else if (isWorking)
        {
            if (currentAction != INPCAction.Working)
            {
                SetAction(INPCAction.Working);
            }
            StopMoving();
        }
        else
        {
            ResumeMoving();
            if (!_roaming)
            {
                if (_roamTimer > 0f)
                {
                    _roamTimer -= Time.deltaTime;
                }
                else
                {
                    FreeRoam();
                }
            }
            else
            {
                CheckRoam();
            }
        }
        _animator.SetFloat("Speed", _navComponent.velocity.magnitude);
    }

    /// <summary>
    /// Engages the NPC in interaction with the player, stopping movement and facing the player.
    /// </summary>
    /// <param name="player">The player's transform.</param>
    public void EngageInteraction(Transform player)
    {
        if (player == null) return;
        _interacting = true;
        _player = player;
        StopMoving();
        Vector3 lookPos = player.position - transform.position;
        lookPos.y = 0;
        if (lookPos.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(lookPos);
        }
        _animator.Play("Talking");
        SetAction(INPCAction.Talking);
    }

    /// <summary>
    /// Disengages the NPC from interaction, allowing it to resume its normal behavior.
    /// </summary>
    public void DisengageInteraction()
    {
        _interacting = false;
        _player = null;
        if (!isWorking)
        {
            SetAction(INPCAction.None);
            _roamTimer = _roamDelay;
        }
    }

    /// <summary>
    /// Sets the NPC's current action and updates the animator.
    /// </summary>
    /// <param name="action">The action to set.</param>
    private void SetAction(INPCAction action)
    {
        int currentAnimatorValue = _animator.GetInteger("ActionState");
        if ((int)action == currentAnimatorValue) return;

        currentAction = action;
        _animator.SetInteger("ActionState", (int)action);
    }

    /// <summary>
    /// Checks if the NPC has reached its roaming destination and prepares for the next roam.
    /// </summary>
    private void CheckRoam()
    {
        if (_navComponent.remainingDistance <= _navComponent.stoppingDistance)
        {
            _roaming = false;
            SetAction(INPCAction.None);
            _roamTimer = _roamDelay;
        }
    }

    /// <summary>
    /// Sets the NPC to roam to a random valid position within the roam radius.
    /// </summary>
    private void FreeRoam()
    {
        Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
        randomDirection += _spawnPosition;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, roamRadius, NavMesh.AllAreas))
        {
            _navComponent.destination = hit.position;
            _roaming = true;
        }
    }

    /// <summary>
    /// Stops the NPC's movement.
    /// </summary>
    private void StopMoving()
    {
        if (_navComponent != null && !_navComponent.isStopped) _navComponent.isStopped = true;
    }

    /// <summary>
    /// Resumes the NPC's movement.
    /// </summary>
    private void ResumeMoving()
    {
        if (_navComponent != null && _navComponent.isStopped) _navComponent.isStopped = false;
    }
}