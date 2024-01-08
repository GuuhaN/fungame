using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile info")]
    [SerializeField, Range(1, 100)] private int _damage;
    [SerializeField, Range(1, 100)] private float _speed;

    public int Damage
    {
        get => _damage;
    }
    
    public float Speed 
    {
        get => _speed;
    }

    public Rigidbody _rigidbody { get; set; }

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }
}
