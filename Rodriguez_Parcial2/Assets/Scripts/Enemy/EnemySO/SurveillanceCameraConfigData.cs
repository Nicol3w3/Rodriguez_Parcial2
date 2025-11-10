using UnityEngine;

[CreateAssetMenu(fileName = "SurveillanceCamera_Config", menuName = "Enemy System/Surveillance Camera Config")]
public class SurveillanceCameraConfigData : EnemyConfigData
{
    [Header("Camera Specific")]
    public float rotationAngle = 90f;
    public float rotationSpeed = 30f;
    public bool rotateClockwise = true;
    public float scanPauseTime = 1f;
    public bool canAlertOtherEnemies = true;
    public float alertRadius = 15f;
    
    [Header("Visual Settings")]
    public Light spotlight;
    public Color neutralColor = Color.yellow;
    public Color alertColor = Color.red;
    public GameObject laserBeam;
}
