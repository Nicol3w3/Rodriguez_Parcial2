using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TPMovement_Controller : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float sprintSpeed = 8f;
    
    [Header("Acceleration & Inertia")]
    public float acceleration = 15f;
    public float deceleration = 20f;
    public float airControl = 3f;
    public float groundFriction = 8f;
    public float airFriction = 2f;
    
    [Header("Jump Settings")]
    public float jumpHeight = 1.2f;
    public float jumpTimeout = 0.1f;
    public float fallTimeout = 0.2f;
    public float gravityMultiplier = 2f;
    
    [Header("Camera")]
    public Transform cam;
    public float smoothTurn = 0.1f;
    
    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    
    [Header("Bullet Settings")]
    public float bulletDamage = 25f;
    public float bulletVisualRange = 30f;
    public float bulletRaycastRange = 100f;

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float enemyCollisionDamage = 5f;

    [Header("Stamina Settings")]
    public float maxStamina = 10f;
    public float currentStamina;
    public float staminaDrainRate = 2f;
    public float staminaRegenRate = 1f;
    public float enemySightStaminaDrain = 1f;

    [Header("UI References")]
    public Slider healthBarSlider;
    public Text healthText;
    public Slider staminaBarSlider;

    [Header("Input System")]
    public InputActionReference movementAction;
    public InputActionReference sprintAction;
    private InputAction shootAction;
    public InputActionReference jumpAction;

    [Header("Shooting")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform barrelTransform;
    [SerializeField] private Transform bulletParent;
    // [SerializeField] private float bulletRange = 25f;
    [SerializeField] private float fireRate = 0.5f;

    // Componentes
    private CharacterController controller;
    
    // Estados
    private Vector2 movementInput;
    private bool jumpPressed;
    private bool isSprinting;
    private bool isGrounded;
    private bool isJumping;
    
    // Variables de tiempo
    private float jumpTimeoutDelta;
    private float fallTimeoutDelta;
    
    // Velocidades y vectores
    private Vector3 currentVelocity;
    private Vector3 targetVelocity;
    private Vector3 verticalVelocity;
    private float turnSmoothVelocity;

    // Shooting
    private float nextFireTime = 0f;
    private bool canShoot = true;

    // Referencias al enemigo
    private FieldOfView enemyFOV;
    private bool isBeingWatchedByEnemy = false;

    // Damage Detection
    public Collider damageTrigger;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        
        var playerInput = GetComponent<PlayerInput>();
        shootAction = playerInput.actions["Attack"];
        
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        jumpTimeoutDelta = jumpTimeout;
        fallTimeoutDelta = fallTimeout;

        if (damageTrigger == null)
        {
            damageTrigger = GetComponentInChildren<Collider>();
            if (damageTrigger == null)
            {
                Debug.LogError("No se encontró collider de daño en el jugador");
            }
            else
            {
                damageTrigger.isTrigger = true;
            }
        }

        FindEnemyFOV();
        UpdateHealthUI();
        UpdateStaminaUI();
    }

    private void OnEnable()
    {
        shootAction.performed += _ => TryToShoot();
        
        movementAction.action.Enable();
        movementAction.action.performed += OnMovementPerformed;
        movementAction.action.canceled += OnMovementCanceled;
        
        jumpAction.action.Enable();
        jumpAction.action.performed += OnJumpPerformed;
        jumpAction.action.canceled += OnJumpCanceled;

        sprintAction.action.Enable();
        sprintAction.action.performed += OnSprintPerformed;
        sprintAction.action.canceled += OnSprintCanceled;
    }

    private void OnDisable()
    {
        shootAction.performed -= _ => TryToShoot();
        
        movementAction.action.performed -= OnMovementPerformed;
        movementAction.action.canceled -= OnMovementCanceled;
        movementAction.action.Disable();
        
        jumpAction.action.performed -= OnJumpPerformed;
        jumpAction.action.canceled -= OnJumpCanceled;
        jumpAction.action.Disable();

        sprintAction.action.performed -= OnSprintPerformed;
        sprintAction.action.canceled -= OnSprintCanceled;
        sprintAction.action.Disable();
    }

    private void Update()
    {
        HandleStamina();
        GroundedCheck();
        JumpAndGravity();
        Move();
    }

    private void GroundedCheck()
    {
        bool wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (!wasGrounded && isGrounded)
        {
            isJumping = false;
        }
    }

    private void Move()
{
    float targetSpeed = GetTargetSpeed();
    
    if (movementInput.magnitude < 0.1f)
    {
        targetSpeed = 0f;
    }

    Vector3 inputDirection = new Vector3(movementInput.x, 0f, movementInput.y).normalized;
    
    // ✅ ROTACIÓN: Siempre hacia donde mira la cámara
    RotateTowardsCamera();
    
    // ✅ MOVIMIENTO: Relativo a la rotación actual del personaje
    if (inputDirection.magnitude >= 0.1f)
    {
        // Movimiento relativo a la dirección que mira el personaje
        Vector3 targetDirection = (transform.forward * inputDirection.z + transform.right * inputDirection.x).normalized;
        targetVelocity = targetDirection * targetSpeed;
    }
    else
    {
        targetVelocity = Vector3.zero;
    }

    // ACELERACIÓN Y FRICCIÓN (se mantiene igual)
    float currentAcceleration = isGrounded ? acceleration : airControl;
    float currentDeceleration = isGrounded ? deceleration : airFriction;
    
    if (targetVelocity.magnitude > 0.1f)
    {
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, currentAcceleration * Time.deltaTime);
    }
    else
    {
        currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, currentDeceleration * Time.deltaTime);
    }

    if (isGrounded && currentVelocity.magnitude > 0.1f)
    {
        currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, groundFriction * Time.deltaTime);
    }

    Vector3 motion = currentVelocity + verticalVelocity;
    controller.Move(motion * Time.deltaTime);
}

