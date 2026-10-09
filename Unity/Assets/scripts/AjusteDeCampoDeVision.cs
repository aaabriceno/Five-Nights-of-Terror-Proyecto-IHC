using UnityEngine;

/// <summary>
/// Mantiene en la cámara principal el ancho de visión de una pantalla 16:9.
///
/// Unity conserva el campo de visión vertical y deja que el horizontal
/// dependa de la proporción del monitor: en 16:10 o 4:3 se ve menos a los
/// costados, y lo que la escena tiene en los bordes (como el panel de la
/// cámara, que es un Canvas en World Space) queda cortado. En pantallas más
/// angostas que 16:9 este script abre el campo vertical lo justo para que el
/// horizontal sea el mismo que en 16:9: se ve igual de ancho y un poco más
/// de techo y piso. En pantallas más anchas no cambia nada.
///
/// Se agrega solo a la cámara principal al cargar la escena, así que no hace
/// falta tocar la escena.
/// </summary>
[RequireComponent(typeof(Camera))]
public class AjusteDeCampoDeVision : MonoBehaviour
{
    private const float ProporcionDeDiseno = 16f / 9f;

    private Camera camara;
    private float campoVerticalDeDiseno;
    private float campoHorizontalDeDiseno;
    private float proporcionAplicada;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AgregarALaCamaraPrincipal()
    {
        Camera principal = Camera.main;
        if (principal != null && principal.GetComponent<AjusteDeCampoDeVision>() == null)
            principal.gameObject.AddComponent<AjusteDeCampoDeVision>();
    }

    private void Awake()
    {
        camara = GetComponent<Camera>();
        campoVerticalDeDiseno = camara.fieldOfView;
        campoHorizontalDeDiseno = Camera.VerticalToHorizontalFieldOfView(
            campoVerticalDeDiseno, ProporcionDeDiseno);
    }

    // Se revisa cada cuadro porque la ventana puede cambiar de tamaño.
    private void LateUpdate()
    {
        if (Mathf.Approximately(camara.aspect, proporcionAplicada)) return;
        proporcionAplicada = camara.aspect;

        camara.fieldOfView = camara.aspect < ProporcionDeDiseno
            ? Camera.HorizontalToVerticalFieldOfView(campoHorizontalDeDiseno, camara.aspect)
            : campoVerticalDeDiseno;
    }
}
