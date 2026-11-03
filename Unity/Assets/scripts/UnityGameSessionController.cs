using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Unity is the game authority: it creates tablet tasks and applies their
/// results to animatronics. UnityRelay only forwards WebSocket messages.
/// </summary>
public class UnityGameSessionController : MonoBehaviour
{
    [Serializable] private class Header { public string type; }
    [Serializable] private class ConnectMessage { public string type; public string player_id; public string modo; }
    [Serializable] private class TaskResultMessage { public string type; public int task_id; public bool success = true; }
    [Serializable] private class TutorialSlideMessage { public string type; public int indice; }
    [Serializable] private class TutorialPracticeMessage { public string type; public bool exito; }

    [Serializable] private class TutorialPracticeStatus
    {
        public string type = "tutorial_prueba_estado";
        public bool mirando;
        public bool mirada_completada;
        public bool tarea_completada;
        public bool camara_disponible;
    }

    [Serializable] private class TutorialEstadoMessage
    {
        public string type = "tutorial_estado";
        public int indice;
        public int total;
    }
    [Serializable] private class OutgoingMessage { public string type; }

    [Serializable] public class TaskParameters
    {
        public int num_dials;
        public int[] targets;
        public int tolerance;
    }

    [Serializable] public class TaskData
    {
        public int task_id;
        public string task_type;
        public string description;
        public int duration;
        public int difficulty;
        public long timestamp;
        public string animatronic;
        public TaskParameters task_params = new TaskParameters();
    }

    [Serializable] private class TaskListMessage
    {
        public string type = "task_list";
        public TaskData[] tasks;
        public long timestamp;
    }

    [Serializable] private class NightStatusMessage
    {
        public string type = "night_status";
        public int night;
        public string in_game_time;
        public int risk_percent;
        public long timestamp;
    }

    [Serializable] private class PuppetStatusMessage
    {
        public string type = "estado_puppet";
        public bool en_peligro;
        public int valor_caja_porcentaje;
        public long timestamp;
    }

    // Jugador 2 (operador): ver docs/PROTOCOLO_JUGADOR2.md
    [Serializable] private class AccionOperadorMessage { public string type; public string accion; public string objetivo; }

    [Serializable] private class PuntoDeMapa { public string nombre; public float x; public float z; }

    [Serializable] private class MapaPuntosMessage
    {
        public string type = "mapa_puntos";
        public PuntoDeMapa[] puntos;
        public PuntoDeMapa jugador;
    }

    [Serializable] private class AnimatronicoEnMapa
    {
        public string nombre;
        public float x;
        public float z;
        public float retraso_s;
        public float espera_s;
    }

    [Serializable] private class EstadoMapaMessage
    {
        public string type = "estado_mapa";
        public bool partida_activa;
        public int noche;
        public string hora;
        public AnimatronicoEnMapa[] animatronicos;
        public long timestamp;
    }

    [Serializable] private class EfectoMessage
    {
        public string type = "efecto";
        public string animatronic;
        public string tipo = "ralentizado";
        public float segundos;
        public string message;
    }

    [Serializable] private class PingOperadorMessage { public string type; public long t; }

    [Serializable] private class PongOperadorMessage
    {
        public string type = "pong_operador";
        public long t;
    }

    [Serializable] private class PosicionDeAnimatronico { public string nombre; public float x; public float z; }

    /// 20 veces por segundo: es lo único que la página necesita para dibujar
    /// el movimiento fluido, sin esperar al tick de 0,5 s.
    [Serializable] private class PosicionesMessage
    {
        public string type = "posiciones";
        public long t;
        public PosicionDeAnimatronico[] animatronicos;
        public PuntoDeMapa jugador;
    }

    /// Lo que hace el jugador 1, reflejado en la pantalla del jugador 2.
    [Serializable] private class EstadoJugadorMessage
    {
        public string type = "estado_jugador";
        public bool mirando_pantalla;
        public int tareas_pendientes;
        public int caja_pct;
        public bool caja_en_peligro;
    }

    [Serializable] private class EstadoOperadorMessage
    {
        public string type = "estado_operador";
        public int cargas;
        public int max_cargas;
        public string[] trampas;
        public int progreso;
        public int necesario;
        public bool habilidad_puppet;
        public float ventana_rescate_s;
        public float multa_factor;
    }

    [Serializable] private class AccionRechazadaMessage
    {
        public string type = "accion_rechazada";
        public string objetivo;
        public string motivo;
        public float espera_s;
    }

    [Serializable] private class AttackMessage
    {
        public string type = "attack";
        public string attack_id;
        public int damage = 100;
        public string urgency = "critical";
        public string animatronic;
        public string message;
        public long timestamp;
    }

    [Serializable] private class GameOverMessage
    {
        public string type = "game_over";
        public string result;
        public int night;
        public FinalStats final_stats;
        public long timestamp;
    }

    [Serializable] private class FinalStats
    {
        public int duration;
        public int tasks_completed;
        public int tasks_failed;
        public int total_damage;
        public int score;
    }

    [Header("Conexión")]
    public UnityWebSocketClient websocketClient;
    public AnimatronicGameCoordinator coordinator;
    public HeadOrientationTracker detectorCabeza;
    public GestorSonidoJuego gestorSonido;

    [Header("Actores ya colocados en Face Landmark Detection")]
    public string actorFreddy = "enemigo1";
    public string actorVixy = "enemigo3";
    public string actorPuppet = "personaje2";
    public Transform jugador;

    [Header("Estado de partida")]
    [SerializeField] private bool partidaActiva;
    [SerializeField] private int noche = 1;
    [SerializeField] private int hora = 1;
    [SerializeField] private float valorCajaPuppet = 2000f;

    /// Lo consulta la pantalla de espera para saber si debe taparlo todo.
    public bool PartidaActiva => partidaActiva;

