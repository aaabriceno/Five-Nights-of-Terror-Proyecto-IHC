using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tapa la escena con una imagen de ambiente mientras no hay partida.
///
/// Sin esto, al abrir el ejecutable el jugador ve la escena 3D con los
/// animatrónicos quietos y la cámara encendida, lo que arruina la sorpresa y
/// deja ver que el juego está esperando. La imagen cubre eso y sostiene la
/// atmósfera hasta que la tablet inicie la noche.
///
/// El Canvas se arma por código, igual que el jumpscare de escritorio, para
/// no depender de que alguien lo configure en el Editor ni de referencias
/// que se rompan al cambiar de escena.
/// </summary>
public class PantallaDeEspera : MonoBehaviour
{
    private const string RutaImagen = "pantalla_espera_Juego";

    [SerializeField] private UnityGameSessionController controlador;
    [Tooltip("Imagen de fondo. Si queda vacío se carga desde Resources.")]
    [SerializeField] private Texture2D imagenDeFondo;
    [SerializeField] private string textoDeEspera = "Esperando...";

    private GameObject lienzo;
    private bool visible;

    private void Start()
    {
        if (controlador == null) controlador = FindObjectOfType<UnityGameSessionController>();
        ConstruirLienzo();
        AplicarVisibilidad(true);
    }

    private void Update()
    {
        // La partida la inicia la tablet, así que esto puede cambiar en
        // cualquier momento y hay que seguirlo cuadro a cuadro.
        bool deberiaVerse = controlador == null || !controlador.PartidaActiva;
        if (deberiaVerse != visible) AplicarVisibilidad(deberiaVerse);
    }

    private void AplicarVisibilidad(bool mostrar)
    {
        visible = mostrar;
        if (lienzo != null) lienzo.SetActive(mostrar);
    }

    private void ConstruirLienzo()
    {
        lienzo = new GameObject(
            "LienzoDePantallaDeEspera",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Orden de capas: jumpscare (short.MaxValue) > resultado > espera.
        canvas.sortingOrder = short.MaxValue - 3;

        CanvasScaler escalador = lienzo.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);

        AgregarFondo(canvas.transform);
        AgregarTexto(canvas.transform);
    }

    private void AgregarFondo(Transform padre)
    {
        Texture2D imagen = imagenDeFondo != null
            ? imagenDeFondo
            : Resources.Load<Texture2D>(RutaImagen);

        GameObject objetoFondo = new GameObject("Fondo", typeof(RectTransform), typeof(RawImage));
        objetoFondo.transform.SetParent(padre, false);

        RectTransform rect = objetoFondo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage dibujo = objetoFondo.GetComponent<RawImage>();
        if (imagen != null)
        {
            dibujo.texture = imagen;
            // Recorta el excedente en vez de deformar: la pantalla del
            // jugador puede ser 16:9, 16:10 o cualquier otra proporción, y
            // los bordes de la imagen son oscuridad sin detalle.
            dibujo.uvRect = CalcularRecorte(imagen);
        }
        else
        {
            // Sin imagen la pantalla queda negra, que ya cumple su función.
            dibujo.color = Color.black;
            Debug.LogWarning($"PantallaDeEspera: no se encontró Resources/{RutaImagen}.");
        }
    }

    /// Devuelve el rectángulo UV que llena la pantalla conservando la
    /// proporción de la imagen (equivale a un "cover").
    private Rect CalcularRecorte(Texture2D imagen)
    {
        float proporcionPantalla = (float)Screen.width / Screen.height;
        float proporcionImagen = (float)imagen.width / imagen.height;

        if (Mathf.Approximately(proporcionImagen, proporcionPantalla))
            return new Rect(0f, 0f, 1f, 1f);

        if (proporcionImagen > proporcionPantalla)
        {
            // Imagen más ancha: se recortan los lados.
            float ancho = proporcionPantalla / proporcionImagen;
            return new Rect((1f - ancho) / 2f, 0f, ancho, 1f);
        }

        // Imagen más alta: se recortan arriba y abajo.
        float alto = proporcionImagen / proporcionPantalla;
        return new Rect(0f, (1f - alto) / 2f, 1f, alto);
    }

    private void AgregarTexto(Transform padre)
    {
        GameObject objetoTexto = new GameObject("TextoDeEspera", typeof(RectTransform), typeof(Text));
        objetoTexto.transform.SetParent(padre, false);

        RectTransform rect = objetoTexto.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(60f, 50f);
        rect.sizeDelta = new Vector2(700f, 60f);

        Text texto = objetoTexto.GetComponent<Text>();
        texto.text = textoDeEspera;
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 32;
        texto.alignment = TextAnchor.LowerLeft;
        texto.color = new Color(0.85f, 0.85f, 0.8f, 0.9f);
    }
}
