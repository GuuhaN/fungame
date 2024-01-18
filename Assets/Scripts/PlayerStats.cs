using Unity.Netcode;
using UnityEngine;

public class PlayerStats : NetworkBehaviour
{
    public NetworkVariable<byte> Health { get; private set; }
    public NetworkVariable<float> FireRate { get; private set; }
    [SerializeField, Range(1, 10)]
    private float _fireRate;
    [SerializeField, Range(100, 200, order = 10)]
    private byte _maxHealth;
    
    public void Awake()
    {
        Health = new NetworkVariable<byte>(_maxHealth);
        FireRate = new NetworkVariable<float>(_fireRate);
    }
    
    private void FixedUpdate()
    {
        ShootTimer();
        
        if (Health.Value <= 0)
        {
            Die();
        }
    }

    public void ResetShootTimer()
    {
        FireRate.Value = _fireRate;
    }

    private void ShootTimer()
    {
        if (FireRate.Value > 0f)
        {
            FireRate.Value -= Time.deltaTime;
        }
    }

    private void Die()
    {
        //todo: define what death is: respawn, spectate, thrown out of the game?
        Debug.Log("Dead");
        Health.Value = _maxHealth;
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("HitObject"))
        {
            return;
        }

        var projectile = collision.gameObject.GetComponent<Projectile>();

        if (projectile == null)
        {
            return;
        }

        TakeDamage(projectile.Damage);
    }

    private void TakeDamage(int damage)
    {
        Health.Value -= (byte)damage;
    }
}
