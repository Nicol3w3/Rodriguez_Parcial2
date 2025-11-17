using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Audio;
using System.Collections;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pauseMenuPanel;
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
    private InputAction pauseAction;

    private void Awake()
    {
        InitializePauseSystem();
    }

    private void InitializePauseSystem()
    {
        try
        {
            // ✅ CREAR ACCIÓN DE PAUSA INDEPENDIENTE
            pauseAction = new InputAction(
                name: "PauseAction",
                type: InputActionType.Button,
                binding: "<Keyboard>/escape"
            );
            
            pauseAction.performed += OnPauseInput;
            pauseAction.Enable();
            
//            Debug.Log("✅ Acción de pausa creada correctamente");
            
            // Configurar botones
            if (resumeButton != null)
                resumeButton.onClick.AddListener(ResumeGame);
            else
                Debug.LogWarning("⚠️ ResumeButton no asignado");
                
            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
            else
                Debug.LogWarning("⚠️ QuitButton no asignado");

        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error inicializando sistema de pausa: {e.Message}");
        }
    }

    private void Start()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
        else
            Debug.LogWarning("⚠️ PauseMenuPanel no asignado");
            
        SetCursorState(false);
    }

    private void OnPauseInput(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        
        // ✅ USAR CORRUTINA PARA MANEJAR EL INPUT
        StartCoroutine(HandlePauseInputCoroutine());
    }

    private IEnumerator HandlePauseInputCoroutine()
    {
        // Pequeño delay para procesamiento seguro
        yield return null;
        
        if (this != null && isActiveAndEnabled)
        {
            TogglePause();
        }
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
        
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);
        
        Time.timeScale = 0f;
        SetCursorState(true);
        SetAudioVolume(pausedVolume);
        
//        Debug.Log("⏸️ Juego en pausa");
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        
        isPaused = false;
        
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
        
        Time.timeScale = 1f;
        SetCursorState(false);
        SetAudioVolume(normalVolume);
        
//        Debug.Log("▶️ Juego reanudado");
    }

    public void QuitGame()
    {
//        Debug.Log("🚪 Saliendo del juego...");
        CleanupBeforeExit();
        
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
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Error durante limpieza: {e.Message}");
        }
    }

    public bool IsGamePaused() => isPaused;

    private void OnDestroy()
    {
        CleanupBeforeExit();
    }

    private void OnApplicationQuit()
    {
        CleanupBeforeExit();
    }
}