using UnityEngine;
using System.Collections.Generic;

public class DynamicPathfinding : MonoBehaviour
{
    [Header("Pathfinding Settings")]
    public float nodeSpacing = 2f;
    public int maxNodes = 10;
    public float obstacleCheckRadius = 0.5f;
    public LayerMask obstacleMask;
    
    private List<Vector3> pathNodes = new List<Vector3>();
    private int currentNodeIndex = 0;
    private bool isCalculatingPath = false;

    public bool CalculatePath(Vector3 start, Vector3 target)
    {
        pathNodes.Clear();
        currentNodeIndex = 0;
        
        // Dirección directa al objetivo
        Vector3 directDirection = (target - start).normalized;
        float directDistance = Vector3.Distance(start, target);
        
        // Verificar si el camino directo está despejado
        if (!Physics.Raycast(start, directDirection, directDistance, obstacleMask))
        {
            pathNodes.Add(target);
            return true;
        }
        
        // Si está bloqueado, buscar rutas alternativas
        return FindAlternativePath(start, target);
    }

    private bool FindAlternativePath(Vector3 start, Vector3 target)
    {
        Vector3[] testDirections = {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            new Vector3(1, 0, 1), new Vector3(-1, 0, 1), 
            new Vector3(1, 0, -1), new Vector3(-1, 0, -1)
        };

        Vector3 bestNode = Vector3.zero;
        float bestScore = -Mathf.Infinity;

        foreach (Vector3 testDir in testDirections)
        {
            for (float distance = 2f; distance <= 6f; distance += 2f)
            {
                Vector3 candidateNode = start + testDir * distance;
                
                // Verificar si el nodo es válido
                if (IsNodeValid(candidateNode) && 
                    !Physics.Linecast(start, candidateNode, obstacleMask))
                {
                    float score = EvaluateNodeScore(candidateNode, start, target);
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestNode = candidateNode;
                    }
                }
            }
        }

        if (bestScore > -Mathf.Infinity)
        {
            pathNodes.Add(bestNode);
            pathNodes.Add(target);
            return true;
        }

        return false;
    }

    private float EvaluateNodeScore(Vector3 node, Vector3 start, Vector3 target)
    {
        float score = 0f;
        
        // Bonus por estar más cerca del objetivo
        score += 10f / Vector3.Distance(node, target);
        
        // Bonus por no tener obstáculos hacia el objetivo desde este nodo
        if (!Physics.Raycast(node, (target - node).normalized, Vector3.Distance(node, target), obstacleMask))
        {
            score += 20f;
        }
        
        // Penalización por distancia desde el inicio
        score -= Vector3.Distance(start, node) * 0.5f;
        
        return score;
    }

    private bool IsNodeValid(Vector3 node)
    {
        // Verificar que el nodo no esté dentro de un obstáculo
        return !Physics.CheckSphere(node, obstacleCheckRadius, obstacleMask);
    }

    public Vector3 GetNextNode()
    {
        if (pathNodes.Count == 0 || currentNodeIndex >= pathNodes.Count)
            return Vector3.zero;
            
        return pathNodes[currentNodeIndex];
    }

    public void AdvanceToNextNode()
    {
        currentNodeIndex++;
    }

    public bool HasReachedNode(Vector3 currentPosition, float arrivalDistance = 1f)
    {
        if (pathNodes.Count == 0 || currentNodeIndex >= pathNodes.Count)
            return false;
            
        return Vector3.Distance(currentPosition, pathNodes[currentNodeIndex]) < arrivalDistance;
    }

    public bool HasPath()
    {
        return pathNodes.Count > 0 && currentNodeIndex < pathNodes.Count;
    }

    public void ClearPath()
    {
        pathNodes.Clear();
        currentNodeIndex = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (pathNodes.Count > 0)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < pathNodes.Count - 1; i++)
            {
                Gizmos.DrawLine(pathNodes[i], pathNodes[i + 1]);
                Gizmos.DrawSphere(pathNodes[i], 0.3f);
            }
            Gizmos.DrawSphere(pathNodes[pathNodes.Count - 1], 0.3f);
        }
    }
}
