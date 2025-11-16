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
    protected enum AIState { Patrolling, Chasing, Dead, Idle, Alert, Damaged }
    protected AIState currentState = AIState.Idle;
    protected AIState previousState = AIState.Idle;
    protected AIState stateBeforeAlert; // Para recordar el estado antes del daño

    [Header("Respawn Settings")]
    [HideInInspector] protected Vector3 initialPosition;
    [HideInInspector] protected Quaternion initialRotation;
    [HideInInspector] protected AIState initialState;

    [Header("Patrol System")]
    public bool usePatrol = false;
    public List<Vector3> patrolPoints = new List<Vector3>();
    public float patrolPointReachedDistance = 1f;
    public float patrolWaitTime = 2f;

    protected int currentPatrolIndex = 0;
    protected float patrolWaitTimer = 0f;
    protected bool isWaitingAtPoint = false;


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
    protected float damageTimer = 0f;



    // Eventos
    public System.Action<float> OnHealthChanged;
    public System.Action OnDeath;
    public System.Action<Vector3> OnAlert;

    // Debug de estados
    [Header("State Debug")]
    [SerializeField] public bool enableStateDebug = true;

    protected virtual void Start()
{
    // ✅ GUARDAR POSICIÓN INICIAL
    initialPosition = transform.position;
    initialRotation = transform.rotation;
    
    InitializeFromConfig();
    InitializeShootingSystem();
    
    // ✅ MOVER InitializePatrolSystem AQUÍ para que soldiers lo overrideen
    
    RegisterWithManager();
    
    if (enemyConfig.useObstacleAvoidance)
    {
        obstacleAvoidance = GetComponent<ObstacleAvoidance>();
    }
    
    UpdateStateDisplays();
    SetupDamageCollider();
    
    if (usePathfinding)
    {
        pathfinding = GetComponent<DynamicPathfinding>();
        if (pathfinding == null)
        {
            pathfinding = gameObject.AddComponent<DynamicPathfinding>();
        }
    }
}



 protected virtual void SaveInitialTransform()
    {
        // Este método será overrideado por las clases hijas
        // No guardamos nada aquí en el base para evitar conflictos
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
        case AIState.Damaged:
            damageTimer = 0f;
            if (previousState != AIState.Damaged && previousState != AIState.Alert)
            {
                stateBeforeAlert = previousState;
            }
            // ✅ REHABILITAR DISPARO cuando sale de Damaged
            if (shootingSystem != null && previousState == AIState.Damaged)
            {
                shootingSystem.SetShootingEnabled(canShoot);
            }
            break;
            
        case AIState.Alert:
            alertTimer = 0f;
            searchTimer = 0f;
            if (previousState != AIState.Alert && previousState != AIState.Damaged)
            {
                stateBeforeAlert = previousState;
            }
            break;
            
        case AIState.Patrolling:
            currentPatrolIndex = 0;
            isWaitingAtPoint = false;
            patrolWaitTimer = 0f;
            break;
            
        case AIState.Chasing:
            chaseTimer = 0f;
            break;
            
        case AIState.Idle:
            // Detener cualquier movimiento
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
            break;
    }
    
    UpdateStateDisplays();
    
    if (enableStateDebug)
    {
        Debug.Log($"🔄 {enemyConfig.enemyName} {previousState} -> {currentState}");
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

    // ✅ NUEVO: Determinar estado inicial basado en capacidades
    AIState initialState = GetDefaultState();
    
    // ✅ SI ES SOLDIER Y TIENE RUTA DE PATRULLA, USAR PATROLLING
    
    ChangeState(initialState);
    
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
        case AIState.Damaged:
            damageTimer += Time.deltaTime;
            
            // ✅ CORREGIDO: Después de X segundos en Damaged, pasar a Alert
            if (damageTimer >= 2f) // 2 segundos en estado Damaged
            {
                ChangeState(AIState.Alert);
                alertTimer = 0f;
                searchTimer = 0f;
                
                // ✅ ACTUALIZAR: Guardar posición del jugador al entrar en Alert
                if (fov != null && fov.playerRef != null)
                {
                    lastKnownPlayerPosition = fov.playerRef.transform.position;
                    alertPosition = lastKnownPlayerPosition;
                }
                
                if (enableStateDebug)
                {
//                    Debug.Log($"🚨 {enemyConfig.enemyName} pasó de Damaged a Alert después de {damageTimer:F1}s");
                }
            }
            break;
            
        case AIState.Alert:
            alertTimer += Time.deltaTime;
            searchTimer += Time.deltaTime;
            
            // ✅ ACTUALIZAR: Actualizar posición del jugador constantemente
            if (fov != null && fov.playerRef != null)
            {
                lastKnownPlayerPosition = fov.playerRef.transform.position;
            }
            
            // ✅ MODIFICADO: Solo pasar a Chase si puede ver al jugador
            if (fov != null && fov.canSeePlayer && currentState != AIState.Chasing)
            {
                ChangeState(AIState.Chasing);
            }
            
            // ✅ MODIFICADO: Solo volver si NO hay propagación de alerta
            if (searchTimer >= 10f && !enemyConfig.usePersistentChase)
            {
                ReturnToPreviousState();
            }
            break;
            
        case AIState.Chasing:
            chaseTimer += Time.deltaTime;
            break;
    }
}
    protected virtual AIState GetDefaultState()
{
    // ✅ NUEVO: Si tiene patrulla y está habilitada, usar Patrolling
    if (usePatrol && patrolPoints != null && patrolPoints.Count > 0)
    {
        return AIState.Patrolling;
    }
    
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
            // ✅ INTERRUMPIR PATRULLA si detecta al jugador
            if ((currentState == AIState.Patrolling || currentState == AIState.Idle) && 
                enemyConfig.canChase)
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
            searchTimer = 0f;
        }
    }
}

   protected virtual void AlertOtherEnemies(Vector3 playerPosition)
{
    OnAlert?.Invoke(playerPosition);
    
    // ✅ CORREGIDO: Buscar TODOS los AIController en la escena y filtrar soldiers
    AIController[] allEnemies = FindObjectsByType<AIController>(FindObjectsSortMode.None);
    
    if (enableStateDebug && allEnemies.Length > 0)
    {
//        Debug.Log($"🚨 {enemyConfig.enemyName} alertando a {allEnemies.Length} enemigos en la escena");
    }
    
    foreach (AIController enemy in allEnemies)
    {
        // ✅ No alertarse a sí mismo, solo soldiers vivos que no sean cámaras
        if (enemy != this && !enemy.IsDead() && IsSoldier(enemy))
        {
            enemy.ReceiveAlert(playerPosition);
            
            if (enableStateDebug)
            {
//                Debug.Log($"📢 Alerta enviada a: {enemy.GetEnemyName()} (Distancia: {Vector3.Distance(transform.position, enemy.transform.position):F1}m)");
            }
        }
    }
}

