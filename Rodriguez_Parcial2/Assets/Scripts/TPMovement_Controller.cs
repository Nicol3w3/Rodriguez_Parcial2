using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class TPMovement_Controller : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float sprintSpeed = 8f;
    
    [Header("Crouch Settings")]
    public float crouchHeight = 0.9f;
    public float crouchSpeedMultiplier = 0.75f;
    public float crouchTransitionSpeed = 5f;
    private float originalHeight;
    private Vector3 originalCenter;
    private bool isCrouching = false;
    
    [Header("Visual Crouch")]
    public Transform playerVisual;
    private Vector3 originalVisualScale;
    private Vector3 originalVisualPosition;
    private float crouchVisualScale = 0.5f;
    
    [Header("Ammo Settings")]
    public int maxAmmo = 15;
    public int maxMagazines = 3;
    private int currentAmmo;
    private int currentMagazines;
    private bool isReloading = false;
    
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
    public TextMeshProUGUI healthText;
    public Slider staminaBarSlider;
    public TextMeshProUGUI staminaText;
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI magazinesText;
    public GameObject reloadIndicator;

    [Header("Pause Reference")]
    public PauseMenu pauseMenu;

    [Header("Input System")]
    public InputActionReference movementAction;
    public InputActionReference sprintAction;
    public InputActionReference crouchAction;
    public InputActionReference reloadAction;
    private InputAction shootAction;
    public InputActionReference jumpAction;

    [Header("Shooting")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform barrelTransform;
    [SerializeField] private Transform bulletParent;
    [SerializeField] private float fireRate = 0.5f;

    [Header("Respawn Settings")]
    public Transform spawnPoint;
    public InputActionReference respawnAction;
    public InputActionReference restartSceneAction;
    
    // Variables de estado
    private bool isDead = false;
    private Vector3 initialSpawnPosition;
    private Quaternion initialRotation;
    private Vector3 initialVisualScale;
    private Vector3 initialVisualPosition;

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
    private CameraFieldOfView[] cameraFOVs;
    private bool isBeingWatchedByEnemy = false;

    // Damage Detection
    public Collider damageTrigger;

     private void Awake()
    {
        controller = GetComponent<CharacterController>();
        originalHeight = controller.height;
        originalCenter = controller.center;
        
        if (playerVisual == null)
        {
            playerVisual = transform.Find("TPPlayer_Body");
            if (playerVisual == null)
            {
                Debug.LogWarning("No se encontró el modelo visual TPPlayer_Body");
            }
        }
        
        if (playerVisual != null)
        {
            originalVisualScale = playerVisual.localScale;
            originalVisualPosition = playerVisual.localPosition;
  //          Debug.Log($"🔍 Escala visual original: {originalVisualScale}");
//            Debug.Log($"🔍 Posición visual original: {originalVisualPosition}");
        }
        
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

        initialSpawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        initialRotation = transform.rotation;
        
        if (playerVisual != null)
        {
            initialVisualScale = playerVisual.localScale;
            initialVisualPosition = playerVisual.localPosition;
        }

        // Inicializar munición
        currentAmmo = maxAmmo;
        currentMagazines = maxMagazines - 1;

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
        UpdateAmmoUI();
        
        // Ocultar indicador de recarga si existe
        if (reloadIndicator != null)
            reloadIndicator.SetActive(false);
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

        crouchAction.action.Enable();
        crouchAction.action.performed += OnCrouchPerformed;
        
        reloadAction.action.Enable();
        reloadAction.action.performed += OnReloadPerformed;

         if (respawnAction != null)
        {
            respawnAction.action.Enable();
            respawnAction.action.performed += OnRespawnPerformed;
        }
        
        if (restartSceneAction != null)
        {
            restartSceneAction.action.Enable();
            restartSceneAction.action.performed += OnRestartScenePerformed;
        }
    }

    private void OnDisable()
{
    // ✅ SOLO LIMPIAR SI EL OBJETO AÚN EXISTE
    if (this == null) return;
    
    try
    {
        // Limpiar callback del disparo
        if (shootAction != null)
        {
            shootAction.performed -= _ => TryToShoot();
        }
        
        // Limpiar callback de reinicio de escena específicamente
        if (restartSceneAction != null && restartSceneAction.action != null)
        {
            restartSceneAction.action.performed -= OnRestartScenePerformed;
        }
    }
    catch (System.Exception e)
    {
        Debug.LogWarning($"⚠️ Error en OnDisable: {e.Message}");
    }
}

    private void Update()
    {
         if (IsGamePaused()) 
    {
        // Opcional: Detener cualquier movimiento residual
        if (controller != null && controller.enabled)
        {
            currentVelocity = Vector3.zero;
            verticalVelocity = Vector3.zero;
        }
        return;
    }

        if (isDead) return;
        HandleStamina();
        GroundedCheck();
        JumpAndGravity();
        Move();
        HandleCrouch();
    }

     private void GroundedCheck()
    {
        bool wasGrounded = isGrounded;
        
        // ✅ USAR EL CENTRO Y RADIO DEL CHARACTERCONTROLLER PARA DETECCIÓN MÁS PRECISA
        float checkDistance = controller.height / 2 + 0.1f;
        Vector3 checkPosition = transform.position + controller.center;
        
        isGrounded = Physics.CheckSphere(checkPosition, checkDistance, groundMask);

        // Debug visual
        Debug.DrawRay(checkPosition, Vector3.down * checkDistance, isGrounded ? Color.green : Color.red);

        if (!wasGrounded && isGrounded)
        {
            isJumping = false;
            // Debug.Log("✅ Tocando suelo");
        }
        else if (wasGrounded && !isGrounded)
        {
            // Debug.Log("❌ En el aire");
        }
    }

    private void FixedUpdate()
{
    // ✅ VERIFICAR SI EL JUEGO ESTÁ EN PAUSA
    if (IsGamePaused()) return;
    
    // Tu código FixedUpdate existente...
}

    private void Move()
    {
        float targetSpeed = GetTargetSpeed();
        
        if (movementInput.magnitude < 0.1f)
        {
            targetSpeed = 0f;
        }

        Vector3 inputDirection = new Vector3(movementInput.x, 0f, movementInput.y).normalized;
        
        RotateTowardsCamera();
        
        if (inputDirection.magnitude >= 0.1f)
        {
            Vector3 targetDirection = (transform.forward * inputDirection.z + transform.right * inputDirection.x).normalized;
            targetVelocity = targetDirection * targetSpeed;
        }
        else
        {
            targetVelocity = Vector3.zero;
        }

        // Aceleración y fricción
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

        // Fricción adicional en el suelo
        if (isGrounded && currentVelocity.magnitude > 0.1f)
        {
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, groundFriction * Time.deltaTime);
        }

        // Aplicar movimiento
        Vector3 motion = currentVelocity + verticalVelocity;
        controller.Move(motion * Time.deltaTime);
        
        // Debug de movimiento
        Debug.DrawRay(transform.position, currentVelocity, Color.blue);
        Debug.DrawRay(transform.position, verticalVelocity, Color.yellow);
    }   

    // ✅ ROTACIÓN: Compatible con Cinemachine
    private void RotateTowardsCamera()
    {
        Vector3 cameraForward = cam.forward;
        cameraForward.y = 0f;
        
        if (cameraForward.sqrMagnitude > 0.01f)
        {
            cameraForward.Normalize();
            
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothTurn * Time.deltaTime * 10f);
        }
    }

    private float GetTargetSpeed()
    {
        float baseSpeed;
        
        if (isSprinting && currentStamina > 0 && movementInput.magnitude > 0.1f)
        {
            baseSpeed = sprintSpeed;
        }
        else if (movementInput.magnitude > 0.1f)
        {
            baseSpeed = isGrounded ? runSpeed : runSpeed * 0.8f;
        }
        else
        {
            baseSpeed = walkSpeed;
        }
        
        if (isCrouching)
        {
            baseSpeed *= crouchSpeedMultiplier;
        }
        
        return baseSpeed;
    }

   // ✅ CORREGIDO: Manejar agachado con ajuste visual
     private void HandleCrouch()
    {
        float targetHeight = isCrouching ? crouchHeight : originalHeight;
        Vector3 targetCenter = isCrouching ? new Vector3(0, -crouchHeight/2, 0) : originalCenter;
        
        // Transición suave del CharacterController
        if (Mathf.Abs(controller.height - targetHeight) > 0.01f)
        {
            controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            controller.center = Vector3.Lerp(controller.center, targetCenter, crouchTransitionSpeed * Time.deltaTime);
        }

        // ✅ CORREGIDO: Transición visual MÁS SIMPLE Y DIRECTA
        if (playerVisual != null)
        {
            Vector3 targetVisualScale = isCrouching ? 
                new Vector3(originalVisualScale.x, originalVisualScale.y * crouchVisualScale, originalVisualScale.z) : 
                originalVisualScale;

            Vector3 targetVisualPosition = isCrouching ? 
                new Vector3(originalVisualPosition.x, originalVisualPosition.y - (originalVisualScale.y - targetVisualScale.y) * 0.5f, originalVisualPosition.z) : 
                originalVisualPosition;

            // Aplicar cambios directamente con Lerp para suavidad
            playerVisual.localScale = Vector3.Lerp(playerVisual.localScale, targetVisualScale, crouchTransitionSpeed * Time.deltaTime);
            playerVisual.localPosition = Vector3.Lerp(playerVisual.localPosition, targetVisualPosition, crouchTransitionSpeed * Time.deltaTime);

            // Debug visual
            if (isCrouching && Time.frameCount % 30 == 0)
            {
//                Debug.Log($"🧎 Escala: {playerVisual.localScale} | Posición: {playerVisual.localPosition}");
            }
        }
    }

    [ContextMenu("Forzar Agachado")]
    private void ForceCrouch()
    {
        isCrouching = true;
        if (playerVisual != null)
        {
            Vector3 targetScale = new Vector3(originalVisualScale.x, originalVisualScale.y * crouchVisualScale, originalVisualScale.z);
            playerVisual.localScale = targetScale;
            
            Vector3 targetPosition = new Vector3(originalVisualPosition.x, originalVisualPosition.y - (originalVisualScale.y - targetScale.y) * 0.5f, originalVisualPosition.z);
            playerVisual.localPosition = targetPosition;
            
            Debug.Log($"🔄 Agachado forzado - Escala: {targetScale}");
        }
    }

    [ContextMenu("Forzar De Pie")]
    private void ForceStand()
    {
        isCrouching = false;
        if (playerVisual != null)
        {
            playerVisual.localScale = originalVisualScale;
            playerVisual.localPosition = originalVisualPosition;
            Debug.Log($"🔄 De pie forzado - Escala: {originalVisualScale}");
        }
    }

    private void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        isCrouching = !isCrouching;
