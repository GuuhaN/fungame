using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayer : NetworkBehaviour
{
    [SerializeField] private Transform _playerCamera;
    [SerializeField] private float _movementSpeed;

    private float TargetYRotation { get; set; }
    
    private Vector3 _movement;
    private Vector2 _lookInput;
    private Rigidbody _rigidbody;

    public void Initialize()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (IsOwner)
        {
            GetComponentInChildren<AudioListener>().enabled = true;
            if (Camera.main != null)
            {
                Camera.main.gameObject.SetActive(false);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Initialize();
    }
    
    public void OnMove(InputValue value)
    {
        _movement = value.Get<Vector3>();
    }
    
    public void OnLook(InputValue value)
    {
        _lookInput = value.Get<Vector2>();
    }

    public void Update()
    {
        if (!IsOwner)
        {
            return;
        }
        
        MovePlayerServerRpc();
        RotatePlayerClient();
    }
    
    public void RotatePlayerClient()
    {
        if (_playerCamera != null)
        {
            TargetYRotation += _lookInput.y * 1;
            var clampedYRotation = Mathf.Clamp(TargetYRotation, -50, 70);
            _playerCamera.localRotation *= Quaternion.Euler(clampedYRotation, 0, 0);
        }
        
        transform.localRotation *= Quaternion.Euler(0, _lookInput.x, 0);
    }

    // [ServerRpc]
    // public void RotatePlayerServerRpc()
    // {
    //     transform.localRotation = Quaternion.Euler(transform.localRotation.x, transform.localRotation.x + _lookInput.x, transform.localRotation.z);
    // }

    [ServerRpc]
    public void MovePlayerServerRpc()
    {
        // add direction into position
        var moveDirection = transform.right * _movement.x + transform.forward * _movement.z;
        Debug.Log(_movement);
        _rigidbody.MovePosition(transform.position + moveDirection * (_movementSpeed * Time.deltaTime));
    }
}
