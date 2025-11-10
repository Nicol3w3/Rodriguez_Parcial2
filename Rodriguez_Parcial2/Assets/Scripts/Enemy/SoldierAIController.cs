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
        RotateTowards(targetWaypoint.position);
        
        if (enemyConfig.canMove)
        {
            rb.MovePosition(transform.position + direction * enemyConfig.movementSpeed * Time.deltaTime);
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

    protected override AIState GetDefaultState()
    {
        return soldierConfig != null && soldierConfig.canPatrol ? AIState.Patrolling : AIState.Idle;
    }
    public void SetLastKnownPosition(Vector3 position)
{
    lastKnownPlayerPosition = position;
}
}
