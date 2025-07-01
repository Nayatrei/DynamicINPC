using UnityEngine;
using CelestialCyclesSystem; // Make sure to add this using directive

[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(INPCStats))]
public class INPCBase : MonoBehaviour
{
    #region Public Properties
    [Header("NPC Attributes:")]
    public INPCRole role; 
    public float roamRadius = 10F;

    [Header("Current State")]
    public INPCAction currentAction;
    
    [Tooltip("This is now controlled by the INPCManager based on work hours.")]
    public bool isWorking = false; // The new public flag
    #endregion

    #region Private Properties
    private bool _interacting = false; 
    private UnityEngine.AI.NavMeshAgent _navComponent;
    private Animator _animator; 
    private INPCStats _npcStats;
    private Vector3 _spawnPosition;
    private bool _roaming = false;
    private Transform _player;
    #endregion

    void Awake()
    {
        _navComponent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _npcStats = GetComponent<INPCStats>(); 

        if (_navComponent != null && _npcStats != null)
        {
            _navComponent.speed = _npcStats.MoveSpeed;
        }

        SetAction(INPCAction.None);

        if (INPCManager.Instance != null)
        {
            INPCManager.Instance.RegisterNPC(this);
        }
    }

	void Start ()
    {
        _spawnPosition = transform.position;
	}

    void OnDestroy()
    {
        if (INPCManager.Instance != null)
        {
            INPCManager.Instance.UnregisterNPC(this);
        }
    }
	
	void Update ()
    {
        // The per-frame check to the manager has been removed for performance.
        // Now, we just check the local 'isWorking' flag.
        if (isWorking)
        {
            // Placeholder for work logic. 
            // For example, you might want to stop roaming and play a work animation.
            if (!_interacting) // Don't work if you're talking to the player
            {
                SetAction(INPCAction.Working);
                // You would add logic here to find a work station, etc.
                // For now, we can just stop the agent.
                if(!_navComponent.isStopped) _navComponent.isStopped = true;
            }
        }
        else if (!_interacting) // Not working and not interacting, so roam.
        {
            if (_navComponent.isStopped)
            {
                _navComponent.isStopped = false;
            }

            if (!_roaming)
            {
                FreeRoam();
            }
            else
            {
                CheckRoam();
            }
        }
        else // Is interacting
        {
            if (_player != null)
            {
                Vector3 lookPos = _player.position - transform.position;
                lookPos.y = 0;
                Quaternion rotation = Quaternion.LookRotation(lookPos);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 2F);
            }
        }

        if (_navComponent != null && _animator != null)
        {
            _animator.SetFloat("Speed", _navComponent.velocity.magnitude);
        }
    }
    
    public void EngageInteraction(Transform player, INPCAction interactionAction)
    {
        _interacting = true;
        _player = player;

        if (_navComponent != null)
        {
            _navComponent.isStopped = true; 
            _navComponent.ResetPath();
        }
        
        SetAction(interactionAction);
    }

    public void DisengageInteraction()
    {
        _interacting = false;
        _player = null;

        if (_navComponent != null)
        {
            _navComponent.isStopped = false;
        }
        SetAction(INPCAction.None);
    }

    public void SetAction(INPCAction action)
    {
        if (currentAction == action) return; 

        currentAction = action; 
        if (_animator != null)
        {
            _animator.SetInteger("ActionState", (int)action);
        }
    }

    void CheckRoam()
    {
        if (!_navComponent.pathPending)
        {
            float dist = _navComponent.remainingDistance;
            if (dist != Mathf.Infinity && _navComponent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathComplete && _navComponent.remainingDistance <= _navComponent.stoppingDistance)
            {
                if (!_navComponent.hasPath || _navComponent.velocity.sqrMagnitude == 0f)
                {
                    _roaming = false;
                    SetAction(INPCAction.None);
                }
            }
        }
    }

    void FreeRoam()
    {
        _roaming = true;
        SetAction(INPCAction.None);

        Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
        randomDirection += _spawnPosition;
        UnityEngine.AI.NavMeshHit hit;
        UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out hit, roamRadius, 1);
        Vector3 finalPosition = hit.position;
        _navComponent.destination = finalPosition;
    }
}
