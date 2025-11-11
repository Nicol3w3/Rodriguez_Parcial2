using UnityEngine;
using System.Collections;

public class AIController : MonoBehaviour
{
    [Header("Enemy Configuration")]
    public EnemyConfigData enemyConfig;
    
    [Header("Ground Detection")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundMask = 1; // Capa por defecto
    protected bool isGrounded;

    [Header("Runtime References")]
    protected Rigidbody rb;
    protected FieldOfView fov;
    protected Collider damageCollider;

    [Header("Debug Info - Read Only")]
    [SerializeField] private string currentStateDisplay;
    [SerializeField] private string previousStateDisplay;
    [SerializeField] private float currentHealthDisplay;
    
    // Estados protegidos para herencia
    protected enum AIState { Patrolling, Chasing, Dead, Idle, Damaged }
    protected AIState currentState = AIState.Idle;
    protected AIState previousState = AIState.Idle;
    protected AIState stateBeforeDamage; // Para recordar el estado antes del daño
    
    protected float currentHealth;
    public bool isChasing { get; protected set; } = false;
    protected Vector3 lastKnownPlayerPosition;
    protected float chaseTimer = 0f;
    protected ObstacleAvoidance obstacleAvoidance;
    protected Coroutine damageRecoveryCoroutine;

    // Eventos
    public System.Action<float> OnHealthChanged;
    public System.Action OnDeath;

    // Debug de estados
    [Header("State Debug")]
    [SerializeField] private bool enableStateDebug = true;

    protected virtual void Start()
    {
        InitializeFromConfig();
        RegisterWithManager();
        if (enemyConfig.useObstacleAvoidance)
        {
            obstacleAvoidance = GetComponent<ObstacleAvoidance>();
            if (obstacleAvoidance == null)
            {
                Debug.LogWarning($"ObstacleAvoidance no encontrado en {enemyConfig.enemyName}");
            }
        }
        
        UpdateStateDisplays();
        
        SetupDamageCollider();
        
        if (enemyConfig.useObstacleAvoidance)
        {
            obstacleAvoidance = GetComponent<ObstacleAvoidance>();
            if (obstacleAvoidance == null)
            {
                Debug.LogWarning($"ObstacleAvoidance no encontrado en {enemyConfig.enemyName}");
            }
        }
    }

    // MÉTODO CLAVE: Cambiar estado con debug
    protected virtual void ChangeState(AIState newState)
    {
        if (currentState == newState) return;

        previousState = currentState;
        currentState = newState;
        
        UpdateStateDisplays();
        
        if (enableStateDebug)
        {
            Debug.Log($"🔄 {enemyConfig.enemyName} cambió estado: {previousState} → {currentState}");
        }
    }

    // Actualizar displays para inspector
    private void UpdateStateDisplays()
    {
        currentStateDisplay = currentState.ToString();
        previousStateDisplay = previousState.ToString();
        currentHealthDisplay = currentHealth;
    }

    private void SetupDamageCollider()
    {
        // Asegurarse de que hay un Collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            // Si no hay collider, agregar uno
            gameObject.AddComponent<CapsuleCollider>();
            col = GetComponent<Collider>();
        }
        
        // Configurar collider para detectar balas
        col.isTrigger = false; // ✅ IMPORTANTE: Debe ser false para collision detection
        
        // Agregar Rigidbody si no existe
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    public void DebugTakeDamage(float damageAmount)
    {
        Debug.Log($"🎯 DEBUG: {enemyConfig.enemyName} recibió {damageAmount} de daño. Salud actual: {currentHealth}");
        TakeDamage(damageAmount);
    }

    protected virtual void InitializeFromConfig()
    {
        if (enemyConfig == null)
        {
            Debug.LogError("No EnemyConfig assigned to " + gameObject.name);
            return;
        }

        // Inicializar desde Scriptable Object
        currentHealth = enemyConfig.maxHealth;
        
        // Configurar componentes
        rb = GetComponent<Rigidbody>();
        fov = GetComponent<FieldOfView>();
        
        if (fov != null && enemyConfig != null)
        {
            fov.radius = enemyConfig.detectionRadius;
            fov.angle = enemyConfig.detectionAngle;
            fov.targetMask = enemyConfig.targetMask;
            fov.obstructionMask = enemyConfig.obstructionMask;
        }

        // Configurar collider de daño
        damageCollider = GetComponentInChildren<Collider>();
        if (damageCollider != null && enemyConfig.canDealDamage)
        {
            damageCollider.isTrigger = true;
        }

        UpdateStateDisplays();
    }

    protected virtual void Update()
    {
        UpdateStateDisplays(); // Mantener actualizado en tiempo real
    }

    protected virtual void FixedUpdate()
    {
        if (currentState == AIState.Dead || currentState == AIState.Damaged) return;
        
        CheckGrounded();
        HandleDetection();
        HandleStateBehavior();
        HandleChasePersistence();
    }

    protected virtual AIState GetDefaultState()
    {
        return AIState.Idle;
    }

     private void CheckGrounded()
    {
        // Raycast simple hacia abajo
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, groundCheckDistance, groundMask))
        {
            isGrounded = true;
            
            // Ajustar para mantener una altura adecuada sobre el suelo
            float desiredHeightAboveGround = 1.0f; // Ajusta este valor según la altura de tu personaje
            
            if (transform.position.y < hit.point.y + desiredHeightAboveGround)
            {
                Vector3 pos = transform.position;
                pos.y = hit.point.y + desiredHeightAboveGround;
                transform.position = pos;
                
                // Resetear velocidad vertical
                Vector3 velocity = rb.linearVelocity;
                velocity.y = 0;
                rb.linearVelocity = velocity;
            }
        }
        else
        {
            isGrounded = false;
        }
        
