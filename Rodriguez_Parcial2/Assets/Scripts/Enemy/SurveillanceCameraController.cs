using UnityEngine;

public class SurveillanceCameraController : AIController
{
    private SurveillanceCameraConfigData cameraConfig;
    private float currentRotation = 0f;
    private float rotationDirection = 1f;
    private float pauseTimer = 0f;
    private bool isPaused = false;
    private Light spotLight;
    private Quaternion initialRotation;

    protected override void Start()
    {
        if (enemyConfig is SurveillanceCameraConfigData)
        {
            cameraConfig = (SurveillanceCameraConfigData)enemyConfig;
        }
        else
        {
            Debug.LogError("SurveillanceCameraController requiere SurveillanceCameraConfigData!");
            return;
        }

        base.Start();
        
        initialRotation = transform.rotation;
        SetupVisuals();
    }

    private void SetupVisuals()
    {
        // Configurar luz spotlight
        spotLight = GetComponentInChildren<Light>();
        if (spotLight != null && cameraConfig.spotlight != null)
        {
            spotLight.color = cameraConfig.neutralColor;
            spotLight.spotAngle = cameraConfig.detectionAngle;
            spotLight.range = cameraConfig.detectionRadius;
        }

        // Aplicar material
        if (cameraConfig.enemyMaterial != null)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = cameraConfig.enemyMaterial;
            }
        }
    }

    protected override void HandleDetection()
    {
        base.HandleDetection();
        
        // Actualizar color de la luz según el estado
        if (spotLight != null)
        {
            spotLight.color = isChasing ? cameraConfig.alertColor : cameraConfig.neutralColor;
        }

        // Alertar a otros enemigos si está en alerta
        if (isChasing && cameraConfig.canAlertOtherEnemies)
        {
            AlertNearbyEnemies();
        }
    }

    protected override void IdleBehavior()
    {
        if (isPaused)
        {
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= cameraConfig.scanPauseTime)
            {
                isPaused = false;
                pauseTimer = 0f;
                // Cambiar dirección después de la pausa
                rotationDirection *= -1f;
            }
            return;
        }

        // Rotación de vigilancia
        currentRotation += rotationDirection * cameraConfig.rotationSpeed * Time.deltaTime;
        
        // Limitar rotación
        if (Mathf.Abs(currentRotation) >= cameraConfig.rotationAngle / 2f)
        {
            isPaused = true;
            currentRotation = Mathf.Clamp(currentRotation, -cameraConfig.rotationAngle / 2f, cameraConfig.rotationAngle / 2f);
        }

        // Aplicar rotación
        transform.rotation = initialRotation * Quaternion.Euler(0, currentRotation, 0);
    }

    private void AlertNearbyEnemies()
    {
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, cameraConfig.alertRadius);
        
        foreach (Collider collider in nearbyEnemies)
        {
            AIController enemy = collider.GetComponent<AIController>();
            if (enemy != null && enemy != this && !enemy.IsDead())
            {
                // ✅ CORREGIDO: No acceder directamente a enemy.fov
                // En su lugar, forzar la detección mediante el FieldOfView del enemigo
                ForceEnemyDetection(enemy);
            }
        }
    }

    // ✅ NUEVO: Método para forzar la detección sin acceder directamente a fov
    private void ForceEnemyDetection(AIController enemy)
    {
        // Buscar el FieldOfView del enemigo de manera segura
        FieldOfView enemyFOV = enemy.GetComponent<FieldOfView>();
        if (enemyFOV != null && fov != null && fov.playerRef != null)
        {
            // Forzar que el enemigo vea al jugador temporalmente
            enemyFOV.canSeePlayer = true;
            
            // Opcional: También puedes actualizar la última posición conocida
            if (enemy is SoldierAIController soldier)
            {
                // Si es un soldier, actualizar su última posición conocida
                soldier.SetLastKnownPosition(fov.playerRef.transform.position);
            }
        }
    }

    protected override AIState GetDefaultState()
    {
        return AIState.Idle; // Las cámaras siempre están en modo vigilancia
    }

    // Las cámaras no se mueven
    protected override void ChaseBehavior()
    {
        // Solo rotar hacia el jugador, no moverse
        if (fov != null && fov.playerRef != null)
        {
            RotateTowards(fov.playerRef.transform.position);
        }
    }

    public override void TakeDamage(float damageAmount)
    {
        base.TakeDamage(damageAmount);
        
        // Efecto visual adicional para cámaras
        if (spotLight != null)
        {
            spotLight.intensity = Mathf.Lerp(spotLight.intensity, 0f, 0.3f);
        }
    }

    // ✅ NUEVO: Método para obtener el FieldOfView de esta cámara
    public FieldOfView GetCameraFOV()
    {
        return fov;
    }
}