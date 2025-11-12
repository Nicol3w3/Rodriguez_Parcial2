using UnityEngine;

public class SoldierAIController : AIController
{
    private SoldierConfigData soldierConfig;
    private int currentWaypointIndex = 0;
    private float patrolTimer = 0f;

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
}
