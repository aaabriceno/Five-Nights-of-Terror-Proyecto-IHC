using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra en la PC las diapositivas del tutorial mientras el jugador las
/// pasa desde la tablet.
///
/// Las imágenes se cargan de `Resources/tutorial/` y se ordenan por nombre,
/// así que agregar, quitar o reordenar diapositivas es cuestión de archivos:
/// no hay que tocar código ni declarar cuántas hay. Nombrarlas con número al
/// principio (`slide_01`, `slide_02`, ...) mantiene el orden correcto.
///
/// La cuenta total se le informa al controlador, que la reenvía a la tablet
/// para que pueda desactivar "Anterior" y "Siguiente" en los extremos — así
/// el número de diapositivas vive en un solo lugar.
/// </summary>
public class PantallaDeTutorial : MonoBehaviour
{
    private const string CarpetaDeDiapositivas = "tutorial";

    [SerializeField] private UnityGameSessionController controlador;

    private GameObject lienzo;
    private RawImage imagen;
    private RawImage vistaCamara;
    private Text avisoCamara;
    private Mediapipe.Unity.Screen pantallaDelJuego;
    private Text aviso;
    private Text estadoPrueba;
    private Texture2D[] diapositivas;
    private int indiceMostrado = -1;

    // Awake y no Start: el controlador puede recibir `tutorial_abrir` apenas
    // arranca la escena, y necesita saber cuántas diapositivas hay para
    // responderle a la tablet con el total correcto.
    private void Awake()
    {
        if (controlador == null) controlador = FindObjectOfType<UnityGameSessionController>();
        CargarDiapositivas();
        ConstruirLienzo();
        lienzo.SetActive(false);
    }

    private void Update()
    {
        if (controlador == null) return;

        if (!controlador.EnTutorial)
        {
            if (lienzo.activeSelf) lienzo.SetActive(false);
            indiceMostrado = -1;
            return;
        }

        if (!lienzo.activeSelf) lienzo.SetActive(true);
        if (controlador.IndiceDeDiapositiva != indiceMostrado) MostrarDiapositiva();
        if (estadoPrueba.gameObject.activeSelf)
        {
            ActualizarVistaCamara();
            ActualizarEstadoDePrueba();
        }
    }

    private void CargarDiapositivas()
    {
        Texture2D[] cargadas = Resources.LoadAll<Texture2D>(CarpetaDeDiapositivas);
        List<Texture2D> ordenadas = new List<Texture2D>(cargadas);
        ordenadas.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        diapositivas = ordenadas.ToArray();

        if (controlador != null) controlador.TotalDeDiapositivas = diapositivas.Length;

        if (diapositivas.Length == 0)
            Debug.LogWarning($"PantallaDeTutorial: no hay imágenes en Resources/{CarpetaDeDiapositivas}/.");
    }

    private void MostrarDiapositiva()
    {
        indiceMostrado = controlador.IndiceDeDiapositiva;

        if (diapositivas.Length == 0)
        {
            imagen.texture = null;
            imagen.color = Color.black;
            aviso.text = $"Faltan las diapositivas en Resources/{CarpetaDeDiapositivas}/";
            return;
        }

        int indice = Mathf.Clamp(indiceMostrado, 0, diapositivas.Length - 1);
        Texture2D textura = diapositivas[indice];
        imagen.texture = textura;
        imagen.color = Color.white;
        aviso.text = string.Empty;
        estadoPrueba.gameObject.SetActive(indice == 2);
        vistaCamara.gameObject.SetActive(indice == 2);
        avisoCamara.gameObject.SetActive(indice == 2);
    }

    private void ActualizarVistaCamara()
    {
        if (pantallaDelJuego == null)
            pantallaDelJuego = FindObjectOfType<Mediapipe.Unity.Screen>(true);

        RawImage original = pantallaDelJuego != null ? pantallaDelJuego.display : null;
        Texture textura = original != null ? original.texture : null;
        vistaCamara.texture = textura;
        vistaCamara.color = textura != null ? Color.white : Color.black;
        avisoCamara.gameObject.SetActive(textura == null);

        if (original == null) return;
        vistaCamara.uvRect = original.uvRect;
        Vector3 giro = original.rectTransform.localEulerAngles;
        vistaCamara.rectTransform.localEulerAngles = giro;
        if (textura != null)
        {
            bool cuartoDeVuelta = Mathf.Abs(Mathf.DeltaAngle(giro.z, 90f)) < 1f ||
                                  Mathf.Abs(Mathf.DeltaAngle(giro.z, 270f)) < 1f;
            float anchoVisible = cuartoDeVuelta ? textura.height : textura.width;
            float altoVisible = cuartoDeVuelta ? textura.width : textura.height;
            float escala = Mathf.Min(700f / anchoVisible, 360f / altoVisible);
            vistaCamara.rectTransform.sizeDelta = new Vector2(
                textura.width * escala, textura.height * escala);
        }
    }

    private void ActualizarEstadoDePrueba()
    {
        if (!controlador.TutorialCamaraDisponible)
        {
            estadoPrueba.text = "DETECTOR NO DISPONIBLE\nRevisa la escena y la cámara";
            estadoPrueba.color = new Color(1f, 0.65f, 0.3f);
            return;
        }

        string mirada = controlador.TutorialMiradaCompletada
            ? "MIRADA VERIFICADA"
            : controlador.TutorialMirandoCamara
                ? "ROSTRO DETECTADO · MANTÉN LA MIRADA"
                : "MIRA A LA PANTALLA Y A LA CÁMARA";
        string tarea = controlador.TutorialTareaCompletada
            ? "CABLES COMPLETADOS"
            : "CONECTA LOS CABLES EN LA TABLET";
        estadoPrueba.text = mirada + "\n" + tarea;
        estadoPrueba.color = controlador.TutorialMiradaCompletada && controlador.TutorialTareaCompletada
            ? new Color(0.45f, 0.95f, 0.65f)
            : Color.white;
    }

