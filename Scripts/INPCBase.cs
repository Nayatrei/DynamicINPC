using UnityEngine;

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
    #endregion

    // Use this for initialization
	void Start ()
    {
        _navComponent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _spawnPosition = transform.position;
	}
	
	// Update is called once per frame
	void Update ()
    {
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
