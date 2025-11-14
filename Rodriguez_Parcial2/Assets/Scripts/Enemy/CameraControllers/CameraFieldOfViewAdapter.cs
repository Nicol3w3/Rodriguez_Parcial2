using UnityEngine;

public class CameraFieldOfViewAdapter : MonoBehaviour
{
    [Header("Camera References")]
    public Transform pivotPoint; // El transform que realmente rota
    private FieldOfView fovComponent;
    
    private void Start()
    {
        if (fovComponent == null)
            fovComponent = GetComponent<FieldOfView>();
            
        if (pivotPoint == null)
        {
            // Buscar automáticamente el pivot point
            pivotPoint = transform.Find("PivotPoint");
            if (pivotPoint == null)
            {
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
        
        if (fovComponent == null)
        {
            Debug.LogError("CameraFieldOfViewAdapter: No se encontró FieldOfView component");
            return;
        }
        
        if (pivotPoint == null)
        {
            Debug.LogWarning("CameraFieldOfViewAdapter: No se encontró pivotPoint, usando transform principal");
            pivotPoint = transform;
        }
        
        Debug.Log($"✅ CameraFieldOfViewAdapter configurado - Pivot: {pivotPoint.name}");
    }
    
    // Métodos públicos para obtener la dirección correcta
    public Vector3 GetCameraForward()
    {
        return pivotPoint != null ? pivotPoint.forward : transform.forward;
    }
    
    public Vector3 GetCameraPosition()
    {
        return pivotPoint != null ? pivotPoint.position : transform.position;
    }
    
    public Vector3 GetCameraRight()
    {
        return pivotPoint != null ? pivotPoint.right : transform.right;
    }
}
