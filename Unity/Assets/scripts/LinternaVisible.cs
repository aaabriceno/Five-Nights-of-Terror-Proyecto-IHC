using UnityEngine;

/// <summary>
/// La luz de la linterna del jugador 1: un foco colgado de la cámara (alumbra
/// hacia donde mira, también al girar con los botones de la tablet), con el
/// cono de la regla (`LinternaDeOficina`, 40°). La regla vive allá; esto es
/// solo lo que se ve.
/// </summary>
public sealed class LinternaVisible : MonoBehaviour
{
    private Light luz;

    public static LinternaVisible Crear(Transform camara)
    {
        var objeto = new GameObject("Linterna");
        objeto.transform.SetParent(camara, false);
        // A la altura de la mano derecha, apuntando adonde mira la cámara.
        objeto.transform.localPosition = new Vector3(0.15f, -0.2f, 0.1f);
        objeto.transform.localRotation = Quaternion.identity;
        var linterna = objeto.AddComponent<LinternaVisible>();
        linterna.luz = objeto.AddComponent<Light>();
        linterna.luz.type = LightType.Spot;
        linterna.luz.spotAngle = LinternaDeOficina.MitadDelAngulo * 2f;
        // Un poco más allá del alcance de la regla: en el borde (10 m) todavía se
        // ve la luz; con el alcance justo se apagaría a cero ahí mismo.
        linterna.luz.range = LinternaDeOficina.Alcance * 1.2f;
        linterna.luz.intensity = 4f;
        linterna.luz.color = new Color(1f, 0.95f, 0.8f);
        linterna.luz.shadows = LightShadows.None;
        linterna.luz.enabled = false;
        return linterna;
    }

    public void Mostrar(bool prendida)
    {
        if (luz != null && luz.enabled != prendida) luz.enabled = prendida;
    }
}
