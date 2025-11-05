using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FieldOfView))]
public class FieldOfViewEditor : Editor
{
    private void OnSceneGUI()
    {
        FieldOfView fov = (FieldOfView)target;
        if (fov == null) return;

        // Dibujar círculo completo del radio
        Handles.color = Color.white;
        Handles.DrawWireDisc(fov.transform.position, Vector3.up, fov.radius);

        // Calcular direcciones de los ángulos
        float facingAngle = fov.transform.eulerAngles.y;
        Vector3 viewAngleA = DirFromAngle(facingAngle - fov.angle / 2);
        Vector3 viewAngleB = DirFromAngle(facingAngle + fov.angle / 2);

        // Dibujar líneas de los límites del FOV
        Handles.color = Color.yellow;
        Handles.DrawLine(fov.transform.position, fov.transform.position + viewAngleA * fov.radius);
        Handles.DrawLine(fov.transform.position, fov.transform.position + viewAngleB * fov.radius);

        // Dibujar arco del FOV
        Handles.color = new Color(1, 1, 0, 0.1f);
        Handles.DrawSolidArc(
            fov.transform.position, 
            Vector3.up, 
            viewAngleA, 
            fov.angle, 
            fov.radius
        );

        // Línea al jugador si es visible
        if (fov.canSeePlayer && fov.playerRef != null)
        {
            Handles.color = Color.green;
            Handles.DrawLine(fov.transform.position, fov.playerRef.transform.position, 2f);
        }

        // Forzar repintado de la escena
        SceneView.RepaintAll();
    }

    private Vector3 DirFromAngle(float angleInDegrees)
    {
        return new Vector3(
            Mathf.Sin(angleInDegrees * Mathf.Deg2Rad),
            0,
            Mathf.Cos(angleInDegrees * Mathf.Deg2Rad)
        );
    }
}