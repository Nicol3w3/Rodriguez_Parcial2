using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FixEnemyPhysics : MonoBehaviour
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
        
        // Configuración corregida
        rb.mass = desiredMass;
        rb.linearDamping = desiredDrag;
        rb.angularDamping = 8f;
        rb.useGravity = true;
        rb.isKinematic = false; // ¡IMPORTANTE!
        
        // Congelar rotaciones para evitar tambaleo
        rb.constraints = RigidbodyConstraints.FreezeRotationX | 
                        RigidbodyConstraints.FreezeRotationZ;
        
        // Configuración de colisión
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
//        Debug.Log("Física del enemigo corregida", gameObject);
    }

    // Método para aplicar knockback controlado
    public void ApplyControlledKnockback(Vector3 force, float upwardReduction = 0.3f)
    {
        Vector3 controlledForce = new Vector3(
            force.x,
            force.y * upwardReduction, // Reducir fuerza vertical
            force.z
        );
        
        rb.AddForce(controlledForce, ForceMode.Impulse);
    }

    // Limitar velocidad máxima para evitar movimiento excesivo
    void FixedUpdate()
    {
        if (rb.linearVelocity.magnitude > 15f)
        {
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, 15f);
        }
    }
}