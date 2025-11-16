using UnityEngine;
using System.Collections.Generic;

public class PatrolRoute : MonoBehaviour
{
    [Header("Patrol Route Configuration")]
    public List<Transform> waypoints = new List<Transform>();
    public float patrolWaitTime = 2f;
    public bool loopPatrol = true;
    
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
            
            // Conexión al siguiente waypoint
            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
            
            // Conexión del último al primero si es loop
            if (loopPatrol && i == waypoints.Count - 1 && waypoints[0] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
            }
        }
    }
}