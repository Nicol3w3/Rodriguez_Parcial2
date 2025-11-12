using UnityEngine;

public class SoldierAIController : AIController
{
    private SoldierConfigData soldierConfig;
    private int currentWaypointIndex = 0;
    private float patrolTimer = 0f;

    private float nextFireTime = 0f;
    private bool canShoot = true;

    [Header("Shooting References - Por Instancia")]
    public Transform shootPoint;

    protected override void Start()
    {
        // Verificar que tenemos la config correcta
        if (enemyConfig is SoldierConfigData)
        {
            soldierConfig = (SoldierConfigData)enemyConfig;
        }
        else
        {
            Debug.LogError("SoldierAIController requiere SoldierConfigData!");
            return;
        }

        base.Start();
    }

    protected override void InitializeFromConfig()
    {
        base.InitializeFromConfig();
        
        if (soldierConfig != null && soldierConfig.canPatrol)
        {
            currentState = AIState.Patrolling;
        }
    }

    protected override void Update()
    {
        base.Update(); // ✅ IMPORTANTE: Llamar al base.Update()
        
        // ✅ NUEVO: Manejar disparos en el estado Chase
        if (currentState == AIState.Chasing && soldierConfig != null && soldierConfig.canShoot)
        {
            HandleShooting();
        }
    }

    protected override void PatrolBehavior()
    {
        if (soldierConfig == null || !soldierConfig.canPatrol || 
            soldierConfig.patrolWaypoints == null || soldierConfig.patrolWaypoints.Length == 0)
        {
            currentState = AIState.Idle;
            return;
        }

        Transform targetWaypoint = soldierConfig.patrolWaypoints[currentWaypointIndex];
        
        if (targetWaypoint == null) return;

        Vector3 direction = (targetWaypoint.position - transform.position).normalized;
        direction.y = 0; // Solo movimiento horizontal
        
        RotateTowards(targetWaypoint.position);
        
        if (enemyConfig.canMove && isGrounded)
        {
            // ✅ USAR velocity en lugar de MovePosition para mejor física
            Vector3 targetVelocity = direction * enemyConfig.movementSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }

        // Cambiar waypoint cuando se acerca
        if (Vector3.Distance(transform.position, targetWaypoint.position) < 0.5f)
        {
            patrolTimer += Time.deltaTime;
            
            if (patrolTimer >= soldierConfig.patrolWaitTime)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % soldierConfig.patrolWaypoints.Length;
                patrolTimer = 0f;
            }
        }
    }

    protected override void ChaseBehavior()
{
    if (!enemyConfig.canMove || !isGrounded) return;

    // ✅ ACTUALIZAR POSICIÓN DEL JUGADOR SI ESTÁ VISIBLE
    if (fov != null && fov.playerRef != null && fov.canSeePlayer)
    {
        lastKnownPlayerPosition = fov.playerRef.transform.position;
    }

    Vector3 directionToPlayer = (lastKnownPlayerPosition - transform.position).normalized;
    directionToPlayer.y = 0;
    
    Vector3 finalDirection = directionToPlayer;
    
    // ✅ SISTEMA MEJORADO ESPECÍFICO PARA SOLDADOS
    if (enemyConfig.useObstacleAvoidance && obstacleAvoidance != null)
    {
        finalDirection = GetSoldierAvoidanceDirection(directionToPlayer, lastKnownPlayerPosition);
    }
    
    RotateTowards(lastKnownPlayerPosition);
    
    float currentSpeed = enemyConfig.chaseSpeed;
    
    // ✅ MOVIMIENTO CON INERCIA
    Vector3 targetVelocity = finalDirection * currentSpeed;
    Vector3 currentVelocity = rb.linearVelocity;
    Vector3 newVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.fixedDeltaTime * 5f);
    newVelocity.y = rb.linearVelocity.y;
    
    rb.linearVelocity = newVelocity;
    
    // ✅ Debug visual
    Debug.DrawLine(transform.position, lastKnownPlayerPosition, 
                  fov != null && fov.canSeePlayer ? Color.red : Color.yellow);
    Debug.DrawRay(transform.position, finalDirection * 2f, Color.green);
}

// ✅ SISTEMA MEJORADO PARA SOLDADOS
private Vector3 GetSoldierAvoidanceDirection(Vector3 desiredDirection, Vector3 targetPosition)
{
    Vector3 avoidanceDirection = desiredDirection;
    
    if (obstacleAvoidance != null)
    {
        // Los soldados son más agresivos en la evasión
        Vector3 avoidanceDir = obstacleAvoidance.GetAvoidanceDirection(targetPosition);
        
        // Combinar dirección deseada con dirección de evasión
        float soldierAvoidanceWeight = 2.5f; // Más agresivo que el enemigo base
        avoidanceDirection = (desiredDirection + avoidanceDir * soldierAvoidanceWeight).normalized;
        
        // Si el camino está muy bloqueado, priorizar completamente la evasión
        if (obstacleAvoidance.IsPathBlocked(targetPosition))
        {
            // Verificar si hay una ruta alternativa clara
            RaycastHit hit;
            if (!Physics.Raycast(transform.position, avoidanceDir, out hit, 3f, obstacleAvoidance.obstacleMask))
            {
                avoidanceDirection = avoidanceDir;
            }
        }
        
        // Aplicar fuerza de evasión adicional directamente
        if (avoidanceDir != desiredDirection && rb != null)
        {
            rb.AddForce(avoidanceDir * obstacleAvoidance.avoidanceForce * 0.5f, ForceMode.Acceleration);
        }
    }
    
    return avoidanceDirection;
}

