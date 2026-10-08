using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra en la PC cómo terminó la noche: superada, perdida, o el turno
/// completo si se sobrevivieron las seis.
///
/// Permanece a la vista hasta que la tablet avisa que volvió a su menú (el
/// mensaje `disconnect`), no por temporizador. El jugador decide en la
/// tablet, y hasta que decida la pantalla grande sigue contando el final —
/// si se fuera sola quedaría "Esperando..." como si nada hubiera pasado.
///
/// En la derrota espera a que el jumpscare termine antes de aparecer, para
/// no taparlo.
///
/// El fondo admite una imagen por resultado colocada en Resources; si no
/// está, queda negro y el texto se lee igual.
/// </summary>
public class PantallaDeResultado : MonoBehaviour
{
    private const string RutaImagenDerrota = "pantalla_derrota";
    private const string RutaImagenVictoria = "pantalla_victoria";
    private const string RutaImagenTurnoCompleto = "pantalla_turno_completo";

    [SerializeField] private UnityGameSessionController controlador;

    private GameObject lienzo;
    private Text titulo;
    private Text subtitulo;
    private RawImage fondo;
    private string resultadoMostrado = string.Empty;

    private void Start()
    {
        if (controlador == null) controlador = FindObjectOfType<UnityGameSessionController>();
        ConstruirLienzo();
        lienzo.SetActive(false);
    }

    private void Update()
    {
        if (controlador == null) return;

        string resultado = controlador.ResultadoDeLaUltimaPartida;
        if (string.IsNullOrEmpty(resultado))
        {
            if (lienzo.activeSelf) lienzo.SetActive(false);
            resultadoMostrado = string.Empty;
            return;
        }

        // La derrota comparte pantalla con el jumpscare: primero el susto,
        // después el resultado.
        if (resultado == "loss" && Time.time < controlador.MomentoEnQueTerminaElJumpscare) return;

        if (resultadoMostrado != resultado)
        {
            Mostrar(resultado, controlador.NocheDeLaUltimaPartida);
            resultadoMostrado = resultado;
        }
    }

    private void Mostrar(string resultado, int noche)
    {
        switch (resultado)
        {
            case "win":
                titulo.text = "6:00 AM";
                subtitulo.text = $"Noche {noche} completada";
                AplicarFondo(RutaImagenVictoria);
                break;
            case "final_victory":
                titulo.text = "TURNO COMPLETADO";
                subtitulo.text = "Sobreviviste las seis noches";
                AplicarFondo(RutaImagenTurnoCompleto);
                break;
            default:
                titulo.text = $"NOCHE {noche}";
                subtitulo.text = "No sobreviviste";
                AplicarFondo(RutaImagenDerrota);
                break;
        }
        lienzo.SetActive(true);
    }

    private void AplicarFondo(string ruta)
    {
        Texture2D imagen = Resources.Load<Texture2D>(ruta);
        if (imagen != null)
        {
            fondo.texture = imagen;
            fondo.color = Color.white;
            fondo.uvRect = CalcularRecorte(imagen);
        }
        else
        {
            fondo.texture = null;
            fondo.color = Color.black;
        }
    }

    /// Rectángulo UV que llena la pantalla sin deformar la imagen, para que
    /// sirva en 16:9, 16:10 o la proporción que tenga el monitor.
    private Rect CalcularRecorte(Texture2D imagen)
    {
        float proporcionPantalla = (float)Screen.width / Screen.height;
        float proporcionImagen = (float)imagen.width / imagen.height;

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
            "LienzoDePantallaDeResultado",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Orden de capas: jumpscare (short.MaxValue) > resultado > espera.
        canvas.sortingOrder = short.MaxValue - 2;

        CanvasScaler escalador = lienzo.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);

        fondo = CrearFondo(canvas.transform);
        titulo = CrearTexto(canvas.transform, "Titulo", 110, new Vector2(0f, 60f),
            new Color(0.93f, 0.91f, 0.85f));
        subtitulo = CrearTexto(canvas.transform, "Subtitulo", 44, new Vector2(0f, -60f),
            new Color(0.72f, 0.70f, 0.64f));
    }

    private RawImage CrearFondo(Transform padre)
    {
        GameObject objeto = new GameObject("Fondo", typeof(RectTransform), typeof(RawImage));
        objeto.transform.SetParent(padre, false);

        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage imagen = objeto.GetComponent<RawImage>();
        imagen.color = Color.black;
        return imagen;
    }

    private Text CrearTexto(Transform padre, string nombre, int tamano, Vector2 posicion, Color color)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        objeto.transform.SetParent(padre, false);

        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = new Vector2(1400f, 160f);

        Text texto = objeto.GetComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = tamano;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = color;
        return texto;
    }
}
