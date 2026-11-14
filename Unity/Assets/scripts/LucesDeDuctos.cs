using UnityEngine;

/// <summary>
/// Lo que el jugador 1 ve en la oficina de los dos ductos: una lámpara encima
/// de cada boca (el semáforo: verde, amarillo o rojo) y la compuerta, una placa
/// de metal que tapa la boca mientras está cerrada. Las reglas viven en
/// SemaforoDeDuctos y CompuertasDeDuctos; esto es solo lo que se ve.
///
/// Medidas sacadas del plano real: la oficina va de x = 17 a 23 y de z = 0 a 6;
/// las bocas de los ductos son huecos de 2 m (z = 2 a 4) en las paredes
/// laterales (x = 17 y x = 23). La cámara está en (20, 1,6, 0,55) y ve ±54°:
/// con la lámpara adelantada a z = 3,6 queda a ~44°, a la vista sin girar.
/// </summary>
public sealed class LucesDeDuctos : MonoBehaviour
{
    private static readonly Vector3 LamparaIzquierda = new Vector3(17.21f, 2.75f, 3.6f);
    private static readonly Vector3 LamparaDerecha = new Vector3(22.79f, 2.75f, 3.6f);
    // La boca llega hasta el techo (visto en las capturas): la placa sube 3,4 m
    // y el techo tapa lo que sobra.
    private static readonly Vector3 CompuertaIzquierda = new Vector3(17.09f, 1.7f, 3f);
    private static readonly Vector3 CompuertaDerecha = new Vector3(22.91f, 1.7f, 3f);

    private static readonly Color Apagada = new Color(0.12f, 0.12f, 0.12f);
    private static readonly Color Verde = new Color(0.15f, 0.95f, 0.3f);
    private static readonly Color Amarillo = new Color(1f, 0.8f, 0.1f);
    private static readonly Color Rojo = new Color(1f, 0.1f, 0.05f);

    private Lampara izquierda;
    private Lampara derecha;
    private GameObject compuertaIzquierda;
    private GameObject compuertaDerecha;
    private bool empujandoIzquierda;
    private bool empujandoDerecha;

    private sealed class Lampara
    {
        public Renderer foco;
        public Light luz;
        public SemaforoDeDuctos.Luz estado;
        public bool encendida;
    }

    public static LucesDeDuctos Crear()
    {
        var raiz = new GameObject("LucesDeDuctos");
        var luces = raiz.AddComponent<LucesDeDuctos>();
        luces.izquierda = CrearLampara(raiz.transform, "Lampara_Izquierda", LamparaIzquierda);
        luces.derecha = CrearLampara(raiz.transform, "Lampara_Derecha", LamparaDerecha);
        luces.compuertaIzquierda = CrearCompuerta(raiz.transform, "Compuerta_Izquierda", CompuertaIzquierda);
        luces.compuertaDerecha = CrearCompuerta(raiz.transform, "Compuerta_Derecha", CompuertaDerecha);
        luces.Mostrar(SemaforoDeDuctos.Luz.Verde, SemaforoDeDuctos.Luz.Verde, false, false, false);
        return luces;
    }

    /// `encendidas` = false fuera de una partida: lámparas apagadas y compuertas abiertas.
    public void Mostrar(SemaforoDeDuctos.Luz luzIzquierda, SemaforoDeDuctos.Luz luzDerecha,
        bool cerradaIzquierda, bool cerradaDerecha, bool encendidas)
    {
        Pintar(izquierda, luzIzquierda, encendidas);
        Pintar(derecha, luzDerecha, encendidas);
        compuertaIzquierda.SetActive(encendidas && cerradaIzquierda);
        compuertaDerecha.SetActive(encendidas && cerradaDerecha);
        if (!encendidas) FijarEmpujones(false, false);
    }

    /// Un animatrónico empuja la compuerta cerrada de ese lado (la fase antes
    /// de retroceder, ver RetrocesoPorCompuerta): la placa da golpes hacia la
    /// oficina, para que el jugador 1 sepa que está ahí y que debe aguantar.
    public void FijarEmpujones(bool izquierda, bool derecha)
    {
        empujandoIzquierda = izquierda;
        empujandoDerecha = derecha;
    }

