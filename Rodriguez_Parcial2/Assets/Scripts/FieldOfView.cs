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

    // ✅ NUEVO: Referencia al AIController
    private AIController aiController;

    private void Start()
    {
        playerRef = GameObject.FindGameObjectWithTag("Player");
        
        // ✅ OBTENER referencia al AIController
        aiController = GetComponent<AIController>();
        if (aiController == null)
        {
            aiController = GetComponentInParent<AIController>();
        }
        
        if (playerRef == null)
        {
            Debug.LogError("No se encontró objeto con tag 'Player'");
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
        // ✅ BLOQUEAR detección si el enemigo está muerto
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

        Collider[] rangeChecks = Physics.OverlapSphere(transform.position, radius, targetMask);

        if (rangeChecks.Length != 0)
        {
            Transform target = rangeChecks[0].transform;
            Vector3 directionToTarget = (target.position - transform.position).normalized;

            if (Vector3.Angle(transform.forward, directionToTarget) < angle / 2)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.position);
                canSeePlayer = !Physics.Raycast(transform.position, directionToTarget, distanceToTarget, obstructionMask);
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

    // ✅ NUEVO: Método para forzar el estado de detección
    public void SetDetectionEnabled(bool enabled)
    {
        if (!enabled)
        {
            canSeePlayer = false;
        }
        // Si se habilita, la detección volverá a funcionar normalmente en el próximo frame
    }

    // ✅ NUEVO: Método para reiniciar la detección al revivir
    public void ResetDetection()
    {
        canSeePlayer = false;
        StopAllCoroutines();
        StartCoroutine(FOVRoutine());
    }

    // Resto del código se mantiene igual...
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, radius);

        Gizmos.color = Color.yellow;
        Vector3 viewAngleA = DirFromAngle(-angle / 2);
        Vector3 viewAngleB = DirFromAngle(angle / 2);

        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * radius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * radius);

        if (canSeePlayer && playerRef != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, playerRef.transform.position);
        }
    }

    private Vector3 DirFromAngle(float angleInDegrees)
    {
        angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}
