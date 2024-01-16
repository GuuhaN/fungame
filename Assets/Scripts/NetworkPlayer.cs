using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Player")] [SerializeField] private GameObject _playerCamera;
    [SerializeField] private Transform _shootingPoint;

    [Header("Movement")] [SerializeField] private float _movementSpeed;
    [SerializeField, Range(0, 90)] private float _clampedYRotation;

    [Header("Shooting")] [SerializeField, Range(1, 10)]
    private int _shootingForce;

    private float TargetYRotation { get; set; }

    private MyPlayerInput _playerInput;
    private Rigidbody _rigidbody;

    [Header("Networking")] 
    private NetworkTimer timer;
    private const float tickRate = 30f;
    private const int BUFFER_SIZE = 1024;
    
    // Network client behavior
    public CircularBuffer<StatePayload> stateBuffer;
    public CircularBuffer<InputPayload> inputBuffer;
    private StatePayload latestServerState;
    private StatePayload lastProcessedState;
    
    // Network server behavior
    private CircularBuffer<StatePayload> serverStateBuffer;
    private Queue<InputPayload> serverInputQueue;

    
    public void Awake()
    {
        timer = new NetworkTimer(tickRate);
        stateBuffer = new CircularBuffer<StatePayload>(BUFFER_SIZE);
        inputBuffer = new CircularBuffer<InputPayload>(BUFFER_SIZE);

        serverStateBuffer = new CircularBuffer<StatePayload>(BUFFER_SIZE);
        serverInputQueue = new Queue<InputPayload>();
    }

    public void Initialize()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _playerInput = new();
        _playerInput.Enable();

        if (IsOwner)
        {
            GetComponentInChildren<AudioListener>().enabled = true;
            _playerCamera.GetComponent<Camera>().enabled = true;
            if (Camera.main != null)
            {
                Camera.main.enabled = false;
            }

            Cursor.visible = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Initialize();
    }

    public void Update()
    {
        timer.Update();
        
        if (!IsOwner && !Application.isFocused)
        {
            return;
        }

        while (timer.ShouldTick())
        {
            HandleClientTick();
            HandleServerTick();
        }

        // var moveInput = _playerInput.Player.Move.ReadValue<Vector3>();
        var lookInput = _playerInput.Player.Look.ReadValue<Vector2>();
        
        // MovePlayer(moveInput);
        RotatePlayer(lookInput);
        JumpPlayer();
        //
        // if (IsServer && IsLocalPlayer)
        // {
        //     ShootPlayer(_playerInput.Player.Fire.triggered);
        // }
        // else if (IsClient && IsLocalPlayer)
        // {
        //     ShootPlayerServerRPC(_playerInput.Player.Fire.triggered);
        // }

        // todo: Server Authoritative Movement add with Client Side Prediction and Server Reconciliation (and Lag Compensation)
        // if (IsServer && IsLocalPlayer)
        // {
        //     MovePlayer(moveInput);
        //     RotatePlayer(lookInput);
        // }
        // else if (IsClient && IsLocalPlayer)
        // {
        //     MovePlayerServerRPC(moveInput);
        //     RotatePlayerServerRPC(lookInput);
        // }
    }

    private void HandleServerTick()
    {
        var bufferIndex = -1;
        while (serverInputQueue.Count > 0)
        {
            var inputPayload = serverInputQueue.Dequeue();
            
             bufferIndex = inputPayload.tick % BUFFER_SIZE;

             var statePayload = SimulateMovement(inputPayload);
             serverStateBuffer.Add(statePayload, bufferIndex);
        }

        if (bufferIndex == -1) return;
        SendToClientRpc(serverStateBuffer.Get(bufferIndex));
    }
    
    [ClientRpc]
    private void SendToClientRpc(StatePayload statePayload)
    {
        if (!IsOwner) return;

        latestServerState = statePayload;
    }
    
    StatePayload SimulateMovement(InputPayload input)
    {
        Physics.simulationMode = SimulationMode.Script;
        MovePlayer(input.inputVector);
        Physics.Simulate(Time.fixedDeltaTime);
        Physics.simulationMode = SimulationMode.FixedUpdate;

        return new StatePayload
        {
            tick = input.tick,
            position = transform.position,
            rotation = transform.rotation,
            velocity = _rigidbody.velocity,
            angularVelocity = _rigidbody.angularVelocity
        };
    }

    private void HandleClientTick()
    {
        if (!IsClient) return;

        var currentTick = timer.CurrentTick;
        var bufferIndex = currentTick % BUFFER_SIZE;

        var inputPayload = new InputPayload()
        {
            tick = currentTick,
            inputVector = _playerInput.Player.Move.ReadValue<Vector3>()
        };
        
        inputBuffer.Add(inputPayload, bufferIndex);
        SendToServerRpc(inputPayload);
        
        var statePayload = ProcessMovement(inputPayload);
        stateBuffer.Add(statePayload, bufferIndex);
        
        // HandleServerReconciliation();
    }
    
    [ServerRpc]
    private void SendToServerRpc(InputPayload inputPayload)
    {
        serverInputQueue.Enqueue(inputPayload);
    }

    private StatePayload ProcessMovement(InputPayload input)
    {
        MovePlayer(input.inputVector);

        return new StatePayload
        {
            tick = input.tick,
            position = transform.position,
            rotation = transform.rotation,
            velocity = _rigidbody.velocity,
            angularVelocity = _rigidbody.angularVelocity
        };
    }

    private void MovePlayer(Vector3 movement)
    {
        var moveDirection = transform.right * movement.x + transform.forward * movement.z;
        _rigidbody.MovePosition(transform.position + moveDirection * (_movementSpeed * Time.deltaTime));
    }

    [ServerRpc]
    private void MovePlayerServerRPC(Vector3 movement) => MovePlayer(movement);

    private void RotatePlayer(Vector2 lookInput)
    {
        if (_playerCamera != null)
        {
            TargetYRotation += lookInput.y * 1;

            if (TargetYRotation > _clampedYRotation)
                TargetYRotation = _clampedYRotation;
            else if (TargetYRotation < -_clampedYRotation)
                TargetYRotation = -_clampedYRotation;

            var clampedYRotation = Mathf.Clamp(TargetYRotation, -50f, 70f);
            _playerCamera.transform.localRotation = Quaternion.Euler(-clampedYRotation, 0, 0);
        }
        transform.localRotation *= Quaternion.Euler(0, lookInput.x, 0);
    }

    [ServerRpc]
    private void RotatePlayerServerRPC(Vector2 lookInput) => RotatePlayer(lookInput);

    private void JumpPlayer()
    {
        var isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);

        if (!isGrounded) return;

        if (_playerInput.Player.Jump.triggered)
        {
            _rigidbody.AddForce(Vector3.up * 5f, ForceMode.Impulse);
        }
    }

    private void ShootPlayer(bool isFiring)
    {
        if (!isFiring)
        {
            return;
        }

        if (_shootingPoint == null)
        {
            return;
        }

        var bulletObject =
            Instantiate(Resources.Load("Bullet"), _shootingPoint.position, _shootingPoint.rotation) as GameObject;

        if (bulletObject == null)
        {
            return;
        }

        var bullet = bulletObject.GetComponent<Projectile>();

        bulletObject.GetComponent<NetworkObject>().Spawn();
        bulletObject.GetComponent<Rigidbody>().AddForce(_shootingPoint.forward * Mathf.Floor(2f * bullet.Speed), ForceMode.Impulse);
    }

    [ServerRpc]
    private void ShootPlayerServerRPC(bool isFiring) => ShootPlayer(isFiring);
}
