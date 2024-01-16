using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [SerializeField] protected int _health;
    [SerializeField, Range(100, 500)] protected int _maxHealth;

    [SerializeField, Range(1, 5)] private float _fireTime;
    
    public int Health { get => _health; }
    public int MaxHealth { get => _maxHealth; }
    public float FireTime { get => _fireTime; private set => _fireTime = value; }

    private void FixedUpdate()
    {
        ShootTimer();
    }

    private void ShootTimer()
    {
        if(FireTime > 0)
        {
            FireTime -= Time.deltaTime;
        }
    }

    private void TakeDamage(int damage)
    {
        _health -= damage;
        if (_health <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        //todo: define what death is: respawn, spectate, thrown out of the game?
        Debug.Log("Dead");
    }
    
    public void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("HitObject"))
        {
            return;
        }
        
        var projectile = collision.gameObject.GetComponent<Projectile>();
        
        if(projectile == null)
        {
            return;
        }
        
        TakeDamage(projectile.Damage);
    }
}
