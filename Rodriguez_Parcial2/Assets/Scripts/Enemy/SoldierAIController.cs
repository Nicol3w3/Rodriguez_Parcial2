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

        Vector3 direction = (lastKnownPlayerPosition - transform.position).normalized;
        direction.y = 0; // Solo movimiento horizontal
        
        RotateTowards(lastKnownPlayerPosition);
        
        float currentSpeed = enemyConfig.chaseSpeed;
        
        // ✅ Evasión de obstáculos solo si está habilitado
        if (enemyConfig.useObstacleAvoidance && obstacleAvoidance != null)
        {
            if (obstacleAvoidance.IsPathBlocked(lastKnownPlayerPosition))
            {
                Vector3 alternativeDirection = obstacleAvoidance.FindAlternativeDirection(lastKnownPlayerPosition);
                direction = alternativeDirection;
                Debug.Log("🚧 Camino bloqueado, buscando ruta alternativa");
            }
        }
        
        // ✅ USAR velocity para movimiento físico consistente
        Vector3 targetVelocity = direction * currentSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        
        // ✅ Debug visual de la persecución
        Debug.DrawLine(transform.position, lastKnownPlayerPosition, Color.red);
    }

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