    private void ConstruirLienzo()
    {
        lienzo = new GameObject(
            "LienzoDeTutorial",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Orden de capas: jumpscare > resultado > tutorial > espera.
        canvas.sortingOrder = short.MaxValue - 3;

        CanvasScaler escalador = lienzo.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        // Expand garantiza que un rectángulo de 1920x1080 entre completo en
        // cualquier monitor: la diapositiva se ve entera (con franjas negras
        // si la pantalla no es 16:9) en vez de recortarse, y la cámara y el
        // estado de la práctica caen siempre sobre el mismo punto de ella.
        escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        GameObject objetoFondo = new GameObject("FondoNegro", typeof(RectTransform), typeof(Image));
        objetoFondo.transform.SetParent(canvas.transform, false);
        RectTransform rectFondo = objetoFondo.GetComponent<RectTransform>();
        rectFondo.anchorMin = Vector2.zero;
        rectFondo.anchorMax = Vector2.one;
        rectFondo.offsetMin = Vector2.zero;
        rectFondo.offsetMax = Vector2.zero;
        objetoFondo.GetComponent<Image>().color = Color.black;

        GameObject objetoImagen = new GameObject("Diapositiva", typeof(RectTransform), typeof(RawImage));
        objetoImagen.transform.SetParent(canvas.transform, false);
        RectTransform rectImagen = objetoImagen.GetComponent<RectTransform>();
        rectImagen.anchorMin = new Vector2(0.5f, 0.5f);
        rectImagen.anchorMax = new Vector2(0.5f, 0.5f);
        rectImagen.pivot = new Vector2(0.5f, 0.5f);
        rectImagen.sizeDelta = new Vector2(1920f, 1080f);
        imagen = objetoImagen.GetComponent<RawImage>();
        imagen.color = Color.black;

        GameObject objetoCamara = new GameObject("CamaraEnVivo", typeof(RectTransform), typeof(RawImage));
        objetoCamara.transform.SetParent(canvas.transform, false);
        RectTransform rectCamara = objetoCamara.GetComponent<RectTransform>();
        rectCamara.anchorMin = new Vector2(0.5f, 0.5f);
        rectCamara.anchorMax = new Vector2(0.5f, 0.5f);
        rectCamara.pivot = new Vector2(0.5f, 0.5f);
        rectCamara.anchoredPosition = new Vector2(440f, 30f);
        rectCamara.sizeDelta = new Vector2(700f, 360f);
        vistaCamara = objetoCamara.GetComponent<RawImage>();
        vistaCamara.color = Color.black;
        vistaCamara.gameObject.SetActive(false);

        GameObject objetoAvisoCamara = new GameObject("AvisoCamara", typeof(RectTransform), typeof(Text));
        objetoAvisoCamara.transform.SetParent(canvas.transform, false);
        RectTransform rectAvisoCamara = objetoAvisoCamara.GetComponent<RectTransform>();
        rectAvisoCamara.anchorMin = new Vector2(0.5f, 0.5f);
        rectAvisoCamara.anchorMax = new Vector2(0.5f, 0.5f);
        rectAvisoCamara.pivot = new Vector2(0.5f, 0.5f);
        rectAvisoCamara.anchoredPosition = new Vector2(440f, 30f);
        rectAvisoCamara.sizeDelta = new Vector2(680f, 110f);
        avisoCamara = objetoAvisoCamara.GetComponent<Text>();
        avisoCamara.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        avisoCamara.fontSize = 31;
        avisoCamara.alignment = TextAnchor.MiddleCenter;
        avisoCamara.color = Color.white;
        avisoCamara.text = "ESPERANDO SEÑAL DE CÁMARA";
        avisoCamara.gameObject.SetActive(false);

        GameObject objetoAviso = new GameObject("Aviso", typeof(RectTransform), typeof(Text));
        objetoAviso.transform.SetParent(canvas.transform, false);
        RectTransform rectAviso = objetoAviso.GetComponent<RectTransform>();
        rectAviso.anchorMin = new Vector2(0.5f, 0.5f);
        rectAviso.anchorMax = new Vector2(0.5f, 0.5f);
        rectAviso.pivot = new Vector2(0.5f, 0.5f);
        rectAviso.sizeDelta = new Vector2(1400f, 120f);
        aviso = objetoAviso.GetComponent<Text>();
        aviso.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        aviso.fontSize = 32;
        aviso.alignment = TextAnchor.MiddleCenter;
        aviso.color = new Color(0.8f, 0.8f, 0.75f);

        GameObject objetoEstado = new GameObject("EstadoDePrueba", typeof(RectTransform), typeof(Text));
        objetoEstado.transform.SetParent(canvas.transform, false);
        RectTransform rectEstado = objetoEstado.GetComponent<RectTransform>();
        rectEstado.anchorMin = new Vector2(0.5f, 0.5f);
        rectEstado.anchorMax = new Vector2(0.5f, 0.5f);
        rectEstado.pivot = new Vector2(0.5f, 0.5f);
        rectEstado.anchoredPosition = new Vector2(440f, -345f);
        rectEstado.sizeDelta = new Vector2(920f, 150f);
        estadoPrueba = objetoEstado.GetComponent<Text>();
        estadoPrueba.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        estadoPrueba.fontSize = 30;
        estadoPrueba.alignment = TextAnchor.MiddleCenter;
        estadoPrueba.color = Color.white;
        estadoPrueba.gameObject.SetActive(false);
    }
}
