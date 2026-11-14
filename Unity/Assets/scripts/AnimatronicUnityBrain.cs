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

    /// Un punto al que a veces se desvía entre dos puntos de su ruta fija.
    [Serializable]
    public class DesvioDeRuta
    {
        [Tooltip("Índice del punto de la ruta tras el cual puede hacerse el desvío (entre ese punto y el siguiente).")]
        public int despuesDe;
        public Transform punto;
    }

    [Header("Identidad y ruta")]
    public string nombreAnimatronico = "Freddy";
    [Tooltip("Ruta fija, en orden: puntos, punto penúltimo y posición del jugador al final.")]
    public Transform[] ruta;
    [Tooltip("Desvíos ocasionales entre dos puntos de la ruta (solo Freddy y Vixy). La ruta no se altera: se pasa por el desvío y se vuelve.")]
    public DesvioDeRuta[] desvios = new DesvioDeRuta[0];
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
    [Min(0f)] public float segundosEntreMovimientos = 35f;
    public HeadOrientationTracker detectorCabeza;

    [Header("Retroalimentación visual")]
    public StringEvent alAtacarJugador = new StringEvent();

    [Header("Estado (solo lectura en ejecución)")]
    [SerializeField] private int indiceRuta;
    [SerializeField] private int nivelIA;
    [SerializeField] private bool activo;
    [SerializeField] private bool fueraDeCaja;

    private NavMeshAgent agent;
    // Jugador 2: mientras lo retrasa, el tiempo de este animatrónico corre más
    // lento (1 = normal). Lo fija UnityGameSessionController cada cuadro.
    private float factorDeVelocidad = 1f;
    private float velocidadBaseDelAgente;
    // Con un jugador 2 conectado los intervalos entre avances se acortan
    // (1 = normal, menor = más agresivo). Lo fija UnityGameSessionController.
    private float factorDeAgresividad = 1f;
    // Jugador 2: una trampa lo detiene unos segundos; mientras Puppet espera el
    // rescate (ventana) tampoco avanza.
    private float detenidoHasta;
    private bool mantenidoQuieto;
    // La compuerta del ducto por el que entra este animatrónico (Vixy y Puppet).
    // Mientras está cerrada y no la pasó, no entra a la oficina ni ataca. La
    // fija UnityGameSessionController cada cuadro (ver CompuertasDeDuctos).
    private bool compuertaCerrada;
    // La compuerta está en la boca del ducto, a ~3 m del jugador 1 (V04 y V08
    // quedan a 3,7 m): más cerca que esto ya entró a la oficina.
    private const float DistanciaDeLaCompuertaAlJugador = 3f;
    // En la boca con la compuerta cerrada la empuja 4 s y, si aguanta, vuelve a
    // la entrada del ducto (ver RetrocesoPorCompuerta). Mientras vuelve
    // (`retrocediendoHacia` >= 0) no avanza ni ataca.
    private readonly RetrocesoPorCompuerta retroceso = new RetrocesoPorCompuerta();
    private int retrocediendoHacia = -1;
    private readonly Dictionary<string, float> segundosTareaPendiente = new Dictionary<string, float>();
    private readonly HashSet<int> puntosVisitados = new HashSet<int>();
    // Freddy y Vixy siguen su ruta en orden; de vez en cuando hacen un desvío.
    private RutaFija rutaFija;
    private float probabilidadDeDesvio;
    // Freddy no avanza a un ritmo perfectamente fijo: cada paso dura entre un
    // 80 % y un 120 % del intervalo, para que el jugador 2 no pueda cronometrarlo.
    private const float VariacionDelIntervaloDeFreddy = 0.2f;
    private float variacionDelIntervalo = 1f;
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
    // Vixy mide el descuido de la tablet en conjunto, no solo sus propias
    // tareas: cuenta todas las pendientes (10 tipos entre Freddy y ella).
    // Los umbrales bajan con la noche, así el mismo descuido que en la noche
    // 1 era tolerable en la noche 6 ya la trae encima. Índice = noche - 1.
    private static readonly int[] TareasParaQueVixyAvancePorNoche = { 6, 6, 5, 5, 4, 4 };
    private static readonly int[] TareasParaQueVixyAtaquePorNoche = { 8, 8, 7, 7, 6, 6 };
    // 8 -> 6 tras las pruebas con testers: las noches 1 y 2 se pasaban muy
    // fácil.
    private const float SegundosEntreAvancesDeVixy = 6f;
    // Revisa más seguido una vez llega al penúltimo punto: ya decidido el
    // desenlace, la espera corta sube la tensión sin quitarle al jugador el
    // margen para resolver y frenarla.
    private const float SegundosEntreRevisionesDeVixyEnPenultimo = 5f;

    // Segundos que Freddy tolera sin ser mirado antes de avanzar un nodo.
    // Índice = noche - 1. Antes {35, 32, 29, 26, 23, 20}: se bajaron 3 s en
    // todas las noches tras las pruebas con testers (las noches 1 y 2 se
    // pasaban muy fácil).
    private static readonly float[] SegundosDeFreddyPorNoche = { 22f, 20f, 18f, 16f, 14f, 12f };

    // Freddy se defiende solo con la mirada: sus tareas siguen apareciendo en
    // la tablet (las genera UnityGameSessionController), pero no alteran su
    // avance. Vixy es la contraparte: solo las tareas la mueven.
    private static readonly string[] FreddyTasks = Array.Empty<string>();
    private static readonly string[] VixyTasks = { "wifi", "sequence", "rhythm", "procesar_datos", "subir_datos" };
    private static readonly string[] PuppetTasks = Array.Empty<string>();

    public bool EsPuppet => esPuppet || string.Equals(nombreAnimatronico, "Puppet", StringComparison.OrdinalIgnoreCase);

    private int IndiceDeNoche => Mathf.Clamp(currentNight - 1, 0, 5);

    private int TareasParaAvanzar => TareasParaQueVixyAvancePorNoche[IndiceDeNoche];

    private int TareasParaAtacar => TareasParaQueVixyAtaquePorNoche[IndiceDeNoche];

    /// Freddy escala con la noche; el resto conserva el valor del Inspector.
    private float SegundosEntreAvances => Mathf.Max(0.1f, factorDeAgresividad * (detenerseCuandoMira
        ? SegundosDeFreddyPorNoche[IndiceDeNoche]
        : segundosEntreMovimientos));

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // La velocidad sale del código, no de la escena (que tiene 1,5).
        if (agent != null)
        {
            agent.speed = RecorridosDeAnimatronicos.VelocidadAlCaminar;
            velocidadBaseDelAgente = agent.speed;
        }
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        if (detectorCabeza == null && usaSeguimientoDeMirada)
            detectorCabeza = FindObjectOfType<HeadOrientationTracker>();

        if (tiposDeTarea == null || tiposDeTarea.Length == 0)
            tiposDeTarea = GetDefaultTasks();

    }

    /// Velocidad relativa de este animatrónico (1 = normal, 0.5 = a la mitad).
    /// Afecta a sus esperas entre avances, a su amenaza por tareas pendientes,
    /// a la cuenta del ataque desde el penúltimo punto y a su paso por la ruta.
    public void FijarFactorDeVelocidad(float factor)
    {
        factorDeVelocidad = Mathf.Clamp(factor, 0.05f, 1f);
        if (agent != null && velocidadBaseDelAgente > 0f)
            agent.speed = velocidadBaseDelAgente * factorDeVelocidad;
    }

    public bool FueraDeCaja => fueraDeCaja;

    /// Lo detiene `segundos` (trampa del jugador 2). Dos trampas no se suman.
    public void DetenerPor(float segundos)
    {
        detenidoHasta = Mathf.Max(detenidoHasta, Time.time + segundos);
    }

    public void FijarCompuertaCerrada(bool cerrada) { compuertaCerrada = cerrada; }

    /// Está en la boca del ducto empujando la compuerta cerrada (la placa tiembla).
    public bool EmpujandoLaCompuerta => retroceso.Empujando;

    /// En su penúltimo punto: para Vixy y Puppet, la boca del ducto.
    private bool EnLaBocaDelDucto => ruta != null && ruta.Length >= 2 && IsAtWaypoint(PenultimateWaypointIndex);

    /// El punto de su ruta más cercano a donde está ahora (por su posición real,
    /// no por su destino: Puppet fija el destino siguiente cada segundo aunque
    /// no haya llegado). Con esto se enciende el semáforo de su ducto.
    public string PuntoMasCercano
    {
        get
        {
            if (ruta == null) return null;
            string nombre = null;
            float mejor = float.MaxValue;
            foreach (Transform punto in ruta)
            {
                if (punto == null) continue;
                float d = HorizontalDistance(transform.position, punto.position);
                if (d < mejor) { mejor = d; nombre = punto.name; }
            }
            return nombre;
        }
    }

    private bool BloqueadoPorCompuerta => compuertaCerrada && !PasoLaCompuerta();

    private bool PasoLaCompuerta()
    {
        if (ruta == null || ruta.Length == 0 || ruta[ruta.Length - 1] == null) return false;
        return HorizontalDistance(transform.position, ruta[ruta.Length - 1].position) < DistanciaDeLaCompuertaAlJugador;
    }

    /// Mantiene al animatrónico quieto mientras sea verdadero (ventana de rescate).
    public void MantenerQuieto(bool quieto) { mantenidoQuieto = quieto; }

    /// Rescate: Puppet vuelve a su caja (que además se vuelve a llenar).
    public void VolverALaCaja()
    {
        fueraDeCaja = false;
        puppetPercent = 100f;
        puppetExitClock = 0f;
    }

    public void FijarFactorDeAgresividad(float factor)
    {
        factorDeAgresividad = Mathf.Clamp(factor, 0.2f, 1f);
    }

    /// Solo para las capturas de prueba (-prueba-posar): se planta a `distancia`
    /// metros frente al jugador, mirándolo, y no se mueve más.
    public void PosarFrenteAlJugador(Transform jugador, float distancia)
    {
        posando = true;
        Vector3 adelante = jugador.forward;
        adelante.y = 0f;
        adelante.Normalize();
        Vector3 destino = jugador.position + adelante * distancia;
        destino.y = transform.position.y;
        if (agent != null && agent.isOnNavMesh) agent.Warp(destino);
        else transform.position = destino;
        transform.rotation = Quaternion.LookRotation(-adelante, Vector3.up);
    }

    private bool posando;

    private void Update()
    {
        if (posando)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }
        if (!activo || attackSent || ruta == null || ruta.Length == 0) return;
        float deltaTime = Time.deltaTime * factorDeVelocidad;

        if (mantenidoQuieto || Time.time < detenidoHasta)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            // El reloj de espera usa Time.time: se atrasa lo que estuvo detenido.
            if (esperandoAtaquePenultimo) proximoIntentoPenultimo += Time.deltaTime;
            return;
        }

        if (detenerseCuandoMira && usaSeguimientoDeMirada && detectorCabeza != null && detectorCabeza.mirandoPC)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        if (agent != null && agent.isOnNavMesh && !esperandoAtaquePenultimo)
            agent.isStopped = false;

        // Volviendo a la entrada del ducto tras la compuerta: hasta llegar no
        // avanza ni ataca, aunque el jugador 1 la abra mientras tanto.
        if (retrocediendoHacia >= 0)
        {
            if (!IsAtWaypoint(retrocediendoHacia)) return;
            retrocediendoHacia = -1;
            movementClock = 0f;
        }

        // Le cerraron la compuerta en la cara: la empuja y, si aguanta, retrocede.
        if (retroceso.Avanzar(deltaTime, compuertaCerrada && EnLaBocaDelDucto, compuertaCerrada))
        {
            RetrocederPorLaCompuerta();
            return;
        }

        if (esperandoAtaquePenultimo)
        {
            // El reloj de espera usa Time.time: se atrasa lo que se ralentizó.
            proximoIntentoPenultimo += Time.deltaTime * (1f - factorDeVelocidad);
            if (Time.time >= proximoIntentoPenultimo)
                TryPenultimateJumpscare();
            return;
        }

        if (usarRutaProbabilistica && waypointActivo == PenultimateWaypointIndex && IsAtWaypoint(waypointActivo))
        {
            esperandoAtaquePenultimo = true;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            // Llegar al punto arranca la cuenta, no el ataque: el jugador
            // siempre dispone del intervalo completo para reaccionar.
            proximoIntentoPenultimo = Time.time + (ataqueVixyPorTresTareas
                ? SegundosEntreRevisionesDeVixyEnPenultimo
                : SegundosEntreAvances);
            return;
        }

        // Puppet iba de la boca del ducto a la oficina y le cerraron la
        // compuerta antes de pasarla: vuelve a la boca y espera ahí.
        if (EsPuppet && BloqueadoPorCompuerta && indiceRuta == ruta.Length - 1)
        {
            indiceRuta = ruta.Length - 2;
            if (agent != null && agent.isOnNavMesh && ruta[indiceRuta] != null)
                agent.SetDestination(ruta[indiceRuta].position);
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
                TryMove(deltaTime, 1f);
            else
                TryPuppetExit(deltaTime);
            return;
        }

        if (!usarPatrullaExistente)
            TryMove(deltaTime, ataqueVixyPorTresTareas
                ? Mathf.Max(0.1f, SegundosEntreAvancesDeVixy * factorDeAgresividad)
                : SegundosEntreAvances);
        UpdatePendingTaskThreat(deltaTime);
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
        if (rutaFija != null) rutaFija.Reiniciar();
        retroceso.Reiniciar();
        retrocediendoHacia = -1;
        variacionDelIntervalo = 1f;
        waypointActivo = -1;
        esperandoAtaquePenultimo = false;
        proximoIntentoPenultimo = 0f;
        segundosTareaPendiente.Clear();
        fueraDeCaja = false;
        puppetPercent = 100f;
        detenidoHasta = 0f;
        mantenidoQuieto = false;
        attackSent = false;
        if (agent != null && !usarPatrullaExistente)
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.Warp(initialPosition);
                transform.rotation = initialRotation;
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

    /// Desvíos ocasionales de la ruta fija. `probabilidad` es la de hacer un
    /// desvío en cada tramo que lo tiene (0 = nunca, 1 = siempre).
    public void ConfigurarDesvios(DesvioDeRuta[] lista, float probabilidad)
    {
        desvios = lista ?? new DesvioDeRuta[0];
        probabilidadDeDesvio = Mathf.Clamp01(probabilidad);
        rutaFija = null;
    }

    /// Un punto de la ruta, o de los desvíos (los desvíos se numeran después de la ruta).
    private Transform PuntoDeRuta(int indice)
    {
        if (ruta == null || indice < 0) return null;
        if (indice < ruta.Length) return ruta[indice];
        int d = indice - ruta.Length;
        return desvios != null && d < desvios.Length ? desvios[d].punto : null;
    }

    private RutaFija RutaDeEsteAnimatronico()
    {
        if (rutaFija != null) return rutaFija;
        int[] tras = new int[desvios == null ? 0 : desvios.Length];
        for (int i = 0; i < tras.Length; i++) tras[i] = desvios[i].despuesDe;
        rutaFija = new RutaFija(PenultimateWaypointIndex + 1, ruta.Length, tras);
        return rutaFija;
    }

    public void SetTasks(List<AnimatronicGameCoordinator.TaskState> tasks)
    {
        HashSet<string> current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tasks != null)
        {
            foreach (AnimatronicGameCoordinator.TaskState task in tasks)
            {
                if (task == null || string.IsNullOrEmpty(task.task_type)) continue;
                // Vixy mide el descuido de la tablet entera, así que cuenta
                // cualquier tarea pendiente, no solo las de su propia lista.
                if (ataqueVixyPorTresTareas || OwnsTask(task.task_type))
                    current.Add(task.task_type);
            }
        }

        List<string> removed = new List<string>();
        foreach (string existing in segundosTareaPendiente.Keys)
            if (!current.Contains(existing)) removed.Add(existing);
        foreach (string taskType in removed) segundosTareaPendiente.Remove(taskType);

        foreach (string taskType in current)
            if (!segundosTareaPendiente.ContainsKey(taskType)) segundosTareaPendiente.Add(taskType, 0f);

        // Vixy solo mata desde el penúltimo punto, nunca a distancia: hasta
        // llegar ahí el jugador siempre puede frenarla resolviendo tareas.
        if (activo && ataqueVixyPorTresTareas && esperandoAtaquePenultimo &&
            current.Count >= TareasParaAtacar && !BloqueadoPorCompuerta)
            TriggerAttack("Vixy te alcanzó con las tareas sin resolver");
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
        float intervaloDeEstePaso = interval * variacionDelIntervalo;
        if (movementClock < intervaloDeEstePaso) return;
        movementClock -= intervaloDeEstePaso;
        if (detenerseCuandoMira)
            variacionDelIntervalo = UnityEngine.Random.Range(1f - VariacionDelIntervaloDeFreddy, 1f + VariacionDelIntervaloDeFreddy);

        if (ataqueVixyPorTresTareas)
        {
            // Vixy no usa azar: avanza solo mientras se acumulen sus tareas
            // sin resolver. Bajar de ese umbral la deja clavada donde esté,
            // así el jugador puede frenarla incluso al borde del ataque.
            if (segundosTareaPendiente.Count < TareasParaAvanzar) return;
        }
        else if (!detenerseCuandoMira)
        {
            // Freddy (detenerseCuandoMira) avanza a un ritmo casi fijo (±20 %):
            // mirarlo es la única defensa, así que su tiempo debe ser
            // predecible. El resto conserva el avance probabilístico original.
            float oldestPendingTask = 0f;
            foreach (float age in segundosTareaPendiente.Values)
                oldestPendingTask = Mathf.Max(oldestPendingTask, age);
            int movementLevel = Mathf.Clamp(nivelIA + 4 + Mathf.FloorToInt(oldestPendingTask / 4f), 0, 20);
            if (UnityEngine.Random.Range(0, 21) > movementLevel) return;
        }

        if (usarRutaProbabilistica)
        {
            if (!EnsureAgentOnNavMesh()) return;
            int nextIndex = SelectNextWaypoint();
            if (nextIndex < 0) return;
            Transform probabilisticTarget = PuntoDeRuta(nextIndex);
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
        // El último paso entra a la oficina: con la compuerta cerrada espera.
        if (indiceRuta + 1 == ruta.Length - 1 && BloqueadoPorCompuerta) return;
        indiceRuta++;
        Transform next = ruta[indiceRuta];
        if (next == null) return;

        if (!EnsureAgentOnNavMesh()) return;
        agent.isStopped = false;
        if (!agent.SetDestination(next.position))
            Debug.LogWarning($"{nombreAnimatronico}: no se pudo calcular una ruta a {next.name}.");
    }

    private int PenultimateWaypointIndex => ruta == null ? -1 : ruta.Length - 2;

    /// La compuerta aguantó la fase: vuelve a la entrada del ducto (3 puntos
    /// atrás) y desde ahí sigue su ruta como siempre. Vixy además retrocede su
    /// RutaFija, para que sus siguientes avances partan de ahí.
    private void RetrocederPorLaCompuerta()
    {
        int destino = usarRutaProbabilistica
            ? RutaDeEsteAnimatronico().Retroceder(RetrocesoPorCompuerta.PuntosQueRetrocede)
            : RetrocesoPorCompuerta.PuntoAlQueVuelve(PenultimateWaypointIndex);
        esperandoAtaquePenultimo = false;
        indiceRuta = destino;
        movementClock = 0f;
        Transform punto = PuntoDeRuta(destino);
        if (punto == null || !EnsureAgentOnNavMesh()) return;
        if (usarRutaProbabilistica) waypointActivo = destino;
        agent.isStopped = false;
        Vector3 posicion = posicionesDeRutaNavMesh.TryGetValue(destino, out Vector3 ajustada) ? ajustada : punto.position;
        if (agent.SetDestination(posicion))
            retrocediendoHacia = destino;
        else
            Debug.LogWarning($"{nombreAnimatronico}: no se pudo calcular una ruta de vuelta a {punto.name}.");
    }

    /// Ruta fija: el siguiente punto de la ruta en orden, o a veces un desvío.
    /// Nunca se salta un punto (solo retrocede por la compuerta: RetrocederPorLaCompuerta). Si un punto no tiene malla de
    /// navegación se avisa y se sigue con el siguiente, para no quedarse parado.
    private int SelectNextWaypoint()
    {
        int penultimate = PenultimateWaypointIndex;
        if (penultimate < 0 || ruta[penultimate] == null) return -1;

        RutaFija recorrido = RutaDeEsteAnimatronico();
        float probabilidad = probabilidadDeDesvio;
        for (int intento = 0; intento <= ruta.Length + (desvios == null ? 0 : desvios.Length); intento++)
        {
            int siguiente = recorrido.Siguiente(probabilidad, () => UnityEngine.Random.value);
            if (siguiente < 0) return -1;
            if (PuntoDeRuta(siguiente) != null && CacheWalkableWaypoint(siguiente, out _)) return siguiente;

            Debug.LogWarning($"{nombreAnimatronico}: el punto {(PuntoDeRuta(siguiente) != null ? PuntoDeRuta(siguiente).name : siguiente.ToString())} no está en la malla de navegación.");
            if (recorrido.EsDesvio(siguiente))
            {
                recorrido.AbandonarDesvio();
                probabilidad = 0f;
            }
        }
        return -1;
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
        Transform punto = PuntoDeRuta(index);
        if (punto == null || agent == null || !agent.isOnNavMesh)
            return false;
        if (!NavMesh.SamplePosition(punto.position, out NavMeshHit hit, 2f, agent.areaMask))
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
        Transform punto = PuntoDeRuta(index);
        if (punto == null) return false;
        Vector3 here = transform.position;
        Vector3 target = posicionesDeRutaNavMesh.TryGetValue(index, out Vector3 snappedPosition)
            ? snappedPosition
            : punto.position;
        return HorizontalDistance(here, target) <= Mathf.Max(1.1f, agent != null ? agent.stoppingDistance + 0.5f : 1.1f);
    }

    private void TryPenultimateJumpscare()
    {
        if (detenerseCuandoMira && usaSeguimientoDeMirada && detectorCabeza != null && detectorCabeza.mirandoPC)
        {
            // Mirarlo no solo cancela este intento: reinicia la cuenta, así el
            // jugador recupera el margen completo cada vez que vuelve a mirar.
            proximoIntentoPenultimo = Time.time + SegundosEntreAvances;
            return;
        }

        if (ataqueVixyPorTresTareas)
        {
            // Vixy espera en el penúltimo punto hasta que las tareas sin
            // resolver lleguen al umbral; el azar no decide su ataque.
            // Con la compuerta cerrada no puede salir del ducto: sigue esperando.
            if (segundosTareaPendiente.Count >= TareasParaAtacar && !BloqueadoPorCompuerta)
                TriggerAttack(nombreAnimatronico + " te alcanzó con las tareas sin resolver");
            else
                proximoIntentoPenultimo = Time.time + SegundosEntreRevisionesDeVixyEnPenultimo;
            return;
        }

        // Freddy ya no depende del azar: llegado al penúltimo punto, aguantar
        // sin mirarlo el intervalo completo es la condición de muerte.
        TriggerAttack(nombreAnimatronico + " te alcanzó por dejar de mirar la pantalla");
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right)
    {
        left.y = 0f;
        right.y = 0f;
        return Vector3.Distance(left, right);
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
