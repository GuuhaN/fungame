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
        if (IsOwner)
        {
            GetComponentInChildren<AudioListener>().enabled = true;
        }
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
        if (!IsOwner)
        {
            return;
        }
        
        MovePlayerServerRpc();
    }

    [ServerRpc]
    public void MovePlayerServerRpc()
    {
        _rigidbody.MovePosition(transform.position + _movement * (_movementSpeed * Time.deltaTime));
    }
}
