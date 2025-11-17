using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;
using System.Collections;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pauseMenuPanel; // Panel principal
    public GameObject pauseMenuUI; // ✅ NUEVO: Referencia a TODO el UI del menú de pausa
    public Button resumeButton;
    public Button quitButton;
    
    [Header("Audio Settings")]
    public AudioMixer audioMixer;
    public string masterVolumeParameter = "MasterVolume";
    public float pausedVolume = -20f;
    public float normalVolume = 0f;
    
    [Header("Cursor Settings")]
    public bool showCursorOnPause = true;
    public bool hideCursorOnResume = true;
    
    private bool isPaused = false;
    private PlayerInput playerInput;
    private InputAction pauseAction;
    private bool isInitialized = false;

    private static PauseMenu _instance;
    public static PauseMenu Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        
        InitializePauseSystem();
    }

    private void InitializePauseSystem()
    {
        if (isInitialized) return;

        try
        {
            playerInput = FindObjectOfType<PlayerInput>();
            
            if (playerInput != null)
            {
                var actionMap = playerInput.actions.FindActionMap("Player");
                if (actionMap != null)
                {
                    pauseAction = actionMap.FindAction("Pause");
                }
                
                if (pauseAction == null)
                {
                    pauseAction = playerInput.actions.FindAction("Pause");
                }

                if (pauseAction != null)
                {
                    pauseAction.performed -= OnPauseInput;
                    pauseAction.performed += OnPauseInput;
                    pauseAction.Enable();
                }
                else
                {
                    Debug.LogWarning("⚠️ No se encontró la acción 'Pause' en el Input System");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ No se encontró PlayerInput en la escena");
            }
            
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(ResumeGame);
            }
            else
            {
                Debug.LogWarning("⚠️ ResumeButton no asignado en el inspector");
            }
                
            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(QuitGame);
            }
            else
            {
                Debug.LogWarning("⚠️ QuitButton no asignado en el inspector");
            }

            isInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error inicializando sistema de pausa: {e.Message}");
        }
    }

    private void Start()
    {
        // ✅ OCULTAR TODO EL MENÚ AL INICIAR
        SetPauseMenuVisible(false);
    }

    private void OnPauseInput(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        
        if (this == null || !isActiveAndEnabled || !gameObject.activeInHierarchy) return;
        
        TogglePause();
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (isPaused) return;
        
        isPaused = true;
        
        // ✅ MOSTRAR TODO EL MENÚ
        SetPauseMenuVisible(true);
        
        Time.timeScale = 0f;
        SetCursorState(true);
        DisablePlayerInput();
        SetAudioVolume(pausedVolume);
        
//        Debug.Log("⏸️ Juego en pausa");
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        
        isPaused = false;
        
        // ✅ OCULTAR TODO EL MENÚ
        SetPauseMenuVisible(false);
        
        Time.timeScale = 1f;
        SetCursorState(false);
        EnablePlayerInput();
        SetAudioVolume(normalVolume);
        
//        Debug.Log("▶️ Juego reanudado");
    }

    // ✅ NUEVO: Método para mostrar/ocultar todo el menú
    private void SetPauseMenuVisible(bool visible)
    {
        // Opción 1: Si tienes un GameObject padre que contiene TODO el menú
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(visible);
        }
        // Opción 2: Si usas el panel principal y otros elementos por separado
        else if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(visible);
        }
        // Opción 3: Fallback - desactivar este GameObject (si es el menú completo)
        else
        {
            gameObject.SetActive(visible);
            Debug.LogWarning("⚠️ Usando GameObject completo como menú de pausa");
        }
        
        // ✅ ACTIVAR/DESACTIVAR BOTONES POR SEPARADO POR SI ACASO
        if (resumeButton != null)
            resumeButton.gameObject.SetActive(visible);
        if (quitButton != null)
            quitButton.gameObject.SetActive(visible);
    }

    // ... (el resto de los métodos permanecen igual)
    public void QuitGame()
    {
//        Debug.Log("🚪 Saliendo del juego...");
        CleanupBeforeExit();
        Time.timeScale = 1f;
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void SetCursorState(bool paused)
    {
        try
        {
            if (paused && showCursorOnPause)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else if (!paused && hideCursorOnResume)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Error configurando cursor: {e.Message}");
        }
    }

    private void SetAudioVolume(float volume)
    {
        try
        {
            if (audioMixer != null && !string.IsNullOrEmpty(masterVolumeParameter))
            {
                audioMixer.SetFloat(masterVolumeParameter, volume);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Error configurando audio: {e.Message}");
        }
    }

    private void DisablePlayerInput()
    {
        try
        {
            if (playerInput != null)
                playerInput.enabled = false;
                
            TPMovement_Controller playerController = FindObjectOfType<TPMovement_Controller>();
            if (playerController != null && playerController.isActiveAndEnabled)
            {
                playerController.SetCanShoot(false);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Error deshabilitando input del jugador: {e.Message}");
        }
    }

    private void EnablePlayerInput()
    {
        try
        {
            if (playerInput != null)
                playerInput.enabled = true;
                
            TPMovement_Controller playerController = FindObjectOfType<TPMovement_Controller>();
            if (playerController != null && playerController.isActiveAndEnabled)
            {
                playerController.SetCanShoot(true);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Error habilitando input del jugador: {e.Message}");
        }
    }

    private void CleanupBeforeExit()
    {
        try
        {
            Time.timeScale = 1f;
            SetCursorState(false);
            SetAudioVolume(normalVolume);
            
            if (pauseAction != null)
            {
                pauseAction.performed -= OnPauseInput;
                pauseAction.Disable();
                pauseAction = null;
            }
            
            if (resumeButton != null)
                resumeButton.onClick.RemoveAllListeners();
                
            if (quitButton != null)
                quitButton.onClick.RemoveAllListeners();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Error durante limpieza: {e.Message}");
        }
    }

    public bool IsGamePaused() => isPaused;

    public static bool IsPaused()
    {
        return _instance != null && _instance.isPaused;
    }

    private void OnDestroy()
    {
        CleanupBeforeExit();
        _instance = null;
    }

    private void OnApplicationQuit()
    {
        CleanupBeforeExit();
    }
}
