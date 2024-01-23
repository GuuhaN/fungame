using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerStats : NetworkBehaviour
{
    [SerializeField] 
    private TextMeshProUGUI _healthText;
    public NetworkVariable<byte> Health { get; private set; }
    public NetworkVariable<float> FireRate { get; private set; }
    [SerializeField, Range(1, 10)]
    private float _fireRate;
    [SerializeField, Range(100, 200, order = 10)]
    private byte _maxHealth;
    
    private PlayerSpawner _playerSpawner;
    
    public void Awake()
    {
        Health = new NetworkVariable<byte>(_maxHealth);
        FireRate = new NetworkVariable<float>(_fireRate);
        _playerSpawner = FindObjectOfType<PlayerSpawner>();
    }
    
    public void Start()
    {
        _healthText.text = Health.Value.ToString();
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
        Health.Value = _maxHealth;
        transform.position = _playerSpawner.GetSpawnPoint();
        _healthText.text = Health.Value.ToString();
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
        projectile.NetworkObject.Despawn();
    }

    private void TakeDamage(int damage)
    {
        if (Health.Value > 0)
        {
            Health.Value -= (byte)damage;
        }
        
        _healthText.text = Health.Value.ToString();
    }
}
