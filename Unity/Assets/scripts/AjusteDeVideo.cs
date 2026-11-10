using UnityEngine;

/// <summary>
/// Aplica el perfil de video (ver PerfilDeVideo) al abrir el juego, sin que haya
/// que poner nada en la escena: 1920x1080 a pantalla completa, sin vsync y con
/// un tope de 60 cuadros por segundo.
///
/// El vsync se apaga porque con él los cuadros dependen de la pantalla: en la de
/// 90 Hz de esta laptop el juego llegaría a 90, y el acuerdo es no pasar de 60.
/// </summary>
public static class AjusteDeVideo
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Aplicar()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = PerfilDeVideo.FpsMaximos;

        if (!PerfilDeVideo.LaLineaDeComandosFijaLaPantalla(System.Environment.GetCommandLineArgs()))
            Screen.SetResolution(PerfilDeVideo.Ancho, PerfilDeVideo.Alto, FullScreenMode.ExclusiveFullScreen);

        var objeto = new GameObject("MedidorDeCuadros");
        Object.DontDestroyOnLoad(objeto);
        objeto.AddComponent<MedidorDeCuadros>();
    }
}

/// <summary>
/// Deja en Player.log cuántos cuadros por segundo da el juego (cada 15 s), con la
/// resolución real y la tarjeta que dibuja: así se puede comprobar el rendimiento
/// de cualquier PC sin herramientas externas.
/// </summary>
public class MedidorDeCuadros : MonoBehaviour
{
    private const float SegundosPorMedicion = 15f;
    private float acumulado;
    private int cuadros;

    private System.Collections.IEnumerator Start()
    {
        // Screen.* tarda un momento en reflejar el cambio de resolución pedido al abrir.
        yield return new WaitForSecondsRealtime(2f);
        Debug.Log($"[Video] pantalla {Screen.width}x{Screen.height} ({Screen.fullScreenMode}), dibuja a {Display.main.renderingWidth}x{Display.main.renderingHeight}, " +
                  $"escritorio {Screen.currentResolution.width}x{Screen.currentResolution.height}, tope {Application.targetFrameRate} FPS, tarjeta: {SystemInfo.graphicsDeviceName}");
    }

    private void Update()
    {
        acumulado += Time.unscaledDeltaTime;
        cuadros++;
        if (acumulado < SegundosPorMedicion) return;
        Debug.Log($"[Video] {cuadros / acumulado:F0} FPS medios en {acumulado:F0} s ({Screen.width}x{Screen.height})");
        acumulado = 0f;
        cuadros = 0;
    }
}