        // Debug visual
        Debug.DrawRay(transform.position + Vector3.up * 0.5f, Vector3.down * groundCheckDistance, 
                     isGrounded ? Color.green : Color.red);
    }

    protected virtual void HandleDetection()
    {
        if (fov != null && fov.playerRef != null)
        {
            if (fov.canSeePlayer)
            {
                if (!isChasing && enemyConfig.canChase && currentState != AIState.Damaged)
                {
                    StartChasing();
                }
                lastKnownPlayerPosition = fov.playerRef.transform.position;
                chaseTimer = 0f;
            }
            // ✅ PERSECUCIÓN PERSISTENTE: Seguir al jugador aunque no lo vea
            else if (isChasing && enemyConfig.usePersistentChase)
            {
                // Actualizar posición continuamente mientras esté persiguiendo
                lastKnownPlayerPosition = fov.playerRef.transform.position;
            }
        }
    }

     protected virtual void HandleChasePersistence()
    {
        // ✅ SOLO para enemigos con persecución persistente
        if (!enemyConfig.usePersistentChase) return;
        
        if (isChasing)
        {
            chaseTimer += Time.deltaTime;
        }
    }

    protected virtual void StopChasing()
    {
        // ✅ SOLO se detiene si no usa persecución persistente
        if (enemyConfig.usePersistentChase)
        {
            if (enableStateDebug)
            {
                Debug.Log($"{enemyConfig.enemyName} sigue en persecución persistente");
            }
            return;
        }
        
        isChasing = false;
        ChangeState(GetDefaultState()); // Usar ChangeState en lugar de asignación directa
        chaseTimer = 0f;
        
        if (enableStateDebug)
        {
            Debug.Log($"{enemyConfig.enemyName} dejó de perseguir al jugador");
        }
    }

    protected virtual void HandleStateBehavior()
    {
        switch (currentState)
        {
            case AIState.Patrolling:
                PatrolBehavior();
                break;
            case AIState.Chasing:
                ChaseBehavior();
                break;
            case AIState.Idle:
                IdleBehavior();
                break;
            case AIState.Damaged:
                DamagedBehavior();
                break;
        }
    }

    protected virtual void ChaseBehavior()
    {
        if (!enemyConfig.canMove || !isGrounded) return;

        // ✅ SIEMPRE intentar obtener la posición actual del jugador si está disponible
        if (fov != null && fov.playerRef != null)
        {
            lastKnownPlayerPosition = fov.playerRef.transform.position;
        }

        Vector3 direction = (lastKnownPlayerPosition - transform.position).normalized;
        direction.y = 0;
        
        RotateTowards(lastKnownPlayerPosition);
        
        float currentSpeed = enemyConfig.chaseSpeed;
        
        if (enemyConfig.useObstacleAvoidance && obstacleAvoidance != null)
        {
            if (obstacleAvoidance.IsPathBlocked(lastKnownPlayerPosition))
            {
                Vector3 alternativeDirection = obstacleAvoidance.FindAlternativeDirection(lastKnownPlayerPosition);
                direction = alternativeDirection;
                
                if (enableStateDebug)
                {
//                    Debug.Log("🚧 Camino bloqueado, buscando ruta alternativa");
                }
            }
        }
        
        Vector3 targetVelocity = direction * currentSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        
        // ✅ Debug visual mejorado
        Debug.DrawLine(transform.position, lastKnownPlayerPosition, 
                      fov != null && fov.canSeePlayer ? Color.red : Color.yellow);
    }

    protected virtual void PatrolBehavior()
    {
        // Comportamiento base vacío - será overrideado en SoldierAIController
        ChangeState(AIState.Idle); // Usar ChangeState en lugar de asignación directa
    }

    protected virtual void IdleBehavior()
    {
        // Comportamiento cuando está inactivo
    }

    protected virtual void DamagedBehavior()
    {
        // Frenar en seco al enemigo
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }
        
        // No hacer nada más - la corutina se encargará de volver al estado anterior
    }

    protected virtual void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
                enemyConfig.rotationSpeed * Time.deltaTime);
        }
    }

    protected virtual void StartChasing()
    {
        isChasing = true;
        ChangeState(AIState.Chasing); // Usar ChangeState en lugar de asignación directa
        chaseTimer = 0f;
        
        if (enemyConfig.detectionSound != null)
        {
            AudioSource.PlayClipAtPoint(enemyConfig.detectionSound, transform.position);
        }
        
        if (enableStateDebug)
        {
            Debug.Log($"{enemyConfig.enemyName} comenzó a perseguir al jugador");
        }
    }

    public virtual void TakeDamage(float damageAmount)
    {
        if (enemyConfig.isInvulnerable || currentState == AIState.Dead) return;

        // Guardar el estado actual antes del daño (excepto si ya está en Damaged)
        if (currentState != AIState.Damaged)
        {
            stateBeforeDamage = currentState;
        }

        currentHealth -= damageAmount;
        OnHealthChanged?.Invoke(currentHealth / enemyConfig.maxHealth);
        
        // Cambiar al estado Damaged
        if (currentState != AIState.Damaged)
        {
            ChangeState(AIState.Damaged);
            
            // Iniciar la recuperación del estado Damaged
            if (damageRecoveryCoroutine != null)
            {
                StopCoroutine(damageRecoveryCoroutine);
            }
            damageRecoveryCoroutine = StartCoroutine(RecoverFromDamage());
        }
        
        // Efecto de golpe
        if (enemyConfig.hitEffect != null)
        {
            Instantiate(enemyConfig.hitEffect, transform.position, Quaternion.identity);
        }
        
        if (enemyConfig.hitSound != null)
        {
            AudioSource.PlayClipAtPoint(enemyConfig.hitSound, transform.position);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
        
        UpdateStateDisplays();
    }

    protected virtual IEnumerator RecoverFromDamage()
    {
        if (enableStateDebug)
        {
//            Debug.Log($"💥 {enemyConfig.enemyName} en estado Damaged por 1 segundo");
        }
        
        // Esperar 1 segundo en estado Damaged
        yield return new WaitForSeconds(1f);
        
        // Volver al estado anterior (a menos que esté muerto)
        if (currentState != AIState.Dead)
        {
            if (enableStateDebug)
            {
//                Debug.Log($"🔄 {enemyConfig.enemyName} recuperándose del daño, volviendo a: {stateBeforeDamage}");
            }
            
            ChangeState(stateBeforeDamage);
        }
        
        damageRecoveryCoroutine = null;
    }

    protected virtual void Die()
    {
        // Detener la corutina de daño si está activa
        if (damageRecoveryCoroutine != null)
        {
            StopCoroutine(damageRecoveryCoroutine);
            damageRecoveryCoroutine = null;
        }

        currentHealth = 0;
        ChangeState(AIState.Dead); // Usar ChangeState en lugar de asignación directa
        
        // Efectos de muerte
        if (enemyConfig.deathEffect != null)
        {
            Instantiate(enemyConfig.deathEffect, transform.position, Quaternion.identity);
        }
        
        if (enemyConfig.deathSound != null)
        {
            AudioSource.PlayClipAtPoint(enemyConfig.deathSound, transform.position);
        }

        if (EnemyRespawnManager.Instance != null)
        {
            EnemyRespawnManager.Instance.NotifyEnemyDeath(this);
        }
        
        OnDeath?.Invoke();
        SetEnemyVisible(false);
        
        Debug.Log($"{enemyConfig.enemyName} ha sido derrotado!");
        UpdateStateDisplays();
    }

    public virtual void Revive()
    {
        // Detener la corutina de daño si está activa
        if (damageRecoveryCoroutine != null)
        {
            StopCoroutine(damageRecoveryCoroutine);
            damageRecoveryCoroutine = null;
        }

        ChangeState(GetDefaultState()); // Usar ChangeState en lugar de asignación directa
        currentHealth = enemyConfig.maxHealth;
        isChasing = false;
        lastKnownPlayerPosition = Vector3.zero;
        stateBeforeDamage = GetDefaultState();
        
        if (fov != null)
        {
            fov.canSeePlayer = false;
        }
        
        SetEnemyVisible(true);
        OnHealthChanged?.Invoke(1f);
        UpdateStateDisplays();
    }

    protected virtual void SetEnemyVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.enabled = visible;
        }
        
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
        {
            c.enabled = visible;
        }
    }

    protected virtual void RegisterWithManager()
    {
        if (EnemyRespawnManager.Instance != null)
        {
            EnemyRespawnManager.Instance.RegisterEnemy(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && enemyConfig.canDealDamage && currentState != AIState.Damaged)
        {
            TPMovement_Controller player = other.GetComponent<TPMovement_Controller>();
            if (player != null)
            {
                player.TakeDamage(enemyConfig.damageToPlayer);
            }
        }
    }

    public bool IsDead()
    {
        return currentState == AIState.Dead;
    }

    public string GetEnemyName()
{
    return enemyConfig != null ? enemyConfig.enemyName : "Unnamed Enemy";
}

    // MÉTODO DE DEBUG: Forzar cambio de estado desde inspector
    [ContextMenu("Debug - Change to Idle")]
    private void DebugChangeToIdle()
    {
        ChangeState(AIState.Idle);
    }

    [ContextMenu("Debug - Change to Patrolling")]
    private void DebugChangeToPatrolling()
    {
        ChangeState(AIState.Patrolling);
    }

    [ContextMenu("Debug - Change to Chasing")]
    private void DebugChangeToChasing()
    {
        ChangeState(AIState.Chasing);
    }

    [ContextMenu("Debug - Change to Damaged")]
    private void DebugChangeToDamaged()
    {
        stateBeforeDamage = currentState;
        ChangeState(AIState.Damaged);
        
        if (damageRecoveryCoroutine != null)
        {
            StopCoroutine(damageRecoveryCoroutine);
        }
        damageRecoveryCoroutine = StartCoroutine(RecoverFromDamage());
    }

    [ContextMenu("Debug - Print Current State")]
    private void DebugPrintCurrentState()
    {
        Debug.Log($"🔍 {enemyConfig.enemyName} - Estado actual: {currentState}, Estado anterior: {previousState}, Estado antes del daño: {stateBeforeDamage}, Persiguiendo: {isChasing}");
    }
    public string GetCurrentState()
    {
        return currentState.ToString();
    }
    
}