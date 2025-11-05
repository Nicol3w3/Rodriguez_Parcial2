using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class SwitchCMcamera : MonoBehaviour
{
    [SerializeField]
    private PlayerInput playerInput;

    [SerializeField]
    private int priorityBoost = 2;

    [SerializeField]
    private Canvas thirdPersonCanvas;

    [SerializeField]
    private Canvas aimCanvas;
    private CinemachineCamera virtualCamera;
    private InputAction aimAction;

    private void Start()
    {
//        aimCanvas.enabled = false;
    }
    private void Awake()
    {
        virtualCamera = GetComponent<CinemachineCamera>();
        aimAction = playerInput.actions["Aim"];
    }

    private void OnEnable()
    {
        aimAction.performed += _ => StartAim();
        aimAction.canceled += _ => CancelAim();
    }

    private void OnDisable()
    {
        aimAction.performed -= _ => StartAim();
        aimAction.canceled -= _ => CancelAim();
    }

    private void StartAim()
    {
        virtualCamera.Priority += priorityBoost;
//        aimCanvas.enabled = true;
  //      thirdPersonCanvas.enabled = false;
    }

    private void CancelAim()
    {
        virtualCamera.Priority -= priorityBoost;
//        aimCanvas.enabled = false;
  //      thirdPersonCanvas.enabled = true;
    }
}
