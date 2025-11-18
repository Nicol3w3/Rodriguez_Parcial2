using UnityEngine;
using System.Collections.Generic;

public class SoldierAIController : AIController
{
    private float nextFireTime = 0f;
    private bool canShoot = true;
    private EnemyShootingSystem shootingSystem;
    private bool shootingSystemInitialized = false;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private AIState initialState;
    private Vector3[] initialPatrolPoints;
    private int initialPatrolIndex = 0;

    [Header("Shooting References - Por Instancia")]
    public Transform shootPoint;

    private SoldierConfigData soldierConfig;

   protected override void Start()
    {
        if (enemyConfig is SoldierConfigData)
        {
            soldierConfig = (SoldierConfigData)enemyConfig;
            
            // CONFIGURAR DISPARO DESDE SOLDIER CONFIG
            canShoot = soldierConfig.canShoot;
            shootRange = soldierConfig.shootRange;
            fireRate = soldierConfig.fireRate;
            bulletDamage = soldierConfig.bulletDamage;
            projectilePrefab = soldierConfig.bulletPrefab;
            
            if (soldierConfig.shootPoint != null)
            {
                shootPoint = soldierConfig.shootPoint;
            }
        }
        else
        {
            Debug.LogError("SoldierAIController requiere SoldierConfigData!");
            return;
        }

        base.Start();

}
        
        // ✅ FORZAR INICIALIZACIÓN DEL SISTEMA DE DISPARO
private void ForceInitializeShootingSystem()
{
    if (!canShoot) return;
    
    shootingSystem = GetComponent<EnemyShootingSystem>();
    if (shootingSystem == null)
    {
        shootingSystem = gameObject.AddComponent<EnemyShootingSystem>();
        Debug.Log($"🔄 Creando EnemyShootingSystem para {enemyConfig.enemyName}");
    }

    // ✅ CONFIGURAR MANUALMENTE TODAS LAS PROPIEDADES
    shootingSystem.SetShootingEnabled(true);
    shootingSystem.SetFireRate(fireRate);
    shootingSystem.SetShootRange(shootRange);
    shootingSystem.SetBulletDamage(bulletDamage);
    shootingSystem.SetProjectilePrefab(projectilePrefab);
    
    if (shootPoint != null)
    {
        shootingSystem.SetShootPoint(shootPoint);
    }

    // ✅ CONFIGURAR EL TARGET DEL PLAYER
    if (fov != null && fov.playerRef != null)
    {
        shootingSystem.SetPlayerTarget(fov.playerRef);
    }

    shootingSystemInitialized = true;
    
    if (enableStateDebug)
    {
        Debug.Log($"✅ Sistema de disparo forzado - Ready: {shootingSystem != null}");
    }
}

