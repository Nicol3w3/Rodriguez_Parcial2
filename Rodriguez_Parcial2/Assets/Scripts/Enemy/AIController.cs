using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AIController : MonoBehaviour
{
    [Header("Enemy Configuration")]
    public EnemyConfigData enemyConfig;

    [Header("Shooting Configuration")]
    public bool canShoot = false;
    public float shootRange = 15f;
    public float fireRate = 1.5f;
    public float bulletDamage = 20f;
    public GameObject projectilePrefab;
    public Transform shootPoint;
    
    [Header("Ground Detection")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundMask = 1; // Capa por defecto
    protected bool isGrounded;

    [Header("Runtime References")]
    protected Rigidbody rb;
    protected FieldOfView fov;
    protected Collider damageCollider;
    protected EnemyShootingSystem shootingSystem;

    [Header("Debug Info - Read Only")]
    [SerializeField] private string currentStateDisplay;
    [SerializeField] private string previousStateDisplay;
    [SerializeField] private float currentHealthDisplay;
    
    [Header("Obstacle Avoidance")]
    public bool useAdvancedAvoidance = true;
    public float avoidanceWeight = 2f; // ✅ AGREGAR ESTA LÍNEA
    protected Vector3 currentAvoidanceDirection;

    [Header("Pathfinding")]
    public bool usePathfinding = true;
    public float recalculatePathInterval = 2f;
    private DynamicPathfinding pathfinding;
    private float lastPathRecalculationTime;
    private Vector3 currentPathTarget;
    // Estados protegidos para herencia
     protected enum AIState { Patrolling, Chasing, Dead, Idle, Alert }
    protected AIState currentState = AIState.Idle;
    protected AIState previousState = AIState.Idle;
    protected AIState stateBeforeAlert; // Para recordar el estado antes del daño

    
    
    protected float currentHealth;
    public bool isChasing { get; protected set; } = false;
    protected Vector3 lastKnownPlayerPosition;
    protected float chaseTimer = 0f;
    protected ObstacleAvoidance obstacleAvoidance;
    protected Coroutine damageRecoveryCoroutine;

    protected float alertTimer = 0f;
    protected float searchTimer = 0f;
    protected Vector3 alertPosition;
    protected bool isFirstDamage = true;

    protected List<Vector3> patrolPoints = new List<Vector3>();
    protected int currentPatrolIndex = 0;
    protected bool hasPatrolRoute = false;
    protected Vector3 lastPatrolPosition;

    // Eventos
    public System.Action<float> OnHealthChanged;
    public System.Action OnDeath;
    public System.Action<Vector3> OnAlert;

    // Debug de estados
    [Header("State Debug")]
    [SerializeField] public bool enableStateDebug = true;

    protected virtual void Start()
    {
        InitializeFromConfig();
        InitializeShootingSystem();
        RegisterWithManager();
        if (enemyConfig.useObstacleAvoidance)
        {
            obstacleAvoidance = GetComponent<ObstacleAvoidance>();
            if (obstacleAvoidance == null)
            {
//                Debug.LogWarning($"ObstacleAvoidance no encontrado en {enemyConfig.enemyName}");
            }
        }
        

        lastPatrolPosition = transform.position;

        UpdateStateDisplays();
        
        SetupDamageCollider();
        
        if (enemyConfig.useObstacleAvoidance)
        {
            obstacleAvoidance = GetComponent<ObstacleAvoidance>();
            if (obstacleAvoidance == null)
            {
//                Debug.LogWarning($"ObstacleAvoidance no encontrado en {enemyConfig.enemyName}");
            }
        }
     if (usePathfinding)
    {
        pathfinding = GetComponent<DynamicPathfinding>();
        if (pathfinding == null)
        {
            pathfinding = gameObject.AddComponent<DynamicPathfinding>();
        }
    }
}

    // MÉTODO CLAVE: Cambiar estado con debug
    protected virtual void ChangeState(AIState newState)
    {
        if (currentState == newState) return;

        previousState = currentState;
        currentState = newState;
        
        // Resetear timers según el estado
        switch (currentState)
        {
            case AIState.Alert:
                alertTimer = 0f;
                searchTimer = 0f;
                if (previousState != AIState.Alert)
                {
                    stateBeforeAlert = previousState;
                }
                break;
            case AIState.Patrolling:
                currentPatrolIndex = 0;
                break;
            case AIState.Chasing:
                chaseTimer = 0f;
                break;
        }
        
        UpdateStateDisplays();
        
        if (enableStateDebug)
        {
            Debug.Log($" {enemyConfig.enemyName} {currentState}");
        }
    }

    // Actualizar displays para inspector
    protected virtual void UpdateStateDisplays()
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
        HandleShooting();
        HandleStateTimers();

    }

    protected virtual void FixedUpdate()
    {
        if (currentState == AIState.Dead) return;
        
        CheckGrounded();
        HandleDetection();
        HandleStateBehavior();
        HandleChasePersistence();
    }

     protected virtual void HandleStateTimers()
    {
        switch (currentState)
        {
            case AIState.Alert:
                alertTimer += Time.deltaTime;
                searchTimer += Time.deltaTime;
                
                // Después de 3 segundos en alerta (por daño), pasar a Chase
                if (alertTimer >= 3f && isFirstDamage)
                {
                    ChangeState(AIState.Chasing);
                }
                
                // Después de 10 segundos buscando, volver a patrullar
                if (searchTimer >= 10f)
                {
                    ReturnToPatrol();
                }
                break;
                
            case AIState.Chasing:
                chaseTimer += Time.deltaTime;
                break;
        }
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
                if (!isChasing && enemyConfig.canChase && currentState != AIState.Alert)
                {
                    StartChasing();
                }
                
                lastKnownPlayerPosition = fov.playerRef.transform.position;
                
                // Si ve al jugador, comunicar alerta a otros enemigos
                if (currentState == AIState.Chasing || currentState == AIState.Alert)
                {
                    AlertOtherEnemies(lastKnownPlayerPosition);
                }
                
                chaseTimer = 0f;
                searchTimer = 0f; // Resetear timer de búsqueda
            }
        }
    }

    protected virtual void AlertOtherEnemies(Vector3 playerPosition)
    {
        OnAlert?.Invoke(playerPosition);
        
        // También buscar otros soldiers en la escena y alertarlos
        AIController[] allEnemies = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        foreach (AIController enemy in allEnemies)
        {
            if (enemy != this && enemy is SoldierAIController && !enemy.IsDead())
            {
                enemy.ReceiveAlert(playerPosition);
            }
        }
    }

    public virtual void ReceiveAlert(Vector3 alertPosition)
    {
        if (currentState == AIState.Dead || currentState == AIState.Chasing) return;
        
        this.alertPosition = alertPosition;
        
        if (currentState != AIState.Alert)
        {
            ChangeState(AIState.Alert);
            searchTimer = 0f;
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
            case AIState.Alert:
                AlertBehavior();
                break;
        }
    }

    protected virtual void AlertBehavior()
    {
        if (!enemyConfig.canMove || !isGrounded) return;

        // Moverse hacia la posición de alerta
        Vector3 moveDirection = GetMovementDirectionToAlert();
        RotateTowardsTarget(alertPosition);
        
        float currentSpeed = enemyConfig.movementSpeed * 0.7f; // Velocidad reducida en alerta
        
        Vector3 targetVelocity = moveDirection * currentSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        
        // Debug
        Debug.DrawLine(transform.position, alertPosition, Color.cyan);
        
        // Si llega a la posición de alerta, buscar alrededor
        if (Vector3.Distance(transform.position, alertPosition) < 1f)
        {
            // Comportamiento de búsqueda (puede girar o moverse aleatoriamente)
            SearchBehavior();
        }
    }

    protected virtual Vector3 GetMovementDirectionToAlert()
    {
        Vector3 direction = (alertPosition - transform.position).normalized;
        
        if (obstacleAvoidance != null)
        {
            Vector3 avoidanceDir = obstacleAvoidance.GetAvoidanceDirection(alertPosition);
            direction = (direction + avoidanceDir * avoidanceWeight).normalized;
        }
        
        return direction;
    }

    protected virtual void SearchBehavior()
    {
        // Comportamiento básico de búsqueda - puede ser overrideado
        // Por ejemplo, rotar lentamente o moverse en pequeños círculos
        transform.Rotate(0, 30f * Time.deltaTime, 0);
    }
    protected virtual void ReturnToPatrol()
    {
        if (hasPatrolRoute)
        {
            ChangeState(AIState.Patrolling);
        }
        else
        {
            ChangeState(AIState.Idle);
        }
        
        if (enableStateDebug)
        {
            Debug.Log($"🔄 {enemyConfig.enemyName} volviendo a patrullar después de búsqueda");
        }
    }

    // NUEVO: Inicializar puntos de patrulla (para soldiers)
    protected virtual void InitializePatrolPoints()
    {
        // Será implementado en SoldierAIController
    }

   protected virtual void ChaseBehavior()
{
    if (!enemyConfig.canMove || !isGrounded) return;

    // ✅ ACTUALIZAR POSICIÓN DEL JUGADOR
    if (fov != null && fov.playerRef != null)
    {
        lastKnownPlayerPosition = fov.playerRef.transform.position;
    }

    Vector3 moveDirection = GetMovementDirection();
    
    RotateTowardsTarget(lastKnownPlayerPosition);
    
    float currentSpeed = enemyConfig.chaseSpeed;
    
    // ✅ MOVIMIENTO
    Vector3 targetVelocity = moveDirection * currentSpeed;
    rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    
    // ✅ Debug
    Debug.DrawLine(transform.position, lastKnownPlayerPosition, 
                  fov != null && fov.canSeePlayer ? Color.red : Color.yellow);
}

