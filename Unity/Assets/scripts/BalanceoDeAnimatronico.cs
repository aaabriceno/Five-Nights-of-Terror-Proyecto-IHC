using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Animación procedural para un modelo sin esqueleto: al caminar se balancea de
/// lado a lado, cabecea y sube un poco con cada paso (según la velocidad del
/// NavMeshAgent), y al detenerse gira el cuerpo hacia el jugador para mirarlo.
///
/// Para no pelear con el agente (que fija la posición y la rotación de la raíz
/// cada cuadro), los hijos del modelo se cuelgan de un pivote nuevo y el
/// balanceo se aplica al pivote. Los números salen de AmbienteDelJuego.
/// </summary>
public sealed class BalanceoDeAnimatronico : MonoBehaviour
{
    /// Hasta dónde gira el cuerpo para mirar al jugador cuando está quieto.
    public float giroMaximoGrados = 60f;
    public float velocidadDeGiro = 90f;

    private Transform pivote;
    private NavMeshAgent agente;
    private Transform jugador;
    private float fase;
    private float giro;

    /// Velocidad actual del cuerpo (m/s), para el sonido de los pasos.
    public float Velocidad { get; private set; }

    private void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        if (Camera.main != null) jugador = Camera.main.transform;

        pivote = new GameObject("Balanceo").transform;
        pivote.SetParent(transform, false);
        // Los hijos existentes (mallas, luces de los ojos) pasan al pivote.
        var hijos = new System.Collections.Generic.List<Transform>();
        foreach (Transform hijo in transform)
            if (hijo != pivote) hijos.Add(hijo);
        foreach (Transform hijo in hijos) hijo.SetParent(pivote, true);
    }

    private void LateUpdate()
    {
        if (pivote == null) return;
        Velocidad = agente != null && agente.isOnNavMesh && !agente.isStopped ? agente.velocity.magnitude : 0f;
        fase += 2f * Mathf.PI * AmbienteDelJuego.CadenciaDePasos(Velocidad) * Time.deltaTime;
        if (fase > 1000f) fase -= 1000f;
        AmbienteDelJuego.Balanceo balanceo =
            AmbienteDelJuego.BalanceoConFase(AmbienteDelJuego.AmplitudDeBalanceo(Velocidad), fase);

        // Quieto: gira hacia el jugador. Caminando: vuelve al frente.
        float giroDeseado = 0f;
        if (Velocidad < 0.05f && jugador != null)
        {
            Vector3 haciaElJugador = jugador.position - transform.position;
            haciaElJugador.y = 0f;
            if (haciaElJugador.sqrMagnitude > 0.01f)
                giroDeseado = Mathf.Clamp(Vector3.SignedAngle(transform.forward, haciaElJugador, Vector3.up),
                    -giroMaximoGrados, giroMaximoGrados);
        }
        giro = Mathf.MoveTowards(giro, giroDeseado, velocidadDeGiro * Time.deltaTime);

        pivote.localPosition = new Vector3(0f, balanceo.DesplazamientoY, 0f);
        pivote.localRotation = Quaternion.Euler(balanceo.CabeceoGrados, giro, balanceo.BalanceoGrados);
    }
}