     protected override void SaveInitialTransform()
{
    initialPosition = transform.position;
    initialRotation = transform.rotation;
    initialState = GetDefaultState();
    
    // ✅ GUARDAR PATROL POINTS INICIALES SI EXISTEN
    if (patrolPoints != null && patrolPoints.Count > 0)
    {
        initialPatrolPoints = patrolPoints.ToArray();
        initialPatrolIndex = currentPatrolIndex;
    }
    
    if (enableStateDebug)
    {
        Debug.Log($"💾 Soldier guardó posición inicial: {initialPosition}, PatrolPoints: {patrolPoints?.Count ?? 0}");
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
        
        // CONFIGURAR DESDE VARIABLES DEL AICONTROLLER
        shootingSystem.SetShootingEnabled(canShoot);
        shootingSystem.SetFireRate(fireRate);
        shootingSystem.SetShootRange(shootRange);
        shootingSystem.SetBulletDamage(bulletDamage);
        shootingSystem.SetProjectilePrefab(projectilePrefab);
        
        if (shootPoint != null)
        {
            shootingSystem.SetShootPoint(shootPoint);
        }
        
        if (enableStateDebug)
        {
            Debug.Log($"🔫 {enemyConfig.enemyName} - Sistema de disparo inicializado: {shootingSystem != null}");
        }
    }


    protected override void InitializeFromConfig()
{
    base.InitializeFromConfig();
    
    // ✅ SOLO la llamada al base, sin lógica de patrulla
}

   

  protected override void AlertBehavior()
    {
        if (!enemyConfig.canMove || !isGrounded) return;

        if (fov != null && fov.playerRef != null)
        {
            lastKnownPlayerPosition = fov.playerRef.transform.position;
            
            // ✅ FORZAR TRANSICIÓN INMEDIATA A CHASING SI VE AL JUGADOR
            if (fov.canSeePlayer)
            {
                if (currentState != AIState.Chasing)
                {
                    ChangeState(AIState.Chasing);
                }
                
                // ✅ INCLUSO EN ALERT, PREPARAR EL SISTEMA DE DISPARO
                if (shootingSystem == null)
                {
                    ForceInitializeShootingSystem();
                }
            }
        }

        // Comportamiento normal de alerta
        Vector3 moveDirection = GetMovementDirectionToPlayer();
        RotateTowardsTarget(lastKnownPlayerPosition);
        
        float currentSpeed = enemyConfig.movementSpeed * 0.9f;
        Vector3 targetVelocity = moveDirection * currentSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        
        Debug.DrawLine(transform.position, lastKnownPlayerPosition, Color.magenta);
    }

    protected virtual void Update()
    {
        UpdateStateDisplays();
        HandleStateTimers();
        EmergencyShootingFix();
    }

     protected override void ChaseBehavior()
    {
        base.ChaseBehavior();
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
    return AIState.Idle; // ❌ ELIMINAR la lógica de patrulla
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
    
    // Usar BulletPool
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
            
            // Configurar como bala enemiga
            HybridBullet bulletScript = bullet.GetComponent<HybridBullet>();
            if (bulletScript != null)
            {
                bulletScript.isPlayerBullet = false;
                bulletScript.dueño = this.gameObject;
            }
        }
    }
    
    // Efecto de sonido
    if (soldierConfig.shootSound != null)
    {
        AudioSource.PlayClipAtPoint(soldierConfig.shootSound, transform.position);
    }
}

    // ✅ NUEVO MÉTODO: Obtener posición de disparo
    private Vector3 GetShootPosition()
{
    if (shootPoint != null)
    {
        return shootPoint.position;
    }
    
    if (soldierConfig != null && soldierConfig.shootPoint != null)
    {
        return soldierConfig.shootPoint.position;
    }
    
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
    
    if (bullet != null)
    {
        bullet.OnBulletHit -= OnEnemyBulletHit;
    }
}

   public override void TakeDamage(float damageAmount)
{
    // ✅ NUEVO: Si ya está en Chasing o Alert, usar la lógica base
    if (currentState == AIState.Chasing || currentState == AIState.Alert)
    {
        base.TakeDamage(damageAmount);
        return;
    }

    // ✅ COMPORTAMIENTO ORIGINAL solo si NO está en Chasing o Alert
    base.TakeDamage(damageAmount);
    
    // El comportamiento adicional ya está manejado en el base
}
protected override void DamagedBehavior()
{
    base.DamagedBehavior();
    
    // Comportamiento adicional específico para soldiers durante el estado Damaged
    // Por ejemplo: no pueden disparar, movilidad muy reducida
    
    // Soldiers no disparan mientras están dañados
    // (el sistema de disparo ya está desactivado en este estado)
    
    // Efecto visual adicional para soldiers
    if (Time.frameCount % 20 == 0)
    {
        Debug.DrawRay(transform.position + Vector3.up * 2f, Vector3.forward * 2f, Color.red);
    }
}

protected override void PatrolBehavior()
{
    // Los soldiers pueden tener comportamientos específicos durante la patrulla
    // Por ejemplo, estar más atentos o tener diferentes velocidades
    
    if (!enemyConfig.canMove || !isGrounded) 
    {
        base.PatrolBehavior();
        return;
    }

    // Comportamiento base de patrulla
    base.PatrolBehavior();
    
    // Comportamiento adicional específico para soldiers
    // Por ejemplo: escanear el área mientras patrulla
    if (!isWaitingAtPoint && currentState == AIState.Patrolling)
    {
        // Los soldiers pueden rotar ligeramente mientras se mueven para escanear
        transform.Rotate(0, Mathf.Sin(Time.time) * 10f * Time.deltaTime, 0);
    }
}

