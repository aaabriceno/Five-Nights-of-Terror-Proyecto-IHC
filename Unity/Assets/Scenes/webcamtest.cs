using UnityEngine;
using UnityEngine.UI;

public class WebcamTest : MonoBehaviour
{
    public RawImage display;
    private WebCamTexture webcamTexture;

    void Start()
    {
        // Detecta y activa la primera cámara web conectada a la PC
        if (WebCamTexture.devices.Length > 0)
        {
            webcamTexture = new WebCamTexture();
            display.texture = webcamTexture;
            webcamTexture.Play();
        }
        else
        {
            Debug.LogError("No se detectó ninguna cámara web conectada.");
        }
    }
}