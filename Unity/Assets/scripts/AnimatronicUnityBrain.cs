using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Unity-side movement and threat behavior. Unity owns game state while
/// Flutter keeps its existing task protocol and UI through the relay.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AnimatronicUnityBrain : MonoBehaviour
{
    [Serializable]
    public class StringEvent : UnityEvent<string> { }

    [Header("Identidad y ruta")]
    public string nombreAnimatronico = "Freddy";
    [Tooltip("Para Freddy/Vixy: puntos candidatos, punto penúltimo y posición del jugador al final. Para Puppet: ruta en orden.")]
    public Transform[] ruta;
    [Tooltip("Índices de ruta donde este animatrónico puede atacar si no se le mira.")]
    public int[] puntosDeObservacion;
    [Tooltip("Tipos de tarea que aumentan el riesgo de este animatrónico.")]
    public string[] tiposDeTarea;

    [Header("Reglas")]
    public bool esPuppet;
    [SerializeField] private bool usarPatrullaExistente;
    [SerializeField] private bool usarRutaProbabilistica;
    [SerializeField] private bool ataqueVixyPorTresTareas;
    public bool usaSeguimientoDeMirada = true;
    [Tooltip("Pausa el avance mientras mirandoPC sea verdadero, como la patrulla original.")]
    public bool detenerseCuandoMira = false;
    [Min(0f)] public float segundosEntreMovimientos = 5f;
    public HeadOrientationTracker detectorCabeza;

    [Header("Retroalimentación visual")]
    public StringEvent alAtacarJugador = new StringEvent();

    [Header("Estado (solo lectura en ejecución)")]
    [SerializeField] private int indiceRuta;
    [SerializeField] private int nivelIA;
    [SerializeField] private bool activo;
    [SerializeField] private bool fueraDeCaja;

    private NavMeshAgent agent;
    private readonly Dictionary<string, float> segundosTareaPendiente = new Dictionary<string, float>();
    private readonly HashSet<int> puntosVisitados = new HashSet<int>();
    private readonly Dictionary<int, Vector3> posicionesDeRutaNavMesh = new Dictionary<int, Vector3>();
    private float movementClock;
    private float riskClock;
    private float puppetExitClock;
    private int currentNight;
    private float puppetPercent = 100f;
    private bool puppetDanger;
    private bool attackSent;
    private int waypointActivo = -1;
    private bool esperandoAtaquePenultimo;
    private float proximoIntentoPenultimo;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private static readonly string[] FreddyTasks = { "temperatura", "ventiladores", "cables", "dials", "trazar_curso" };
    private static readonly string[] VixyTasks = { "wifi", "sequence", "rhythm", "procesar_datos", "subir_datos" };
    private static readonly string[] PuppetTasks = Array.Empty<string>();

    public bool EsPuppet => esPuppet || string.Equals(nombreAnimatronico, "Puppet", StringComparison.OrdinalIgnoreCase);

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        if (detectorCabeza == null && usaSeguimientoDeMirada)
            detectorCabeza = FindObjectOfType<HeadOrientationTracker>();

        if (tiposDeTarea == null || tiposDeTarea.Length == 0)
            tiposDeTarea = GetDefaultTasks();

    }

    private void Update()
    {
        if (!activo || attackSent || ruta == null || ruta.Length == 0) return;

        if (detenerseCuandoMira && usaSeguimientoDeMirada && detectorCabeza != null && detectorCabeza.mirandoPC)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        if (agent != null && agent.isOnNavMesh && !esperandoAtaquePenultimo)
            agent.isStopped = false;

        if (esperandoAtaquePenultimo)
        {
            if (Time.time >= proximoIntentoPenultimo)
                TryPenultimateJumpscare();
            return;
        }

        if (usarRutaProbabilistica && waypointActivo == PenultimateWaypointIndex && IsAtWaypoint(waypointActivo))
        {
            esperandoAtaquePenultimo = true;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            TryPenultimateJumpscare();
            return;
        }

        // Puppet still attacks on physical arrival. Freddy and Vixy stop at
        // their penultimate point and use their own attack rules there.
        if (EsPuppet && HasReachedPlayer())
        {
            TriggerAttack(nombreAnimatronico + " llegó al jugador");
            return;
        }

        if (EsPuppet)
        {
            // The server publishes Puppet's winding state every game tick.
            if (fueraDeCaja)
                TryMove(Time.deltaTime, 1f);
            else
                TryPuppetExit(Time.deltaTime);
            return;
        }

        if (!usarPatrullaExistente)
            TryMove(Time.deltaTime, Mathf.Max(0.1f, segundosEntreMovimientos));
        UpdatePendingTaskThreat(Time.deltaTime);
    }

    public void SetNight(int night, string inGameTime)
    {
        int selectedNight = Mathf.Clamp(night, 1, 6);
        if (!activo || currentNight != selectedNight)
        {
            attackSent = false;
            currentNight = selectedNight;
        }
        nivelIA = GetNightLevel(selectedNight, ParseGameHour(inGameTime));
        activo = true;
    }

    public void BeginSession(int night, string inGameTime)
    {
        indiceRuta = -1;
        movementClock = 0f;
        riskClock = 0f;
        puppetExitClock = 0f;
        puntosVisitados.Clear();
        posicionesDeRutaNavMesh.Clear();
        waypointActivo = -1;
        esperandoAtaquePenultimo = false;
        proximoIntentoPenultimo = 0f;
        segundosTareaPendiente.Clear();
        fueraDeCaja = false;
        puppetPercent = 100f;
        attackSent = false;
        if (agent != null && !usarPatrullaExistente)
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.Warp(initialPosition);
            }
            else
            {
                transform.SetPositionAndRotation(initialPosition, initialRotation);
            }
            agent.isStopped = false;
        }
        currentNight = 0;
        activo = false;
        SetNight(night, inGameTime);
    }

    public void Configurar(string nombre, Transform[] waypoints, bool usaMirada, bool freezeWhenLookedAt,
        bool puppet, bool keepExistingPatrol = false, bool probabilisticRoute = false, bool vixyTaskThreshold = false)
    {
        nombreAnimatronico = nombre;
        ruta = waypoints;
        usaSeguimientoDeMirada = usaMirada;
        detenerseCuandoMira = freezeWhenLookedAt;
        esPuppet = puppet;
        usarPatrullaExistente = keepExistingPatrol;
        usarRutaProbabilistica = probabilisticRoute;
        ataqueVixyPorTresTareas = vixyTaskThreshold;
        tiposDeTarea = GetDefaultTasks();
        if (detectorCabeza == null && usaSeguimientoDeMirada)
            detectorCabeza = FindObjectOfType<HeadOrientationTracker>();
    }

    public void SetTasks(List<AnimatronicGameCoordinator.TaskState> tasks)
    {
        HashSet<string> current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tasks != null)
        {
            foreach (AnimatronicGameCoordinator.TaskState task in tasks)
            {
                if (task == null || string.IsNullOrEmpty(task.task_type)) continue;
                if (OwnsTask(task.task_type)) current.Add(task.task_type);
            }
        }

        List<string> removed = new List<string>();
        foreach (string existing in segundosTareaPendiente.Keys)
            if (!current.Contains(existing)) removed.Add(existing);
        foreach (string taskType in removed) segundosTareaPendiente.Remove(taskType);

        foreach (string taskType in current)
            if (!segundosTareaPendiente.ContainsKey(taskType)) segundosTareaPendiente.Add(taskType, 0f);

        // Vixy wins by overwhelming the player with three of her unresolved
        // task types; this is independent of her route attack roll.
        if (activo && ataqueVixyPorTresTareas &&
            string.Equals(nombreAnimatronico, "Vixy", StringComparison.OrdinalIgnoreCase) &&
            current.Count >= 3)
            TriggerAttack("Vixy te alcanzó con tres tareas pendientes");
    }

    public void SetPuppetState(float percentage, bool danger)
    {
        puppetPercent = Mathf.Clamp(percentage, 0f, 100f);
        puppetDanger = danger;
        if (puppetPercent > 0f) puppetExitClock = 0f;
    }

    public void RecibirAtaqueDelServidor(string message)
    {
        TriggerAttack(message);
    }

    public void Detener()
    {
        activo = false;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
    }

    private void TryMove(float deltaTime, float interval)
    {
        if (usarRutaProbabilistica && waypointActivo >= 0)
        {
            if (!IsAtWaypoint(waypointActivo)) return;
            waypointActivo = -1;
            movementClock = 0f;
            return;
        }

        movementClock += deltaTime;
        if (movementClock < interval) return;
        movementClock -= interval;

        // Every animatronic has a visible chance to advance on night one;
        // neglected owned tasks raise that chance instead of causing a remote
        // instant attack.
        float oldestPendingTask = 0f;
        foreach (float age in segundosTareaPendiente.Values)
            oldestPendingTask = Mathf.Max(oldestPendingTask, age);
        int movementLevel = Mathf.Clamp(nivelIA + 4 + Mathf.FloorToInt(oldestPendingTask / 4f), 0, 20);
        if (UnityEngine.Random.Range(0, 21) > movementLevel) return;

        if (usarRutaProbabilistica)
        {
            if (!EnsureAgentOnNavMesh()) return;
            int nextIndex = SelectNextWaypoint();
            if (nextIndex < 0) return;
            Transform probabilisticTarget = ruta[nextIndex];
            if (probabilisticTarget == null) return;
            agent.isStopped = false;
            Vector3 destination = posicionesDeRutaNavMesh.TryGetValue(nextIndex, out Vector3 snappedPosition)
                ? snappedPosition
                : probabilisticTarget.position;
            if (agent.SetDestination(destination))
            {
                waypointActivo = nextIndex;
                puntosVisitados.Add(nextIndex);
                indiceRuta = nextIndex;
            }
            else
            {
                puntosVisitados.Remove(nextIndex);
                Debug.LogWarning($"{nombreAnimatronico}: no se pudo calcular una ruta a {probabilisticTarget.name}.");
            }
            return;
        }

        if (indiceRuta >= ruta.Length - 1) return;
        indiceRuta++;
        Transform next = ruta[indiceRuta];
        if (next == null) return;

        if (!EnsureAgentOnNavMesh()) return;
        agent.isStopped = false;
        if (!agent.SetDestination(next.position))
            Debug.LogWarning($"{nombreAnimatronico}: no se pudo calcular una ruta a {next.name}.");
    }

    private int PenultimateWaypointIndex => ruta == null ? -1 : ruta.Length - 2;

    private int SelectNextWaypoint()
    {
        int penultimate = PenultimateWaypointIndex;
        if (penultimate < 0 || ruta[penultimate] == null) return -1;

        Vector3 playerPosition = ruta[ruta.Length - 1] != null
            ? ruta[ruta.Length - 1].position
            : transform.position;
        if (HorizontalDistance(transform.position, playerPosition) <= 9f)
            return CacheWalkableWaypoint(penultimate, out _) ? penultimate : -1;

        float currentPlayerDistance = HorizontalDistance(transform.position, playerPosition);
        List<WaypointChoice> advancing = new List<WaypointChoice>();
        List<WaypointChoice> reachable = new List<WaypointChoice>();
        for (int i = 0; i < penultimate; i++)
        {
            if (puntosVisitados.Contains(i) || ruta[i] == null) continue;
            if (!CacheWalkableWaypoint(i, out float pathDistance)) continue;

            WaypointChoice choice = new WaypointChoice(i, pathDistance);
            reachable.Add(choice);
            if (HorizontalDistance(ruta[i].position, playerPosition) < currentPlayerDistance - 0.25f)
                advancing.Add(choice);
        }

        List<WaypointChoice> pool = advancing.Count > 0 ? advancing : reachable;
        if (pool.Count == 0) return CacheWalkableWaypoint(penultimate, out _) ? penultimate : -1;
        pool.Sort((left, right) => left.pathDistance.CompareTo(right.pathDistance));

        // Roll among the three closest route points, weighted toward the
        // nearest one. Mirrored points make either side of the map eligible.
        int choiceCount = Mathf.Min(3, pool.Count);
        float totalWeight = 0f;
        for (int i = 0; i < choiceCount; i++)
            totalWeight += 1f / (pool[i].pathDistance + 0.5f);

        float roll = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < choiceCount; i++)
        {
            roll -= 1f / (pool[i].pathDistance + 0.5f);
            if (roll <= 0f) return pool[i].index;
        }
        return pool[choiceCount - 1].index;
    }

    private bool TryGetPathDistance(Vector3 destination, out float distance)
    {
        distance = 0f;
        if (agent == null || !agent.isOnNavMesh) return false;
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, destination, agent.areaMask, path) ||
            path.status != NavMeshPathStatus.PathComplete)
            return false;
        for (int i = 1; i < path.corners.Length; i++)
            distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return true;
    }

    private bool CacheWalkableWaypoint(int index, out float pathDistance)
    {
        pathDistance = 0f;
        if (index < 0 || index >= ruta.Length || ruta[index] == null || agent == null || !agent.isOnNavMesh)
            return false;
        if (!NavMesh.SamplePosition(ruta[index].position, out NavMeshHit hit, 2f, agent.areaMask))
            return false;
        if (!TryGetPathDistance(hit.position, out pathDistance))
            return false;
        posicionesDeRutaNavMesh[index] = hit.position;
        return true;
    }

    private bool EnsureAgentOnNavMesh()
    {
        if (agent.isOnNavMesh) return true;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, agent.areaMask))
        {
            Debug.LogWarning($"{nombreAnimatronico}: no se encontró NavMesh cerca de {name}.");
            return false;
        }
        agent.Warp(hit.position);
        return agent.isOnNavMesh;
    }

    private bool IsAtWaypoint(int index)
    {
        if (index < 0 || index >= ruta.Length || ruta[index] == null) return false;
        Vector3 here = transform.position;
        Vector3 target = posicionesDeRutaNavMesh.TryGetValue(index, out Vector3 snappedPosition)
            ? snappedPosition
            : ruta[index].position;
        return HorizontalDistance(here, target) <= Mathf.Max(1.1f, agent != null ? agent.stoppingDistance + 0.5f : 1.1f);
    }

    private void TryPenultimateJumpscare()
    {
        if (detenerseCuandoMira && usaSeguimientoDeMirada && detectorCabeza != null && detectorCabeza.mirandoPC)
            return;

        float chance = Mathf.Clamp(10f + (currentNight - 1) * 10f, 10f, 100f);
        if (UnityEngine.Random.Range(0f, 100f) < chance)
        {
            TriggerAttack(nombreAnimatronico + " activó un jumpscare desde el penúltimo punto");
            return;
        }

        // A failed roll leaves the animatronic at the penultimate point to
        // try again after its movement interval. Freddy can still be held by
        // looking at him.
        proximoIntentoPenultimo = Time.time + Mathf.Max(1f, segundosEntreMovimientos);
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right)
    {
        left.y = 0f;
        right.y = 0f;
        return Vector3.Distance(left, right);
    }

    private struct WaypointChoice
    {
        public int index;
        public float pathDistance;
        public WaypointChoice(int index, float pathDistance)
        {
            this.index = index;
            this.pathDistance = pathDistance;
        }
    }

    private void UpdatePendingTaskThreat(float deltaTime)
    {
        if (segundosTareaPendiente.Count == 0) return;
        riskClock += deltaTime;
        if (riskClock < 0.5f) return;
        float tick = riskClock;
        riskClock = 0f;
        List<string> keys = new List<string>(segundosTareaPendiente.Keys);
        foreach (string key in keys)
        {
            float age = segundosTareaPendiente[key] + tick;
            segundosTareaPendiente[key] = age;
        }

        // Pending tasks are applied as movement pressure in TryMove. They do
        // not bypass the route or trigger game over on their own.
    }

    private void TryPuppetExit(float deltaTime)
    {
        if (puppetPercent > 0f) return;
        puppetExitClock += deltaTime;
        if (puppetExitClock < 1f) return;
        puppetExitClock -= 1f;

        // The original game checks Puppet's AI roll once per second at zero.
        if (UnityEngine.Random.Range(0, 21) <= nivelIA)
            fueraDeCaja = true;
    }

    private void TriggerAttack(string message)
    {
        if (attackSent) return;
        attackSent = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        alAtacarJugador.Invoke(message);
    }

    private bool OwnsTask(string taskType)
    {
        if (tiposDeTarea == null) return false;
        foreach (string type in tiposDeTarea)
            if (string.Equals(type, taskType, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private bool HasReachedPlayer()
    {
        if (ruta == null || ruta.Length == 0 || ruta[ruta.Length - 1] == null) return false;
        Vector3 target = ruta[ruta.Length - 1].position;
        Vector3 currentFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 targetFlat = new Vector3(target.x, 0f, target.z);
        return Vector3.Distance(currentFlat, targetFlat) <= 1.5f;
    }

    private string[] GetDefaultTasks()
    {
        if (string.Equals(nombreAnimatronico, "Freddy", StringComparison.OrdinalIgnoreCase)) return FreddyTasks;
        if (string.Equals(nombreAnimatronico, "Vixy", StringComparison.OrdinalIgnoreCase)) return VixyTasks;
        return PuppetTasks;
    }

    private int GetNightLevel(int night, int hour)
    {
        int initial;
        if (string.Equals(nombreAnimatronico, "Freddy", StringComparison.OrdinalIgnoreCase))
            initial = new[] { 0, 0, 1, 0, 3, 4 }[night - 1];
        else if (string.Equals(nombreAnimatronico, "Vixy", StringComparison.OrdinalIgnoreCase))
            initial = new[] { 0, 1, 5, 4, 7, 12 }[night - 1];
        else if (EsPuppet)
            initial = new[] { 0, 3, 0, 2, 5, 10 }[night - 1];
        else
            initial = 0;

        if (!string.Equals(nombreAnimatronico, "Freddy", StringComparison.OrdinalIgnoreCase) && hour >= 2)
            initial += 1;
        if (!string.Equals(nombreAnimatronico, "Freddy", StringComparison.OrdinalIgnoreCase) && hour >= 3)
            initial += 1;
        if (!string.Equals(nombreAnimatronico, "Freddy", StringComparison.OrdinalIgnoreCase) && hour >= 4)
            initial += 1;
        return initial;
    }

    private static int ParseGameHour(string value)
    {
        if (string.IsNullOrEmpty(value)) return 1;
        string[] parts = value.Split(':');
        if (parts.Length == 0 || !int.TryParse(parts[0], out int displayHour)) return 1;
        // Backend labels 12 AM as in-game hour 1, then 1 AM as hour 2, etc.
        return displayHour == 12 ? 1 : Mathf.Clamp(displayHour + 1, 1, 6);
    }
}
