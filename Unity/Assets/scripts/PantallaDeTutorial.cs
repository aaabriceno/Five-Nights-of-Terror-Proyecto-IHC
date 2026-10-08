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
    private Text aviso;
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
        imagen.uvRect = CalcularRecorte(textura);
        aviso.text = string.Empty;
    }

    /// Rectángulo UV que llena la pantalla sin deformar la imagen, para que
    /// la diapositiva se vea bien en cualquier proporción de monitor.
    private Rect CalcularRecorte(Texture2D textura)
    {
        float proporcionPantalla = (float)Screen.width / Screen.height;
        float proporcionImagen = (float)textura.width / textura.height;

        if (Mathf.Approximately(proporcionImagen, proporcionPantalla))
            return new Rect(0f, 0f, 1f, 1f);

        if (proporcionImagen > proporcionPantalla)
        {
            float ancho = proporcionPantalla / proporcionImagen;
            return new Rect((1f - ancho) / 2f, 0f, ancho, 1f);
        }

        float alto = proporcionImagen / proporcionPantalla;
        return new Rect(0f, (1f - alto) / 2f, 1f, alto);
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

        GameObject objetoImagen = new GameObject("Diapositiva", typeof(RectTransform), typeof(RawImage));
        objetoImagen.transform.SetParent(canvas.transform, false);
        RectTransform rectImagen = objetoImagen.GetComponent<RectTransform>();
        rectImagen.anchorMin = Vector2.zero;
        rectImagen.anchorMax = Vector2.one;
        rectImagen.offsetMin = Vector2.zero;
        rectImagen.offsetMax = Vector2.zero;
        imagen = objetoImagen.GetComponent<RawImage>();
        imagen.color = Color.black;

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
    }
}
