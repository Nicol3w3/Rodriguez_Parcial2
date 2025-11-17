using UnityEngine;
using System.Collections.Generic;

public class SoldierAIController : AIController
{
    private float nextFireTime = 0f;
    private bool canShoot = true;

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
        
        // ✅ CONFIGURAR DISPARO DESDE EL CONFIG
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

    base.Start(); // ✅ LLAMAR AL BASE DESPUÉS de configurar
    
    if (enableStateDebug)
    {
        Debug.Log($"🔫 Soldier inicializado - CanShoot: {canShoot}, FireRate: {fireRate}");
    }
}


     protected override void SaveInitialTransform()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialState = GetDefaultState();
        
        if (enableStateDebug)
        {
            Debug.Log($"💾 Soldier guardó posición inicial: {initialPosition}");
        }
    }

   private void InitializeShootingSystem()
    {
        shootingSystem = GetComponent<EnemyShootingSystem>();
        if (shootingSystem == null)
            shootingSystem = gameObject.AddComponent<EnemyShootingSystem>();
        
        if (soldierConfig != null)
        {
            shootingSystem.SetShootingEnabled(soldierConfig.canShoot);
            shootingSystem.SetFireRate(soldierConfig.fireRate);
            shootingSystem.SetShootRange(soldierConfig.shootRange);
            shootingSystem.SetBulletDamage(soldierConfig.bulletDamage);
            
            if (soldierConfig.shootPoint != null)
            {
                shootingSystem.SetShootPoint(soldierConfig.shootPoint);
            }
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

    // Soldiers son más agresivos en alerta
    if (fov != null && fov.playerRef != null)
    {
        lastKnownPlayerPosition = fov.playerRef.transform.position;
        
        // ✅ FORZAR TRANSICIÓN A CHASING SI VE AL JUGADOR
        if (fov.canSeePlayer && currentState != AIState.Chasing)
        {
            ChangeState(AIState.Chasing);
            
            if (enableStateDebug)
            {
                Debug.Log($"🚨 Soldier pasó de Alert a Chasing - Iniciando disparos");
            }
        }
    }

    Vector3 moveDirection = GetMovementDirectionToPlayer();
    RotateTowardsTarget(lastKnownPlayerPosition);
    
    // Soldiers se mueven a velocidad casi normal en alerta
    float currentSpeed = enemyConfig.movementSpeed * 0.9f;
    
    Vector3 targetVelocity = moveDirection * currentSpeed;
    rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    
    // Debug visual
    Debug.DrawLine(transform.position, lastKnownPlayerPosition, Color.magenta);
}

    protected override void Update()
{
    base.Update();
    
    // DEBUG para confirmar que se ejecuta
    if (Time.frameCount % 30 == 0)
    {
        Debug.Log($"🔄 SoldierAIController Update - Estado: {currentState}");
    }
    
    if (currentState == AIState.Chasing && soldierConfig != null && soldierConfig.canShoot)
    {
        Debug.Log($"🎯 Estado Chasing detectado - Llamando TryShootAtPlayer");
        
        if (shootingSystem != null)
        {
            shootingSystem.TryShootAtPlayer();
        }
        else
        {
            Debug.LogError("❌ shootingSystem es NULL!");
        }
    }
}

     protected override void ChaseBehavior()
    {
        base.ChaseBehavior();
        
        // Soldiers disparan mientras persiguen
        if (fov != null && fov.canSeePlayer && soldierConfig != null && soldierConfig.canShoot)
        {
            shootingSystem?.TryShootAtPlayer();
        }
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

    protected override void HandleShooting()
{
    if (!canShoot || soldierConfig == null || !soldierConfig.canShoot) return;
    
    // ✅ PERMITIR DISPARAR en Chasing Y Alert (si tiene línea de visión)
    bool canShootInCurrentState = currentState == AIState.Chasing || 
                                 (currentState == AIState.Alert && fov != null && fov.canSeePlayer);
    
    if (canShootInCurrentState)
    {
        TryShootAtPlayer();
        
        // Debug específico para transición Alert -> Chasing
        if (enableStateDebug && currentState == AIState.Chasing && fov != null && fov.canSeePlayer)
        {
            Debug.Log($"🎯 Soldier disparando en estado Chasing - CanSeePlayer: {fov.canSeePlayer}");
        }
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
    protected override Vector3 GetShootPosition()
{
    // 1. Prioridad: shootPoint del soldier específico
    if (shootPoint != null)
    {
        return shootPoint.position;
    }
    
    // 2. Fallback: shootPoint del config
    if (soldierConfig != null && soldierConfig.shootPoint != null)
    {
        return soldierConfig.shootPoint.position;
    }
    
    // 3. Último recurso: usar el método base
    return base.GetShootPosition();
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
}