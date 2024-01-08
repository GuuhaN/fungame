using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject _playerCamera;
    [SerializeField] private Transform _shootingPoint;
    
    [Header("Movement")]
    [SerializeField] private float _movementSpeed;
    [SerializeField, Range(0, 90)] private float _clampedYRotation;
    
    [Header("Shooting")]
    [SerializeField, Range(1, 10)] private int _shootingForce;

    private float TargetYRotation { get; set; }
    
    private MyPlayerInput _playerInput;
    private Rigidbody _rigidbody;

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
        if (!IsOwner || !Application.isFocused)
        {
            return;
        }
        
        var moveInput = _playerInput.Player.Move.ReadValue<Vector3>();
        var lookInput = _playerInput.Player.Look.ReadValue<Vector2>();

        MovePlayer(moveInput);
        RotatePlayer(lookInput);
        JumpPlayer();

        if (IsServer && IsLocalPlayer)
        {
            ShootPlayer(_playerInput.Player.Fire.triggered);
        }
        else if(IsClient && IsLocalPlayer)
        {
            ShootPlayerServerRPC(_playerInput.Player.Fire.triggered);
        }
        
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
    
            if(TargetYRotation > _clampedYRotation)
                TargetYRotation = _clampedYRotation;
            else if(TargetYRotation < -_clampedYRotation)
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

        var bulletObject = Instantiate(Resources.Load("Bullet"), _shootingPoint.position, _shootingPoint.rotation) as GameObject;
        
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

    public void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("HitObject"))
        {
            return;
        }
        
        var projectile = collision.gameObject.GetComponent<Projectile>();
        
        if(projectile == null)
        {
            return;
        }
        
        Debug.Log(projectile.Damage);
    }
}
