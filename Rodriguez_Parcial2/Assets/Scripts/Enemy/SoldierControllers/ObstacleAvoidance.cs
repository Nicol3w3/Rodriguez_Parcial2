using UnityEngine;
using System.Collections.Generic;

public class ObstacleAvoidance : MonoBehaviour
{
    [Header("Obstacle Detection")]
    public float avoidanceDistance = 3f;
    public float sideDetectionDistance = 2f;
    public LayerMask obstacleMask = -1;
    
    [Header("Avoidance Behavior")]
    public float avoidanceForce = 8f;
    public float steeringForce = 5f;
    public float minObstacleDistance = 1f;
    
    [Header("Advanced Settings")]
    public bool usePredictiveAvoidance = true;
    public float predictionDistance = 2f;
    public float avoidanceSmoothness = 2f;
    
    private Rigidbody rb;
    private AIController aiController;
    private Vector3 smoothedAvoidanceDirection;
    private List<Vector3> obstacleHits = new List<Vector3>();

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        aiController = GetComponent<AIController>();
    }

    void FixedUpdate()
    {
        if (aiController != null && aiController.IsDead()) return;
        
        Vector3 avoidance = CalculateObstacleAvoidance();
        ApplyAvoidanceForce(avoidance);
    }

    private Vector3 CalculateObstacleAvoidance()
    {
        Vector3 avoidanceForce = Vector3.zero;
        obstacleHits.Clear();
        
        // 1. Detección frontal principal
        Vector3 frontAvoidance = CalculateFrontAvoidance();
        avoidanceForce += frontAvoidance;
        
        // 2. Detección de laterales
        Vector3 sideAvoidance = CalculateSideAvoidance();
        avoidanceForce += sideAvoidance;
        
        // 3. Detección predictiva si está habilitada
        if (usePredictiveAvoidance)
        {
            Vector3 predictiveAvoidance = CalculatePredictiveAvoidance();
            avoidanceForce += predictiveAvoidance;
        }
        
        return avoidanceForce;
    }

    private Vector3 CalculateFrontAvoidance()
    {
        Vector3 avoidance = Vector3.zero;
        
        // Rayos frontales en abanico con diferentes ángulos y distancias
        float[] angles = { 0f, 30f, -30f, 45f, -45f };
        float[] distances = { 1f, 0.8f, 0.8f, 0.6f, 0.6f };
        
        for (int i = 0; i < angles.Length; i++)
        {
            Vector3 rayDirection = Quaternion.Euler(0, angles[i], 0) * transform.forward;
            RaycastHit hit;
            
            if (Physics.Raycast(transform.position, rayDirection, out hit, avoidanceDistance * distances[i], obstacleMask))
            {
                float severity = 1.0f - (hit.distance / (avoidanceDistance * distances[i]));
                Vector3 avoidanceDir = CalculateOptimalAvoidanceDirection(hit);
                avoidance += avoidanceDir * severity;
                obstacleHits.Add(hit.point);
                
                Debug.DrawRay(transform.position, rayDirection * hit.distance, Color.red);
            }
            else
            {
                Debug.DrawRay(transform.position, rayDirection * avoidanceDistance * distances[i], Color.green);
            }
        }
        
        return avoidance;
    }

    private Vector3 CalculateSideAvoidance()
    {
        Vector3 avoidance = Vector3.zero;
        
        // Detección de laterales
        Vector3[] sideDirections = {
            transform.right,                    // Derecha
            -transform.right,                   // Izquierda
            transform.right * 0.7f + transform.forward * 0.3f,   // Diagonal derecha-frontal
            -transform.right * 0.7f + transform.forward * 0.3f   // Diagonal izquierda-frontal
        };
        
        foreach (Vector3 direction in sideDirections)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, sideDetectionDistance, obstacleMask))
            {
                float severity = 1.0f - (hit.distance / sideDetectionDistance);
                Vector3 pushDirection = -direction.normalized;
                avoidance += pushDirection * severity * 0.5f; // Menor peso que los frontales
                
                Debug.DrawRay(transform.position, direction * hit.distance, Color.yellow);
            }
        }
        
        return avoidance;
    }

    private Vector3 CalculatePredictiveAvoidance()
    {
        Vector3 avoidance = Vector3.zero;
        
        if (rb == null || rb.linearVelocity.magnitude < 0.1f) return avoidance;
        
        // Predecir posición futura basada en la velocidad actual
        Vector3 futurePosition = transform.position + rb.linearVelocity.normalized * predictionDistance;
        
        // Verificar obstáculos en la trayectoria predicha
        RaycastHit hit;
        if (Physics.Raycast(transform.position, rb.linearVelocity.normalized, out hit, predictionDistance, obstacleMask))
        {
            float severity = 1.0f - (hit.distance / predictionDistance);
            Vector3 avoidanceDir = CalculateOptimalAvoidanceDirection(hit);
            avoidance += avoidanceDir * severity * 0.7f; // Peso moderado
            
            Debug.DrawLine(transform.position, hit.point, Color.magenta);
        }
        
        return avoidance;
    }

    private Vector3 CalculateOptimalAvoidanceDirection(RaycastHit hit)
    {
        Vector3 hitNormal = hit.normal;
        hitNormal.y = 0; // Ignorar componente vertical
        
        if (hitNormal == Vector3.zero) 
            hitNormal = Vector3.up;
        
        // Calcular direcciones potenciales de evasión
        Vector3 rightPerpendicular = Vector3.Cross(hitNormal, Vector3.up).normalized;
        Vector3 leftPerpendicular = -rightPerpendicular;
        
        // Evaluar ambas direcciones y elegir la mejor
        float rightScore = EvaluateDirectionScore(rightPerpendicular);
        float leftScore = EvaluateDirectionScore(leftPerpendicular);
        
        Vector3 bestDirection = rightScore > leftScore ? rightPerpendicular : leftPerpendicular;
        
        // Si estamos persiguiendo a un jugador, priorizar direcciones que mantengan el pursuit
        if (aiController != null && aiController.isChasing)
        {
            bestDirection = AdjustDirectionForPursuit(bestDirection);
        }
        
        return bestDirection;
    }

    private Vector3 AdjustDirectionForPursuit(Vector3 avoidanceDirection)
    {
        // Obtener dirección hacia el jugador
        FieldOfView fov = GetComponent<FieldOfView>();
        if (fov != null && fov.playerRef != null)
        {
            Vector3 toPlayer = (fov.playerRef.transform.position - transform.position).normalized;
            toPlayer.y = 0;
            
            // Combinar dirección de evasión con dirección al jugador
            Vector3 combinedDirection = (avoidanceDirection + toPlayer * 0.3f).normalized;
            return combinedDirection;
        }
        
        return avoidanceDirection;
    }

    private float EvaluateDirectionScore(Vector3 direction)
    {
        float score = 0f;
        
        // Verificar si la dirección está libre de obstáculos
        RaycastHit hit;
        if (!Physics.Raycast(transform.position, direction, out hit, avoidanceDistance, obstacleMask))
        {
            score += 2f; // Gran bonus si no hay obstáculos
        }
        else
        {
            // Penalizar basado en la distancia al obstáculo
            score += hit.distance / avoidanceDistance;
        }
        
        return score;
    }

    private void ApplyAvoidanceForce(Vector3 avoidanceForce)
    {
        if (avoidanceForce.magnitude > 0.1f && aiController != null && aiController.isChasing)
        {
            // Suavizar la dirección de evasión
            smoothedAvoidanceDirection = Vector3.Lerp(
                smoothedAvoidanceDirection, 
                avoidanceForce.normalized, 
                Time.fixedDeltaTime * avoidanceSmoothness
            );
            
            // Aplicar fuerza solo si es significativa
            rb.AddForce(smoothedAvoidanceDirection * this.avoidanceForce, ForceMode.Acceleration);
            
            Debug.DrawRay(transform.position, smoothedAvoidanceDirection * 2f, Color.blue);
        }
        else
        {
            smoothedAvoidanceDirection = Vector3.zero;
        }
    }

    public bool IsPathBlocked(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetPosition);
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction, out hit, distance, obstacleMask))
        {
            // Solo considerar bloqueado si el obstáculo está muy cerca
            return hit.distance < minObstacleDistance;
        }
        
        return false;
    }

    public Vector3 GetAvoidanceDirection(Vector3 targetPosition)
    {
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        
        if (!IsPathBlocked(targetPosition))
        {
            return directionToTarget; // Camino despejado
        }
        
        // Calcular dirección de evasión
        Vector3 avoidanceDirection = CalculateObstacleAvoidance();
        
        if (avoidanceDirection.magnitude > 0.1f)
        {
            // Combinar dirección al objetivo con dirección de evasión
            Vector3 combinedDirection = (directionToTarget + avoidanceDirection.normalized * 0.5f).normalized;
            return combinedDirection;
        }
        
        return directionToTarget;
    }

    // Método para debugging
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        // Dibujar área de detección
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, avoidanceDistance);
        
        // Dibujar dirección de evasión actual
        if (smoothedAvoidanceDirection != Vector3.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, smoothedAvoidanceDirection * 2f);
        }
        
        // Dibujar hits de obstáculos
        Gizmos.color = Color.red;
        foreach (Vector3 hitPoint in obstacleHits)
        {
            Gizmos.DrawSphere(hitPoint, 0.2f);
        }
        
        // Dibujar predicción de movimiento
        if (usePredictiveAvoidance && rb != null && rb.linearVelocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.white;
            Vector3 futurePos = transform.position + rb.linearVelocity.normalized * predictionDistance;
            Gizmos.DrawLine(transform.position, futurePos);
            Gizmos.DrawWireSphere(futurePos, 0.3f);
        }
    }
}