//        Debug.Log($"🧎 {(isCrouching ? "Agachado" : "De pie")}");
    }

    // ✅ NUEVO: Recargar con la tecla R
    private void OnReloadPerformed(InputAction.CallbackContext context)
    {
        if (!isReloading && currentAmmo < maxAmmo && currentMagazines > 0)
        {
            StartReload();
        }
        else if (currentAmmo == maxAmmo)
        {
            Debug.Log("✅ Recámara llena");
        }
        else if (currentMagazines <= 0)
        {
            Debug.Log("❌ Sin cargadores adicionales");
        }
    }

    private void StartReload()
    {
        isReloading = true;
        canShoot = false;
        
        // Mostrar indicador de recarga
        if (reloadIndicator != null)
            reloadIndicator.SetActive(true);
            
//        Debug.Log("🔄 Recargando...");
        
        Invoke(nameof(FinishReload), 1f);
    }

     private void FinishReload()
    {
        int ammoNeeded = maxAmmo - currentAmmo;
        int ammoToAdd = Mathf.Min(ammoNeeded, maxAmmo, currentMagazines * maxAmmo);
        
        currentAmmo += ammoToAdd;
        currentMagazines--;
        
        isReloading = false;
        canShoot = true;
        
        // Ocultar indicador de recarga
        if (reloadIndicator != null)
            reloadIndicator.SetActive(false);
        
     //   Debug.Log($"✅ Recarga completada: {currentAmmo}/{maxAmmo} | Cargadores: {currentMagazines}");
        UpdateAmmoUI();
    }

    // ✅ NUEVO: Actualizar UI de munición
   private void UpdateAmmoUI()
    {
        // Actualizar texto de munición actual
        if (ammoText != null)
        {
            ammoText.text = $"{currentAmmo}";
            
            // Cambiar color según la munición
            if (currentAmmo > maxAmmo * 0.3f)
                ammoText.color = Color.white;
            else if (currentAmmo > 0)
                ammoText.color = Color.yellow;
            else
                ammoText.color = Color.red;
        }

        // Actualizar texto de cargadores
        if (magazinesText != null)
        {
            magazinesText.text = $"{currentMagazines}";
            
            // Cambiar color según cargadores restantes
            if (currentMagazines > 1)
                magazinesText.color = Color.white;
            else if (currentMagazines > 0)
                magazinesText.color = Color.yellow;
            else
                magazinesText.color = Color.red;
        }
    }

     private void JumpAndGravity()
    {
        if (isGrounded)
        {
            fallTimeoutDelta = fallTimeout;

            // Resetear velocidad vertical cuando toca el suelo
            if (verticalVelocity.y < 0f)
            {
                verticalVelocity.y = -2f; // Pequeña fuerza hacia abajo para mantener contacto
            }

            // ✅ SALTO: Permitir saltar solo si está en suelo y no está agachado
            if (jumpPressed && jumpTimeoutDelta <= 0f && !isCrouching)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
                isJumping = true;
                jumpTimeoutDelta = jumpTimeout;
//                Debug.Log("🦘 Saltando!");
            }

            // Manejar timeout del salto
            if (jumpTimeoutDelta >= 0f)
            {
                jumpTimeoutDelta -= Time.deltaTime;
            }
        }
        else
        {
            // Resetear timeout del salto cuando está en el aire
            jumpTimeoutDelta = jumpTimeout;

            // Manejar timeout de caída
            if (fallTimeoutDelta >= 0f)
            {
                fallTimeoutDelta -= Time.deltaTime;
            }

            // Resetear salto presionado
            jumpPressed = false;
        }

        // Aplicar gravedad siempre que no esté en el suelo o esté saltando
        if (!isGrounded || isJumping)
        {
            verticalVelocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
            
            // Limitar velocidad de caída máxima
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
//                Debug.LogWarning("No se encontró componente FieldOfView en el enemigo");
            }
        }

        cameraFOVs = FindObjectsByType<CameraFieldOfView>(FindObjectsSortMode.None);
    }

   private void CheckIfBeingWatched()
{
    // Buscar cualquier FieldOfView que pueda ver al jugador
    FieldOfView soldierFOV = FindAnyObjectByType<FieldOfView>();
    CameraFieldOfView cameraFOV = FindAnyObjectByType<CameraFieldOfView>();
    
    isBeingWatchedByEnemy = (soldierFOV != null && soldierFOV.canSeePlayer) || 
                           (cameraFOV != null && cameraFOV.canSeePlayer);
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
    if (IsGamePaused()) return;

    // ✅ VALIDAR QUE EL OBJETO NO ESTÉ DESTRUIDO
    if (this == null || !isActiveAndEnabled) return;
    
    if (Time.time >= nextFireTime && canShoot && !isReloading)
    {
        if (currentAmmo > 0)
        {
            ShootGun();
            currentAmmo--;
            UpdateAmmoUI();
            nextFireTime = Time.time + fireRate;
            
            if (currentAmmo <= 0)
            {
                // Debug.Log("⚠️ Recámara vacía - Presiona R para recargar");
            }
        }
        else
        {
            // Debug.Log("❌ Sin munición - Presiona R para recargar");
            if (currentMagazines > 0 && !isReloading)
            {
                StartReload();
            }
        }
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
    Vector3 shootDirection = GetShootDirection();
    
    // Debug visual
    Debug.DrawRay(shootPosition, shootDirection * 50f, Color.red, 2f);
    Debug.DrawRay(cam.position, cam.forward * 50f, Color.blue, 2f);

    var bullet = BulletPool.Instance.GetBullet<HybridBullet>(
        gameObject, shootPosition, shootDirection, bulletDamage);

    if (bullet != null)
    {
        // ✅ AGREGAR: Configurar como bala del jugador
        HybridBullet bulletScript = bullet.GetComponent<HybridBullet>();
        if (bulletScript != null)
        {
            bulletScript.dueño = this.gameObject;
            bulletScript.isPlayerBullet = true;
            
            // Opcional: También configurar layer
            bullet.gameObject.layer = LayerMask.NameToLayer("PlayerBullets");
        }

        bullet.SetVisualRange(bulletVisualRange);
        bullet.SetRaycastRange(bulletRaycastRange);
        bullet.OnBulletHit += OnBulletHit;
        
        // Debug.Log($"🔫 Disparo - Posición: {shootPosition}, Dirección: {shootDirection}");
    }
}
private Vector3 GetShootDirection()
{
    if (cam == null) return transform.forward;

    // ✅ OPCIÓN 1: Dirección directa de la cámara (más simple)
    // return cam.forward;
    
    // ✅ OPCIÓN 2: Raycast desde el centro exacto de la pantalla (más preciso)
    return GetPreciseShootDirection();
}

// ✅ CORREGIDO: Dirección precisa con raycast
private Vector3 GetPreciseShootDirection()
{
    // ✅ USAR cam.GetComponent<Camera>() para obtener la cámara
    Camera cameraComponent = cam.GetComponent<Camera>();
    if (cameraComponent == null)
    {
        Debug.LogError("No se encontró componente Camera en el transform de la cámara");
        return cam.forward;
    }
    
    Ray centerRay = cameraComponent.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
    RaycastHit hit;
    float maxRange = 1000f;
    
    // Realizar raycast para obtener punto exacto de impacto
    if (Physics.Raycast(centerRay, out hit, maxRange))
    {
        // ✅ DIRECCIÓN EXACTA DESDE EL CAÑÓN HACIA EL PUNTO DE IMPACTO
        Vector3 exactDirection = (hit.point - barrelTransform.position).normalized;
        
//        Debug.Log($"🎯 Impacto raycast: {hit.collider.name} a {hit.distance:F2}m");
        Debug.DrawLine(barrelTransform.position, hit.point, Color.green, 2f);
        
        return exactDirection;
    }
    else
    {
        // ✅ DIRECCIÓN HACIA UN PUNTO LEJANO EN LA DIRECCIÓN DE LA CÁMARA
        Vector3 targetPoint = centerRay.origin + centerRay.direction * maxRange;
        Vector3 exactDirection = (targetPoint - barrelTransform.position).normalized;
        
        return exactDirection;
    }
}

    private void OnBulletHit(BulletBase bullet, GameObject hitObject)
    {
        if (hitObject.CompareTag("Enemy"))
        {
            // Debug.Log($"🎯 Impacto en enemigo! Munición restante: {currentAmmo}");
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
        if (isDead) return; // ✅ No recibir daño si ya está muerto

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
        if (isDead) return;

        isDead = true;
//        Debug.Log("💀 Jugador muerto!");

        // ✅ DESACTIVAR todas las habilidades y modelo
        SetPlayerActive(false);

        // ✅ MOSTRAR MENSAJE DE MUERTE
        ShowDeathMessage();

        // ✅ OPCIONAL: Sonido de muerte
        // if (deathSound != null) AudioSource.PlayClipAtPoint(deathSound, transform.position);
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
            healthText.text = $"{Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}";
            
            // Cambiar color según la salud
            if (currentHealth > maxHealth * 0.6f)
                healthText.color = Color.green;
            else if (currentHealth > maxHealth * 0.3f)
                healthText.color = Color.yellow;
            else
                healthText.color = Color.red;
        }
    }

     private void UpdateStaminaUI()
    {
        if (staminaBarSlider != null)
        {
            staminaBarSlider.value = currentStamina / maxStamina;
        }

        if (staminaText != null)
        {
            staminaText.text = $"{currentStamina:F1}/{maxStamina}";
            
            // Cambiar color según el stamina
            if (currentStamina > maxStamina * 0.3f)
                staminaText.color = Color.cyan;
            else
                staminaText.color = Color.red;
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

    // ✅ NUEVO: Métodos para gestionar munición
    public void AddMagazine()
    {
        currentMagazines++;
        UpdateAmmoUI();
        Debug.Log($"➕ Cargador añadido. Total: {currentMagazines}");
    }

    public bool HasAmmo()
    {
        return currentAmmo > 0 || currentMagazines > 0;
    }

    public int GetCurrentAmmo()
    {
        return currentAmmo;
    }

    public int GetCurrentMagazines()
    {
        return currentMagazines;
    }

    // DEBUG
    private void OnGUI()
    {
        #if UNITY_EDITOR
        GUILayout.BeginArea(new Rect(10, 10, 300, 250));
        GUILayout.Label($"Vida: {currentHealth:F0}/{maxHealth:F0}");
        GUILayout.Label($"Stamina: {currentStamina:F1}/{maxStamina}");
        GUILayout.Label($"Sprinting: {isSprinting}");
        GUILayout.Label($"Velocidad: {currentVelocity.magnitude:F1}");
        GUILayout.Label($"Enemigo te ve: {isBeingWatchedByEnemy}");
        GUILayout.Label($"En suelo: {isGrounded}");
        GUILayout.Label($"Agachado: {isCrouching}");
        GUILayout.Label($"Munición: {currentAmmo}/{maxAmmo} | Cargadores: {currentMagazines}");
        GUILayout.Label($"Recargando: {isReloading}");
        GUILayout.EndArea();
        #endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }

     private void OnRespawnPerformed(InputAction.CallbackContext context)
    {
        if (isDead)
        {
            RespawnPlayer();
        }
    }

   private void OnRestartScenePerformed(InputAction.CallbackContext context)
{
    if (!context.performed) return;
    
    // ✅ DESREGISTRAR CALLBACKS ANTES de reiniciar
    CleanupAllInputCallbacks();
    
    // ✅ DESHABILITAR el PlayerInput completamente
    var playerInput = GetComponent<PlayerInput>();
    if (playerInput != null)
    {
        playerInput.enabled = false;
    }
    
    // ✅ USAR CORRUTINA para dar tiempo a la limpieza
    StartCoroutine(RestartSceneWithCleanup());
}

private IEnumerator RestartSceneWithCleanup()
{
    // Esperar un frame para que se complete la limpieza
    yield return null;
    
    // ✅ LIMPIAR INVOKES Y CORRUTINAS
    CancelInvoke();
    StopAllCoroutines();
    
    // Reiniciar escena
    Scene currentScene = SceneManager.GetActiveScene();
    SceneManager.LoadScene(currentScene.name);
}

    private void CleanupAllInputCallbacks()
{
//    Debug.Log("🧹 Limpiando todos los callbacks de input...");
    
    try
    {
        // ✅ DESREGISTRAR CALLBACK DEL DISPARO
        if (shootAction != null)
        {
            shootAction.performed -= _ => TryToShoot();
            shootAction = null;
        }
        
        // ✅ DESREGISTRAR TODOS LOS CALLBACKS DE INPUT ACTIONS
        if (movementAction != null && movementAction.action != null)
        {
            movementAction.action.performed -= OnMovementPerformed;
            movementAction.action.canceled -= OnMovementCanceled;
        }
        
        if (jumpAction != null && jumpAction.action != null)
        {
            jumpAction.action.performed -= OnJumpPerformed;
            jumpAction.action.canceled -= OnJumpCanceled;
        }
        
        if (sprintAction != null && sprintAction.action != null)
        {
            sprintAction.action.performed -= OnSprintPerformed;
            sprintAction.action.canceled -= OnSprintCanceled;
        }
        
        if (crouchAction != null && crouchAction.action != null)
        {
            crouchAction.action.performed -= OnCrouchPerformed;
        }
        
        if (reloadAction != null && reloadAction.action != null)
        {
            reloadAction.action.performed -= OnReloadPerformed;
        }
        
        if (respawnAction != null && respawnAction.action != null)
        {
            respawnAction.action.performed -= OnRespawnPerformed;
        }
        
        if (restartSceneAction != null && restartSceneAction.action != null)
        {
            restartSceneAction.action.performed -= OnRestartScenePerformed;
        }
        
        // ✅ DESHABILITAR TODAS LAS ACCIONES
        movementAction?.action?.Disable();
        jumpAction?.action?.Disable();
        sprintAction?.action?.Disable();
        crouchAction?.action?.Disable();
        reloadAction?.action?.Disable();
        respawnAction?.action?.Disable();
        restartSceneAction?.action?.Disable();
        
//        Debug.Log("✅ Callbacks limpiados correctamente");
    }
    catch (System.Exception e)
    {
        Debug.LogWarning($"⚠️ Error durante limpieza: {e.Message}");
    }
}


    private void SetPlayerActive(bool active)
    {
        // Desactivar modelo visual
        if (playerVisual != null)
        {
            playerVisual.gameObject.SetActive(active);
        }

        // Desactivar collider
        if (controller != null)
        {
            controller.enabled = active;
        }

        // Desactivar disparo
        SetCanShoot(active);

        // Detener movimiento
        if (!active)
        {
            currentVelocity = Vector3.zero;
            verticalVelocity = Vector3.zero;
            movementInput = Vector2.zero;
        }

        // Ocultar/mostrar UI según estado
        UpdateDeathUI(!active);
    }

    public void RespawnPlayer()
{
    if (!isDead) return;

    Debug.Log("🔄 Reapareciendo jugador...");

    // ✅ RESTAURAR todos los valores
    currentHealth = maxHealth;
    currentStamina = maxStamina;
    currentAmmo = maxAmmo;
    currentMagazines = maxMagazines - 1;
    isDead = false;

    // ✅ RESTAURAR posición y rotación
    if (spawnPoint != null)
    {
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;
    }
    else
    {
        transform.position = initialSpawnPosition;
        transform.rotation = initialRotation;
    }

    // ✅ RESTAURAR modelo visual
    if (playerVisual != null)
    {
        playerVisual.localScale = initialVisualScale;
        playerVisual.localPosition = initialVisualPosition;
    }

    // ✅ REACTIVAR todas las habilidades
    SetPlayerActive(true);

    // ✅ RESTAURAR UI
    UpdateHealthUI();
    UpdateStaminaUI();
    UpdateAmmoUI();
    HideDeathMessage(); // ✅ NOMBRE CORREGIDO

    Debug.Log("✅ Jugador reaparecido - Valores restaurados");
}

   private void RestartScene()
{
//    Debug.Log("🔄 Reiniciando escena...");
    
    // Obtener la escena actual y recargarla
    Scene currentScene = SceneManager.GetActiveScene();
    SceneManager.LoadScene(currentScene.name);
}

private IEnumerator RestartSceneCoroutine()
{
    // Esperar un frame para que se ejecute la limpieza
    yield return null;
    
    // Obtener la escena actual y recargarla
    Scene currentScene = SceneManager.GetActiveScene();
    SceneManager.LoadScene(currentScene.name);
}

    private void ShowDeathMessage()
    {
        // Puedes implementar UI de muerte aquí
        Debug.Log("💀 PRESIONA F1 PARA REAPARECER O F2 PARA REINICIAR LA ESCENA");
        
        // Ejemplo con UI Text (descomenta y configura si tienes UI)
        /*
        if (deathMessageText != null)
        {
            deathMessageText.text = "¡HAS MUERTO!\nPresiona F1 para reaparecer";
            deathMessageText.gameObject.SetActive(true);
        }
        */
    }
    private void HideDeathMessage()
    {
        // Ocultar UI de muerte
        /*
        if (deathMessageText != null)
        {
            deathMessageText.gameObject.SetActive(false);
        }
        */
    }

     private void UpdateDeathUI(bool isDead)
    {
        // Puedes agregar efectos de UI aquí cuando el jugador muere
        // Por ejemplo: oscurecer la pantalla, mostrar crosshair rojo, etc.
    }

    private void OnDestroy()
{
    // ✅ LIMPIAR CALLBACKS CUANDO EL OBJETO SE DESTRUYE
    CleanupAllInputCallbacks();
    
    // ✅ CANCELAR TODAS LAS CORRUTINAS E INVOKES
    CancelInvoke();
    StopAllCoroutines();
    
//    Debug.Log("🧹 TPMovement_Controller destruido - Callbacks limpiados");
}

public void AddMag()
{
    currentMagazines++;
    UpdateAmmoUI();
    Debug.Log($"➕ Cargador añadido. Total: {currentMagazines}");
}

// ✅ MÉTODO PARA CURAR (ya debería existir)
public void RegenHeal(float healAmount)
{
    currentHealth += healAmount;
    currentHealth = Mathf.Min(currentHealth, maxHealth);
    UpdateHealthUI();
    Debug.Log($"❤️ Salud restaurada: {currentHealth}/{maxHealth}");
}

private bool IsGamePaused()
{
    try
    {
        // ✅ USAR EL SINGLETON EN LUGAR DE FindObjectOfType
        return PauseMenu.IsPaused() || Time.timeScale < 0.1f;
    }
    catch (System.Exception e)
    {
        Debug.LogWarning($"⚠️ Error verificando estado de pausa: {e.Message}");
        return false;
    }
}

private PauseMenu _cachedPauseMenu;

// ✅ NUEVO: Propiedad para cachear la referencia de manera segura
private PauseMenu GetPauseMenu()
{
    if (_cachedPauseMenu == null)
    {
        _cachedPauseMenu = FindObjectOfType<PauseMenu>();
    }
    return _cachedPauseMenu;
}
}