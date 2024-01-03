using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayer : NetworkBehaviour
{
    [SerializeField] private float _movementSpeed;

    private Vector3 _movement;
    private Rigidbody _rigidbody;

    public void Initialize()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Initialize();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }
    
    public void OnMove(InputValue value)
    {
        _movement = value.Get<Vector3>();
    }

    public void Update()
    {
        _rigidbody.MovePosition(transform.position + _movement * (_movementSpeed * Time.deltaTime));
    }
}