    /// Resultado de la partida que acaba de terminar ("win", "loss" o
    /// "final_victory"), o vacío si no hay ninguno pendiente de mostrar. Se
    /// limpia cuando la tablet avisa que volvió a su menú, no por tiempo:
    /// la pantalla del final queda a la vista mientras el jugador decide.
    public string ResultadoDeLaUltimaPartida { get; private set; } = string.Empty;

    public int NocheDeLaUltimaPartida { get; private set; }

    /// Instante en que el jumpscare deja de verse. La pantalla de derrota
    /// espera hasta entonces para no taparlo.
    public float MomentoEnQueTerminaElJumpscare { get; private set; }

    /// El jugador abrió el tutorial desde la tablet. No hay partida en curso:
    /// la PC solo muestra las diapositivas mientras él las pasa.
    public bool EnTutorial { get; private set; }

    public int IndiceDeDiapositiva { get; private set; }
    public bool TutorialMiradaCompletada => tutorialMiradaCompletada;
    public bool TutorialTareaCompletada => tutorialTareaCompletada;
    public bool TutorialMirandoCamara => detectorCabeza != null && detectorCabeza.mirandoPC;
    public bool TutorialCamaraDisponible => detectorCabeza != null;

    /// Lo informa PantallaDeTutorial al cargar las imágenes, para que la
    /// tablet sepa cuántas hay sin tener el número escrito de su lado.
    public int TotalDeDiapositivas { get; set; }

    [Header("Jumpscare en la ventana de Unity")]
    [SerializeField] private Texture2D desktopJumpscareImage;
    [SerializeField] private AudioClip desktopJumpscareSound;
    [SerializeField, Min(0.1f)] private float desktopJumpscareDuration = 1.6f;
    private bool desktopJumpscareShown;
    private ReproductorDeJumpscare reproductorDeJumpscare;

    private const float TickSeconds = 0.5f;
    // Segundos reales que dura cada noche (2:00, 2:15, 2:30, 2:30, 2:45,
    // 2:45). La noche son 6 horas de juego, así que la escala de tiempo sale
    // de dividir 360 unidades de reloj entre esa duración. Índice = noche - 1.
    private static readonly float[] SegundosRealesPorNoche = { 120f, 135f, 150f, 150f, 165f, 165f };
    private const float MaxMusicBox = 2000f;
    private const float SegundosEntreTareas = 30f;
    private const float ProbabilidadDeWifi = 0.2f;
    private const int MaxNight = 6;
    private const string InitialTask = "cables";
    
    private static readonly Dictionary<string, int> Durations = new Dictionary<string, int>
    {
        { "cables", 30 }, { "dials", 20 }, { "sequence", 25 }, { "rhythm", 15 },
        { "wifi", 15 }, { "temperatura", 20 }, { "ventiladores", 20 },
        { "procesar_datos", 15 }, { "subir_datos", 12 }, { "trazar_curso", 20 }
    };
    
