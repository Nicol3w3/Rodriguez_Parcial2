using UnityEngine;

public class ObstacleAvoidance : MonoBehaviour
{
    [Header("Obstacle Avoidance")]
    public float avoidanceDistance = 2f;
    public float avoidanceForce = 5f;
    public LayerMask obstacleMask = -1;
    
    private Rigidbody rb;
    private AIController aiController;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        aiController = GetComponent<AIController>();
    }

    void FixedUpdate()
    {
        if (aiController != null && aiController.IsDead()) return;
        
        AvoidObstacles();
    }

    private void AvoidObstacles()
    {
        Vector3[] rayDirections = {
            transform.forward,
            transform.forward + transform.right * 0.5f,
            transform.forward - transform.right * 0.5f,
            transform.forward + transform.right,
            transform.forward - transform.right
        };

        Vector3 avoidanceForceVector = Vector3.zero;
        int hitCount = 0;

        foreach (Vector3 direction in rayDirections)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, avoidanceDistance, obstacleMask))
            {
                // Calcular fuerza de evasión
                Vector3 forceDirection = Vector3.Reflect(direction, hit.normal).normalized;
                float forceStrength = 1.0f - (hit.distance / avoidanceDistance);
                
                avoidanceForceVector += forceDirection * forceStrength;
                hitCount++;
                
                Debug.DrawRay(transform.position, direction * hit.distance, Color.red);
            }
            else
            {
                Debug.DrawRay(transform.position, direction * avoidanceDistance, Color.green);
            }
        }

        // Aplicar fuerza de evasión si se detectaron obstáculos
        if (hitCount > 0 && aiController != null && aiController.isChasing)
        {
            avoidanceForceVector /= hitCount;
            rb.AddForce(avoidanceForceVector * avoidanceForce, ForceMode.Acceleration);
            
            Debug.DrawRay(transform.position, avoidanceForceVector * 2f, Color.blue);
        }
    }

    public bool IsPathBlocked(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetPosition);
        
        RaycastHit hit;
        return Physics.Raycast(transform.position, direction, out hit, distance, obstacleMask);
    }

    public Vector3 FindAlternativeDirection(Vector3 targetPosition)
    {
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        
        // Probar direcciones alternativas
        Vector3[] testDirections = {
            directionToTarget,
            Quaternion.Euler(0, 30, 0) * directionToTarget,
            Quaternion.Euler(0, -30, 0) * directionToTarget,
            Quaternion.Euler(0, 60, 0) * directionToTarget,
            Quaternion.Euler(0, -60, 0) * directionToTarget,
            transform.right,
            -transform.right
        };

        foreach (Vector3 testDir in testDirections)
        {
            if (!Physics.Raycast(transform.position, testDir, avoidanceDistance, obstacleMask))
            {
                return testDir;
            }
        }

        // Si todas las direcciones están bloqueadas, retroceder
        return -transform.forward;
    }
}