private bool IsSoldier(AIController enemy)
{
    // Los soldiers tienen SoldierConfigData, las cámaras tienen SurveillanceCameraConfigData
    return enemy.enemyConfig is SoldierConfigData;
}

   public virtual void ReceiveAlert(Vector3 alertPosition)
{
    if (currentState == AIState.Dead || currentState == AIState.Chasing) return;
    
    this.alertPosition = alertPosition;
    this.lastKnownPlayerPosition = alertPosition; // ✅ IMPORTANTE: Actualizar posición del jugador
    
    if (currentState != AIState.Alert && currentState != AIState.Damaged)
    {
        ChangeState(AIState.Alert);
        searchTimer = 0f;
        
        if (enableStateDebug)
        {
            Debug.Log($"📢 {enemyConfig.enemyName} recibió alerta - Posición: {alertPosition}");
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
        case AIState.Alert:
            AlertBehavior();
            break;
        case AIState.Damaged:
            DamagedBehavior();
            break;
    }
}

    protected virtual void AlertBehavior()
{
    if (!enemyConfig.canMove || !isGrounded) return;

    // ✅ CORREGIDO: Perseguir al JUGADOR, no a la posición de alerta
    if (fov != null && fov.playerRef != null)
    {
        lastKnownPlayerPosition = fov.playerRef.transform.position;
    }

    // Moverse hacia la última posición conocida del jugador
    Vector3 moveDirection = GetMovementDirectionToPlayer();
    RotateTowardsTarget(lastKnownPlayerPosition);
    
    float currentSpeed = enemyConfig.movementSpeed * 0.8f; // Velocidad reducida en alerta
    
    Vector3 targetVelocity = moveDirection * currentSpeed;
    rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    
    // Debug visual - línea hacia el jugador
    Debug.DrawLine(transform.position, lastKnownPlayerPosition, Color.cyan);
    
    // ✅ ELIMINAR: No buscar alrededor cuando llega a la posición
    // El enemigo debe seguir persiguiendo hasta encontrar al jugador o timeout
}

   protected virtual Vector3 GetMovementDirectionToPlayer()
{
    Vector3 direction = (lastKnownPlayerPosition - transform.position).normalized;
    
    if (obstacleAvoidance != null)
    {
        Vector3 avoidanceDir = obstacleAvoidance.GetAvoidanceDirection(lastKnownPlayerPosition);
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

    protected virtual void ReturnToPreviousState()
{
    if (stateBeforeAlert != AIState.Dead && stateBeforeAlert != AIState.Chasing)
    {
        ChangeState(stateBeforeAlert);
    }
    else
    {
        // Fallback al estado por defecto
        ChangeState(GetDefaultState());
    }
    
    if (enableStateDebug)
    {
        Debug.Log($"🔄 {enemyConfig.enemyName} volviendo a {stateBeforeAlert} después de búsqueda");
    }
}


    // NUEVO: Inicializar puntos de patrulla (para soldiers)

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

    currentHealth -= damageAmount;
    Debug.Log($"💥 {enemyConfig.enemyName} recibió {damageAmount} de daño");

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

    // ✅ CORREGIDO: Siempre entrar en Damaged primero (excepto si ya está en Chase)
    if (currentState != AIState.Chasing && currentState != AIState.Damaged && currentState != AIState.Dead)
    {
        // Guardar el estado actual antes del daño
        stateBeforeAlert = currentState;
        ChangeState(AIState.Damaged);
        damageTimer = 0f;
        isFirstDamage = true;
        
        if (enableStateDebug)
        {
//            Debug.Log($"💢 {enemyConfig.enemyName} entró en estado Damaged");
        }
    }
    // ✅ Si ya está en Chasing, mantener el estado pero procesar daño
    else if (currentState == AIState.Chasing)
    {
        // Solo procesar el daño sin cambiar estado
        Debug.Log($"💥 {enemyConfig.enemyName} en Chasing - Manteniendo estado");
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
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        
        ChangeState(initialState);
        currentHealth = enemyConfig.maxHealth;
        isChasing = false;
        lastKnownPlayerPosition = Vector3.zero;
        alertPosition = Vector3.zero;
        stateBeforeAlert = initialState;
        isFirstDamage = true;
        alertTimer = 0f;
        searchTimer = 0f;
        damageTimer = 0f;
        
        if (fov != null)
        {
            fov.canSeePlayer = false;
        }
        
        SetEnemyVisible(true);
        OnHealthChanged?.Invoke(1f);
        UpdateStateDisplays();
        
        if (enableStateDebug)
        {
            Debug.Log($"🔄 {enemyConfig.enemyName} revivido en posición inicial");
        }
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

   protected virtual void DamagedBehavior()
{
    // Comportamiento cuando está dañado (aturdido, reducir velocidad, etc.)
    if (!enemyConfig.canMove || !isGrounded) return;

    // ✅ COMPORTAMIENTO ESPECÍFICO: Reducir velocidad significativamente o detener movimiento
    float currentSpeed = enemyConfig.movementSpeed * 0.2f; // Muy reducida
    
    // Movimiento muy limitado
    if (rb != null)
    {
        Vector3 slowedVelocity = rb.linearVelocity * 0.3f;
        rb.linearVelocity = new Vector3(slowedVelocity.x, rb.linearVelocity.y, slowedVelocity.z);
    }
    
    // ✅ EFECTO VISUAL: Parpadeo o cambio de color
    if (Time.frameCount % 15 == 0) // Parpadeo cada 15 frames
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.Lerp(Color.white, Color.red, Mathf.PingPong(Time.time * 10f, 1f));
        }
    }
    
    // Los enemigos NO pueden disparar mientras están en estado Damaged
    if (shootingSystem != null)
    {
        shootingSystem.SetShootingEnabled(false);
    }
    
    // Debug visual
    Debug.DrawRay(transform.position, Vector3.up * 3f, Color.yellow);
    
    if (enableStateDebug && Time.frameCount % 60 == 0)
    {
        Debug.Log($"💢 {enemyConfig.enemyName} en Damaged - Tiempo: {damageTimer:F1}/2.0s");
    }
}
protected virtual void PatrolBehavior()
{
    if (!enemyConfig.canMove || !isGrounded || patrolPoints.Count == 0) 
    {
        ChangeState(AIState.Idle);
        return;
    }

    // Si está esperando en un punto
    if (isWaitingAtPoint)
    {
        patrolWaitTimer += Time.deltaTime;
        if (patrolWaitTimer >= patrolWaitTime)
        {
            isWaitingAtPoint = false;
            patrolWaitTimer = 0f;
            
            // ✅ CÁLCULO VECTORIAL: Avanzar al siguiente punto (usando módulo para loop)
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
        }
        return;
    }

    // ✅ CÁLCULO VECTORIAL: Dirección hacia el punto actual de patrulla
    Vector3 currentPatrolPoint = patrolPoints[currentPatrolIndex];
    Vector3 directionToPoint = (currentPatrolPoint - transform.position).normalized;
    
    // ✅ CÁLCULO VECTORIAL: Distancia al punto (magnitud del vector diferencia)
    float distanceToPoint = Vector3.Distance(transform.position, currentPatrolPoint);
    
    // Rotar hacia el punto de patrulla
    RotateTowardsTarget(currentPatrolPoint);
    
    // Moverse hacia el punto
    if (distanceToPoint > patrolPointReachedDistance)
    {
        Vector3 moveDirection = GetPatrolMovementDirection(currentPatrolPoint);
        Vector3 targetVelocity = moveDirection * enemyConfig.movementSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        
        // Debug visual
        Debug.DrawLine(transform.position, currentPatrolPoint, Color.blue);
        Debug.DrawRay(transform.position, moveDirection * 2f, Color.green);
    }
    else
    {
        // Llegó al punto, detenerse y esperar
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        isWaitingAtPoint = true;
        
        if (enableStateDebug)
        {
//            Debug.Log($"🔄 {enemyConfig.enemyName} llegó al punto de patrulla {currentPatrolIndex}");
        }
    }
}

protected virtual Vector3 GetPatrolMovementDirection(Vector3 targetPoint)
{
    Vector3 direction = (targetPoint - transform.position).normalized;
    
    // ✅ CÁLCULO VECTORIAL: Combinar dirección deseada con evasión de obstáculos
    if (obstacleAvoidance != null)
    {
        Vector3 avoidanceDir = obstacleAvoidance.GetAvoidanceDirection(targetPoint);
        direction = (direction + avoidanceDir * avoidanceWeight).normalized;
    }
    
    return direction;
}

protected bool IsSoldier()
{
    // Identificar soldiers por su configuración o componente
    return enemyConfig.enemyType == EnemyType.Soldier || 
           this is SoldierAIController ||
           enemyConfig is SoldierConfigData;
}
}