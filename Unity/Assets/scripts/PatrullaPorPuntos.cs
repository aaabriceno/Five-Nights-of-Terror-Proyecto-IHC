using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mueve un personaje por los puntos asignados en el Inspector, en el orden indicado.
/// Cada enemigo puede usar el mismo componente con una ruta distinta.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PatrullaPorPuntos : MonoBehaviour
{
    [Tooltip("Puntos que el personaje visitará, en el orden mostrado.")]
    public Transform[] puntos;

    [Tooltip("Segundos que espera al llegar a cada punto.")]
    [Min(0f)]
    public float tiempoEspera = 2f;

    [Tooltip("Si está activo, vuelve al primer punto al terminar la ruta.")]
    public bool repetirRuta = true;

    [Header("Regla de mirada")]
    [Tooltip("La ruta la controla AnimatronicUnityBrain; este componente no ejecuta su patrulla propia.")]
    public bool controladoPorIAExterna;

    [Tooltip("Actívalo para que el personaje avance solo cuando el usuario no mira la pantalla.")]
    public bool moverSoloCuandoNoMira = false;

    [Tooltip("Objeto que contiene HeadOrientationTracker. Si se deja vacío, se busca automáticamente.")]
    public HeadOrientationTracker detectorCabeza;

    private NavMeshAgent agente;
    private int indiceActual;

    private void Awake()
    {
        agente = GetComponent<NavMeshAgent>();

        if (detectorCabeza == null)
            detectorCabeza = FindObjectOfType<HeadOrientationTracker>();
    }

    private void Update()
    {
        if (controladoPorIAExterna)
            return;

        if (!moverSoloCuandoNoMira)
            return;

        // Si el objeto de MediaPipe apareció después que el enemigo,
        // se vuelve a buscar automáticamente.
        if (detectorCabeza == null)
            detectorCabeza = FindObjectOfType<HeadOrientationTracker>();

        // Bloqueo continuo: mientras el usuario mira la pantalla,
        // NavMeshAgent no puede avanzar ni aunque ya tenga una ruta calculada.
        if (detectorCabeza != null)
            agente.isStopped = detectorCabeza.mirandoPC;
    }

    private IEnumerator Start()
    {
        if (controladoPorIAExterna)
            yield break;

        if (puntos == null || puntos.Length == 0)
        {
            Debug.LogWarning($"{name}: asigna al menos un punto de patrulla.");
            yield break;
        }

        // Espera a que Unity coloque al agente sobre el NavMesh construido.
        while (!agente.isOnNavMesh)
            yield return null;

        while (true)
        {
            Transform destino = puntos[indiceActual];

            // Si el usuario está mirando la pantalla, el enemigo se congela.
            while (DebePausarsePorMirada())
            {
                agente.isStopped = true;
                yield return null;
            }

            agente.isStopped = false;

            if (destino != null && agente.SetDestination(destino.position))
            {
                while (agente.pathPending)
                {
                    if (DebePausarsePorMirada())
                        agente.isStopped = true;

                    yield return null;
                }

                while (agente.hasPath &&
                       agente.remainingDistance > agente.stoppingDistance)
                {
                    if (DebePausarsePorMirada())
                    {
                        agente.isStopped = true;
                        yield return null;
                        continue;
                    }

                    agente.isStopped = false;
                    yield return null;
                }

                yield return new WaitForSeconds(tiempoEspera);
            }

            indiceActual++;

            if (indiceActual < puntos.Length)
                continue;

            if (!repetirRuta)
                yield break;

            indiceActual = 0;
        }
    }

    private bool DebePausarsePorMirada()
    {
        return moverSoloCuandoNoMira &&
               detectorCabeza != null &&
               detectorCabeza.mirandoPC;
    }
}