// ✅ ELIMINAR GetSoldierAvoidanceDirection ya que usamos el del AIController

// ✅ NUEVO MÉTODO: Evasión específica para soldados


    protected override AIState GetDefaultState()
    {
        return soldierConfig != null && soldierConfig.canPatrol ? AIState.Patrolling : AIState.Idle;
    }

    public void SetLastKnownPosition(Vector3 position)
    {
        lastKnownPlayerPosition = position;
    }

    // ✅ Opcional: Override del IdleBehavior para detener movimiento
    protected override void IdleBehavior()
    {
        base.IdleBehavior();
        
        // Detener movimiento cuando está en idle
        if (isGrounded)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

     private void HandleShooting()
    {
        if (fov == null || !fov.canSeePlayer || fov.playerRef == null) return;
        
        // Verificar si está en rango de disparo
        float distanceToPlayer = Vector3.Distance(transform.position, fov.playerRef.transform.position);
        if (distanceToPlayer > soldierConfig.shootRange) return;
        
        // Verificar línea de visión
        if (!HasLineOfSightToPlayer()) return;
        
        // Verificar rate of fire
        if (Time.time >= nextFireTime && canShoot)
        {
            ShootAtPlayer();
            nextFireTime = Time.time + soldierConfig.fireRate;
        }
    }

    // ✅ NUEVO MÉTODO: Verificar línea de visión
    private bool HasLineOfSightToPlayer()
    {
        if (fov == null || fov.playerRef == null) return false;
        
        Vector3 shootPosition = GetShootPosition();
        Vector3 playerPosition = fov.playerRef.transform.position + Vector3.up * 1f; // Apuntar al centro del cuerpo
        
        RaycastHit hit;
        if (Physics.Raycast(shootPosition, (playerPosition - shootPosition).normalized, out hit, soldierConfig.shootRange, enemyConfig.obstructionMask))
        {
            return hit.collider.CompareTag("Player");
        }
        
        return false;
    }

    // ✅ NUEVO MÉTODO: Realizar disparo
    private void ShootAtPlayer()
    {
        if (fov == null || fov.playerRef == null) return;
        
        Vector3 shootPosition = GetShootPosition();
        Vector3 playerPosition = fov.playerRef.transform.position + Vector3.up * 1f;
        Vector3 shootDirection = (playerPosition - shootPosition).normalized;
        
        // Debug visual del disparo
        Debug.DrawRay(shootPosition, shootDirection * soldierConfig.shootRange, Color.magenta, 0.5f);
        
        // Usar BulletPool si está disponible
        if (BulletPool.Instance != null && soldierConfig.bulletPrefab != null)
        {
            var bullet = BulletPool.Instance.GetBullet<HybridBullet>(
                gameObject, 
                shootPosition, 
                shootDirection, 
                soldierConfig.bulletDamage
            );
            
            if (bullet != null)
            {
                bullet.SetVisualRange(20f);
                bullet.SetRaycastRange(soldierConfig.shootRange);
                bullet.OnBulletHit += OnEnemyBulletHit;
            }
        }
        else
        {
            // Fallback: raycast directo
            RaycastHit hit;
            if (Physics.Raycast(shootPosition, shootDirection, out hit, soldierConfig.shootRange))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    TPMovement_Controller player = hit.collider.GetComponent<TPMovement_Controller>();
                    if (player != null)
                    {
                        player.TakeDamage(soldierConfig.bulletDamage);
                    }
                }
            }
        }
        
        // Efecto de sonido
        if (soldierConfig.shootSound != null)
        {
            AudioSource.PlayClipAtPoint(soldierConfig.shootSound, transform.position);
        }
        
        // Efecto visual (opcional)
        // if (muzzleFlash != null) Instantiate(muzzleFlash, shootPosition, Quaternion.LookRotation(shootDirection));
        
        if (enableStateDebug)
        {
            Debug.Log($"🔫 {enemyConfig.enemyName} disparando al jugador");
        }
    }

    // ✅ NUEVO MÉTODO: Obtener posición de disparo
    private Vector3 GetShootPosition()
    {
        // 1. Prioridad: shootPoint del soldier específico
        if (shootPoint != null)
        {
            return shootPoint.position;
        }
        
        // 2. Fallback: shootPoint del config (para compatibilidad)
        if (soldierConfig != null && soldierConfig.shootPoint != null)
        {
            return soldierConfig.shootPoint.position;
        }
        
        // 3. Último recurso: posición por defecto
        return transform.position + Vector3.up * 1.5f + transform.forward * 0.5f;
    }

    // ✅ NUEVO MÉTODO: Cuando la bala del enemigo impacta
    private void OnEnemyBulletHit(BulletBase bullet, GameObject hitObject)
    {
        if (hitObject.CompareTag("Player"))
        {
            if (enableStateDebug)
            {
                Debug.Log($"🎯 {enemyConfig.enemyName} impactó al jugador");
            }
        }
        
        // Limpiar el evento
        if (bullet != null)
        {
            bullet.OnBulletHit -= OnEnemyBulletHit;
        }
    }

    public override void TakeDamage(float damageAmount)
    {
        base.TakeDamage(damageAmount);
        
        // ✅ FORZAR MODO CHASE SI NO ESTÁ MUERTO
        if (currentState != AIState.Dead && currentState != AIState.Damaged && !isChasing)
        {
            StartChasing();
            
            if (enableStateDebug)
            {
                Debug.Log($"💥 {enemyConfig.enemyName} recibió daño - Activando modo Chase");
            }
        }
    }
}
