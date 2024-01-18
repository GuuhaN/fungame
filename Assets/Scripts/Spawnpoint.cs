using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawnpoint : MonoBehaviour
{
    public Transform SpawnTransform { get; private set; }

    void Awake()
    {
        SpawnTransform = transform;
    }
}
