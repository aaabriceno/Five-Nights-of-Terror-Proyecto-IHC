using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Arma el ambiente del juego al cargar la escena, por código y sin tocar el
/// archivo `.unity` que comparte el equipo:
///
/// * cielo negro (tapa el hueco sobre los ductos), ambiente oscuro y niebla
///   exponencial que se traga los pasillos;
/// * posprocesado en la cámara (ver Posprocesado) y sombras de la luz de la
///   oficina solo en el nivel alto (PerfilDeVideo.NivelGrafico);
/// * ojos que brillan, balanceo procedural y pasos posicionales en los tres
///   animatrónicos;
/// * luces que reaccionan: la oficina parpadea y se apaga a ratos cuando Freddy
///   se acerca, una luz roja pulsa cuando la caja de Puppet está en peligro, las
///   luces de alerta de los ductos titilan cuando alguien entra, y cada noche hay
///   menos luz y más parpadeo (AmbienteDelJuego);
/// * polvo flotando en la luz de la oficina.
///
/// Con `-sin-ambiente` no hace nada (para comparar o para las pruebas viejas).
/// </summary>
public sealed class AjusteDeAmbiente : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Arrancar()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sin-ambiente") >= 0)
        {
            Debug.Log("[Ambiente] Desactivado por -sin-ambiente.");
            return;
        }
        new GameObject("AjusteDeAmbiente").AddComponent<AjusteDeAmbiente>();
    }

    private static readonly Color ColorDeNiebla = new Color(0.012f, 0.012f, 0.016f);
    private const float FactorDeLuzDireccional = 0.35f;

    /// -prueba-estatica: la estática queda encendida (para fotografiarla).
    private static readonly bool PruebaEstatica = Array.IndexOf(Environment.GetCommandLineArgs(), "-prueba-estatica") >= 0;

    private PerfilDeVideo.Nivel nivel;
    private Posprocesado posprocesado;
    private readonly Dictionary<Light, float> intensidadBase = new Dictionary<Light, float>();
    private readonly Dictionary<Light, float> parpadeoHasta = new Dictionary<Light, float>();
    private readonly List<Light> alertas = new List<Light>();
    private readonly List<Light> demas = new List<Light>();
    private readonly List<Transform> animatronicos = new List<Transform>();
    private Light luzDeOficina;
    private Light luzDireccional;
    private Light luzDeEmergencia;
    private Transform jugador;
    private Transform freddy;
    private int nocheAplicada = -1;

    private void Start()
    {
        nivel = PerfilDeVideo.NivelGrafico(SystemInfo.graphicsDeviceName, Environment.GetCommandLineArgs());

        Camera camara = Camera.main;
        if (camara != null)
        {
            jugador = camara.transform;
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = Color.black;
            posprocesado = camara.GetComponent<Posprocesado>() ?? camara.gameObject.AddComponent<Posprocesado>();
            posprocesado.bloom = nivel == PerfilDeVideo.Nivel.Alto;
            if (camara.GetComponent<AudioListener>() == null) camara.gameObject.AddComponent<AudioListener>();
        }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.09f, 0.085f, 0.08f);
        RenderSettings.ambientEquatorColor = new Color(0.05f, 0.05f, 0.055f);
        RenderSettings.ambientGroundColor = new Color(0.015f, 0.014f, 0.012f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = ColorDeNiebla;

        ClasificarLuces();
        PrepararAnimatronicos();
        CrearLuzDeEmergencia();
        CrearPolvo();
        AplicarNoche(AmbienteDelJuego.Noche);

        Debug.Log($"[Ambiente] nivel {nivel}, {intensidadBase.Count} luces (oficina: {(luzDeOficina != null ? luzDeOficina.name : "ninguna")}, " +
                  $"alertas: {alertas.Count}), animatrónicos: {animatronicos.Count}, tarjeta: {SystemInfo.graphicsDeviceName}");
    }

    private void ClasificarLuces()
    {
        foreach (Light luz in FindObjectsOfType<Light>(true))
        {
            // Las luces con vida propia (foco intermitente del escenario, la trampa) no se tocan.
            if (luz.GetComponent<FocoEscenarioIntermitente>() != null || luz.GetComponent<TrampaVisible>() != null) continue;
            intensidadBase[luz] = luz.intensity;
            string nombre = luz.name.ToLowerInvariant();
            if (luz.type == LightType.Directional) luzDireccional = luz;
            else if (nombre.Contains("oficina")) luzDeOficina = luz;
            else if (nombre.StartsWith("alerta")) alertas.Add(luz);
            else demas.Add(luz);
        }
        if (luzDeOficina != null && nivel == PerfilDeVideo.Nivel.Alto)
        {
            luzDeOficina.shadows = LightShadows.Soft;
            luzDeOficina.shadowStrength = 0.85f;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 30f);
        }
    }

    private void PrepararAnimatronicos()
    {
        UnityGameSessionController controlador = FindObjectOfType<UnityGameSessionController>();
        if (controlador == null) return;
        var actores = new (string nombre, Color ojos, bool esFreddy)[]
        {
            (controlador.actorFreddy, new Color(1f, 0.25f, 0.15f), true),
            (controlador.actorVixy, new Color(1f, 0.8f, 0.25f), false),
            (controlador.actorPuppet, new Color(0.9f, 0.95f, 1f), false),
        };
        foreach ((string nombre, Color ojos, bool esFreddy) in actores)
        {
            GameObject actor = GameObject.Find(nombre);
            if (actor == null) continue;
            animatronicos.Add(actor.transform);
            if (esFreddy) freddy = actor.transform;
            PonerOjos(actor.transform, ojos);
            if (actor.GetComponent<BalanceoDeAnimatronico>() == null) actor.AddComponent<BalanceoDeAnimatronico>();
            if (actor.GetComponent<SonidoDeAnimatronico>() == null) actor.AddComponent<SonidoDeAnimatronico>();
        }
    }

    /// Dos esferas emisivas a la altura de la cara, calculadas de la caja del
    /// modelo (no hay esqueleto ni material de ojos del que colgarse).
    private void PonerOjos(Transform actor, Color color)
    {
        Renderer[] partes = actor.GetComponentsInChildren<Renderer>();
        if (partes.Length == 0) return;
        Bounds caja = partes[0].bounds;
        foreach (Renderer parte in partes) caja.Encapsulate(parte.bounds);

        // Todo en metros del mundo: el modelo puede venir escalado (un OBJ en
        // centímetros) y mezclar unidades locales con metros manda los ojos a los pies.
        AmbienteDelJuego.Ojos ojos = AmbienteDelJuego.PosicionDeOjos(caja.size.x, caja.size.y, caja.size.z);
        Vector3 piso = new Vector3(caja.center.x, caja.min.y, caja.center.z);
        float diametro = 2f * Mathf.Clamp(caja.size.y * 0.03f, 0.04f, 0.08f);
        Vector3 escala = actor.lossyScale;

        Shader shader = Resources.Load<Shader>("shaders/Emisivo");
        Material material = shader != null ? new Material(shader) : null;
        if (material != null) material.color = color;

        foreach (float lado in new[] { -1f, 1f })
        {
            GameObject ojo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ojo.name = "Ojo";
            Destroy(ojo.GetComponent<Collider>());
            ojo.transform.SetParent(actor, false);
            ojo.transform.position = piso + Vector3.up * ojos.Altura
                + actor.right * (lado * ojos.Separacion / 2f) + actor.forward * ojos.Adelante;
            // La escala local compensa la del modelo para que el ojo mida `diametro` en el mundo.
            ojo.transform.localScale = new Vector3(
                diametro / Mathf.Max(escala.x, 1e-4f), diametro / Mathf.Max(escala.y, 1e-4f), diametro / Mathf.Max(escala.z, 1e-4f));
            Renderer dibujo = ojo.GetComponent<Renderer>();
            if (material != null) dibujo.sharedMaterial = material;
            dibujo.shadowCastingMode = ShadowCastingMode.Off;
            dibujo.receiveShadows = false;
        }
    }

    private void CrearLuzDeEmergencia()
    {
        if (jugador == null) return;
        GameObject objeto = new GameObject("LuzDeEmergencia");
        objeto.transform.SetParent(jugador, false);
        objeto.transform.localPosition = new Vector3(0f, 0.6f, 0.8f);
        luzDeEmergencia = objeto.AddComponent<Light>();
        luzDeEmergencia.type = LightType.Point;
        luzDeEmergencia.color = new Color(1f, 0.1f, 0.05f);
        luzDeEmergencia.range = 7f;
        luzDeEmergencia.intensity = 0f;
        luzDeEmergencia.shadows = LightShadows.None;
        luzDeEmergencia.enabled = false;
    }

    /// Polvo flotando alrededor de la luz de la oficina (o del jugador si no hay).
    private void CrearPolvo()
    {
        Transform ancla = luzDeOficina != null ? luzDeOficina.transform : jugador;
        if (ancla == null) return;
        Shader shader = Resources.Load<Shader>("shaders/Aditivo");
        if (shader == null) return;

        GameObject objeto = new GameObject("Polvo");
        objeto.transform.position = ancla.position + Vector3.down * 0.6f;
        ParticleSystem polvo = objeto.AddComponent<ParticleSystem>();
        polvo.Stop();
        ParticleSystem.MainModule principal = polvo.main;
        principal.loop = true;
        principal.startLifetime = 9f;
        principal.startSpeed = 0.03f;
        principal.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        principal.startColor = new Color(1f, 0.95f, 0.85f, 0.35f);
        principal.maxParticles = 120;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emision = polvo.emission;
        emision.rateOverTime = nivel == PerfilDeVideo.Nivel.Alto ? 9f : 3f;
        ParticleSystem.ShapeModule forma = polvo.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(4f, 2.5f, 4f);
        ParticleSystem.VelocityOverLifetimeModule velocidad = polvo.velocityOverLifetime;
        velocidad.enabled = true;
        velocidad.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        velocidad.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        velocidad.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        ParticleSystem.ColorOverLifetimeModule color = polvo.colorOverLifetime;
        color.enabled = true;
        var degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = degradado;

        ParticleSystemRenderer dibujo = objeto.GetComponent<ParticleSystemRenderer>();
        dibujo.renderMode = ParticleSystemRenderMode.Billboard;
        dibujo.material = new Material(shader) { mainTexture = TexturaSuave() };
        dibujo.shadowCastingMode = ShadowCastingMode.Off;
        polvo.Play();
    }

    /// Un círculo blanco que se desvanece hacia el borde (32x32).
    private static Texture2D TexturaSuave()
    {
        const int lado = 32;
        var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float dx = (x + 0.5f) / lado - 0.5f, dy = (y + 0.5f) / lado - 0.5f;
                float alfa = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                textura.SetPixel(x, y, new Color(1f, 1f, 1f, alfa * alfa));
            }
        textura.Apply();
        return textura;
    }

    private void AplicarNoche(int noche)
    {
        nocheAplicada = noche;
        AmbienteDelJuego.ParametrosDeNoche parametros = AmbienteDelJuego.ParametrosDeLaNoche(noche);
        RenderSettings.fogDensity = parametros.DensidadDeNiebla;
        if (luzDireccional != null)
            luzDireccional.intensity = intensidadBase[luzDireccional] * FactorDeLuzDireccional * parametros.FactorDeLuz;
    }

    private void Update()
    {
        if (AmbienteDelJuego.Noche != nocheAplicada) AplicarNoche(AmbienteDelJuego.Noche);
        AmbienteDelJuego.ParametrosDeNoche parametros = AmbienteDelJuego.ParametrosDeLaNoche(nocheAplicada);
        bool partida = AmbienteDelJuego.PartidaActiva;
        float ahora = Time.time;
        if (PruebaEstatica) AmbienteDelJuego.PedirEstatica(ahora - 0.5f, 1f);   // nivel medio, fijo

        if (luzDeOficina != null)
        {
            float distancia = partida && freddy != null && jugador != null ? Vector3.Distance(freddy.position, jugador.position) : 99f;
            luzDeOficina.intensity = intensidadBase[luzDeOficina] * parametros.FactorDeLuz
                * AmbienteDelJuego.LuzDeOficina(distancia, UnityEngine.Random.value, ahora);
        }

        foreach (Light luz in demas)
        {
            if (luz == null) continue;
            float baseDeLuz = intensidadBase[luz] * parametros.FactorDeLuz;
            parpadeoHasta.TryGetValue(luz, out float fin);
            if (ahora < fin)
            {
                luz.intensity = baseDeLuz * (0.15f + 0.6f * UnityEngine.Random.value);
            }
            else
            {
                luz.intensity = baseDeLuz;
                if (partida && UnityEngine.Random.value < parametros.ProbabilidadDeParpadeo * Time.deltaTime)
                    parpadeoHasta[luz] = ahora + UnityEngine.Random.Range(0.08f, 0.35f);
            }
        }

        if (luzDeEmergencia != null)
        {
            bool peligro = partida && AmbienteDelJuego.CajaEnPeligro;
            luzDeEmergencia.enabled = peligro;
            if (peligro) luzDeEmergencia.intensity = 2.2f * AmbienteDelJuego.PulsoDeEmergencia(ahora);
        }

        foreach (Light alerta in alertas)
        {
            if (alerta == null) continue;
            float masCercano = 99f;
            foreach (Transform animatronico in animatronicos)
                if (animatronico != null)
                    masCercano = Mathf.Min(masCercano, Vector3.Distance(animatronico.position, alerta.transform.position));
            alerta.enabled = partida && AmbienteDelJuego.AlertaDeVentilacion(masCercano, alerta.range, ahora);
            if (alerta.enabled) alerta.intensity = intensidadBase[alerta];
        }
    }
}
