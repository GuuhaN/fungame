using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour
{
    private Spawnpoint[] SpawnPoints { get; set; }

    private void Start()
    {
        SpawnPoints = FindObjectsOfType<Spawnpoint>();
        NetworkManager.Singleton.ConnectionApprovalCallback += ConnectionApprovalCallback;
    }

    private void ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        Debug.Log(res);
        res.Approved = true;
        res.PlayerPrefabHash = null;
        res.CreatePlayerObject = true;
        res.Position = GetSpawnPoint();

        if (NetworkManager.Singleton.ConnectedClients.Count >= 8)
        {
            res.Approved = false;
            res.Reason = "Server is full";
        }

        res.Pending = false;
    }
    
    public Vector3 GetSpawnPoint()
    {
        return SpawnPoints[Random.Range(0, SpawnPoints.Length)].SpawnTransform.position;
    }
}