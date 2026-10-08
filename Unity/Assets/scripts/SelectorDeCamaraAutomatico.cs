using System;
using System.Collections;
using System.Collections.Generic;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using UnityEngine;

/// <summary>
/// Elige cámara y resolución sin intervención del jugador.
///
/// El sample de MediaPipe toma siempre la primera cámara que lista el
/// sistema y pide la resolución más cercana a 1280px de ancho. Eso falla
/// cuando la primera cámara no sirve o no soporta ese modo: en Linux, Unity
/// captura en YUYV, y muchas webcams ofrecen en ese formato solo hasta
/// 640x480 aunque anuncien 1080p en MJPG. El resultado es una pantalla
/// negra que obliga a configurar la cámara a mano en cada Play — y en un
/// build el jugador ni siquiera debería ver esa pantalla de configuración.
///
/// Este componente prueba las combinaciones reales hasta encontrar una que
/// entregue imagen, y guarda la que funcionó para arrancar directo la
/// próxima vez. Al no depender de nombres de dispositivo fijos, sirve igual
/// en cualquier máquina del equipo y en la que se use para la demo.
/// </summary>
public class SelectorDeCamaraAutomatico : MonoBehaviour
{
    private const string ClaveCamara = "ihc_camara_nombre";
    private const string ClaveAncho = "ihc_camara_ancho";
    private const string ClaveAlto = "ihc_camara_alto";

    /// Frames que se esperan antes de dar una combinación por fallida. La
    /// cámara tarda en entregar el primer cuadro aunque el modo sea válido.
    private const int FramesDeEspera = 45;

    [SerializeField] private Bootstrap bootstrap;
    [Tooltip("Opcional. Solo hace falta si Resources/Bootstrap.prefab no se " +
             "encuentra; es el mismo prefab que usa el runner de MediaPipe.")]
    [SerializeField] private GameObject prefabDeBootstrap;
    [Tooltip("Runner de MediaPipe de la escena (FaceLandmarkerRunner). Se " +
             "mantiene apagado mientras se busca la cámara, porque su Start() " +
             "también arranca la captura y competiría por el dispositivo.")]
    [SerializeField] private MonoBehaviour runnerDeMediaPipe;
    [Tooltip("Si está activo, ignora la cámara guardada y vuelve a probar.")]
    [SerializeField] private bool olvidarCamaraGuardada;

    public bool Termino { get; private set; }

    private void Awake()
    {
        // Apagar en Awake, antes de que corra ningún Start().
        if (runnerDeMediaPipe != null) runnerDeMediaPipe.enabled = false;
    }

    private IEnumerator Start()
    {
        if (bootstrap == null) bootstrap = ObtenerBootstrap();
        if (bootstrap == null)
        {
            Debug.LogError("SelectorDeCamaraAutomatico: no se pudo obtener el Bootstrap " +
                           "de MediaPipe (falta Resources/Bootstrap.prefab).");
            Finalizar();
            yield break;
        }

        yield return new WaitUntil(() => bootstrap.isFinished);

        if (olvidarCamaraGuardada) BorrarCamaraGuardada();

        ImageSource fuente = ImageSourceProvider.ImageSource;
        if (fuente == null || fuente.sourceCandidateNames == null || fuente.sourceCandidateNames.Length == 0)
        {
            Debug.LogError("SelectorDeCamaraAutomatico: el sistema no reporta ninguna cámara.");
            Finalizar();
            yield break;
        }

        yield return BuscarCamaraQueFuncione(fuente);
        Finalizar();
    }

    /// El Bootstrap no está en la escena: `BaseRunner.FindBootstrap()` lo
    /// instancia desde Resources en runtime. Como acá el runner está apagado,
    /// hay que hacer ese mismo trabajo — mismo nombre de objeto y
    /// DontDestroyOnLoad, para que al reactivarse encuentre este y no cree
    /// un segundo.
    private Bootstrap ObtenerBootstrap()
    {
        GameObject objetoExistente = GameObject.Find("Bootstrap");
        if (objetoExistente != null) return objetoExistente.GetComponent<Bootstrap>();

        GameObject prefab = prefabDeBootstrap != null
            ? prefabDeBootstrap
            : Resources.Load<GameObject>("Bootstrap");
        if (prefab == null) return null;

        GameObject creado = Instantiate(prefab);
        creado.name = "Bootstrap";
        DontDestroyOnLoad(creado);
        return creado.GetComponent<Bootstrap>();
    }

    /// Devuelve el control al runner pase lo que pase: si la búsqueda falla,
    /// es preferible que MediaPipe arranque con su comportamiento original
    /// (y falle de forma visible) antes que dejar la escena congelada.
    private void Finalizar()
    {
        Termino = true;
        if (runnerDeMediaPipe != null) runnerDeMediaPipe.enabled = true;
    }