    private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        { "cables", "Conectar los cables del color correcto" }, { "dials", "Girar perillas a posición correcta" },
        { "sequence", "Resolver la secuencia mostrada" }, { "rhythm", "Seguir el ritmo crítico" },
        { "wifi", "Reiniciar WiFi" }, { "temperatura", "Reparar temperatura" },
        { "ventiladores", "Reparar ventiladores" }, { "procesar_datos", "Procesar datos" },
        { "subir_datos", "Subir datos" }, { "trazar_curso", "Trazar curso" }
    };
    
    private static readonly string[] FreddyTypes = { "temperatura", "ventiladores", "cables", "dials", "trazar_curso" };
    private static readonly string[] VixyTypes = { "wifi", "sequence", "rhythm", "procesar_datos", "subir_datos" };
    private static readonly int[] FreddyNightLevels = { 0, 0, 1, 0, 3, 4 };
    private static readonly int[] VixyNightLevels = { 0, 1, 5, 4, 7, 12 };
    private static readonly int[] PuppetNightLevels = { 0, 3, 0, 2, 5, 10 };
    // Ritmo de vaciado de la caja por noche. El drenaje se aplica por unidad
    // de reloj de juego, así que al estirarse la noche (ver
    // SegundosRealesPorNoche) la caja también dura proporcionalmente más en
    // segundos reales; lo que sube de noche en noche es la presión relativa.
    private static readonly int[] PuppetDrainRates = { 13, 15, 17, 19, 22, 27 };

    private readonly List<TaskData> activeTasks = new List<TaskData>();
    private readonly List<AnimatronicUnityBrain> brains = new List<AnimatronicUnityBrain>();
    private readonly HashSet<string> completedTaskTypes = new HashSet<string>();
    private string playerId = "local";
    private bool windingPuppet;
    private float tickClock;
    private float secondsIntoHour;
    private float totalSeconds;
    private int tasksCompleted;
    private int tasksFailed;
    private int taskId = 1;
    private bool puppetEnPeligroPrevio;
    private bool puppetCajaVaciaPrevio;
    private float segundosDesdeUltimaTarea;
    private float tutorialMiradaSegundos;
    private float tutorialEstadoReloj;
    private bool tutorialMiradaCompletada;
    private bool tutorialTareaCompletada;

    // Jugador 2 (operador)
    private readonly FrenoDeOperador freno = new FrenoDeOperador();
    private readonly RecursosDeOperador recursos = new RecursosDeOperador();
    private readonly PresenciaDeOperador presencia = new PresenciaDeOperador();
    private readonly RescateDePuppet rescate = new RescateDePuppet();
    // Las posiciones salen cada 0,05 s (20 veces por segundo).
    private const float SegundosEntrePosiciones = 0.05f;
    private float relojDePosiciones;
    private bool miradaPublicada;
    private string avisoDeOperador = string.Empty;
    private float avisoDeOperadorHasta;
    private GUIStyle estiloDelAviso;

    private void Awake()
    {
        if (websocketClient == null) websocketClient = FindObjectOfType<UnityWebSocketClient>();
        if (coordinator == null) coordinator = FindObjectOfType<AnimatronicGameCoordinator>();
        if (detectorCabeza == null) detectorCabeza = FindObjectOfType<HeadOrientationTracker>();
        if (gestorSonido == null) gestorSonido = FindObjectOfType<GestorSonidoJuego>();
        reproductorDeJumpscare = gameObject.AddComponent<ReproductorDeJumpscare>();
        if (jugador == null && Camera.main != null) jugador = Camera.main.transform;
        ConfigureSceneActors();
    }

    private void Start()
    {
        if (!partidaActiva && gestorSonido != null) gestorSonido.ReproducirMusicaMenu();

        // Solo para probar un jumpscare sin esperar a que ataquen:
        //   FiveNightsOfTerror.x86_64 -jumpscare-prueba freddy
        // Lo muestra a los 4 s de abrir el juego y cierra el juego al terminar.
        string[] argumentos = Environment.GetCommandLineArgs();
        for (int i = 0; i < argumentos.Length - 1; i++)
            if (argumentos[i] == "-jumpscare-prueba")
                StartCoroutine(ProbarJumpscare(argumentos[i + 1]));
    }

    private System.Collections.IEnumerator ProbarJumpscare(string animatronico)
    {
        yield return new WaitForSeconds(4f);
        ShowDesktopJumpscare(animatronico);
        yield return new WaitForSeconds(Mathf.Max(MomentoEnQueTerminaElJumpscare - Time.time, 0f) + 2f);
        Application.Quit();
    }

    private void OnEnable() => UnityWebSocketClient.MessageReceived += OnMessage;
    private void OnDisable() => UnityWebSocketClient.MessageReceived -= OnMessage;

    private void Update()
    {
        if (EnTutorial && IndiceDeDiapositiva == 2)
        {
            bool mirando = TutorialMirandoCamara;
            tutorialMiradaSegundos = mirando ? tutorialMiradaSegundos + Time.deltaTime : 0f;
            if (tutorialMiradaSegundos >= 2f) tutorialMiradaCompletada = true;
            tutorialEstadoReloj += Time.deltaTime;
            if (tutorialEstadoReloj >= 0.5f)
            {
                tutorialEstadoReloj = 0f;
                PublicarEstadoDePrueba();
            }
        }
        // La estática es del juego en curso: sin partida (espera, tutorial,
        // resultado o la X de la tablet) no debe sonar, sea cual sea el camino.
        if (!partidaActiva && gestorSonido != null) gestorSonido.DetenerEstatica();
        if (!partidaActiva) return;
        foreach (AnimatronicUnityBrain brain in brains)
            brain.FijarFactorDeVelocidad(freno.FactorDeVelocidad(brain.nombreAnimatronico, Time.time));
        PublicarAlJugador2EnCadaCuadro();
        tickClock += Time.deltaTime;
        while (tickClock >= TickSeconds)
        {
            tickClock -= TickSeconds;
            TickGame();
        }
    }

    private void OnDrawGizmos()
    {
        string[] routeMarkers =
        {
            "E1_punto1", "E1_punto2", "E1_punto3", "E1_punto4",
            "E2_punto1", "E2_punto2", "E2_punto3", "E2_punto4",
            "punto_penultimo_jumpscare"
        };
        foreach (string markerName in routeMarkers)
        {
            GameObject marker = GameObject.Find(markerName);
            if (marker == null) continue;
            bool penultimate = markerName == "punto_penultimo_jumpscare";
            Gizmos.color = penultimate ? Color.red : (markerName.StartsWith("E2_") ? Color.cyan : Color.yellow);
            Gizmos.DrawSphere(marker.transform.position, penultimate ? 0.35f : 0.22f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(marker.transform.position + Vector3.up * 0.35f, markerName);
#endif
        }
    }

    private void ConfigureSceneActors()
    {
        Transform[] routeFreddy = null;
        GameObject freddyObject = GameObject.Find(actorFreddy);
        if (freddyObject != null)
        {
            PatrullaPorPuntos existingPatrol = freddyObject.GetComponent<PatrullaPorPuntos>();
            routeFreddy = FindRoute("E1_punto1", "E1_punto2", "E1_punto3", "E1_punto4",
                "E2_punto1", "E2_punto2", "E2_punto3", "E2_punto4", "punto_penultimo_jumpscare");
            routeFreddy = AppendPlayerEndpoint(routeFreddy);
            if (existingPatrol != null)
            {
                existingPatrol.controladoPorIAExterna = true;
            }
            AttachBrain(freddyObject, "Freddy", routeFreddy, true, true, false,
                probabilisticRoute: true, vixyTaskThreshold: false);
        }

        GameObject vixyObject = GameObject.Find(actorVixy);
        if (vixyObject != null)
            AttachBrain(vixyObject, "Vixy", AppendPlayerEndpoint(FindRoute(
                "E1_punto1", "E1_punto2", "E1_punto3", "E1_punto4",
                "E2_punto1", "E2_punto2", "E2_punto3", "E2_punto4", "punto_penultimo_jumpscare")),
                true, false, false, probabilisticRoute: true, vixyTaskThreshold: true);

        GameObject puppetObject = GameObject.Find(actorPuppet);
        if (puppetObject != null)
            AttachBrain(puppetObject, "Puppet", FindRoute("E1_punto2", "E1_punto1", jugador != null ? jugador.name : "Main Camera"), false, false, true);
    }

    private AnimatronicUnityBrain AttachBrain(GameObject actor, string role, Transform[] route, bool gaze,
        bool freezeOnLook, bool puppet, bool probabilisticRoute = false, bool vixyTaskThreshold = false)
    {
        if (actor == null) return null;
        AnimatronicUnityBrain brain = actor.GetComponent<AnimatronicUnityBrain>();
        if (brain == null) brain = actor.AddComponent<AnimatronicUnityBrain>();
        brain.Configurar(role, route, gaze, freezeOnLook, puppet, false,
            probabilisticRoute, vixyTaskThreshold);
        brain.alAtacarJugador.AddListener(OnAnimatronicAttack);
        if (!brains.Contains(brain)) brains.Add(brain);
        return brain;
    }

    private static Transform[] FindRoute(params string[] names)
    {
        List<Transform> route = new List<Transform>();
        foreach (string objectName in names)
        {
            if (string.IsNullOrEmpty(objectName)) continue;
            GameObject point = GameObject.Find(objectName);
            if (point != null && !route.Contains(point.transform)) route.Add(point.transform);
        }
        return route.ToArray();
    }

    private Transform[] AppendPlayerEndpoint(Transform[] route)
    {
        List<Transform> complete = route == null ? new List<Transform>() : new List<Transform>(route);
        if (jugador != null && !complete.Contains(jugador)) complete.Add(jugador);
        return complete.ToArray();
    }

    private void OnMessage(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            Header header = JsonUtility.FromJson<Header>(json);
            if (header == null) return;
            switch (header.type)
            {
                case "connect": StartGame(JsonUtility.FromJson<ConnectMessage>(json)); break;
                case "task_completed":
                case "task_failed": ResolveTask(JsonUtility.FromJson<TaskResultMessage>(json), header.type == "task_failed"); break;
                case "dar_cuerda_inicio":
                    windingPuppet = true;
                    if (gestorSonido != null) gestorSonido.ReproducirCuerdaCajaMusica();
                    break;
                case "dar_cuerda_fin": windingPuppet = false; break;
                case "disconnect":
                    // El jugador volvió al menú de la tablet: recién ahí se
                    // retira la pantalla del resultado anterior.
                    partidaActiva = false;
                    ResultadoDeLaUltimaPartida = string.Empty;
                    EnTutorial = false;
                    if (gestorSonido != null) gestorSonido.ReproducirMusicaMenu();
                    break;
                case "tutorial_abrir":
                    EnTutorial = true;
                    IndiceDeDiapositiva = 0;
                    ReiniciarPruebaDeTutorial();
                    PublicarEstadoDeTutorial();
                    break;
                case "tutorial_slide":
                    IndiceDeDiapositiva = JsonUtility.FromJson<TutorialSlideMessage>(json).indice;
                    PublicarEstadoDeTutorial();
                    if (IndiceDeDiapositiva == 2) PublicarEstadoDePrueba();
                    break;
                case "tutorial_prueba_tarea":
                    if (EnTutorial && IndiceDeDiapositiva == 2 &&
                        JsonUtility.FromJson<TutorialPracticeMessage>(json).exito)
                    {
                        tutorialTareaCompletada = true;
                        PublicarEstadoDePrueba();
                    }
                    break;
                case "tutorial_cerrar":
                    EnTutorial = false;
                    break;
                case "operador_connect":
                    // Un jugador 2 se unió (o volvió a mitad de noche): se le
                    // manda el mapa y todos los estados de golpe.
                    presencia.Latir(Time.time);
                    PublicarPuntosDelMapa();
                    PublicarEstadoDelMapa();
                    PublicarEstadoJugador();
                    PublicarEstadoOperador();
                    break;
                case "ping_operador":
                    presencia.Latir(Time.time);
                    Send(new PongOperadorMessage { t = JsonUtility.FromJson<PingOperadorMessage>(json).t });
                    break;
                case "accion_operador":
                    AtenderAccionDelOperador(JsonUtility.FromJson<AccionOperadorMessage>(json));
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("No se pudo procesar un mensaje de juego: " + exception.Message);
        }
    }

    /// Le confirma a la tablet en qué diapositiva está y cuántas hay, para
    /// que pueda desactivar "Anterior"/"Siguiente" en los extremos.
    private void PublicarEstadoDeTutorial()
    {
        if (TotalDeDiapositivas > 0)
            IndiceDeDiapositiva = Mathf.Clamp(IndiceDeDiapositiva, 0, TotalDeDiapositivas - 1);

        Send(new TutorialEstadoMessage
        {
            indice = IndiceDeDiapositiva,
            total = TotalDeDiapositivas,
        });
    }

    private void ReiniciarPruebaDeTutorial()
    {
        tutorialMiradaSegundos = 0f;
        tutorialEstadoReloj = 0f;
        tutorialMiradaCompletada = false;
        tutorialTareaCompletada = false;
    }

    private void PublicarEstadoDePrueba()
    {
        Send(new TutorialPracticeStatus
        {
            mirando = TutorialMirandoCamara,
            mirada_completada = tutorialMiradaCompletada,
            tarea_completada = tutorialTareaCompletada,
            camara_disponible = TutorialCamaraDisponible
        });
    }

    private void StartGame(ConnectMessage message)
    {
        if (message == null) return;

        // "Salir" en el menú de la tablet: cierra el juego de la PC. Viaja como
        // `connect` porque el relay necesita ese mensaje para reconocer a la
        // tablet; no es una partida. (En el editor Application.Quit no hace nada.)
        if (message.modo == "salir")
        {
            Application.Quit();
            return;
        }

        // El tutorial se conecta con el mismo mensaje `connect` (el relay lo
        // necesita para asignarle el rol de tablet y reenviar lo que mande),
        // pero no debe arrancar ninguna noche.
        if (message.modo == "tutorial")
        {
            if (gestorSonido != null) gestorSonido.DetenerEstatica();
            EnTutorial = true;
            IndiceDeDiapositiva = 0;
            ReiniciarPruebaDeTutorial();
            PublicarEstadoDeTutorial();
            return;
        }

        playerId = string.IsNullOrEmpty(message.player_id) ? "local" : message.player_id;
        string key = "ihc_next_night_" + playerId;
        noche = message.modo == "continuar" ? Mathf.Clamp(PlayerPrefs.GetInt(key, 1), 1, MaxNight) : 1;
        PlayerPrefs.SetInt(key, noche);
        hora = 1;
        secondsIntoHour = 0f;
        totalSeconds = 0f;
        tickClock = 0f;
        tasksCompleted = 0;
        tasksFailed = 0;
        activeTasks.Clear();
        completedTaskTypes.Clear();
        windingPuppet = false;
        valorCajaPuppet = MaxMusicBox;
        puppetEnPeligroPrevio = false;
        puppetCajaVaciaPrevio = false;
        segundosDesdeUltimaTarea = 0f;
        partidaActiva = true;
        desktopJumpscareShown = false;
        ResultadoDeLaUltimaPartida = string.Empty;
        if (gestorSonido != null)
        {
            gestorSonido.DetenerMusica();
            gestorSonido.IniciarEstativa();
        }
        if (coordinator != null)
        {
            coordinator.partidaFinalizada = false;
            coordinator.noche = noche;
            coordinator.horaEnJuego = GetInGameTime();
            coordinator.porcentajeCajaPuppet = 100f;
            coordinator.puppetEnPeligro = false;
        }

        freno.Reiniciar();
        recursos.Reiniciar();
        rescate.Reiniciar();
        miradaPublicada = false;
        foreach (AnimatronicUnityBrain brain in brains)
        {
            brain.FijarFactorDeVelocidad(1f);
            brain.BeginSession(noche, GetInGameTime());
            brain.SetPuppetState(100f, false);
        }
        PublicarPuntosDelMapa();

        CreateTask(InitialTask, "Freddy");
        PublishState();
        PublishTaskList();
    }

    /// Unidades de reloj de juego por segundo real. Una noche son 6 horas de
    /// 60 unidades; repartirlas en la duración de la noche actual hace que
    /// las noches largas transcurran más despacio en vez de durar más horas.
    private float EscalaDeTiempoDeLaNoche =>
        360f / SegundosRealesPorNoche[Mathf.Clamp(noche - 1, 0, SegundosRealesPorNoche.Length - 1)];

    private void TickGame()
    {
        totalSeconds += TickSeconds;
        float gameDelta = TickSeconds * EscalaDeTiempoDeLaNoche;
        secondsIntoHour += gameDelta;

        int drain = PuppetDrainRates[Mathf.Clamp(noche - 1, 0, PuppetDrainRates.Length - 1)];
        float signedRate = windingPuppet ? drain * 2f : -drain;
        valorCajaPuppet = Mathf.Clamp(valorCajaPuppet + signedRate * gameDelta, 0f, MaxMusicBox);

        bool puppetEnPeligroAhora = valorCajaPuppet < MaxMusicBox * 0.2f;
        if (puppetEnPeligroAhora && !puppetEnPeligroPrevio && gestorSonido != null)
            gestorSonido.ReproducirPeligro();
        puppetEnPeligroPrevio = puppetEnPeligroAhora;

        bool puppetCajaVaciaAhora = valorCajaPuppet <= 0f;
        if (puppetCajaVaciaAhora && !puppetCajaVaciaPrevio && gestorSonido != null)
            gestorSonido.ReproducirPopGoesTheWeasel();
        puppetCajaVaciaPrevio = puppetCajaVaciaAhora;

        // Las tareas ya no dependen del cambio de hora: aparecen en su propio
        // ritmo para que el jugador tenga tiempo de resolverlas.
        segundosDesdeUltimaTarea += TickSeconds;
        if (segundosDesdeUltimaTarea >= SegundosEntreTareas)
        {
            segundosDesdeUltimaTarea -= SegundosEntreTareas;
            TryGenerateNightTasks();
        }

        if (secondsIntoHour >= 60f)
        {
            secondsIntoHour -= 60f;
            if (hora < 6)
            {
                hora++;
                foreach (AnimatronicUnityBrain brain in brains) brain.SetNight(noche, GetInGameTime());
            }
            else
            {
                // Reaching 6 a.m. means the player survived this night.
                FinishGame(true, false, null);
                return;
            }
        }

        foreach (AnimatronicUnityBrain brain in brains)
            if (brain.EsPuppet) brain.SetPuppetState(100f * valorCajaPuppet / MaxMusicBox, puppetEnPeligroAhora);

        if (coordinator != null)
        {
            coordinator.noche = noche;
            coordinator.horaEnJuego = GetInGameTime();
            coordinator.porcentajeCajaPuppet = 100f * valorCajaPuppet / MaxMusicBox;
            coordinator.puppetEnPeligro = puppetEnPeligroAhora;
        }

        PublishState();
        PublishPuppetState();
        PublicarEstadoDelMapa();
        PublicarEstadoJugador();
        PublicarEstadoOperador();
    }

    private void TryGenerateNightTasks()
    {
        // Keep at least one task for each threat when a new in-game hour starts.
        TryGenerateFor("Freddy", FreddyTypes);
        TryGenerateFor("Vixy", VixyTypes);
    }

    private void TryGenerateFor(string owner, string[] candidates)
    {
        List<string> available = new List<string>();
        foreach (string type in candidates)
        {
            if (HasTask(type)) continue;
            if (owner == "Vixy")
            {
                // Romper el WiFi corta la cadena procesar -> subir, así que
                // es un estorbo puntual, no una tarea rutinaria.
                if (type == "wifi" && UnityEngine.Random.value > ProbabilidadDeWifi) continue;
                if (type == "subir_datos" && !completedTaskTypes.Contains("procesar_datos")) continue;
                if (type == "subir_datos" && HasTask("wifi")) continue;
            }
            available.Add(type);
        }
        if (available.Count == 0) return;
        // Prefer a task not already completed this night to keep the task list
        // varied; once the pool is exhausted, allow tasks to repeat.
        List<string> fresh = available.FindAll(type => !completedTaskTypes.Contains(type));
        List<string> pool = fresh.Count > 0 ? fresh : available;
        CreateTask(pool[UnityEngine.Random.Range(0, pool.Count)], owner);
    }

    private void CreateTask(string type, string owner)
    {
        if (!Durations.ContainsKey(type) || HasTask(type)) return;
        TaskData task = new TaskData
        {
            task_id = taskId++,
            task_type = type,
            description = Descriptions[type],
            duration = Durations[type],
            difficulty = noche,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            animatronic = owner,
            task_params = CreateParameters(type)
        };
        activeTasks.Add(task);
        // WiFi breaking cancels Vixy's dependent upload task.
        if (type == "wifi")
        {
            int cancelled = activeTasks.RemoveAll(active => active.task_type == "subir_datos");
            if (cancelled > 0 && coordinator != null)
                coordinator.alRetirarTarea.Invoke("subir_datos");
        }
        SyncTasksToBrains();
        if (coordinator != null) coordinator.alRecibirTarea.Invoke(type);
        PublishTaskList();
    }

    private TaskParameters CreateParameters(string type)
    {
        TaskParameters parameters = new TaskParameters();
        if (type == "dials")
        {
            parameters.num_dials = noche >= 4 ? 3 : 2;
            parameters.tolerance = noche >= 4 ? 10 : 15;
            parameters.targets = new int[parameters.num_dials];
            for (int i = 0; i < parameters.targets.Length; i++)
                parameters.targets[i] = UnityEngine.Random.Range(0, 12) * 30;
        }
        return parameters;
    }

    private void ResolveTask(TaskResultMessage result, bool explicitlyFailed)
    {
        if (result == null) return;
        int index = activeTasks.FindIndex(task => task.task_id == result.task_id);
        if (index < 0) return;
        TaskData resolved = activeTasks[index];
        activeTasks.RemoveAt(index);
        bool success = !explicitlyFailed && result.success;
        if (success) tasksCompleted++;
        else tasksFailed++;
        if (success) completedTaskTypes.Add(resolved.task_type);

        SyncTasksToBrains();
        if (coordinator != null) coordinator.alRetirarTarea.Invoke(resolved.task_type);

        if (activeTasks.Count == 0)
        {
            List<string> allTypes = new List<string>();
            allTypes.AddRange(FreddyTypes);
            foreach (string type in VixyTypes)
            {
                if (type == "wifi" && UnityEngine.Random.value > ProbabilidadDeWifi) continue;
                if (type == "subir_datos" && !completedTaskTypes.Contains("procesar_datos")) continue;
                allTypes.Add(type);
            }
            List<string> fresh = allTypes.FindAll(type => !completedTaskTypes.Contains(type));
            List<string> pool = fresh.Count > 0 ? fresh : allTypes;
            string nextType = pool[UnityEngine.Random.Range(0, pool.Count)];
            CreateTask(nextType,
                Array.Exists(FreddyTypes, type => type == nextType) ? "Freddy" : "Vixy");
        }
        else PublishTaskList();
    }

    private void SyncTasksToBrains()
    {
        foreach (AnimatronicUnityBrain brain in brains)
            brain.SetTasks(ToCoordinatorTasks());
        if (coordinator != null) coordinator.tareasActivas = ToCoordinatorTasks();
    }

    private List<AnimatronicGameCoordinator.TaskState> ToCoordinatorTasks()
    {
        List<AnimatronicGameCoordinator.TaskState> result = new List<AnimatronicGameCoordinator.TaskState>();
        foreach (TaskData task in activeTasks)
        {
            result.Add(new AnimatronicGameCoordinator.TaskState
            {
                task_id = task.task_id,
                task_type = task.task_type,
                description = task.description,
                duration = task.duration,
                difficulty = task.difficulty
            });
        }
        return result;
    }

    private bool HasTask(string type) => activeTasks.Exists(task => task.task_type == type);

    private void OnAnimatronicAttack(string message)
    {
        if (!partidaActiva) return;
        string name = "Animatrónico";
        foreach (AnimatronicUnityBrain brain in brains)
            if (brain != null && brain.enabled && message.StartsWith(brain.nombreAnimatronico, StringComparison.OrdinalIgnoreCase))
            { name = brain.nombreAnimatronico; break; }
        ShowDesktopJumpscare(name);

        Send(new AttackMessage
        {
            attack_id = "atk_" + Mathf.RoundToInt(totalSeconds * 1000f),
            animatronic = name,
            message = string.IsNullOrEmpty(message) ? name + " te encontró" : message,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        FinishGame(false, true, name);
    }

    private void ShowDesktopJumpscare(string animatronico)
    {
        if (desktopJumpscareShown) return;
        desktopJumpscareShown = true;
        if (gestorSonido != null) gestorSonido.InterrumpirEfectosPorJumpscare();

        Texture2D image = desktopJumpscareImage != null
            ? desktopJumpscareImage
            : Resources.Load<Texture2D>("desktop_jumpscare");
        AudioClip sound = desktopJumpscareSound != null
            ? desktopJumpscareSound
            : Resources.Load<AudioClip>("desktop_jumpscare");

        GameObject overlay = new GameObject(
            "DesktopJumpscareCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(AudioSource));

        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        RawImage imagenFija = null;
        if (image != null)
        {
            GameObject imageObject = new GameObject("JumpscareImage", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(canvas.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            RawImage rawImage = imageObject.GetComponent<RawImage>();
            imagenFija = rawImage;
            rawImage.texture = image;
            rawImage.color = Color.white;
            rawImage.raycastTarget = true;
        }
        else
        {
            Debug.LogError("No se encontró Resources/desktop_jumpscare.jpg para el jumpscare de Unity.");
        }

        // El video del animatrónico va encima de la imagen. Si no hay video
        // (falta el archivo o no se pudo preparar) queda la imagen, como antes.
        float duracionDelVideo = 0f;
        if (reproductorDeJumpscare != null &&
            reproductorDeJumpscare.TryReproducir(animatronico, out RenderTexture texturaDelVideo, out duracionDelVideo))
        {
            GameObject videoObject = new GameObject("JumpscareVideo", typeof(RectTransform), typeof(RawImage));
            videoObject.transform.SetParent(canvas.transform, false);
            RectTransform rectVideo = videoObject.GetComponent<RectTransform>();
            rectVideo.anchorMin = Vector2.zero;
            rectVideo.anchorMax = Vector2.one;
            rectVideo.offsetMin = Vector2.zero;
            rectVideo.offsetMax = Vector2.zero;

            RawImage rawVideo = videoObject.GetComponent<RawImage>();
            rawVideo.texture = texturaDelVideo;
            rawVideo.uvRect = ReproductorDeJumpscare.RecorteParaLaPantalla();
            rawVideo.color = Color.white;
            rawVideo.raycastTarget = false;

            // Con video, el fondo es negro: si quedara la imagen fija, se vería
            // en cuanto el clip termine y antes de la pantalla de derrota.
            if (imagenFija != null)
            {
                imagenFija.texture = null;
                imagenFija.color = Color.black;
            }
        }

        AudioSource audioSource = overlay.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (sound != null)
            audioSource.PlayOneShot(sound);
        else
            Debug.LogError("No se encontró Resources/desktop_jumpscare.ogg para el jumpscare de Unity.");

        float duracionDelSonido = sound != null ? sound.length : 0f;
        float visibleDuration = duracionDelVideo > 0f
            ? Mathf.Max(duracionDelSonido, duracionDelVideo)
            : Mathf.Max(desktopJumpscareDuration, duracionDelSonido);
        MomentoEnQueTerminaElJumpscare = Time.time + visibleDuration;
        Destroy(overlay, visibleDuration);
    }

    private void FinishGame(bool won, bool died, string attacker)
    {
        if (!partidaActiva) return;
        partidaActiva = false;
        foreach (AnimatronicUnityBrain brain in brains) brain.Detener();

        if (gestorSonido != null)
        {
            gestorSonido.DetenerEstatica();
            if (won) gestorSonido.ReproducirCampanas6am();
            gestorSonido.ReproducirMusicaMenu();
        }

        string result = died ? "loss" : "win";
        string key = "ihc_next_night_" + playerId;
        if (won && noche >= MaxNight)
        {
            result = "final_victory";
            PlayerPrefs.SetInt(key, 1);
        }
        else if (won) PlayerPrefs.SetInt(key, noche + 1);
        else PlayerPrefs.SetInt(key, noche);
        PlayerPrefs.Save();

        Send(new GameOverMessage
        {
            result = result,
            night = noche,
            final_stats = new FinalStats
            {
                duration = Mathf.RoundToInt(totalSeconds),
                tasks_completed = tasksCompleted,
                tasks_failed = tasksFailed,
                total_damage = died ? 100 : 0,
                score = tasksCompleted * 100
            },
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        ResultadoDeLaUltimaPartida = result;
        NocheDeLaUltimaPartida = noche;
        if (coordinator != null)
        {
            coordinator.partidaFinalizada = true;
            coordinator.alFinalizarPartida.Invoke(result);
        }
    }

    /// Rutas de los animatrónicos y posición del jugador: es el fondo del
    /// minimapa del jugador 2, que solo necesita pedirlo una vez.
    private void PublicarPuntosDelMapa()
    {
        var puntos = new List<PuntoDeMapa>();
        var vistos = new HashSet<Transform>();
        foreach (AnimatronicUnityBrain brain in brains)
        {
            if (brain.ruta == null) continue;
            foreach (Transform punto in brain.ruta)
            {
                if (punto == null || punto == jugador || !vistos.Add(punto)) continue;
                puntos.Add(new PuntoDeMapa { nombre = punto.name, x = punto.position.x, z = punto.position.z });
            }
        }
        Send(new MapaPuntosMessage
        {
            puntos = puntos.ToArray(),
            jugador = jugador == null ? null
                : new PuntoDeMapa { nombre = "jugador", x = jugador.position.x, z = jugador.position.z }
        });
    }

    private void PublicarEstadoDelMapa()
    {
        var animatronicos = new List<AnimatronicoEnMapa>();
        foreach (AnimatronicUnityBrain brain in brains)
        {
            Vector3 posicion = brain.transform.position;
            animatronicos.Add(new AnimatronicoEnMapa
            {
                nombre = brain.nombreAnimatronico.ToLowerInvariant(),
                x = posicion.x,
                z = posicion.z,
                retraso_s = freno.SegundosDeRetraso(brain.nombreAnimatronico, Time.time),
                espera_s = freno.SegundosDeEspera(brain.nombreAnimatronico, Time.time)
            });
        }
        Send(new EstadoMapaMessage
        {
            partida_activa = partidaActiva,
            noche = noche,
            hora = GetInGameTime(),
            animatronicos = animatronicos.ToArray(),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    /// Lo que el jugador 2 necesita ver al instante: las posiciones a 20 Hz y
    /// un cambio en la mirada del jugador 1 sin esperar al tick.
    private void PublicarAlJugador2EnCadaCuadro()
    {
        if (!presencia.EstaVivo(Time.time)) return;
        bool mirando = detectorCabeza != null && detectorCabeza.mirandoPC;
        if (mirando != miradaPublicada)
        {
            miradaPublicada = mirando;
            PublicarEstadoJugador();
        }
        relojDePosiciones += Time.deltaTime;
        if (relojDePosiciones < SegundosEntrePosiciones) return;
        relojDePosiciones -= SegundosEntrePosiciones;
        if (relojDePosiciones > SegundosEntrePosiciones) relojDePosiciones = 0f;   // no recuperar de golpe tras un tirón
        PublicarPosiciones();
    }

    private void PublicarPosiciones()
    {
        var animatronicos = new List<PosicionDeAnimatronico>();
        foreach (AnimatronicUnityBrain brain in brains)
        {
            Vector3 posicion = brain.transform.position;
            animatronicos.Add(new PosicionDeAnimatronico
            {
                nombre = brain.nombreAnimatronico.ToLowerInvariant(),
                x = posicion.x,
                z = posicion.z
            });
        }
        Send(new PosicionesMessage
        {
            t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            animatronicos = animatronicos.ToArray(),
            jugador = jugador == null ? new PuntoDeMapa { nombre = "jugador" }
                : new PuntoDeMapa { nombre = "jugador", x = jugador.position.x, z = jugador.position.z }
        });
    }

    private void PublicarEstadoJugador()
    {
        if (!presencia.EstaVivo(Time.time)) return;
        Send(new EstadoJugadorMessage
        {
            mirando_pantalla = detectorCabeza != null && detectorCabeza.mirandoPC,
            tareas_pendientes = activeTasks.Count,
            caja_pct = Mathf.RoundToInt(100f * valorCajaPuppet / MaxMusicBox),
            caja_en_peligro = valorCajaPuppet < MaxMusicBox * 0.2f
        });
    }

    private void PublicarEstadoOperador()
    {
        if (!presencia.EstaVivo(Time.time)) return;
        Send(new EstadoOperadorMessage
        {
            cargas = recursos.Cargas,
            max_cargas = RecursosDeOperador.MaxCargas,
            trampas = new List<string>(recursos.Trampas).ToArray(),
            progreso = recursos.Progreso,
            necesario = RecursosDeOperador.MiniTareasParaHabilidad,
            habilidad_puppet = recursos.HabilidadPuppet,
            ventana_rescate_s = rescate.SegundosRestantes(Time.time),
            multa_factor = rescate.FactorDeDescarga
        });
    }

    /// Unity decide: valida la acción del jugador 2 con `FrenoDeOperador`, la
    /// aplica y avisa a todos. El navegador solo pide.
    private void AtenderAccionDelOperador(AccionOperadorMessage accion)
    {
        if (accion == null || accion.accion != "retrasar") return;
        string objetivo = FrenoDeOperador.Normalizar(accion.objetivo);
        if (!partidaActiva)
        {
            Send(new AccionRechazadaMessage { objetivo = accion.objetivo, motivo = "sin_partida" });
            return;
        }

        FrenoDeOperador.Resultado resultado = freno.Intentar(accion.objetivo, Time.time);
        if (resultado == FrenoDeOperador.Resultado.Aceptada)
        {
            string nombre = char.ToUpperInvariant(objetivo[0]) + objetivo.Substring(1);
            string aviso = nombre + " se ralentizó";
            avisoDeOperador = aviso;
            avisoDeOperadorHasta = Time.time + 3f;
            Send(new EfectoMessage
            {
                animatronic = objetivo,
                segundos = FrenoDeOperador.DuracionSegundos,
                message = aviso
            });
            PublicarEstadoDelMapa();
            return;
        }

        Send(new AccionRechazadaMessage
        {
            objetivo = accion.objetivo,
            motivo = resultado == FrenoDeOperador.Resultado.EnEnfriamiento ? "enfriamiento" : "desconocido",
            espera_s = objetivo == null ? 0f : freno.SegundosDeEspera(objetivo, Time.time)
        });
    }

    /// Aviso en la pantalla del jugador 1: el otro jugador hizo algo.
    private void OnGUI()
    {
        if (!partidaActiva || Time.time >= avisoDeOperadorHasta || string.IsNullOrEmpty(avisoDeOperador)) return;
        if (estiloDelAviso == null)
            estiloDelAviso = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        Rect zona = new Rect(0f, Screen.height * 0.08f, Screen.width, 60f);
        estiloDelAviso.normal.textColor = Color.black;
        GUI.Label(new Rect(zona.x + 2f, zona.y + 2f, zona.width, zona.height), avisoDeOperador, estiloDelAviso);
        estiloDelAviso.normal.textColor = new Color(1f, 0.85f, 0.3f);
        GUI.Label(zona, avisoDeOperador, estiloDelAviso);
    }

    private void PublishState()
    {
        int displayHour = hora == 1 ? 12 : hora - 1;
        int minute = Mathf.Clamp(Mathf.FloorToInt(secondsIntoHour), 0, 59);
        Send(new NightStatusMessage
        {
            night = noche,
            in_game_time = $"{displayHour}:{minute:00} AM",
            risk_percent = 0,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    private void PublishPuppetState()
    {
        Send(new PuppetStatusMessage
        {
            en_peligro = valorCajaPuppet < MaxMusicBox * 0.2f,
            valor_caja_porcentaje = Mathf.RoundToInt(100f * valorCajaPuppet / MaxMusicBox),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    private void PublishTaskList()
    {
        if (coordinator != null) coordinator.tareasActivas = ToCoordinatorTasks();
        Send(new TaskListMessage
        {
            tasks = activeTasks.ToArray(),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    private string GetInGameTime()
    {
        int displayHour = hora == 1 ? 12 : hora - 1;
        return $"{displayHour}:{Mathf.Clamp(Mathf.FloorToInt(secondsIntoHour), 0, 59):00} AM";
    }

    private void Send(object payload)
    {
        if (websocketClient != null) websocketClient.SendJsonMessage(JsonUtility.ToJson(payload));
    }
}