    private void Update()
    {
        // El rojo late, para que llame la atención aunque se mire de reojo.
        float latido = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
        foreach (Lampara l in new[] { izquierda, derecha })
            if (l != null && l.encendida && l.estado == SemaforoDeDuctos.Luz.Rojo)
                l.luz.intensity = 1f + 2f * latido;
        // Golpes: ~3 por segundo, la placa se abomba hasta 6 cm hacia dentro.
        Golpear(compuertaIzquierda, CompuertaIzquierda, empujandoIzquierda, +1f);
        Golpear(compuertaDerecha, CompuertaDerecha, empujandoDerecha, -1f);
    }

    /// `haciaDentro`: +1 si la oficina queda hacia +x (compuerta izquierda), -1 si no.
    private static void Golpear(GameObject placa, Vector3 reposo, bool empujando, float haciaDentro)
    {
        if (placa == null || !placa.activeSelf) return;
        float golpe = empujando ? Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.06f : 0f;
        placa.transform.position = reposo + new Vector3(haciaDentro * golpe, 0f, 0f);
    }

    private static void Pintar(Lampara l, SemaforoDeDuctos.Luz estado, bool encendida)
    {
        l.estado = estado;
        l.encendida = encendida;
        Color color = !encendida ? Apagada
            : estado == SemaforoDeDuctos.Luz.Rojo ? Rojo
            : estado == SemaforoDeDuctos.Luz.Amarillo ? Amarillo : Verde;
        l.foco.material.color = color;
        l.luz.enabled = encendida;
        l.luz.color = color;
        l.luz.intensity = 1.5f;
    }

    private static Lampara CrearLampara(Transform padre, string nombre, Vector3 posicion)
    {
        GameObject forma = GameObject.CreatePrimitive(PrimitiveType.Cube);
        forma.name = nombre;
        QuitarColision(forma);
        forma.transform.SetParent(padre, false);
        forma.transform.position = posicion;
        forma.transform.localScale = new Vector3(0.06f, 0.22f, 0.22f);
        Renderer foco = forma.GetComponent<Renderer>();
        // "Sprites/Default" siempre va incluido en la exportación y no depende de
        // las luces (ver TrampaVisible): el color se ve igual en la oscuridad.
        foco.material = new Material(Shader.Find("Sprites/Default"));
        foco.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        foco.receiveShadows = false;
        foco.sortingOrder = 2;   // por delante de la compuerta (también transparente)

        var objetoLuz = new GameObject("Luz");
        objetoLuz.transform.SetParent(forma.transform, false);
        // Un poco hacia dentro de la oficina, para que alumbre la pared y el piso.
        objetoLuz.transform.position = posicion + new Vector3(posicion.x < 20f ? 0.3f : -0.3f, 0f, 0f);
        Light luz = objetoLuz.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.range = 3f;
        return new Lampara { foco = foco, luz = luz };
    }

    private static GameObject CrearCompuerta(Transform padre, string nombre, Vector3 posicion)
    {
        GameObject placa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placa.name = nombre;
        QuitarColision(placa);
        placa.transform.SetParent(padre, false);
        placa.transform.position = posicion;
        placa.transform.localScale = new Vector3(0.14f, 3.4f, 2f);
        Pintura(placa, new Color(0.07f, 0.08f, 0.09f), 0);

        // Una franja amarilla a media altura: se lee como "cerrado" de un vistazo.
        GameObject franja = GameObject.CreatePrimitive(PrimitiveType.Cube);
        franja.name = "Franja";
        QuitarColision(franja);
        franja.transform.SetParent(placa.transform, false);
        franja.transform.localScale = new Vector3(1.2f, 0.06f, 0.96f);
        franja.transform.localPosition = new Vector3(0f, -0.13f, 0f);   // a ~1,25 m del piso
        // Va después de la placa: las dos son transparentes ("Sprites/Default", sin
        // escribir profundidad) y con el mismo centro la placa la tapaba.
        Pintura(franja, new Color(0.95f, 0.75f, 0.1f), 1);
        placa.SetActive(false);
        return placa;
    }

    private static void Pintura(GameObject objeto, Color color, int orden)
    {
        Renderer r = objeto.GetComponent<Renderer>();
        r.material = new Material(Shader.Find("Sprites/Default")) { color = color };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.sortingOrder = orden;
    }

    private static void QuitarColision(GameObject objeto)
    {
        Collider colision = objeto.GetComponent<Collider>();
        if (colision != null) Destroy(colision);
    }
}
