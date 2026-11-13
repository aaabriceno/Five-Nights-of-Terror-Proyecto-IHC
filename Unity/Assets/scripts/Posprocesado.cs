using UnityEngine;

/// <summary>
/// Efectos de imagen de la cámara del juego (pipeline integrado, `OnRenderImage`):
/// viñeta, grano, aberración cromática, color frío y desaturado, bloom (solo en
/// el nivel alto) y la estática de la pantalla que pide AmbienteDelJuego.
/// Solo actúa durante la partida: la pantalla de espera, el tutorial y el
/// resultado se ven como siempre. El shader vive en Resources/shaders para que
/// entre en la exportación; si no carga, la cámara dibuja como antes.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class Posprocesado : MonoBehaviour
{
    public bool bloom = true;
    [Range(0f, 1f)] public float umbralDeBloom = 0.75f;
    [Range(0f, 3f)] public float intensidadDeBloom = 1.1f;
    [Range(0f, 1f)] public float vineta = 0.8f;
    [Range(0f, 0.3f)] public float grano = 0.05f;
    [Range(0f, 0.02f)] public float aberracion = 0.004f;
    [Range(0f, 1f)] public float desaturacion = 0.25f;
    public Color tinte = new Color(0.9f, 0.96f, 1.08f, 1f);

    private Material material;
    private static readonly int Tiempo = Shader.PropertyToID("_Tiempo");
    private static readonly int Estatica = Shader.PropertyToID("_Estatica");
    private static readonly int Umbral = Shader.PropertyToID("_Umbral");
    private static readonly int IntensidadBloom = Shader.PropertyToID("_IntensidadBloom");
    private static readonly int Brillo = Shader.PropertyToID("_Brillo");
    private static readonly int Vineta = Shader.PropertyToID("_Vineta");
    private static readonly int Grano = Shader.PropertyToID("_Grano");
    private static readonly int Aberracion = Shader.PropertyToID("_Aberracion");
    private static readonly int Desaturacion = Shader.PropertyToID("_Desaturacion");
    private static readonly int Tinte = Shader.PropertyToID("_Tinte");

    private void Awake()
    {
        Shader shader = Resources.Load<Shader>("shaders/Posprocesado");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("[Ambiente] No se pudo cargar el shader de posprocesado: la cámara dibuja sin efectos.");
            enabled = false;
            return;
        }
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    private void OnRenderImage(RenderTexture origen, RenderTexture destino)
    {
        if (material == null || !AmbienteDelJuego.PartidaActiva)
        {
            Graphics.Blit(origen, destino);
            return;
        }

        material.SetFloat(Tiempo, Time.unscaledTime);
        material.SetFloat(Estatica, AmbienteDelJuego.NivelDeEstatica(Time.time));
        material.SetFloat(Vineta, vineta);
        material.SetFloat(Grano, grano);
        material.SetFloat(Aberracion, aberracion);
        material.SetFloat(Desaturacion, desaturacion);
        material.SetColor(Tinte, tinte);

        RenderTexture brillo = null;
        if (bloom)
        {
            int ancho = Mathf.Max(1, origen.width / 4);
            int alto = Mathf.Max(1, origen.height / 4);
            brillo = RenderTexture.GetTemporary(ancho, alto, 0, origen.format);
            RenderTexture auxiliar = RenderTexture.GetTemporary(ancho, alto, 0, origen.format);
            material.SetFloat(Umbral, umbralDeBloom);
            Graphics.Blit(origen, brillo, material, 0);
            Graphics.Blit(brillo, auxiliar, material, 1);
            Graphics.Blit(auxiliar, brillo, material, 2);
            RenderTexture.ReleaseTemporary(auxiliar);
            material.SetTexture(Brillo, brillo);
            material.SetFloat(IntensidadBloom, intensidadDeBloom);
        }
        else
        {
            material.SetTexture(Brillo, Texture2D.blackTexture);
            material.SetFloat(IntensidadBloom, 0f);
        }

        Graphics.Blit(origen, destino, material, 3);
        if (brillo != null) RenderTexture.ReleaseTemporary(brillo);
    }
}
