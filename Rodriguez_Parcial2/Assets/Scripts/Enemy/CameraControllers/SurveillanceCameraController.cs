using UnityEngine;
using System.Collections;

public class SurveillanceCameraController : AIController
{
    private SurveillanceCameraConfigData cameraConfig;
    private float currentRotation = 0f;
    private float rotationDirection = 1f;
    private float pauseTimer = 0f;
    private bool isPaused = false;
    private Light spotLight;
    private LineRenderer laserSight;
    private Quaternion initialRotation;
    private float lastAlertTime = 0f;
    private bool isDestroyed = false;
    private AudioSource audioSource;
    private Renderer cameraRenderer;
    private Collider cameraCollider;

     [Header("Camera References")]
    [SerializeField] private Transform pivotPoint;
    [SerializeField] private Transform detectionOrigin;

    private CameraFieldOfView cameraFOV;

    private enum CameraState { Scanning, Alert, Searching, Destroyed }
    private CameraState cameraState = CameraState.Scanning;
    private CameraFieldOfViewAdapter fovAdapter;

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

    SetupRigidbody();
    SetupDamageCollider();
    
    base.Start();
    
    FindCameraReferences();
    
    // ✅ CONFIGURAR CAMERA FIELD OF VIEW
    SetupCameraFieldOfView();
    
    initialRotation = pivotPoint != null ? pivotPoint.rotation : transform.rotation;
    rotationDirection = cameraConfig.rotateClockwise ? 1f : -1f;
    
    cameraRenderer = GetComponent<Renderer>();
    
    SetupVisuals();
    SetupAudio();
}

private void SetupCameraFieldOfView()
{
    // Buscar el componente CameraFieldOfView
    cameraFOV = GetComponent<CameraFieldOfView>();
    if (cameraFOV == null)
    {
        cameraFOV = gameObject.AddComponent<CameraFieldOfView>();
    }
    
    // Configurar referencias
    if (cameraFOV != null)
    {
        cameraFOV.pivotPoint = pivotPoint;
        cameraFOV.detectionOrigin = detectionOrigin;
        cameraFOV.radius = enemyConfig.detectionRadius;
        cameraFOV.angle = enemyConfig.detectionAngle;
        cameraFOV.targetMask = enemyConfig.targetMask;
        cameraFOV.obstructionMask = enemyConfig.obstructionMask;
    }
    
    // Mantener referencia al FieldOfView base para compatibilidad
    fov = GetComponent<FieldOfView>();
    if (fov != null)
    {
        // Desactivar el FieldOfView normal
        fov.enabled = false;
    }
}

private void SetupDamageCollider()
{
    // Buscar el hijo DamageCollider
    Transform damageColliderChild = transform.Find("DamageCollider");
    if (damageColliderChild != null)
    {
        damageCollider = damageColliderChild.GetComponent<Collider>();
    }
    
    if (damageCollider == null)
    {
        // Crear hijo si no existe
        GameObject damageObj = new GameObject("DamageCollider");
        damageObj.transform.SetParent(transform);
        damageObj.transform.localPosition = Vector3.zero;
        damageObj.transform.localRotation = Quaternion.identity; // ✅ Importante
        
        // ✅ USAR CAPSULECOLLIDER EN LUGAR DE BOXCOLLIDER
        CapsuleCollider capsuleCollider = damageObj.AddComponent<CapsuleCollider>();
        capsuleCollider.radius = 0.5f;
        capsuleCollider.height = 2f;
        capsuleCollider.direction = 1; // Eje Y
        capsuleCollider.isTrigger = false;
        damageCollider = capsuleCollider;
    }
}

