using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Carga los videos de jumpscare (uno por animatrónico) y los reproduce cuando
/// atacan.
///
/// Los tres se preparan apenas abre el juego: preparar un video toma un
/// instante, y en el momento del ataque no puede haber retraso. Si un video
/// falta o no se puede reproducir, simplemente no está disponible y el
/// controlador muestra el jumpscare de siempre (imagen y sonido).
///
/// Los videos se reproducen sin su propio audio: el sonido del susto lo pone el
/// controlador, igual para todos.
/// </summary>
public class ReproductorDeJumpscare : MonoBehaviour
{
    private const int AnchoDeLaTextura = 1280;
    private const int AltoDeLaTextura = 720;
    // Segundos extra tras el final del video antes de volver a prepararlo.
    private const float MargenAlTerminar = 0.3f;

    private class Clip
    {
        public VideoPlayer reproductor;
        public RenderTexture textura;
        public bool listo;
        public float duracion;
        public bool reproduciendo;
        public float terminaEn;
    }

    private readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();

    private void Start()
    {
        foreach (string animatronico in RutaDeJumpscare.Animatronicos)
            Preparar(animatronico);
    }

    private void Preparar(string animatronico)
    {
        string archivo = RutaDeJumpscare.NombreDeArchivo(animatronico);
        if (archivo == null) return;

        string ruta = Path.Combine(Application.streamingAssetsPath, RutaDeJumpscare.Carpeta, archivo);
        if (!File.Exists(ruta))
        {
            Debug.Log($"ReproductorDeJumpscare: no hay video para {animatronico} ({archivo}); se usará la imagen.");
            return;
        }

        GameObject objeto = new GameObject("Jumpscare_" + animatronico);
        objeto.transform.SetParent(transform, false);

        Clip clip = new Clip
        {
            reproductor = objeto.AddComponent<VideoPlayer>(),
            textura = new RenderTexture(AnchoDeLaTextura, AltoDeLaTextura, 0)
        };
        clip.reproductor.playOnAwake = false;
        clip.reproductor.isLooping = false;
        clip.reproductor.skipOnDrop = true;
        clip.reproductor.audioOutputMode = VideoAudioOutputMode.None;
        clip.reproductor.renderMode = VideoRenderMode.RenderTexture;
        clip.reproductor.targetTexture = clip.textura;
        clip.reproductor.source = VideoSource.Url;
        clip.reproductor.url = ruta;
        clip.reproductor.prepareCompleted += _ =>
        {
            clip.duracion = (float)clip.reproductor.length;
            clip.listo = true;
            Debug.Log($"ReproductorDeJumpscare: {archivo} listo ({clip.duracion:F1} s).");
        };
        clip.reproductor.errorReceived += (_, mensaje) =>
        {
            clip.listo = false;
            Debug.LogWarning($"ReproductorDeJumpscare: no se pudo reproducir {archivo}: {mensaje}");
        };
        clips[animatronico] = clip;
        clip.reproductor.Prepare();
    }

    /// Empieza el video del animatrónico y devuelve la textura donde se dibuja
    /// y cuánto dura. False si no hay video listo (falta, falló o todavía se
    /// está preparando).
    public bool TryReproducir(string animatronico, out RenderTexture textura, out float duracion)
    {
        textura = null;
        duracion = 0f;
        string archivo = RutaDeJumpscare.NombreDeArchivo(animatronico);
        if (archivo == null) return false;

        string clave = animatronico.Trim().ToLowerInvariant();
        if (!clips.TryGetValue(clave, out Clip clip) || !clip.listo)
        {
            Debug.Log($"ReproductorDeJumpscare: no hay video listo para {animatronico}; se usa la imagen.");
            return false;
        }

        clip.reproductor.time = 0;
        clip.reproductor.Play();
        clip.reproduciendo = true;
        clip.duracion = clip.duracion > 0.1f ? clip.duracion : 3f;
        clip.terminaEn = Time.time + clip.duracion + MargenAlTerminar;
        textura = clip.textura;
        duracion = clip.duracion;
        Debug.Log($"ReproductorDeJumpscare: reproduciendo {archivo} ({clip.duracion:F1} s).");
        return true;
    }

    // Tras reproducirse, el video se detiene y se prepara de nuevo para el
    // próximo ataque (el jugador puede morir varias veces en una misma sesión).
    private void Update()
    {
        foreach (Clip clip in clips.Values)
        {
            if (!clip.reproduciendo || Time.time < clip.terminaEn) continue;
            clip.reproduciendo = false;
            clip.listo = false;
            clip.reproductor.Stop();
            clip.reproductor.Prepare();
        }
    }

    private void OnDestroy()
    {
        foreach (Clip clip in clips.Values)
            if (clip.textura != null) clip.textura.Release();
    }

    /// Rectángulo UV que llena la pantalla sin deformar el video (un "cover"),
    /// para monitores 16:10 u otras proporciones.
    public static Rect RecorteParaLaPantalla()
    {
        float proporcionVideo = (float)AnchoDeLaTextura / AltoDeLaTextura;
        float proporcionPantalla = (float)Screen.width / Screen.height;
        if (Mathf.Approximately(proporcionVideo, proporcionPantalla)) return new Rect(0f, 0f, 1f, 1f);
        if (proporcionVideo > proporcionPantalla)
        {
            float ancho = proporcionPantalla / proporcionVideo;
            return new Rect((1f - ancho) / 2f, 0f, ancho, 1f);
        }
        float alto = proporcionVideo / proporcionPantalla;
        return new Rect(0f, (1f - alto) / 2f, 1f, alto);
    }
}