    private IEnumerator BuscarCamaraQueFuncione(ImageSource fuente)
    {
        string[] camaras = fuente.sourceCandidateNames;
        int camaraGuardada = IndiceDeCamaraGuardada(camaras);

        foreach (int indiceCamara in OrdenDeCamaras(camaras.Length, camaraGuardada))
        {
            fuente.SelectSource(indiceCamara);
            ImageSource.ResolutionStruct[] resoluciones = fuente.availableResolutions;
            if (resoluciones == null || resoluciones.Length == 0) continue;

            foreach (int indiceResolucion in OrdenDeResoluciones(resoluciones, indiceCamara == camaraGuardada))
            {
                fuente.SelectResolution(indiceResolucion);
                bool funciona = false;
                yield return ProbarCombinacion(fuente, resultado => funciona = resultado);

                if (!funciona) continue;

                GuardarCamara(camaras[indiceCamara], resoluciones[indiceResolucion]);
                Debug.Log($"Cámara seleccionada: {camaras[indiceCamara]} " +
                          $"a {resoluciones[indiceResolucion].width}x{resoluciones[indiceResolucion].height}");
                // La selección queda aplicada en la fuente; se detiene para
                // que el runner la inicie él mismo, como haría normalmente.
                fuente.Stop();
                yield break;
            }
        }

        Debug.LogError("SelectorDeCamaraAutomatico: ninguna cámara entregó imagen. " +
                       "Revisá que no esté en uso por otra aplicación.");
    }

    /// La cámara que ya funcionó se prueba primero; el resto queda en el
    /// orden en que los reporta el sistema.
    private int[] OrdenDeCamaras(int cantidad, int camaraGuardada)
    {
        var indices = new List<int>();
        if (camaraGuardada >= 0 && camaraGuardada < cantidad) indices.Add(camaraGuardada);
        for (int i = 0; i < cantidad; i++)
            if (i != camaraGuardada) indices.Add(i);
        return indices.ToArray();
    }

    /// Prueba de mayor a menor resolución: la primera que funcione es la
    /// mejor que esa cámara soporta de verdad. Si hay una guardada, se
    /// intenta esa antes que ninguna otra.
    private int[] OrdenDeResoluciones(ImageSource.ResolutionStruct[] resoluciones, bool usarGuardada)
    {
        var indices = new List<int>();
        for (int i = 0; i < resoluciones.Length; i++) indices.Add(i);
        indices.Sort((a, b) => (resoluciones[b].width * resoluciones[b].height)
            .CompareTo(resoluciones[a].width * resoluciones[a].height));

        if (!usarGuardada) return indices.ToArray();

        int anchoGuardado = PlayerPrefs.GetInt(ClaveAncho, 0);
        int altoGuardado = PlayerPrefs.GetInt(ClaveAlto, 0);
        int indiceGuardado = indices.FindIndex(i =>
            resoluciones[i].width == anchoGuardado && resoluciones[i].height == altoGuardado);
        if (indiceGuardado > 0)
        {
            int valor = indices[indiceGuardado];
            indices.RemoveAt(indiceGuardado);
            indices.Insert(0, valor);
        }
        return indices.ToArray();
    }

    /// Arranca la fuente y espera a que entregue una textura con tamaño
    /// real. Una combinación no soportada deja la textura en 16x16 (el
    /// placeholder de Unity) o nunca llega a reproducir.
    private IEnumerator ProbarCombinacion(ImageSource fuente, Action<bool> alTerminar)
    {
        bool fallo = false;
        yield return EjecutarSinRomper(fuente.Play(), () => fallo = true);

        if (fallo)
        {
            alTerminar(false);
            yield break;
        }

        for (int frame = 0; frame < FramesDeEspera; frame++)
        {
            if (fuente.isPlaying && fuente.textureWidth > 16 && fuente.textureHeight > 16)
            {
                alTerminar(true);
                yield break;
            }
            yield return null;
        }

        fuente.Stop();
        alTerminar(false);
    }

    /// Play() lanza excepciones cuando el modo pedido no existe; hay que
    /// atraparlas sin cortar la corrutina para poder seguir probando. C# no
    /// permite `yield` dentro de un try/catch, así que el avance se hace en
    /// un método aparte y acá solo se decide si continuar.
    private IEnumerator EjecutarSinRomper(IEnumerator rutina, Action alFallar)
    {
        while (true)
        {
            bool hayMas = Avanzar(rutina, out object actual, out string error);
            if (error != null)
            {
                Debug.Log($"Combinación de cámara descartada: {error}");
                alFallar();
                yield break;
            }
            if (!hayMas) yield break;
            yield return actual;
        }
    }

    private bool Avanzar(IEnumerator rutina, out object actual, out string error)
    {
        actual = null;
        error = null;
        try
        {
            if (!rutina.MoveNext()) return false;
            actual = rutina.Current;
            return true;
        }
        catch (Exception excepcion)
        {
            error = excepcion.Message;
            return false;
        }
    }

    private int IndiceDeCamaraGuardada(string[] camaras)
    {
        string guardada = PlayerPrefs.GetString(ClaveCamara, string.Empty);
        if (string.IsNullOrEmpty(guardada)) return -1;
        return Array.IndexOf(camaras, guardada);
    }

    private void GuardarCamara(string nombre, ImageSource.ResolutionStruct resolucion)
    {
        PlayerPrefs.SetString(ClaveCamara, nombre);
        PlayerPrefs.SetInt(ClaveAncho, resolucion.width);
        PlayerPrefs.SetInt(ClaveAlto, resolucion.height);
        PlayerPrefs.Save();
    }

    private void BorrarCamaraGuardada()
    {
        PlayerPrefs.DeleteKey(ClaveCamara);
        PlayerPrefs.DeleteKey(ClaveAncho);
        PlayerPrefs.DeleteKey(ClaveAlto);
    }
}