private Vector3 GetMovementDirection()
{
    // ✅ SISTEMA HÍBRIDO: Pathfinding + Evasión de obstáculos
    if (usePathfinding && pathfinding != null)
    {
        return GetPathfindingDirection();
    }
    else
    {
        // Sistema original de evasión
        return GetAvoidanceAdjustedDirection(
            (lastKnownPlayerPosition - transform.position).normalized, 
            lastKnownPlayerPosition
        );
    }
}

private Vector3 GetPathfindingDirection()
{
    Vector3 directionToPlayer = (lastKnownPlayerPosition - transform.position).normalized;
    
    // Recalcular ruta periódicamente o si el objetivo cambió
    if (Time.time - lastPathRecalculationTime > recalculatePathInterval || 
        currentPathTarget != lastKnownPlayerPosition)
    {
        if (pathfinding.CalculatePath(transform.position, lastKnownPlayerPosition))
        {
            currentPathTarget = lastKnownPlayerPosition;
            lastPathRecalculationTime = Time.time;
        }
    }
    
    // Si tenemos un camino, seguirlo
    if (pathfinding.HasPath())
    {
        Vector3 nextNode = pathfinding.GetNextNode();
        
        // Avanzar al siguiente nodo si hemos llegado al actual
        if (pathfinding.HasReachedNode(transform.position, 1f))
        {
            pathfinding.AdvanceToNextNode();
            nextNode = pathfinding.GetNextNode();
        }
        
        if (nextNode != Vector3.zero)
        {
            Vector3 directionToNode = (nextNode - transform.position).normalized;
            
            // Combinar con evasión local de obstáculos
            return GetAvoidanceAdjustedDirection(directionToNode, nextNode);
        }
    }
    
    // Fallback: usar sistema de evasión directo
    return GetAvoidanceAdjustedDirection(directionToPlayer, lastKnownPlayerPosition);
}