private void ForceShootingInChasing()
    {
        if (shootingSystem == null) 
        {
            if (enableStateDebug && Time.frameCount % 120 == 0)
            {
                Debug.LogError("❌ shootingSystem es NULL en Soldier!");
            }
            return;
        }

        // Verificar condiciones básicas
        if (fov == null || !fov.canSeePlayer || fov.playerRef == null) 
        {
            if (enableStateDebug && Time.frameCount % 120 == 0)
            {
                Debug.Log($"👁️ No puede disparar - CanSeePlayer: {fov?.canSeePlayer}");
            }
            return;
        }

        // ✅ LLAMAR AL SISTEMA DE DISPARO
        shootingSystem.TryShootAtPlayer();
        
        // Debug para confirmar que se está intentando disparar
        if (enableStateDebug && Time.frameCount % 90 == 0)
        {
            Debug.Log($"🎯 Soldier en Chasing - Intentando disparar");
        }
    }

    protected override void ChangeState(AIState newState)
    {
        AIState previousState = currentState;
        base.ChangeState(newState);

        // ✅ CUANDO PASA DE ALERT A CHASING, FORZAR DISPARO
        if (previousState == AIState.Alert && newState == AIState.Chasing)
        {
            OnEnterChasingState();
        }
    }

    private void OnEnterChasingState()
    {
        if (enableStateDebug)
        {
            Debug.Log($"🎯 Soldier entrando en Chasing - Activando sistema de disparo");
        }

        // ✅ FORZAR REINICIALIZACIÓN DEL SISTEMA DE DISPARO
        if (shootingSystem == null)
        {
            ForceInitializeShootingSystem();
        }
        else
        {
            // ✅ REACTIVAR EL SISTEMA EXISTENTE
            shootingSystem.SetShootingEnabled(true);
            shootingSystem.nextFireTime = Time.time; // Resetear el timer de disparo
        }

        // ✅ DEBUG PARA CONFIRMAR QUE ESTÁ LISTO PARA DISPARAR
        if (enableStateDebug)
        {
            Debug.Log($"🔫 Sistema de disparo listo - CanShoot: {shootingSystem?.canShoot}, Player: {shootingSystem?.player != null}");
        }
    }

    private void EmergencyShootingFix()
{
    if (currentState == AIState.Chasing && shootingSystem != null)
    {
        // Verificar condiciones manualmente
        if (fov != null && fov.canSeePlayer && fov.playerRef != null)
        {
            float distance = Vector3.Distance(transform.position, fov.playerRef.transform.position);
            if (distance <= shootRange)
            {
                // Llamar al método interno de shootingSystem si TryShootAtPlayer no funciona
                shootingSystem.TryShootAtPlayer();
                
                // Debug de emergencia
                if (enableStateDebug && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"🚨 EMERGENCY SHOOTING - Distance: {distance}, InRange: {distance <= shootRange}");
                }
            }
        }
    }
}

protected override Vector3 GetRespawnPosition()
{
    // Si tiene patrol points, usar el primero
    if (usePatrol && patrolPoints != null && patrolPoints.Count > 0)
    {
        return patrolPoints[0];
    }
    
    // Si no tiene patrol, usar posición inicial
    return base.GetRespawnPosition();
}

protected override Quaternion GetRespawnRotation()
{
    // Si tiene patrol points, rotar hacia el segundo waypoint (si existe)
    if (usePatrol && patrolPoints != null && patrolPoints.Count > 1)
    {
        Vector3 directionToNextPoint = (patrolPoints[1] - patrolPoints[0]).normalized;
        if (directionToNextPoint != Vector3.zero)
        {
            return Quaternion.LookRotation(directionToNextPoint);
        }
    }
    
    // Si no, usar rotación inicial
    return base.GetRespawnRotation();
}

public override void Revive()
{
    base.Revive();
    
    // ✅ RESET ESPECÍFICO PARA SOLDIERS
    currentPatrolIndex = 0;
    isWaitingAtPoint = false;
    patrolWaitTimer = 0f;
    
    // ✅ REINICIALIZAR SISTEMA DE DISPARO
    if (canShoot && shootingSystem != null)
    {
        shootingSystem.SetShootingEnabled(true);
        shootingSystem.nextFireTime = Time.time;
    }
    
    if (enableStateDebug)
    {
        Debug.Log($"🎖️ Soldier revivido - PatrolIndex: {currentPatrolIndex}, Position: {transform.position}");
    }
}
}