// ✅ NUEVO: Rotación independiente del movimiento
private void RotateTowardsCamera()
{
    // Obtener dirección forward de la cámara (horizontal solamente)
    Vector3 cameraForward = cam.forward;
    cameraForward.y = 0f;
    
    if (cameraForward.sqrMagnitude > 0.01f)
    {
        cameraForward.Normalize();
        
        // Rotación suave hacia la dirección de la cámara
        Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothTurn * Time.deltaTime * 10f);
    }
}

    private float GetTargetSpeed()
    {
        if (isSprinting && currentStamina > 0 && movementInput.magnitude > 0.1f)
        {
            return sprintSpeed;
        }
        else if (movementInput.magnitude > 0.1f)
        {
            return isGrounded ? runSpeed : runSpeed * 0.8f;
        }
        
        return walkSpeed;
    }

    private void JumpAndGravity()
    {
        if (isGrounded)
        {
            fallTimeoutDelta = fallTimeout;

            if (verticalVelocity.y < 0f)
            {
                verticalVelocity.y = -2f;
            }

            if (jumpPressed && jumpTimeoutDelta <= 0f)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
                isJumping = true;
                jumpTimeoutDelta = jumpTimeout;
            }

            if (jumpTimeoutDelta >= 0f)
            {
                jumpTimeoutDelta -= Time.deltaTime;
            }
        }
        else
        {
            jumpTimeoutDelta = jumpTimeout;

            if (fallTimeoutDelta >= 0f)
            {
                fallTimeoutDelta -= Time.deltaTime;
            }

            jumpPressed = false;
        }

        if (!isGrounded || isJumping)
        {
            verticalVelocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, -50f);
        }
    }

    private void HandleStamina()
    {
        CheckIfBeingWatched();

        if (isBeingWatchedByEnemy)
        {
            currentStamina -= enemySightStaminaDrain * Time.deltaTime;
            currentStamina = Mathf.Max(currentStamina, 0f);
        }
        else if (isSprinting && movementInput.magnitude > 0.1f && currentStamina > 0)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            currentStamina = Mathf.Max(currentStamina, 0f);

            if (currentStamina <= 0)
            {
                isSprinting = false;
                currentStamina = 0f;
            }
        }
        else
        {
            if (currentStamina < maxStamina && !isBeingWatchedByEnemy)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);
            }
        }

        UpdateStaminaUI();
    }

    private void FindEnemyFOV()
    {
        GameObject enemy = GameObject.FindGameObjectWithTag("Enemy");
        if (enemy != null)
        {
            enemyFOV = enemy.GetComponent<FieldOfView>();
            if (enemyFOV == null)
            {
                Debug.LogWarning("No se encontró componente FieldOfView en el enemigo");
            }
        }
    }

    private void CheckIfBeingWatched()
    {
        if (enemyFOV != null)
        {
            isBeingWatchedByEnemy = enemyFOV.canSeePlayer;
        }
        else
        {
            if (enemyFOV == null) FindEnemyFOV();
            isBeingWatchedByEnemy = false;
        }
    }

    // INPUT METHODS
    private void OnMovementPerformed(InputAction.CallbackContext context)
    {
        movementInput = context.ReadValue<Vector2>();
    }

    private void OnMovementCanceled(InputAction.CallbackContext context)
    {
        movementInput = Vector2.zero;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        jumpPressed = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        jumpPressed = false;
    }

    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        if (currentStamina > 0)
        {
            isSprinting = true;
        }
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isSprinting = false;
    }

    // SHOOTING METHODS
    private void TryToShoot()
    {
        if (Time.time >= nextFireTime && canShoot)
        {
            ShootGun();
            nextFireTime = Time.time + fireRate;
        }
    }

    private void ShootGun()
    {
        if (BulletPool.Instance == null)
        {
            Debug.LogError("BulletPool no encontrado!");
            return;
        }

        Vector3 shootPosition = barrelTransform.position;
        Vector3 shootDirection = cam.forward;

        var bullet = BulletPool.Instance.GetBullet<HybridBullet>(
            gameObject, shootPosition, shootDirection, bulletDamage);

        if (bullet != null)
        {
            bullet.SetVisualRange(bulletVisualRange);
            bullet.SetRaycastRange(bulletRaycastRange);
            bullet.OnBulletHit += OnBulletHit;
        }
    }

    private void OnBulletHit(BulletBase bullet, GameObject hitObject)
    {
        if (hitObject.CompareTag("Enemy"))
        {
//            Debug.Log($"🎯 Impacto en enemigo!");
        }
    }

    // HEALTH & DAMAGE METHODS
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            TakeDamage(enemyCollisionDamage);
            ApplyKnockback(other.transform.position);
        }
    }

    private void ApplyKnockback(Vector3 enemyPosition)
    {
        Vector3 knockbackDirection = (transform.position - enemyPosition).normalized;
        knockbackDirection.y = 0.1f;
        
        if (TryGetComponent<CharacterController>(out var controller))
        {
            controller.Move(knockbackDirection * 2f * Time.deltaTime);
        }
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("¡Jugador muerto!");
    }

    public void Heal(float healAmount)
    {
        currentHealth += healAmount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthUI();
    }

    // UI METHODS
    private void UpdateHealthUI()
    {
        if (healthBarSlider != null)
        {
            healthBarSlider.value = currentHealth / maxHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth:F0}/{maxHealth:F0}";
        }
    }

    private void UpdateStaminaUI()
    {
        if (staminaBarSlider != null)
        {
            staminaBarSlider.value = currentStamina / maxStamina;
        }
    }

    // PUBLIC METHODS
    public float GetStaminaPercent()
    {
        return currentStamina / maxStamina;
    }

    public bool IsSprinting()
    {
        return isSprinting;
    }

    public bool CanSprint()
    {
        return currentStamina > 0;
    }

    public bool IsBeingWatchedByEnemy()
    {
        return isBeingWatchedByEnemy;
    }

    public void SetCanShoot(bool value)
    {
        canShoot = value;
    }
    
    public void ChangeFireRate(float newFireRate)
    {
        fireRate = newFireRate;
    }

    // DEBUG
    private void OnGUI()
    {
        #if UNITY_EDITOR
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Vida: {currentHealth:F0}/{maxHealth:F0}");
        GUILayout.Label($"Stamina: {currentStamina:F1}/{maxStamina}");
        GUILayout.Label($"Sprinting: {isSprinting}");
        GUILayout.Label($"Velocidad: {currentVelocity.magnitude:F1}");
        GUILayout.Label($"Enemigo te ve: {isBeingWatchedByEnemy}");
        GUILayout.Label($"En suelo: {isGrounded}");
        GUILayout.EndArea();
        #endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }
}