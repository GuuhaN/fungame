using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Player")] 
    [SerializeField] private GameObject _playerCamera;
    [SerializeField] private Transform _shootingPoint;

    [Header("Movement")]
    [SerializeField] private float _movementSpeed;
    [SerializeField, Range(0, 90)] private float _clampedYRotation;

    [Header("Shooting")]
    [SerializeField, Range(1, 10)]
    private int _shootingForce;

    private float TargetYRotation { get; set; }

    private MyPlayerInput _playerInput;
    private Rigidbody _rigidbody;
    private PlayerStats _playerStats;

    [Header("Networking")] 
    private NetworkTimer timer;
    private const float tickRate = 64f;
    private const int BUFFER_SIZE = 1024;
    
    // Network client behavior
    public CircularBuffer<StatePayload> stateBuffer;
    public CircularBuffer<InputPayload> inputBuffer;
    private StatePayload lastServerState;
    private StatePayload lastProcessedState;
    
    // Network server behavior
    private CircularBuffer<StatePayload> serverStateBuffer;
    private Queue<InputPayload> serverInputQueue;
    [SerializeField] private float reconciliationThreshold = 10f;


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
        _playerStats = GetComponent<PlayerStats>();
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

        lastServerState = statePayload;
    }
    
    StatePayload SimulateMovement(InputPayload input)
    {
        Physics.simulationMode = SimulationMode.Script;
        MovePlayer(input.inputVector);
        Physics.Simulate(Time.fixedDeltaTime);
        Physics.simulationMode = SimulationMode.FixedUpdate;
        
        RotatePlayer(input.rotationVector);

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
        if (!IsClient && !IsOwner) return;

        var currentTick = timer.CurrentTick;
        var bufferIndex = currentTick % BUFFER_SIZE;
        var inputPayload = new InputPayload()
        {
            tick = currentTick,
            inputVector = _playerInput.Player.Move.ReadValue<Vector3>(),
            rotationVector = _playerInput.Player.Look.ReadValue<Vector2>(),
            isJumping = _playerInput.Player.Jump.IsPressed(),
            isFiring = _playerInput.Player.Fire.IsPressed()
        };
        
        inputBuffer.Add(inputPayload, bufferIndex);
        SendToServerRpc(inputPayload);
        
        var statePayload = ProcessMovement(inputPayload);
        stateBuffer.Add(statePayload, bufferIndex);
        
        HandleServerReconciliation();
    }

    private bool ShouldReconcile()
    {
        var isNewServerState = !lastServerState.Equals(default);
        var isLastStateUndefinedOrDifferent = lastProcessedState.Equals(default) || !lastProcessedState.Equals(lastServerState);
        
        return isNewServerState && !isLastStateUndefinedOrDifferent;
    }

    private void ReconcileState(StatePayload rewindState)
    {
        transform.position = rewindState.position;
        transform.rotation = rewindState.rotation;
        _rigidbody.velocity = rewindState.velocity;
        _rigidbody.angularVelocity = rewindState.angularVelocity;

        if (!rewindState.Equals(lastServerState)) return;
        
        stateBuffer.Add(rewindState, rewindState.tick);

        var tickToReplay = lastServerState.tick;

        while (tickToReplay < timer.CurrentTick)
        {
            var bufferIndex = tickToReplay % BUFFER_SIZE;
            var statePayload = ProcessMovement(inputBuffer.Get(bufferIndex));
            stateBuffer.Add(statePayload, bufferIndex);
            tickToReplay++;
        }
    }

    private void HandleServerReconciliation()
    {
        if (!ShouldReconcile()) return;

        float positionError;
        int bufferIndex;

        StatePayload rewindState = default;
        
        bufferIndex = lastServerState.tick % BUFFER_SIZE;
        if (bufferIndex - 1 < 0) return;

        rewindState = IsHost ? serverStateBuffer.Get(bufferIndex - 1) : lastServerState;
        positionError = Vector3.Distance(rewindState.position, stateBuffer.Get(bufferIndex).position);

        if (positionError > reconciliationThreshold)
        {
            ReconcileState(rewindState);
        }

        lastProcessedState = lastServerState;
    }
    
    [ServerRpc]
    private void SendToServerRpc(InputPayload inputPayload)
    {
        serverInputQueue.Enqueue(inputPayload);
    }

    private StatePayload ProcessMovement(InputPayload input)
    {
        MovePlayer(input.inputVector);
        RotatePlayer(input.rotationVector);
        JumpPlayer(input.isJumping);
        ShootPlayer(input.isFiring);

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

    private void JumpPlayer(bool isJumping)
    {
        if (!isJumping) return;
        
        var isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);

        if (!isGrounded) return;

        _rigidbody.AddForce(Vector3.up * 5f, ForceMode.Impulse);
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
        
        // fire once every 1 second based on playerstats firetime
        if (timer.CurrentTick % (tickRate / _playerStats.FireTime) != 0)
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

        bullet.NetworkObject.Spawn();
        bullet.Rigidbody.AddForce(_shootingPoint.forward * Mathf.Floor(2f * bullet.Speed), ForceMode.Impulse);
    }
}
