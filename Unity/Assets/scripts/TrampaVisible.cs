using UnityEngine;

/// <summary>
/// La trampa del jugador 2 hecha cosa del mapa: un disco rojo en el suelo con
/// una luz que late. El jugador 1 la ve si mira hacia allí; desaparece cuando
/// un animatrónico la pisa. La regla (cargas, quién la pisa) vive en
/// UnityGameSessionController y RecursosDeOperador: esto es solo lo que se ve.
/// </summary>
public class TrampaVisible : MonoBehaviour
{
    private const float DiametroDelDisco = 0.9f;

    private Light luz;
    private Transform disco;
    private float fase;

    /// Crea la trampa sobre el punto del mapa. El disco no tiene colisión: no
    /// estorba a los animatrónicos ni a la navegación.
    public static GameObject Crear(string nombreDelPunto, Vector3 posicion)
    {
        GameObject raiz = new GameObject("Trampa_" + nombreDelPunto);
        raiz.transform.position = posicion + Vector3.up * 0.02f;

        GameObject forma = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        forma.name = "Disco";
        Collider colision = forma.GetComponent<Collider>();
        if (colision != null) Destroy(colision);
        forma.transform.SetParent(raiz.transform, false);
        forma.transform.localScale = new Vector3(DiametroDelDisco, 0.03f, DiametroDelDisco);
        forma.transform.localPosition = new Vector3(0f, 0.03f, 0f);

        // "Sprites/Default" siempre va incluido en la exportación y no depende
        // de las luces: un rojo plano que se ve incluso en el comedor a oscuras.
        Renderer pintura = forma.GetComponent<Renderer>();
        pintura.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.95f, 0.08f, 0.05f) };
        pintura.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pintura.receiveShadows = false;

        GameObject foco = new GameObject("Luz");
        foco.transform.SetParent(raiz.transform, false);
        foco.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        Light luzRoja = foco.AddComponent<Light>();
        luzRoja.type = LightType.Point;
        luzRoja.color = new Color(1f, 0.1f, 0.05f);
        luzRoja.range = 2.5f;
        luzRoja.intensity = 1f;

        TrampaVisible trampa = raiz.AddComponent<TrampaVisible>();
        trampa.luz = luzRoja;
        trampa.disco = forma.transform;
        trampa.fase = Random.value * 6.28f;
        return raiz;
    }

    private void Update()
    {
        float latido = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f + fase);
        if (luz != null) luz.intensity = 0.5f + 1.5f * latido;
        if (disco != null)
        {
            float lado = DiametroDelDisco * (0.92f + 0.12f * latido);
            disco.localScale = new Vector3(lado, 0.03f, lado);
        }
    }
}
