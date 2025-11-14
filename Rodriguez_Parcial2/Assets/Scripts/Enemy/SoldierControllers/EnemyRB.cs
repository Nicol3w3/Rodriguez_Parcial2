using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyRB : MonoBehaviour
{
    [Header("Configuración para evitar tambaleo")]
    public bool fixPhysicsOnStart = true;
    public float desiredMass = 80f;
    public float desiredDrag = 8f;
    
    private Rigidbody rb;

    void Start()
    {
        if (fixPhysicsOnStart)
        {
            FixPhysics();
        }
    }

    [ContextMenu("Corregir Física")]
    public void FixPhysics()
    {
        rb = GetComponent<Rigidbody>();
        
        // Configuración corregida para Unity 6
        rb.mass = desiredMass;
        rb.linearDamping = desiredDrag;
        rb.angularDamping = 8f;
        rb.useGravity = true;
        rb.isKinematic = false;
        
        // Congelar rotaciones para evitar tambaleo
        rb.freezeRotation = true; // Nueva forma en Unity 6
        
        // Configuración de colisión
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
//        Debug.Log("✅ Física del enemigo corregida: " + gameObject.name);
    }

    void FixedUpdate()
    {
        // Limitar velocidad máxima para evitar movimiento excesivo
        if (rb.linearVelocity.magnitude > 15f)
        {
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, 15f);
        }
        
        // Fuerza anti-caída - mantener en altura mínima
        if (transform.position.y < 0.5f)
        {
            Vector3 pos = transform.position;
            pos.y = 1.0f;
            rb.MovePosition(pos);
            
            // Resetear velocidad vertical
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0;
            rb.linearVelocity = velocity;
        }
    }
}