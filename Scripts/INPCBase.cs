using UnityEngine;
using CelestialCyclesSystem; // Make sure to add this using directive

// Add RequireComponent for Animator and INPCStats to ensure they are always present on the GameObject
[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(INPCStats))] // Added this to ensure INPCStats is present
public class INPCBase : MonoBehaviour
{
    // This script is primarily designed for NPCs within their environments.

    #region Public Properties
    [Header("NPC Attributes:")]
    public INPCRole role; // Current role of the NPC
    public float roamRadius = 10F; // Radius for free roaming

    [Header("Current State")]
    public INPCAction currentAction; // Current action of the NPC
    #endregion

    #region Private Properties
    private bool _interacting = false; // Flag for interaction state
    private UnityEngine.AI.NavMeshAgent _navComponent; // Reference to NavMeshAgent
    private Animator _animator; // Reference to the Animator component
    private INPCStats _npcStats; // Reference to the INPCStats component
    private Vector3 _spawnPosition; // Original spawn position for roaming
    private bool _roaming = false; // Flag for roaming state
    private Transform _player; // Reference to the player (for interaction)
    #endregion

    // Awake is called when the script instance is being loaded.
    void Awake()
    {
        // Get references to components
        _navComponent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _animator = GetComponent<Animator>(); // Automatically connect Animator
        _npcStats = GetComponent<INPCStats>(); // Get INPCStats component

        // Set NavMeshAgent speed based on INPCStats MoveSpeed
        if (_navComponent != null && _npcStats != null)
        {
            _navComponent.speed = _npcStats.MoveSpeed; // Set NavMeshAgent speed
        }

        // Initialize current action
        SetAction(INPCAction.None); // Default action on Awake (Idle/BlendTree)

        // Register this NPC with the INPCManager
        if (INPCManager.Instance != null)
        {
            INPCManager.Instance.RegisterNPC(this);
        }
    }

    // Start is called before the first frame update.
	void Start ()
    {
        _spawnPosition = transform.position;
	}

    // OnDestroy is called when the behaviour is destroyed.
    void OnDestroy()
    {
        // Unregister this NPC when it is destroyed
        if (INPCManager.Instance != null)
        {
            INPCManager.Instance.UnregisterNPC(this);
        }
    }
	
	// Update is called once per frame.
	void Update ()
    {
        // Example of checking work hours (can be integrated into state machine later)
        if (INPCManager.Instance != null)
        {
            bool isWorkingHours = INPCManager.Instance.IsWorkHours(this);
            // You can use 'isWorkingHours' to influence NPC behavior here.
            // For example, if(!isWorkingHours) then the NPC might prioritize sleeping or idling over working.
            // This is where you might set INPCAction.Sleeping, INPCAction.Working, etc.
        }

if (!_interacting)
        {
            // Ensure the agent is not stopped if it's not interacting
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
        else
        {
            // NPC is interacting (e.g., with player)
            SetAction(INPCAction.Talking); // Set action to Talking when interacting
            Vector3 lookPos = _player.position - transform.position;
            lookPos.y = 0;
            Quaternion rotation = Quaternion.LookRotation(lookPos);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 2F);
            _navComponent.ResetPath();
        }

        // Always update the Animator's "Speed" parameter based on NavMeshAgent velocity
        if (_navComponent != null && _animator != null)
        {
            _animator.SetFloat("Speed", _navComponent.velocity.magnitude);
        }
    }
/// <summary>
    /// Puts the NPC into an interaction state, freezing its movement.
    /// </summary>
    /// <param name="player">The transform of the player to interact with.</param>
    public void EngageInteraction(Transform player)
    {
        _interacting = true;
        _player = player;

        if (_navComponent != null)
        {
            _navComponent.isStopped = true; // Freeze the NavMeshAgent
            _navComponent.ResetPath();      // Clear any existing path
        }
    }

    /// <summary>
    /// Releases the NPC from an interaction state, allowing it to move again.
    /// </summary>
    public void DisengageInteraction()
    {
        _interacting = false;
        _player = null;

        if (_navComponent != null)
        {
            _navComponent.isStopped = false; // Unfreeze the NavMeshAgent
        }
        SetAction(INPCAction.None); // Return to idle/roaming state
    }
    /// <summary>
    /// Sets the current action of the NPC and updates the Animator's "ActionState" parameter.
    /// </summary>
    /// <param name="action">The new action for the NPC.</param>
    public void SetAction(INPCAction action)
    {
        if (currentAction == action) return; // Only update if action changes

        currentAction = action; // Update the current action
        if (_animator != null) // Ensure Animator component exists
        {
            // Set the Animator's "ActionState" integer parameter to the enum's integer value
            _animator.SetInteger("ActionState", (int)action);
        }
    }

    //Movement logic
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
                    SetAction(INPCAction.None); // Transition to Idle/BlendTree when roaming stops
                }
            }
        }
    }

    void FreeRoam()
    {
        _roaming = true;
        SetAction(INPCAction.None); // Set to Idle/BlendTree for roaming (assuming blend tree handles movement)

        Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
        randomDirection += _spawnPosition;
        UnityEngine.AI.NavMeshHit hit;
        UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out hit, roamRadius, 1);
        Vector3 finalPosition = hit.position;
        _navComponent.destination = finalPosition;
    }
}