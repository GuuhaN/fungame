using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class StartServer : NetworkBehaviour
{
    public void OnStartServer()
    {
        NetworkManager.Singleton.StartServer();
    }
    
    public void OnHostServer()
    {
        NetworkManager.Singleton.StartHost();
    }
    
    public void OnJoinServer()
    {
        NetworkManager.Singleton.StartClient();
    }
}
