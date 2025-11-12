using UnityEngine;

public class HybridBullet : BulletBase
{
    [Header("Hybrid Bullet Settings")]
    [SerializeField] private float projectileSpeed = 50f;
    [SerializeField] private float maxVisualRange = 30f; // Rango visual variable
    [SerializeField] private bool showTracer = true;
    [SerializeField] private float tracerWidth = 0.05f;
    [SerializeField] private Material tracerMaterial;
    
    [Header("Raycast Settings")]
    [SerializeField] private float raycastRange = 100f; // Rango de detección variable
    [SerializeField] private bool usePreciseHit = true;
    
    private Vector3 shootDirection;
    private Vector3 hitPoint;
    private bool hasRaycastHit;
    private GameObject hitObject;
    private LineRenderer tracerLine;
    private Rigidbody rb;
    private bool isTracerVisible = true;

   public override void Initialize(GameObject bulletOwner, Vector3 position, Vector3 direction, float bulletDamage = -1)
{
    base.Initialize(bulletOwner, position, direction, bulletDamage);
    
    shootDirection = direction.normalized;
    hasRaycastHit = false;
    
    SetupComponents();
    
    // ✅ REALIZAR RAYCAST CON LA DIRECCIÓN EXACTA QUE RECIBIMOS
    PerformRaycastDetection(position, shootDirection);
    
    // ✅ USAR SIEMPRE LA DIRECCIÓN ORIGINAL (que ahora viene corregida)
    LaunchProjectile(shootDirection);
    
//    Debug.Log($"🎯 Bala inicializada - Pos: {position}, Dir: {shootDirection}");
}

    private void SetupComponents()
    {
        // Configurar Rigidbody para el proyectil visual
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Configurar LineRenderer para el tracer
        if (showTracer)
        {
            tracerLine = GetComponent<LineRenderer>();
            if (tracerLine == null) tracerLine = gameObject.AddComponent<LineRenderer>();
            
            tracerLine.startWidth = tracerWidth;
            tracerLine.endWidth = tracerWidth;
            tracerLine.material = tracerMaterial != null ? tracerMaterial : CreateDefaultMaterial();
            tracerLine.positionCount = 2;
            tracerLine.useWorldSpace = true;
        }
    }

