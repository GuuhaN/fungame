using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
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

    public Rigidbody Rigidbody { get; private set; }
    public NetworkObject NetworkObject { get; private set; }

    void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        NetworkObject = GetComponent<NetworkObject>();
    }
}