protected virtual Vector3 GetAvoidanceAdjustedDirection(Vector3 desiredDirection, Vector3 targetPosition)
{
    Vector3 avoidanceDirection = desiredDirection;
    
    if (obstacleAvoidance != null)
    {
        // Obtener dirección de evasión del sistema mejorado
        Vector3 avoidanceDir = obstacleAvoidance.GetAvoidanceDirection(targetPosition);
        
        // Combinar dirección deseada con dirección de evasión
        avoidanceDirection = (desiredDirection + avoidanceDir * avoidanceWeight).normalized;
        
        // Actualizar dirección actual para debug
        currentAvoidanceDirection = avoidanceDir;
        
        if (enableStateDebug && avoidanceDir != desiredDirection)
        {
//            Debug.Log($"🔄 {enemyConfig.enemyName} ajustando dirección por obstáculos");
        }
    }
    
    return avoidanceDirection;
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


     protected virtual void RotateTowardsTarget(Vector3 targetPosition)
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
//            Debug.Log($"{enemyConfig.enemyName} comenzó a perseguir al jugador");
        }
    }

     public virtual void TakeDamage(float damageAmount)
    {
        if (currentState == AIState.Dead) return;

        // Guardar el estado actual antes de la alerta
        if (currentState != AIState.Alert)
        {
            stateBeforeAlert = currentState;
        }

        currentHealth -= damageAmount;
        Debug.Log($"💥 {enemyConfig.enemyName} recibió {damageAmount} de daño");

        OnHealthChanged?.Invoke(currentHealth / enemyConfig.maxHealth);
        
        // Cambiar al estado Alert en lugar de Damaged
        if (currentState != AIState.Alert)
        {
            ChangeState(AIState.Alert);
            alertPosition = lastKnownPlayerPosition;
            isFirstDamage = true;
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
        
//        Debug.Log($"{enemyConfig.enemyName} ha sido derrotado!");
        UpdateStateDisplays();
    }

    public virtual void Revive()
    {
        ChangeState(GetDefaultState());
        currentHealth = enemyConfig.maxHealth;
        isChasing = false;
        lastKnownPlayerPosition = Vector3.zero;
        alertPosition = Vector3.zero;
        stateBeforeAlert = GetDefaultState();
        isFirstDamage = true;
        alertTimer = 0f;
        searchTimer = 0f;
        
        if (fov != null)
        {
            fov.canSeePlayer = false;
        }
        
        SetEnemyVisible(true);
        OnHealthChanged?.Invoke(1f);
        UpdateStateDisplays();
    }

    public virtual void SetPatrolPoints(List<Vector3> points)
    {
        patrolPoints = new List<Vector3>(points);
        hasPatrolRoute = patrolPoints.Count > 0;
        lastPatrolPosition = transform.position;
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
        if (other.CompareTag("Player") && enemyConfig.canDealDamage && currentState != AIState.Alert) // ✅ CAMBIADO: de Damaged a Alert
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

    [ContextMenu("Debug - Change to Alert")] // ✅ CAMBIADO: de Damaged a Alert
    private void DebugChangeToAlert()
    {
        stateBeforeAlert = currentState;
        ChangeState(AIState.Alert);
    }

    [ContextMenu("Debug - Print Current State")]
    private void DebugPrintCurrentState()
    {
        Debug.Log($"🔍 {enemyConfig.enemyName} - Estado actual: {currentState}, Estado anterior: {previousState}, Estado antes de alerta: {stateBeforeAlert}, Persiguiendo: {isChasing}");
    }
    public string GetCurrentState()
    {
        return currentState.ToString();
    }
    public void ForceRecalculatePath()
{
    if (obstacleAvoidance != null)
    {
        // Forzar recálculo del camino
        lastKnownPlayerPosition = GetComponent<FieldOfView>().playerRef.transform.position;
    }
}

 protected virtual void InitializeShootingSystem()
    {
        if (!canShoot) return; // Solo inicializar si puede disparar
        
        shootingSystem = GetComponent<EnemyShootingSystem>();
        if (shootingSystem == null)
        {
            shootingSystem = gameObject.AddComponent<EnemyShootingSystem>();
        }
        
        // Configurar desde variables del AIController
        shootingSystem.SetShootingEnabled(canShoot);
        shootingSystem.SetFireRate(fireRate);
        shootingSystem.SetShootRange(shootRange);
        shootingSystem.SetBulletDamage(bulletDamage);
        shootingSystem.SetProjectilePrefab(projectilePrefab);
        
        if (shootPoint != null)
        {
            shootingSystem.SetShootPoint(shootPoint);
        }
    }

    protected virtual void HandleShooting()
    {
        if (!canShoot) return;
        if (currentState != AIState.Chasing) return;
        
        shootingSystem?.TryShootAtPlayer();
    }
}