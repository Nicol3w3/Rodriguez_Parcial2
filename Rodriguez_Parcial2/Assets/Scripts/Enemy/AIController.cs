using UnityEngine;

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
    [SerializeField] private float currentHealthDisplay;
    
    // Estados protegidos para herencia
    protected enum AIState { Patrolling, Chasing, Dead, Idle }
    protected AIState currentState = AIState.Idle;
    
    
    protected float currentHealth;
    public bool isChasing { get; protected set; } = false;
    protected Vector3 lastKnownPlayerPosition;
    protected float chaseTimer = 0f;
    protected ObstacleAvoidance obstacleAvoidance;

    // Eventos
    public System.Action<float> OnHealthChanged;
    public System.Action OnDeath;

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
        
        Debug.Log($"✅ {enemyConfig.enemyName} inicializado - " +
                 $"Persecución: {enemyConfig.usePersistentChase}, " +
                 $"Evasión: {enemyConfig.useObstacleAvoidance}");
        
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

//        Debug.Log($"✅ {enemyConfig.enemyName} inicializado - Salud: {currentHealth}");
    }

    protected virtual void Update()
    {
        UpdateInspectorDisplay();
    }

    protected virtual void FixedUpdate()
    {
        if (currentState == AIState.Dead) return;
        
        CheckGrounded();
        HandleDetection();
        HandleStateBehavior();
        HandleChasePersistence();
    }

    protected virtual AIState GetDefaultState()
    {
        return AIState.Idle;
    }

    private void UpdateInspectorDisplay()
    {
        currentStateDisplay = currentState.ToString();
        currentHealthDisplay = currentHealth;
        //isGroundedDisplay = isGrounded;
        //lastKnownPositionDisplay = lastKnownPlayerPosition;
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
        
//        Debug.Log($"✅ EN SUELO - Distancia: {hit.distance:F3}, Altura ajustada: {transform.position.y:F2}");
    }
    else
    {
        isGrounded = false;
    //    Debug.Log("❌ NO EN SUELO");
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
            if (!isChasing && enemyConfig.canChase)
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
            
            // Debug para mostrar que está persiguiendo sin ver
           // Debug.Log($"{enemyConfig.enemyName} persiguiendo sin visión directa");
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
            
            // ✅ Perseguir INDEFINIDAMENTE hasta encontrar al jugador
            // No hay timeout, solo seguimos persiguiendo
            
            // Debug opcional para ver cuánto tiempo lleva persiguiendo
            if (chaseTimer % 10f < 0.1f) // Cada 10 segundos
            {
//                Debug.Log($"{enemyConfig.enemyName} lleva {chaseTimer:F0}s persiguiendo al jugador");
            }
        }
    }

    protected virtual void StopChasing()
    {
        // ✅ SOLO se detiene si no usa persecución persistente
        if (enemyConfig.usePersistentChase)
        {
            Debug.Log($"{enemyConfig.enemyName} sigue en persecución persistente");
            return;
        }
        
        isChasing = false;
        currentState = GetDefaultState();
        chaseTimer = 0f;
        Debug.Log($"{enemyConfig.enemyName} dejó de perseguir al jugador");
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
            Debug.Log("🚧 Camino bloqueado, buscando ruta alternativa");
        }
    }
    
    Vector3 targetVelocity = direction * currentSpeed;
    rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    
    // ✅ Debug visual mejorado
    Debug.DrawLine(transform.position, lastKnownPlayerPosition, 
                  fov != null && fov.canSeePlayer ? Color.red : Color.yellow);
    
    // Mostrar estado de persecución
    if (fov != null && !fov.canSeePlayer)
    {
//        Debug.Log($"🎯 {enemyConfig.enemyName} persiguiendo sin visión - Posición: {lastKnownPlayerPosition}");
    }
}

// En AIController.cs
protected virtual void PatrolBehavior()
{
    // Comportamiento base vacío - será overrideado en SoldierAIController
    currentState = AIState.Idle; // Por defecto pasar a idle si no hay patrulla
}

    protected virtual void IdleBehavior()
    {
        // Comportamiento cuando está inactivo
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
        currentState = AIState.Chasing;
        chaseTimer = 0f;
        
        if (enemyConfig.detectionSound != null)
        {
            AudioSource.PlayClipAtPoint(enemyConfig.detectionSound, transform.position);
        }
        
     //   Debug.Log($"{enemyConfig.enemyName} comenzó a perseguir al jugador - PERSISTENTE!");
    }

    public virtual void TakeDamage(float damageAmount)
    {
        if (enemyConfig.isInvulnerable || currentState == AIState.Dead) return;

        currentHealth -= damageAmount;
        OnHealthChanged?.Invoke(currentHealth / enemyConfig.maxHealth);
        
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
    }

    protected virtual void Die()
    {
        currentHealth = 0;
        currentState = AIState.Dead;
        
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
    }

    public virtual void Revive()
    {
        currentState = GetDefaultState();
        currentHealth = enemyConfig.maxHealth;
        isChasing = false;
        lastKnownPlayerPosition = Vector3.zero;
        
        if (fov != null)
        {
            fov.canSeePlayer = false;
        }
        
        SetEnemyVisible(true);
        OnHealthChanged?.Invoke(1f);
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
        if (other.CompareTag("Player") && enemyConfig.canDealDamage)
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
}
