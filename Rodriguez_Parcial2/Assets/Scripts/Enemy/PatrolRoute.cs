using UnityEngine;
using System.Collections.Generic;

public class PatrolRoute : MonoBehaviour
{
    [Header("Patrol Route Configuration")]
    public List<Transform> waypoints = new List<Transform>();
    public float patrolWaitTime = 2f;
    public bool loopPatrol = true;
    
    [Header("Vector Calculations")]
    public float totalRouteDistance = 0f;
    
    [Header("Gizmos")]
    public Color routeColor = Color.blue;
    public float waypointSize = 0.5f;
    
    public bool HasWaypoints()
    {
        return waypoints != null && waypoints.Count > 0;
    }
    
    public List<Vector3> GetWaypointPositions()
    {
        List<Vector3> positions = new List<Vector3>();
        foreach (Transform waypoint in waypoints)
        {
            if (waypoint != null)
                positions.Add(waypoint.position);
        }
        return positions;
    }
    
    // ✅ NUEVO: Calcular distancia total de la ruta usando operaciones vectoriales
    public float CalculateTotalDistance()
    {
        if (!HasWaypoints()) return 0f;
        
        totalRouteDistance = 0f;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i] != null && waypoints[i + 1] != null)
            {
                // ✅ RESTA DE VECTORES + MAGNITUD: Distancia entre waypoints
                totalRouteDistance += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
            }
        }
        
        // ✅ CERRAR EL CÍRCULO si es loop
        if (loopPatrol && waypoints.Count > 1 && waypoints[0] != null && waypoints[waypoints.Count - 1] != null)
        {
            totalRouteDistance += Vector3.Distance(waypoints[waypoints.Count - 1].position, waypoints[0].position);
        }
        
        return totalRouteDistance;
    }
    
    private void OnDrawGizmos()
    {
        if (!HasWaypoints()) return;
        
        Gizmos.color = routeColor;
        
        // Dibujar waypoints y conexiones
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            
            // Waypoint
            Gizmos.DrawSphere(waypoints[i].position, waypointSize);
            
            // ✅ LÍNEA ENTRE VECTORES: Conexión al siguiente waypoint
            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                
                // ✅ MOSTRAR DISTANCIA ENTRE WAYPOINTS
                Vector3 midPoint = (waypoints[i].position + waypoints[i + 1].position) / 2f;
                float distance = Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(midPoint, $"{distance:F1}m");
                #endif
            }
            
            // ✅ CERRAR EL CÍRCULO si es loop
            if (loopPatrol && i == waypoints.Count - 1 && waypoints[0] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
                
                Vector3 midPoint = (waypoints[i].position + waypoints[0].position) / 2f;
                float distance = Vector3.Distance(waypoints[i].position, waypoints[0].position);
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(midPoint, $"{distance:F1}m");
                #endif
            }
        }
        
        // ✅ MOSTRAR DISTANCIA TOTAL
        #if UNITY_EDITOR
        if (waypoints.Count > 0 && waypoints[0] != null)
        {
            CalculateTotalDistance();
            UnityEditor.Handles.Label(waypoints[0].position + Vector3.up * 2f, $"Ruta: {totalRouteDistance:F1}m total");
        }
        #endif
    }
}