private void SetupRigidbody()
{
    Rigidbody rb = GetComponent<Rigidbody>();
    if (rb == null)
    {
        rb = gameObject.AddComponent<Rigidbody>();
    }
    
    // CONFIGURACIÓN ESPECÍFICA PARA CÁMARA
    rb.isKinematic = true; // ✅ IMPORTANTE: No afectado por física
    rb.useGravity = false; // ✅ No necesita gravedad
    rb.interpolation = RigidbodyInterpolation.None;
    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    
    // Congelar todas las rotaciones y posiciones
    rb.constraints = RigidbodyConstraints.FreezeAll;
    
//    Debug.Log("✅ Rigidbody configurado para cámara (Kinematic)");
}

 private void FindCameraReferences()
    {
        // Buscar PivotPoint en los hijos
        if (pivotPoint == null)
        {
            pivotPoint = transform.Find("PivotPoint");
            if (pivotPoint == null)
            {
                // Buscar cualquier hijo que tenga "pivot" en el nombre
                foreach (Transform child in transform)
                {
                    if (child.name.ToLower().Contains("pivot"))
                    {
                        pivotPoint = child;
                        break;
                    }
                }
            }
        }

        // Buscar DetectionOrigin en los hijos
        if (detectionOrigin == null)
        {
            detectionOrigin = transform.Find("DetectionOrigin");
            if (detectionOrigin == null)
            {
                // Si no existe, usar el pivot point o este transform
                detectionOrigin = pivotPoint != null ? pivotPoint : transform;
            }
        }

        // Si no se encontró pivotPoint, usar este GameObject
        if (pivotPoint == null)
        {
            pivotPoint = transform;
            Debug.LogWarning("⚠️ No se encontró PivotPoint, usando GameObject principal");
        }

//        Debug.Log($"✅ PivotPoint: {pivotPoint.name}, DetectionOrigin: {detectionOrigin.name}");
    }

   private void SetupVisuals()
    {
        // ✅ BUSCAR SPOTLIGHT EN EL PIVOT POINT
        if (pivotPoint != null)
        {
            spotLight = pivotPoint.GetComponentInChildren<Light>();
        }
        else
        {
            spotLight = GetComponentInChildren<Light>();
        }

        if (spotLight != null)
        {
            spotLight.color = cameraConfig.neutralColor;
            spotLight.spotAngle = enemyConfig.detectionAngle;
            spotLight.range = enemyConfig.detectionRadius;
        }

        if (cameraConfig.useLaserSight)
        {
            laserSight = GetComponent<LineRenderer>();
            if (laserSight == null)
            {
                laserSight = gameObject.AddComponent<LineRenderer>();
            }
            
            laserSight.startWidth = 0.02f;
            laserSight.endWidth = 0.02f;
            laserSight.material = new Material(Shader.Find("Sprites/Default"));
            laserSight.startColor = cameraConfig.neutralColor;
            laserSight.endColor = cameraConfig.neutralColor;
            laserSight.positionCount = 2;
        }

        if (enemyConfig.enemyMaterial != null && cameraRenderer != null)
        {
            cameraRenderer.material = enemyConfig.enemyMaterial;
        }
    }

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
        }
    }

    protected override void Update()
    {
        if (isDestroyed) return;
        
        base.Update();
        UpdateLaserSight();
        UpdateCameraState();
    }

    private void UpdateLaserSight()
    {
        if (!cameraConfig.useLaserSight || laserSight == null) return;

        // ✅ USAR DETECTION ORIGIN PARA EL LÁSER
        Vector3 laserStart = detectionOrigin.position;
        Vector3 laserDirection = detectionOrigin.forward;
        Vector3 laserEnd = laserStart + laserDirection * cameraConfig.laserRange;

        RaycastHit hit;
        if (Physics.Raycast(laserStart, laserDirection, out hit, cameraConfig.laserRange))
        {
            laserEnd = hit.point;
        }

        laserSight.SetPosition(0, laserStart);
        laserSight.SetPosition(1, laserEnd);

        Color laserColor = cameraConfig.neutralColor;
        switch (cameraState)
        {
            case CameraState.Alert:
                laserColor = cameraConfig.alertColor;
                break;
            case CameraState.Searching:
                laserColor = cameraConfig.searchingColor;
                break;
        }
        
        laserSight.startColor = laserColor;
        laserSight.endColor = laserColor;
    }

    private void UpdateCameraState()
    {
        if (spotLight != null)
        {
            Color lightColor = cameraConfig.neutralColor;
            switch (cameraState)
            {
                case CameraState.Alert:
                    lightColor = cameraConfig.alertColor;
                    break;
                case CameraState.Searching:
                    lightColor = cameraConfig.searchingColor;
                    break;
                case CameraState.Destroyed:
                    lightColor = Color.gray;
                    break;
            }
            spotLight.color = Color.Lerp(spotLight.color, lightColor, Time.deltaTime * 5f);
        }
    }

  protected override void HandleDetection()
    {
        if (isDestroyed) return;
        
        // ✅ LLAMAR AL BASE PRIMERO para que actualice la detección
        base.HandleDetection();
        
        if (cameraFOV != null && cameraFOV.canSeePlayer)
        {
            if (cameraState != CameraState.Alert)
            {
                SetCameraState(CameraState.Alert);
                PlaySound(cameraConfig.cameraDetectionSound);
                
                // ✅ FORZAR cambio de estado en el AIController también
                ChangeState(AIState.Alert);
                
                // Comunicar alerta a todos los soldiers
                if (fov != null && fov.playerRef != null)
                {
                    AlertOtherEnemies(fov.playerRef.transform.position);
                }
                else if (cameraFOV.playerRef != null)
                {
                    AlertOtherEnemies(cameraFOV.playerRef.transform.position);
                }
            }
        }
        else if (cameraState == CameraState.Alert && cameraState != CameraState.Destroyed)
        {
            // ✅ MODIFICADO: La cámara vuelve a Scanning cuando pierde de vista al jugador
            SetCameraState(CameraState.Scanning);
            ChangeState(AIState.Idle); // ✅ También cambiar el estado del AIController
        }

        if (cameraState == CameraState.Alert && cameraConfig.canAlertOtherEnemies)
        {
            if (Time.time - lastAlertTime >= cameraConfig.alertCooldown)
            {
                AlertNearbyEnemies();
                lastAlertTime = Time.time;
            }
        }
    }

     protected override void AlertBehavior()
    {
        // Las cámaras en alerta siguen al jugador con la rotación
        if (fov != null && fov.playerRef != null && cameraFOV != null && cameraFOV.canSeePlayer)
        {
            // Rotar hacia el jugador
            if (pivotPoint != null)
            {
                RotateTransformTowards(pivotPoint, fov.playerRef.transform.position);
            }
        }
    }

     protected override void HandleStateTimers()
    {
        // Las cámaras no tienen timers de alerta por daño
        // Solo manejan timers de scanning
    }

    protected override void HandleStateBehavior()
    {
        switch (currentState)
        {
            case AIState.Alert:
                AlertBehavior(); // ✅ Usar el comportamiento específico de cámara
                break;
            case AIState.Idle:
                IdleBehavior(); // Scanning normal
                break;
            default:
                base.HandleStateBehavior();
                break;
        }
    }

    protected override void IdleBehavior()
{
    if (cameraState != CameraState.Scanning || isDestroyed) return;

    if (isPaused)
    {
        pauseTimer += Time.deltaTime;
        if (pauseTimer >= cameraConfig.scanPauseTime)
        {
            isPaused = false;
            pauseTimer = 0f;
            rotationDirection *= -1f;
        }
        return;
    }

    // ✅ ROTAR EL PIVOT POINT, NO EL GAMEOBJECT PRINCIPAL
    currentRotation += rotationDirection * enemyConfig.rotationSpeed * Time.deltaTime;
    
    if (Mathf.Abs(currentRotation) >= cameraConfig.cameraRotationAngle / 2f)
    {
        isPaused = true;
        currentRotation = Mathf.Clamp(currentRotation, -cameraConfig.cameraRotationAngle / 2f, cameraConfig.cameraRotationAngle / 2f);
    }

    // ✅ APLICAR ROTACIÓN AL PIVOT POINT
    if (pivotPoint != null)
    {
        pivotPoint.rotation = initialRotation * Quaternion.Euler(0, currentRotation, 0);
    }
    else
    {
        transform.rotation = initialRotation * Quaternion.Euler(0, currentRotation, 0);
    }
}

    private void SetCameraState(CameraState newState)
    {
        if (cameraState == newState) return;
        
        cameraState = newState;
        
        switch (cameraState)
        {
            case CameraState.Alert:
                if (cameraConfig.alertSound != null)
                    PlaySound(cameraConfig.alertSound);
                break;
            case CameraState.Destroyed:
                OnCameraDestroyed();
                break;
        }
    }

    public override void TakeDamage(float damageAmount)
    {
        Debug.Log($"📷 Cámara recibiendo {damageAmount} de daño");
        if (isDestroyed || !cameraConfig.canBeDestroyed) return;

        // ✅ MODIFICADO: Las cámaras NO cambian de estado por recibir daño
        // Solo procesan el daño a la salud
        currentHealth -= damageAmount;

        OnHealthChanged?.Invoke(currentHealth / enemyConfig.maxHealth);

        // Efecto visual de daño (opcional)
        if (cameraRenderer != null)
        {
            StartCoroutine(DamageFlash());
        }

        // Sonido de daño
        if (enemyConfig.hitSound != null)
        {
            PlaySound(enemyConfig.hitSound);
        }

        if (currentHealth <= 0)
        {
            SetCameraState(CameraState.Destroyed);
            ChangeState(AIState.Dead); // ✅ Solo cambia a Dead cuando se destruye
        }
        
        UpdateStateDisplays();
    }

    private IEnumerator DamageFlash()
    {
        if (cameraRenderer == null) yield break;

        Color originalColor = cameraRenderer.material.color;
        cameraRenderer.material.color = Color.red;
        
        yield return new WaitForSeconds(0.1f);
        
        if (cameraRenderer != null)
            cameraRenderer.material.color = originalColor;
    }

    private void OnCameraDestroyed()
    {
        isDestroyed = true;
        
//        Debug.Log("📷 Cámara destruida!");

        if (spotLight != null)
        {
            spotLight.color = Color.gray;
            spotLight.intensity = 0.3f;
        }

        if (laserSight != null)
        {
            laserSight.enabled = false;
        }

        if (fov != null)
        {
            fov.canSeePlayer = false;
        }

        if (cameraConfig.destroyedSound != null)
        {
            PlaySound(cameraConfig.destroyedSound);
        }

        StartCoroutine(DestructionSequence());

        if (EnemyRespawnManager.Instance != null)
        {
            EnemyRespawnManager.Instance.NotifyEnemyDeath(this);
        }
    }

    private IEnumerator DestructionSequence()
    {
        if (cameraRenderer != null)
        {
            for (int i = 0; i < 3; i++)
            {
                cameraRenderer.enabled = false;
                yield return new WaitForSeconds(0.1f);
                cameraRenderer.enabled = true;
                yield return new WaitForSeconds(0.1f);
            }
        }

        if (spotLight != null)
            spotLight.enabled = false;
        
        if (cameraCollider != null)
            cameraCollider.enabled = false;

        ChangeState(AIState.Dead);
    }

     protected override void ChangeState(AIState newState)
    {
        // ✅ PREVENIR que las cámaras entren en estado Chasing
        if (newState == AIState.Chasing)
        {
            Debug.LogWarning("⚠️ Las cámaras no pueden entrar en estado Chasing");
            return;
        }
        
        base.ChangeState(newState);
    }

    private void AlertNearbyEnemies()
    {
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, cameraConfig.alertRadius);
        
        foreach (Collider collider in nearbyEnemies)
        {
            AIController enemy = collider.GetComponent<AIController>();
            if (enemy != null && enemy != this && !enemy.IsDead())
            {
                ForceEnemyDetection(enemy);
            }
        }
    }

    private void ForceEnemyDetection(AIController enemy)
    {
        FieldOfView enemyFOV = enemy.GetComponent<FieldOfView>();
        if (enemyFOV != null && fov != null && fov.playerRef != null)
        {
            enemyFOV.canSeePlayer = true;
            
            if (enemy is SoldierAIController soldier)
            {
                soldier.SetLastKnownPosition(fov.playerRef.transform.position);
            }
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    protected override AIState GetDefaultState()
    {
        return AIState.Idle;
    }

    protected override void ChaseBehavior()
    {
        if (isDestroyed) return;

        if (fov != null && fov.playerRef != null)
        {
            // ✅ ROTAR EL PIVOT POINT HACIA EL JUGADOR
            if (pivotPoint != null)
            {
                RotateTransformTowards(pivotPoint, fov.playerRef.transform.position);
            }
            else
            {
                RotateTowardsTarget(fov.playerRef.transform.position); // ✅ CAMBIADO
            }
        }
    }

    private void RotateTransformTowards(Transform targetTransform, Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - targetTransform.position).normalized;
        direction.y = 0; // Solo rotación horizontal
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            targetTransform.rotation = Quaternion.Slerp(targetTransform.rotation, targetRotation, 
                enemyConfig.rotationSpeed * Time.deltaTime);
        }
    }

    public bool IsCameraDestroyed()
    {
        return isDestroyed;
    }

    public override void Revive()
    {
        base.Revive();
        
        isDestroyed = false;
        cameraState = CameraState.Scanning;
        currentRotation = 0f;
        
        if (cameraRenderer != null)
            cameraRenderer.enabled = true;
        
        if (cameraCollider != null)
            cameraCollider.enabled = true;
        
        if (spotLight != null)
        {
            spotLight.enabled = true;
            spotLight.intensity = 1f;
        }
        
        if (laserSight != null)
            laserSight.enabled = cameraConfig.useLaserSight;
        
        transform.rotation = initialRotation;
    }

   private void SetupFieldOfViewAdapter()
{
    fovAdapter = GetComponent<CameraFieldOfViewAdapter>();
    if (fovAdapter == null)
    {
        fovAdapter = gameObject.AddComponent<CameraFieldOfViewAdapter>();
    }
    
    // Asignar el pivotPoint al adaptador
    if (fovAdapter != null && pivotPoint != null)
    {
        fovAdapter.pivotPoint = pivotPoint;
    }
}

protected override void RotateTowardsTarget(Vector3 targetPosition)
    {
        if (pivotPoint != null)
        {
            RotateTransformTowards(pivotPoint, targetPosition);
        }
        else
        {
            base.RotateTowardsTarget(targetPosition);
        }
    }
}