using System.Collections;
using UnityEngine;

public class FieldOfView : MonoBehaviour
{
    public float radius = 5f;
    [Range(0, 360)]
    public float angle = 90f;

    public GameObject playerRef;
    public LayerMask targetMask;
    public LayerMask obstructionMask;
    public bool canSeePlayer;

    private AIController aiController;

    private void Start()
    {
        // Buscar player si no está asignado
        if (playerRef == null)
        {
            playerRef = GameObject.FindGameObjectWithTag("Player");
        }
        
        aiController = GetComponent<AIController>();
        if (aiController == null)
        {
            aiController = GetComponentInParent<AIController>();
        }
        
        if (playerRef == null)
        {
            Debug.LogError("No se encontró objeto con tag 'Player'");
        }
        else
        {
//            Debug.Log($"✅ FieldOfView - Player asignado: {playerRef.name}");
        }
        
        StartCoroutine(FOVRoutine());
    }

    private IEnumerator FOVRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(0.2f);
        while (true)
        {
            yield return wait;
            FieldOfViewCheck();
        }
    }

    private void FieldOfViewCheck()
    {
        // Bloquear detección si está muerto
        if (aiController != null && aiController.IsDead())
        {
            canSeePlayer = false;
            return;
        }

        if (playerRef == null) 
        {
            canSeePlayer = false;
            return;
        }

        // VERIFICACIÓN MEJORADA - Buscar específicamente al player
        bool playerInRange = false;
        bool playerInAngle = false;
        bool noObstruction = false;

        // 1. Verificar si el player está en rango
        float distanceToPlayer = Vector3.Distance(transform.position, playerRef.transform.position);
        playerInRange = distanceToPlayer <= radius;

        if (playerInRange)
        {
            // 2. Verificar si está dentro del ángulo
            Vector3 directionToPlayer = (playerRef.transform.position - transform.position).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            playerInAngle = angleToPlayer < angle / 2;

            if (playerInAngle)
            {
                // 3. Verificar que no haya obstrucciones
                noObstruction = !Physics.Raycast(transform.position, directionToPlayer, distanceToPlayer, obstructionMask);
            }
        }

        canSeePlayer = playerInRange && playerInAngle && noObstruction;

        // DEBUG DETALLADO
        if (playerInRange)
        {
            Vector3 directionToPlayer = (playerRef.transform.position - transform.position).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            
            string debugMsg = $"Player en rango: {playerInRange} (distancia: {distanceToPlayer:F1}/{radius}), " +
                            $"en ángulo: {playerInAngle} ({angleToPlayer:F1}/{angle/2}), " +
                            $"sin obstrucción: {noObstruction}, " +
                            $"CAN SEE: {canSeePlayer}";
            
            if (canSeePlayer)
            {
//                Debug.Log($"🎯 {debugMsg}");
            }
            else if (playerInRange && playerInAngle && !noObstruction)
            {
    //            Debug.Log($"🚫 {debugMsg} - OBSTRUIDO");
            }
            else if (playerInRange && !playerInAngle)
            {
      //          Debug.Log($"📐 {debugMsg} - FUERA DE ÁNGULO");
            }
        }
    }

    // Método para debug rápido desde el inspector
    [ContextMenu("Debug FieldOfView")]
    public void DebugFieldOfView()
    {
        Debug.Log("=== FIELD OF VIEW DEBUG ===");
        Debug.Log($"PlayerRef: {playerRef}");
        Debug.Log($"Radius: {radius}");
        Debug.Log($"Angle: {angle}");
        Debug.Log($"Target Mask: {targetMask.value}");
        Debug.Log($"Obstruction Mask: {obstructionMask.value}");
        Debug.Log($"Can See Player: {canSeePlayer}");
        
        if (playerRef != null)
        {
            float distance = Vector3.Distance(transform.position, playerRef.transform.position);
            Vector3 direction = (playerRef.transform.position - transform.position).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, direction);
            
            Debug.Log($"Distancia al player: {distance:F1}");
            Debug.Log($"Ángulo al player: {angleToPlayer:F1}");
            Debug.Log($"Dentro de rango: {distance <= radius}");
            Debug.Log($"Dentro de ángulo: {angleToPlayer < angle / 2}");
        }
    }

    public void SetDetectionEnabled(bool enabled)
    {
        if (!enabled)
        {
            canSeePlayer = false;
        }
    }

    public void ResetDetection()
    {
        canSeePlayer = false;
        StopAllCoroutines();
        StartCoroutine(FOVRoutine());
    }

    private void OnDrawGizmos()
    {
        // Área completa de detección
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, radius);

        // Cono de visión
        Gizmos.color = canSeePlayer ? Color.red : Color.yellow;
        Vector3 viewAngleA = DirFromAngle(-angle / 2);
        Vector3 viewAngleB = DirFromAngle(angle / 2);

        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * radius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * radius);

        // Línea al player si está en rango
        if (playerRef != null)
        {
            float distance = Vector3.Distance(transform.position, playerRef.transform.position);
            if (distance <= radius)
            {
                Gizmos.color = canSeePlayer ? Color.green : Color.blue;
                Gizmos.DrawLine(transform.position, playerRef.transform.position);
            }
        }
    }

    private Vector3 DirFromAngle(float angleInDegrees)
    {
        angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}