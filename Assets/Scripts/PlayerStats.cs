using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [SerializeField] protected int _health { get; private set; }
    [SerializeField] private int _maxHealth;
    // Start is called before the first frame update

    // Update is called once per frame
    void Update()
    {
        
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
