using UnityEngine;

[CreateAssetMenu(fileName = "SurveillanceCamera_Config", menuName = "Enemy System/Surveillance Camera Config")]
public class SurveillanceCameraConfigData : EnemyConfigData
{
    [Header("Camera Rotation Settings")]
    public float cameraRotationAngle = 90f;
    public float cameraRotationSpeed = 30f;
    public bool rotateClockwise = true;
    public float scanPauseTime = 1f;
    
    [Header("Alert System")]
    public bool canAlertOtherEnemies = true;
    public float alertRadius = 15f;
    public float alertCooldown = 5f;
    
    [Header("Visual Settings")]
    public Color neutralColor = Color.yellow;
    public Color alertColor = Color.red;
    public Color searchingColor = new Color(1f, 0.5f, 0f, 1f); // ✅ CORREGIDO: Color naranja (RGB: 255, 127, 0)
    
    [Header("Audio Settings")]
    public AudioClip cameraDetectionSound;
    public AudioClip alertSound; // ✅ AGREGADO
    public AudioClip destroyedSound;
    
    [Header("Metal Gear Solid Features")]
    public bool useLaserSight = true; // ✅ AGREGADO
    public float laserRange = 10f; // ✅ AGREGADO
    public bool canBeDestroyed = true;
    public float destructionTime = 2f;
}
