using UnityEngine;
using CelestialCyclesSystem;

[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
public class INPCBase : MonoBehaviour
{
    // This script is primarily designed for NPCs within their environments.

    #region Public Properties
    [Header("NPC Attributes:")]

    public INPCRole role;
    public float roamRadius = 10F;
    #endregion

    #region Private Properties
    private bool _interacting = false;
    private UnityEngine.AI.NavMeshAgent _navComponent;
    private Vector3 _spawnPosition;
    private bool _roaming = false;
    private Transform _player;
    private INPCStats _npcStats; // Add reference to INPCStats
    #endregion

    void Awake()
    {
        // Register this NPC with the INPCManager as soon as it awakes
        if (INPCManager.Instance != null)
        {
            INPCManager.Instance.RegisterNPC(this);
        }
    }

    // Use this for initialization
	void Start ()
    {
        _navComponent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _spawnPosition = transform.position;

        // Get the INPCStats component and apply its MoveSpeed to NavMeshAgent
        _npcStats = GetComponent<INPCStats>();
        if (_npcStats != null)
        {
            _navComponent.speed = _npcStats.MoveSpeed; // Apply MoveSpeed
            Debug.Log($"NPC {gameObject.name} NavMeshAgent speed set to {_npcStats.MoveSpeed} from INPCStats.");
        }
        else
        {
            Debug.LogWarning($"No INPCStats component found on {gameObject.name}. NavMeshAgent speed will use its default value.");
        }
	}
	
    void OnDestroy()
    {
        // Unregister this NPC when it is destroyed
        if (INPCManager.Instance != null)
        {
            INPCManager.Instance.UnregisterNPC(this);
        }
    }

	// Update is called once per frame
	void Update ()
    {
        // Example of checking work hours (can be integrated into state machine later)
        if (INPCManager.Instance != null)
        {
            bool isWorkingHours = INPCManager.Instance.IsWorkHours(this);
            // You can use 'isWorkingHours' to influence NPC behavior here.
        }

        if (!_interacting)
        {
            if (!_roaming)
                FreeRoam();
            else
                CheckRoam();
        }
        else
        {
            Vector3 lookPos = _player.position - transform.position;
            lookPos.y = 0;
            Quaternion rotation = Quaternion.LookRotation(lookPos);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 2F);
            _navComponent.ResetPath();
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
                    _roaming = false;
            }
        }
    }

    void FreeRoam()
    {
        _roaming = true;
        Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
        randomDirection += _spawnPosition;
        UnityEngine.AI.NavMeshHit hit;
        UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out hit, roamRadius, 1);
        Vector3 finalPosition = hit.position;
        _navComponent.destination = finalPosition;
    }
}