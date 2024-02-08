using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : NetworkBehaviour
{
    private MyPlayerInput _playerInput;
    // Start is called before the first frame update

    private void Initialize()
    {
        _playerInput = GetComponent<MyPlayerInput>();
    }
    
    public override void OnNetworkSpawn()
    {
        Initialize();
    }

    // Update is called once per frame
    void Update()
    {
        if (IsServer && IsLocalPlayer)
        {
            OnInteract(_playerInput.Player.Interact.IsPressed() && _playerInput.Player.Interact.ReadValue<float>() > 0);
        }
        else if (IsClient && IsLocalPlayer)
        {
            OnInteractServerRpc(_playerInput.Player.Interact.IsPressed() && _playerInput.Player.Interact.ReadValue<float>() > 0);
        }
    }
    
    private void OnInteract(bool isPressed)
    {
        if (!IsOwner)
        {
            return;
        }
        
        if (!isPressed)
        {
            return;
        }

        Debug.Log("Interact");
    }
    
    [ServerRpc]
    private void OnInteractServerRpc(bool isPressed)
    {
       OnInteract(isPressed);
    }
}
