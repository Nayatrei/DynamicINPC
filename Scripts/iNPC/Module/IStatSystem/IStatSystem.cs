using UnityEngine;
using UnityEngine.Events;

public class IStatSystem : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHP = 100f;
    private float _currentHP;
    public float CurrentHP
    {
        get { return _currentHP; }
        set
        {
            _currentHP = value;
            onDamage?.Invoke();
            CheckStatDeath();
        }
    }

    [Header("Combat Stats")]
    public float Attack = 10f;
    public float Defense = 0f;
    public float MoveSpeed = 5f;

    [Header("Events")]
    public UnityEvent onDamage;
    public UnityEvent onDeath;
    public bool isDead = false;

    protected virtual void Awake()
    {
        _currentHP = maxHP;
        isDead = false;
    }

    private void CheckStatDeath()
    {
        if (_currentHP <= 0 && !isDead)
        {
            isDead = true;
            onDeath?.Invoke();
        }
    }
}