    private Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.color = Color.yellow;
        return mat;
    }

    private void PerformRaycastDetection(Vector3 fromPosition, Vector3 direction)
{
    RaycastHit hit;
    
    // ✅ USAR LA POSICIÓN Y DIRECCIÓN EXACTAS QUE RECIBIMOS
    if (Physics.Raycast(fromPosition, direction, out hit, raycastRange, hitLayers | obstacleLayers))
    {
        hasRaycastHit = true;
        hitPoint = hit.point;
        hitObject = hit.collider.gameObject;
        
//        Debug.Log($"🎯 Raycast bala detectó: {hitObject.name} a {hit.distance:F2}m");
        
        // Si es un obstáculo, ajustar el rango visual
        bool isObstacle = obstacleLayers != 0 && ((1 << hitObject.layer) & obstacleLayers) != 0;
        if (isObstacle && hit.distance < maxVisualRange)
        {
            maxVisualRange = hit.distance;
        }
    }
    else
    {
        hasRaycastHit = false;
        hitPoint = fromPosition + direction * raycastRange;
    }
}

    private void LaunchProjectile(Vector3 direction)
{
    if (rb != null)
    {
        rb.linearVelocity = direction * projectileSpeed;
    }

    // Actualizar dirección para el tracer
    shootDirection = direction;

    if (tracerLine != null)
    {
        tracerLine.SetPosition(0, transform.position);
        tracerLine.SetPosition(1, transform.position + direction * 0.1f);
    }
}

    protected override void UpdateBullet()
    {
        UpdateTracer();
        CheckVisualRange();
    }

    private void UpdateTracer()
    {
        if (!showTracer || tracerLine == null) return;
        
        if (isTracerVisible)
        {
            tracerLine.SetPosition(0, transform.position);
            
            // El extremo del tracer apunta hacia el punto de impacto del raycast
            Vector3 tracerEndPoint = hasRaycastHit && usePreciseHit ? 
                Vector3.Lerp(transform.position, hitPoint, 0.1f) : 
                transform.position + shootDirection * 2f;
                
            tracerLine.SetPosition(1, tracerEndPoint);
        }
    }

    private void CheckVisualRange()
    {
        float currentDistance = Vector3.Distance(startPosition, transform.position);
        
        if (currentDistance >= maxVisualRange)
        {
            // Desvanecer tracer al alcanzar el rango máximo visual
            if (showTracer && tracerLine != null)
            {
                FadeOutTracer();
            }
            else
            {
                Deactivate();
            }
        }
    }

    private void FadeOutTracer()
    {
        if (!isTracerVisible) return;
        
        isTracerVisible = false;
        
        // Efecto de desvanecimiento rápido
        LeanTween.value(gameObject, 1f, 0f, 0.1f)
            .setOnUpdate((float alpha) =>
            {
                if (tracerLine != null)
                {
                    Color color = tracerLine.material.color;
                    color.a = alpha;
                    tracerLine.material.color = color;
                }
            })
            .setOnComplete(() =>
            {
                Deactivate();
            });
    }

    private void FixedUpdate()
    {
        if (!isActive) return;
        
        // Rotar el proyectil hacia la dirección del movimiento
        if (rb != null && rb.linearVelocity != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive) return;
        
        ContactPoint contact = collision.GetContact(0);
        
        // Si ya teníamos un hit de raycast, usar esa información para mayor precisión
        Vector3 finalHitPoint = hasRaycastHit && usePreciseHit ? hitPoint : contact.point;
        Vector3 finalHitNormal = contact.normal;
        
        ProcessHit(collision.gameObject, finalHitPoint, finalHitNormal);
    }

   protected override void ProcessHit(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
{
    if (!isActive) return;
    
    // Ignorar al dueño de la bala
    if (hitObject == owner) return;
    
//    Debug.Log($"🔫 HybridBullet impactó: {hitObject.name}");
    
    // ✅ APLICAR DAÑO SI EL OBJETO ESTÁ EN LAS CAPAS DE HIT
    if (CanDamageObject(hitObject))
    {
        ApplyDamage(hitObject, damage, hitPoint);
//        Debug.Log($"✅ Daño aplicado a: {hitObject.name}");
    }
    else
    {
//        Debug.Log($"❌ Objeto no dañable: {hitObject.name}, Layer: {hitObject.layer}");
    }
    
    SpawnImpactEffect(hitPoint, hitNormal);
    OnBulletHit?.Invoke(this, hitObject);
    Deactivate();
}

    public override void Deactivate()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        if (tracerLine != null)
        {
            tracerLine.positionCount = 0;
        }
        
        base.Deactivate();
    }

    // Métodos públicos para configurar rangos en tiempo de ejecución
    public void SetVisualRange(float newRange)
    {
        maxVisualRange = newRange;
    }

    public void SetRaycastRange(float newRange)
    {
        raycastRange = newRange;
    }

    public void SetProjectileSpeed(float newSpeed)
    {
        projectileSpeed = newSpeed;
        if (rb != null && isActive)
        {
            rb.linearVelocity = shootDirection * projectileSpeed;
        }
    }

    // Visualización en el editor
    private void OnDrawGizmosSelected()
    {
        if (!isActive) return;
        
        // Rango visual
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(startPosition, maxVisualRange);
        
        // Rango de raycast
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + shootDirection * raycastRange);
        
        // Punto de impacto del raycast
        if (hasRaycastHit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(hitPoint, 0.1f);
            Gizmos.DrawLine(transform.position, hitPoint);
        }
    }
}
