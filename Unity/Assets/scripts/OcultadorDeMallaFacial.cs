using System.Collections.Generic;
using Mediapipe.Unity;
using UnityEngine;

/// <summary>
/// Oculta la malla facial que el sample de MediaPipe dibuja sobre el video:
/// los puntos de los landmarks y las líneas que los unen. Es una ayuda para
/// depurar la detección, pero en el juego estorba — el jugador tiene que
/// vigilar la escena, no su propia cara.
///
/// No desactiva los GameObjects, solo apaga sus Renderer. La diferencia
/// importa: <see cref="HeadOrientationTracker"/> exige que los puntos estén
/// activos en la jerarquía para leer sus posiciones, así que desactivarlos
/// dejaría `mirandoPC` en false de forma permanente y Freddy nunca se
/// congelaría. Invisibles pero activos, la detección sigue funcionando.
///
/// El video de la cámara no se toca: se dibuja en otro objeto (la Screen del
/// sample) y sigue visible.
///
/// MediaPipe crea los puntos durante la partida, a medida que detecta caras,
/// por eso hay que revisar cada tanto si aparecieron nuevos en vez de
/// apagarlos una sola vez al arrancar.
/// </summary>
public class OcultadorDeMallaFacial : MonoBehaviour
{
    [Tooltip("Desmarcar para volver a ver la malla mientras se depura.")]
    [SerializeField] private bool ocultarMalla = true;

    [Tooltip("Cada cuántos segundos se buscan partes nuevas de la malla.")]
    [SerializeField, Min(0.1f)] private float segundosEntreRevisiones = 0.5f;

    private readonly List<Renderer> renderersDeLaMalla = new List<Renderer>();
    private MultiFaceLandmarkListAnnotation anotacion;
    private float tiempoHastaProximaRevision;
    private bool ultimoEstadoAplicado;

    private void Update()
    {
        tiempoHastaProximaRevision -= Time.deltaTime;
        if (tiempoHastaProximaRevision > 0f) return;
        tiempoHastaProximaRevision = segundosEntreRevisiones;

        if (anotacion == null)
        {
            anotacion = FindObjectOfType<MultiFaceLandmarkListAnnotation>(true);
            renderersDeLaMalla.Clear();
        }
        if (anotacion == null) return;

        int cantidadAnterior = renderersDeLaMalla.Count;
        anotacion.GetComponentsInChildren(true, renderersDeLaMalla);

        // Reaplicar solo cuando cambió algo, para no tocar cientos de
        // Renderer dos veces por segundo sin motivo.
        if (renderersDeLaMalla.Count == cantidadAnterior &&
            ultimoEstadoAplicado == ocultarMalla)
            return;

        foreach (Renderer renderizador in renderersDeLaMalla)
            if (renderizador != null) renderizador.enabled = !ocultarMalla;
        ultimoEstadoAplicado = ocultarMalla;
    }
}
