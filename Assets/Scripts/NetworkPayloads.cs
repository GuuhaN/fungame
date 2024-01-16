using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct StatePayload : INetworkSerializable
{
    public int tick;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref rotation);
        serializer.SerializeValue(ref velocity);
        serializer.SerializeValue(ref angularVelocity);
    }
}

public struct InputPayload : INetworkSerializable
{
    public int tick;
    public Vector3 inputVector;
    public Vector3 rotationVector;
    public bool isJumping;
    public bool isFiring;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref inputVector);
        serializer.SerializeValue(ref rotationVector);
        serializer.SerializeValue(ref isJumping);
        serializer.SerializeValue(ref isFiring);
    }
}