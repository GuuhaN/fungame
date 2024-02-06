using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody), typeof(NetworkObject), typeof(MyPlayerInput))]
public class NetworkPlayer : NetworkBehaviour
{
    [Header("Player")] 
    [SerializeField] private GameObject _playerCamera;
    [SerializeField] private Transform _shootingPoint;

    [Header("Movement")]
    [SerializeField, Range(1, 10)] private int _movementSpeed;
    [SerializeField] private float _maxVelocity;
    [SerializeField, Range(0, 90)] private float _clampedYRotation;
    [SerializeField] private bool _isGrounded;
    [SerializeField] private bool _jumped;
    

    [Header("Shooting")]
    [SerializeField, Range(1, 10)]
    private int _shootingForce;

    private float TargetYRotation { get; set; }

    private MyPlayerInput _playerInput;
    private Rigidbody _rigidbody;
    private PlayerStats _playerStats;
    private Animator _animator;
    private Collider _collider;

    [Header("Networking")] 
    private NetworkTimer timer;
    private const float tickRate = 64f;
    private const ushort BUFFER_SIZE = 1024;
    
    // Network client behavior
    public CircularBuffer<StatePayload> stateBuffer;
    public CircularBuffer<InputPayload> inputBuffer;
    private StatePayload lastServerState;
    private StatePayload lastProcessedState;
    
    // Network server behavior
    private CircularBuffer<StatePayload> serverStateBuffer;
    private Queue<InputPayload> serverInputQueue;
    [SerializeField] private float reconciliationThreshold = 10f;
    
    [SerializeField] private Vector3 lastInputDirection;
    private float currentDrag;
    
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
        _animator = GetComponent<Animator>();
        _collider = GetComponent<Collider>();

        if (IsOwner)
        {
            GetComponentInChildren<AudioListener>().enabled = true;
            _playerCamera.GetComponent<Camera>().enabled = true;
            _playerInput = new();
            _playerInput.Enable();
            currentDrag = _rigidbody.drag;
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

    public void FixedUpdate()
    {
        timer.Update();
        
        if (!Application.isFocused)
        {
            return;
        }

        while (timer.ShouldTick())
        {
            HandleClientTick();
            HandleServerTick();
        }
    }

    private void Update()
    {
        if (IsClient && IsLocalPlayer)
        {
            IsGrounded(_isGrounded);
        }
        else if (IsClient && IsLocalPlayer)
        {
            IsGroundedServerRpc(_isGrounded);
        }
    }

    private void HandleServerTick()
    {
        if (!IsServer)
        {
            return;
        }
        
        var bufferIndex = -1;
        while (serverInputQueue.Count > 0)
        {
            var inputPayload = serverInputQueue.Dequeue();
            
             bufferIndex = inputPayload.tick % BUFFER_SIZE;

             var statePayload = ProcessMovement(inputPayload);
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

    private void HandleClientTick()
    {
        if (!IsClient && !IsOwner) return;

        var currentTick = timer.CurrentTick;
        var bufferIndex = currentTick % BUFFER_SIZE;
        var inputPayload = new InputPayload
        {
            tick = currentTick,
            inputVector = _playerInput.Player.Move.ReadValue<Vector3>(),
            rotationVector = _playerInput.Player.Look.ReadValue<Vector2>(),
            isJumping = _playerInput.Player.Jump.triggered && _playerInput.Player.Jump.ReadValue<float>() > 0,
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
        ShootBulletServerRpc(input.isFiring);

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
        
        // if (moveDirection == Vector3.zero)
        // {
        //     _rigidbody.velocity = new Vector3(_rigidbody.velocity.x / 1.025f, _rigidbody.velocity.y, _rigidbody.velocity.z / 1.025f);
        // }
        //
        // _rigidbody.velocity = new Vector3(_rigidbody.velocity.x, _rigidbody.velocity.y > 0 ? _rigidbody.velocity.y : _rigidbody.velocity.y  * 1.04f, _rigidbody.velocity.z) +
        //                       moveDirection * (_movementSpeed * 15f * Time.deltaTime);
        //
        // _animator.speed = Mathf.InverseLerp(0, 1, _rigidbody.velocity.magnitude);
        //

        if (_rigidbody.velocity.magnitude < _maxVelocity)
        {
            Vector3 force = moveDirection * (_movementSpeed * (_isGrounded ? 5f : 1f));
            _rigidbody.AddForce(force, ForceMode.Force);
        }
        
        _animator.speed = Mathf.InverseLerp(0, 1, _rigidbody.velocity.magnitude);

        lastInputDirection = moveDirection;
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

        if (!_isGrounded) return;

        _rigidbody.AddForce(Vector3.up * 5.0f, ForceMode.Impulse);
    }

    [ServerRpc]
    private void ShootBulletServerRpc(bool isFiring)
    {
        if (!IsClient && !IsLocalPlayer)
        {
            return;
        }

        SpawnBullet(isFiring);
    }

    [ServerRpc]
    private void IsGroundedServerRpc(bool isGrounded)
    {
        IsGrounded(isGrounded);
    }

    private void IsGrounded(bool isGrounded)
    {
        if (!IsOwner)
        {
            return;
        }

        isGrounded = Physics.Raycast(transform.position, -transform.up, _collider.bounds.size.y / 2 + .01f);
    }

    private void SpawnBullet(bool isFiring)
    {
        if (!isFiring)
        {
            return;
        }
        
        if(_playerStats.FireRate.Value > 0f)
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
        _playerStats.ResetShootTimer();
    }
}
