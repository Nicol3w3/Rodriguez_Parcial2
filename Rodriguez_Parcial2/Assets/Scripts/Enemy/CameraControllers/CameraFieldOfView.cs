// CameraFieldOfView.cs - ARCHIVO NUEVO
using UnityEngine;
using System.Collections;

public class CameraFieldOfView : MonoBehaviour
{
    [Header("Camera Field of View Settings")]
    public float radius = 10f;
    [Range(0, 360)]
    public float angle = 90f;
    public GameObject playerRef;
    public LayerMask targetMask;
    public LayerMask obstructionMask;
    public bool canSeePlayer;

    [Header("Camera References")]
    public Transform pivotPoint; // El pivot que realmente rota
    public Transform detectionOrigin; // Origen de detección

    private SurveillanceCameraController cameraController;

    private void Start()
    {
        cameraController = GetComponent<SurveillanceCameraController>();
        
        // Buscar player si no está asignado
        if (playerRef == null)
        {
            playerRef = GameObject.FindGameObjectWithTag("Player");
        }
        
        // Buscar referencias de cámara si no están asignadas
        if (pivotPoint == null)
        {
            pivotPoint = transform.Find("PivotPoint");
        }
        
        if (detectionOrigin == null)
        {
            detectionOrigin = transform.Find("DetectionOrigin");
            if (detectionOrigin == null && pivotPoint != null)
            {
                detectionOrigin = pivotPoint;
            }
        }

        if (playerRef == null)
        {
            Debug.LogError("CameraFieldOfView: No se encontró objeto con tag 'Player'");
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
        // Bloquear detección si está destruida
        if (cameraController != null && cameraController.IsCameraDestroyed())
        {
            canSeePlayer = false;
            return;
        }

        if (playerRef == null) 
        {
            canSeePlayer = false;
            return;
        }

        // ✅ USAR EL PIVOT POINT PARA DETECCIÓN
        Vector3 detectionPos = detectionOrigin != null ? detectionOrigin.position : transform.position;
        Vector3 detectionForward = pivotPoint != null ? pivotPoint.forward : transform.forward;

        // 1. Verificar si el player está en rango
        float distanceToPlayer = Vector3.Distance(detectionPos, playerRef.transform.position);
        bool playerInRange = distanceToPlayer <= radius;

        if (playerInRange)
        {
            // 2. Verificar si está dentro del ángulo
            Vector3 directionToPlayer = (playerRef.transform.position - detectionPos).normalized;
            float angleToPlayer = Vector3.Angle(detectionForward, directionToPlayer);
            bool playerInAngle = angleToPlayer < angle / 2;

            if (playerInAngle)
            {
                // 3. Verificar que no haya obstrucciones
                bool noObstruction = !Physics.Raycast(detectionPos, directionToPlayer, distanceToPlayer, obstructionMask);
                canSeePlayer = noObstruction;

                // Debug
                if (canSeePlayer)
                {
                    Debug.DrawLine(detectionPos, playerRef.transform.position, Color.red, 0.2f);
                }
                else
                {
                    Debug.DrawLine(detectionPos, playerRef.transform.position, Color.yellow, 0.2f);
                }
            }
            else
            {
                canSeePlayer = false;
            }
        }
        else
        {
            canSeePlayer = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (pivotPoint == null) return;

        // Área completa de detección
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, radius);

        // Cono de visión (usa el pivot point)
        Vector3 detectionPos = detectionOrigin != null ? detectionOrigin.position : transform.position;
        Gizmos.color = canSeePlayer ? Color.red : Color.yellow;
        
        Vector3 viewAngleA = DirFromAngle(-angle / 2, pivotPoint);
        Vector3 viewAngleB = DirFromAngle(angle / 2, pivotPoint);

        Gizmos.DrawLine(detectionPos, detectionPos + viewAngleA * radius);
        Gizmos.DrawLine(detectionPos, detectionPos + viewAngleB * radius);

        // Línea al player si está en rango
        if (playerRef != null && canSeePlayer)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(detectionPos, playerRef.transform.position);
        }
    }

    private Vector3 DirFromAngle(float angleInDegrees, Transform referenceTransform)
    {
        angleInDegrees += referenceTransform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}