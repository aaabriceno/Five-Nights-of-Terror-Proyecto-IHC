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
        /// 1 = la caja se descarga a su ritmo; 2 = al doble (un rescate), 4...
        public float multa_factor = 1f;
        /// Lo que dura ahora la caja llena, en segundos (30 sin rescates, luego 15, 7,5...).
        public float segundos_caja = 30f;
        public long timestamp;
    }

    // Jugador 2 (operador): ver docs/PROTOCOLO_JUGADOR2.md
    [Serializable] private class AccionOperadorMessage { public string type; public string accion; public string objetivo; public string punto; public int id; }

    // Las tareas de la terminal del jugador 2 (ver TareasDeOperador).
    [Serializable] private class TareaPedirMessage { public string type; public string tipo; }
    [Serializable] private class TareaRespuestaMessage { public string type; public int id; public int valor; public int[] interruptores; }
    [Serializable] private class PuertaMessage { public string id; public string op; public string a; public string b; }
    [Serializable] private class TareaMessage
    {
        public string type = "tarea";
        public int id;
        public string tipo;          // "calculo" o "circuito"
        public int noche;
        public string clase;
        public string enunciado;
        public string[] datos;
        public string[] entradas;
        public PuertaMessage[] puertas;
        public string salida;
        public bool objetivo;
    }
    [Serializable] private class TareaResultadoMessage
    {
        public string type = "tarea_resultado";
        public int id;
        public string tipo;
        public bool correcto;
        public float bloqueo_s;
    }

    [Serializable] private class PuntoDeMapa { public string nombre; public float x; public float z; }

    /// El camino fijo de un animatrónico, punto por punto: la guía del jugador 2.
    /// Los desvíos ocasionales no van aquí: son la sorpresa.
    [Serializable] private class RutaDeAnimatronico { public string animatronico; public string[] puntos; }

    /// Una pieza del plano 2D (piso, muro, tarima, mesa, caja): su rectángulo en
    /// el plano XZ, en metros. Ver PlanoDelMapa.
    [Serializable] private class PiezaDelPlano
    {
        public string tipo;
        public string etiqueta;
        public float x0;
        public float z0;
        public float x1;
        public float z1;
    }

    [Serializable] private class MapaPuntosMessage
    {
        public string type = "mapa_puntos";
        public PuntoDeMapa[] puntos;
        public PuntoDeMapa jugador;
        public RutaDeAnimatronico[] rutas;
        /// El plano del edificio (salas, pasillos, ductos, muros), sacado de la geometría real.
        public PiezaDelPlano[] plano;
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
        public int id;
        /// Solo en `puppet_rescatado`: cuántas veces más rápido se descarga la
        /// caja desde ahora. 0 en los demás efectos.
        public float multa_factor;
        /// Solo en `puppet_rescatado`: lo que dura ahora la caja llena (15, 7,5...). 0 en los demás efectos.
        public float segundos_caja;
    }

    [Serializable] private class PingOperadorMessage { public string type; public long t; }

    /// De la tablet del jugador 1: -1 mantiene apretado «izquierda», 1 «derecha», 0 soltó.
    [Serializable] private class CamaraMessage { public string type; public int direccion; }

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
        /// Hacia dónde mira la cámara del jugador 1, en grados: 0 al frente, negativo a la izquierda.
        public float camara_yaw;
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
        public float bloqueo_s;      // lo que falta para que la terminal vuelva a entregar tareas tras un error
        public float ventana_rescate_s;
        public float multa_factor;
        public float segundos_caja = 30f;
    }

    [Serializable] private class AccionRechazadaMessage
    {
        public string type = "accion_rechazada";
        public string objetivo;
        public string motivo;
        public float espera_s;
        public int id;
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
    /// El objeto de la escena que contiene el modelo del edificio (para el plano del jugador 2).
    public string objetoDelMapa = "Mapa_V2";
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

    // Opciones solo para las pruebas automáticas (pruebas/probar_vixy_y_puppet_con_juego.py).
    // Sin ellas en la línea de comandos el juego es el de siempre:
    //   -prueba-sin-freddy        Freddy no aparece (para ver a Vixy y a Puppet sin que él termine la partida)
    //   -prueba-tareas-cada <s>   una tarea nueva cada <s> segundos en vez de cada 30
    //   -prueba-posar <nombre>   ese animatrónico se planta frente al jugador, mirándolo, y no se mueve
    //                            (para fotografiar el ambiente: ojos, balanceo, luces)
    private static readonly bool PruebaSinFreddy = Array.IndexOf(Environment.GetCommandLineArgs(), "-prueba-sin-freddy") >= 0;
    private static readonly string PruebaPosar = TextoDeOpcion("-prueba-posar");
    //   -prueba-segundos-noche <s>   la noche dura <s> segundos reales (para probar el final sin esperar)
    private static readonly float PruebaSegundosNoche = ValorDeOpcion("-prueba-segundos-noche");
    private bool posadoParaPrueba;
    private static readonly float PruebaTareasCada = ValorDeOpcion("-prueba-tareas-cada");
    private static float SegundosEntreTareasActual => PruebaTareasCada > 0f ? PruebaTareasCada : SegundosEntreTareas;

    private static string TextoDeOpcion(string nombre)
    {
        string[] argumentos = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(argumentos, nombre);
        return i >= 0 && i + 1 < argumentos.Length ? argumentos[i + 1] : null;
    }

    private void PosarParaPrueba(string nombre)
    {
        foreach (AnimatronicUnityBrain brain in brains)
            if (string.Equals(brain.nombreAnimatronico, nombre, StringComparison.OrdinalIgnoreCase) && jugador != null)
                brain.PosarFrenteAlJugador(jugador, 3.5f);
    }

    private static float ValorDeOpcion(string nombre)
    {
        string[] argumentos = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(argumentos, nombre);
        float valor;
        return i >= 0 && i + 1 < argumentos.Length && float.TryParse(argumentos[i + 1],
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out valor) ? valor : 0f;
    }
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
    // La caja de Puppet dura lo mismo todas las noches (RescateDePuppet.SegundosDeLaCaja,
    // 30 s) y solo se acorta con los rescates del jugador 2; la presión de
    // noche en noche viene del nivel con que Puppet intenta salir.

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
    private readonly TareasDeOperador tareas = new TareasDeOperador(new System.Random());
    private readonly PresenciaDeOperador presencia = new PresenciaDeOperador();
    private readonly RescateDePuppet rescate = new RescateDePuppet();
    // Cámara del jugador 1: la mueve desde la tablet (botones izquierda/derecha).
    private readonly ControlDeCamara camara = new ControlDeCamara();
    private Transform camaraDeJuego;
    private Quaternion rotacionBaseDeCamara;
    // Las posiciones salen 30 veces por segundo (cada 33 ms): el ritmo lo fija PerfilDeVideo.
    private const float SegundosEntrePosiciones = PerfilDeVideo.SegundosEntrePosiciones;
    private float relojDePosiciones;
    private const float RadioDeTrampaMetros = 1.5f;
    private Dictionary<string, Vector3> puntosDeRuta;
    private bool puppetVentanaYaAbierta;
    private bool miradaPublicada;
    private readonly Dictionary<string, GameObject> trampasVisibles = new Dictionary<string, GameObject>();
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
        // Lo poco que el ambiente (luces, niebla, posprocesado) necesita saber del juego.
        AmbienteDelJuego.PartidaActiva = partidaActiva;
        AmbienteDelJuego.Noche = noche;
        AmbienteDelJuego.CajaEnPeligro = partidaActiva && valorCajaPuppet < MaxMusicBox * 0.2f;
        if (!partidaActiva) return;
        ActualizarCamara();
        if (!string.IsNullOrEmpty(PruebaPosar) && !posadoParaPrueba)
        {
            posadoParaPrueba = true;
            PosarParaPrueba(PruebaPosar);
        }
        bool operadorVivo = presencia.EstaVivo(Time.time);
        AvisarSiCambiaElCompanero();
        foreach (AnimatronicUnityBrain brain in brains)
        {
            brain.FijarFactorDeVelocidad(freno.FactorDeVelocidad(brain.nombreAnimatronico, Time.time));
            brain.FijarFactorDeAgresividad(RecursosDeOperador.FactorDeAgresividad(brain.nombreAnimatronico, operadorVivo));
        }
        RevisarTrampas();
        GestionarRescateDePuppet();
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
        DrawRouteGizmos(new Color(1f,.72f,.12f), "P01","P03","P04","P05","P06","P07","J1");
        DrawRouteGizmos(new Color(.2f,.85f,1f), "P02","P10","P11","P12","V01","V02","V03","V04","J1");
        DrawRouteGizmos(new Color(1f,.25f,.3f), "P13","P14","P15","P16","V05","V06","V07","V08","J1");
        DrawRouteGizmos(Color.magenta, "P08","P09");
    }

    private static void DrawRouteGizmos(Color color, params string[] names)
    {
        Gizmos.color = color;
        Vector3? previous = null;
        foreach (string markerName in names)
        {
            GameObject marker = GameObject.Find(markerName);
            if (marker == null) continue;
            Vector3 position = marker.transform.position;
            Gizmos.DrawSphere(position, markerName == "J1" ? .3f : .19f);
            if (previous.HasValue) Gizmos.DrawLine(previous.Value, position);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position + Vector3.up * .3f, markerName);
#endif
            previous = position;
        }
    }

    private void ConfigureSceneActors()
    {
        GameObject freddyObject = GameObject.Find(actorFreddy);
        if (freddyObject != null && PruebaSinFreddy)
        {
            freddyObject.SetActive(false);
            freddyObject = null;
        }
        if (freddyObject != null)
        {
            PatrullaPorPuntos existingPatrol = freddyObject.GetComponent<PatrullaPorPuntos>();
            Transform[] routeFreddy = FindRoute("P01", "P03", "P04", "P05", "P06", "P07", "J1");
            if (existingPatrol != null)
            {
                existingPatrol.controladoPorIAExterna = true;
            }
            AnimatronicUnityBrain freddy = AttachBrain(freddyObject, "Freddy", routeFreddy, true, true, false,
                probabilisticRoute: true, vixyTaskThreshold: false);
            // Ruta fija; de vez en cuando se asoma a un lado del comedor.
            if (freddy != null)
                freddy.ConfigurarDesvios(Desvios((2, "P08"), (2, "P09")), ProbabilidadDeDesvio);
        }

        GameObject vixyObject = GameObject.Find(actorVixy);
        if (vixyObject != null)
        {
            // P10 queda como desvío y no como parte de la ruta: el escenario no
            // tiene puerta al pasillo izquierdo, así que ir a P10 y volver a P11
            // son 35 m de ida y vuelta. Con una puerta en el muro oeste del
            // escenario se puede volver a poner en la ruta.
            AnimatronicUnityBrain vixy = AttachBrain(vixyObject, "Vixy", FindRoute(
                "P02", "P11", "P12", "V01", "V02", "V03", "V04", "J1"),
                true, false, false, probabilisticRoute: true, vixyTaskThreshold: true);
            if (vixy != null)
                vixy.ConfigurarDesvios(Desvios((0, "P10")), ProbabilidadDeDesvio);
        }

        GameObject puppetObject = GameObject.Find(actorPuppet);
        if (puppetObject != null)
            AttachBrain(puppetObject, "Puppet", FindRoute("P13", "P14", "P15", "P16", "V05", "V06", "V07", "V08", "J1"), false, false, true);
    }

    /// Probabilidad de hacer un desvío en cada tramo que lo tiene. Valor
    /// inicial para ajustar con el profesor.
    private const float ProbabilidadDeDesvio = 0.25f;

    /// Desvíos por nombre de punto: (índice del punto de la ruta tras el que se hace, nombre del punto).
    private static AnimatronicUnityBrain.DesvioDeRuta[] Desvios(params (int despuesDe, string punto)[] lista)
    {
        List<AnimatronicUnityBrain.DesvioDeRuta> desvios = new List<AnimatronicUnityBrain.DesvioDeRuta>();
        foreach ((int despuesDe, string punto) in lista)
        {
            GameObject objeto = GameObject.Find(punto);
            if (objeto != null)
                desvios.Add(new AnimatronicUnityBrain.DesvioDeRuta { despuesDe = despuesDe, punto = objeto.transform });
        }
        return desvios.ToArray();
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
                case "reconnect": ReconectarTablet(JsonUtility.FromJson<ConnectMessage>(json)); break;
                case "tablet_caida":
                    // La tablet se quedó sin conexión (lo avisa el relay): nadie va a
                    // mandar el «soltar» de la cuerda ni de la cámara.
                    windingPuppet = false;
                    CentrarCamara();
                    AmbienteDelJuego.PedirEstatica(Time.time, 1f);
                    break;
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
                case "camara":
                    if (partidaActiva) camara.Ordenar(JsonUtility.FromJson<CamaraMessage>(json).direccion, Time.time);
                    break;
                case "tarea_pedir":
                    AtenderPedidoDeTarea(JsonUtility.FromJson<TareaPedirMessage>(json));
                    break;
                case "tarea_respuesta":
                    AtenderRespuestaDeTarea(JsonUtility.FromJson<TareaRespuestaMessage>(json));
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

    /// La tablet perdió la conexión (Wi-Fi) y volvió: se retoma la noche en
    /// curso en vez de reiniciarla, y se le manda el estado para que su pantalla
    /// se ponga al día. Sin partida en curso (Unity se reinició o la noche ya
    /// terminó) no hay nada que retomar: se empieza como con `connect`.
    private void ReconectarTablet(ConnectMessage message)
    {
        if (!partidaActiva)
        {
            StartGame(message);
            return;
        }
        windingPuppet = false;
        CentrarCamara();
        presencia.OlvidarCambio();      // la tablet vuelve a saber si hay compañero
        PublishState();
        PublishPuppetState();
        PublishTaskList();
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
        tareas.Reiniciar();
        CentrarCamara();
        QuitarTodasLasTrampas();
        rescate.Reiniciar();
        presencia.OlvidarCambio();
        puppetVentanaYaAbierta = false;
        puntosDeRuta = null;
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
        360f / (PruebaSegundosNoche > 0f ? PruebaSegundosNoche
            : SegundosRealesPorNoche[Mathf.Clamp(noche - 1, 0, SegundosRealesPorNoche.Length - 1)]);

    private void TickGame()
    {
        totalSeconds += TickSeconds;
        float gameDelta = TickSeconds * EscalaDeTiempoDeLaNoche;
        secondsIntoHour += gameDelta;

        float cambioPorSegundo = windingPuppet
            ? rescate.RecargaPorSegundo(MaxMusicBox)
            : -rescate.DescargaPorSegundo(MaxMusicBox);
        valorCajaPuppet = Mathf.Clamp(valorCajaPuppet + cambioPorSegundo * TickSeconds, 0f, MaxMusicBox);

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
        if (segundosDesdeUltimaTarea >= SegundosEntreTareasActual)
        {
            segundosDesdeUltimaTarea -= SegundosEntreTareasActual;
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
        CentrarCamara();
        QuitarTodasLasTrampas();
        rescate.Reiniciar();
        foreach (AnimatronicUnityBrain brain in brains) brain.MantenerQuieto(false);

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
        foreach (var punto in PuntosDeRuta())
        {
            puntos.Add(new PuntoDeMapa
            {
                nombre = punto.Key,
                x = punto.Value.x,
                z = punto.Value.z
            });
        }
        Send(new MapaPuntosMessage
        {
            puntos = puntos.ToArray(),
            jugador = jugador == null ? null
                : new PuntoDeMapa { nombre = "jugador", x = jugador.position.x, z = jugador.position.z },
            rutas = RutasParaElJugador2(),
            plano = PlanoDelEdificio()
        });
    }

    private PiezaDelPlano[] planoDelEdificio;

    /// El plano 2D del mapa para el jugador 2, armado una sola vez con la
    /// geometría que está en la escena: cada muro, piso, tarima, mesa y la caja
    /// de música, con su caja en el mundo (Renderer.bounds). Los muros del modelo
    /// son cajas alineadas a los ejes, así que su rectángulo es exacto.
    private PiezaDelPlano[] PlanoDelEdificio()
    {
        if (planoDelEdificio != null) return planoDelEdificio;
        var piezas = new List<PiezaDelPlano>();
        GameObject mapa = GameObject.Find(objetoDelMapa);
        Renderer[] partes = mapa != null ? mapa.GetComponentsInChildren<Renderer>() : FindObjectsOfType<Renderer>();
        foreach (Renderer parte in partes)
        {
            PlanoDelMapa.Pieza pieza = PlanoDelMapa.Clasificar(parte.gameObject.name);
            if (string.IsNullOrEmpty(pieza.Tipo)) continue;
            Bounds caja = parte.bounds;
            PlanoDelMapa.Rectangulo rect = PlanoDelMapa.RectanguloDeCaja(caja.min.x, caja.min.z, caja.max.x, caja.max.z, 0f);
            piezas.Add(new PiezaDelPlano
            {
                tipo = pieza.Tipo, etiqueta = pieza.Etiqueta,
                x0 = rect.x0, z0 = rect.z0, x1 = rect.x1, z1 = rect.z1
            });
        }
        // Los pisos primero: se dibujan debajo de los muros.
        piezas.Sort((a, b) => Orden(a.tipo).CompareTo(Orden(b.tipo)));
        Debug.Log($"[Plano] {piezas.Count} piezas para el plano del jugador 2 (de {partes.Length} objetos del mapa)");
        planoDelEdificio = piezas.ToArray();
        return planoDelEdificio;
    }

    private static int Orden(string tipo) =>
        tipo == "muro" ? 3 : (tipo == "mesa" || tipo == "caja" || tipo == "tarima") ? 2 : 1;

    /// La ruta de cada animatrónico sin su último elemento (la posición del
    /// jugador 1): termina en el punto penúltimo, por donde entra a atacar.
    private RutaDeAnimatronico[] RutasParaElJugador2()
    {
        var rutas = new List<RutaDeAnimatronico>();
        foreach (AnimatronicUnityBrain brain in brains)
        {
            if (brain.ruta == null || brain.ruta.Length < 2) continue;
            var nombres = new List<string>();
            for (int i = 0; i < brain.ruta.Length - 1; i++)
                if (brain.ruta[i] != null) nombres.Add(brain.ruta[i].name);
            rutas.Add(new RutaDeAnimatronico { animatronico = brain.nombreAnimatronico.ToLowerInvariant(), puntos = nombres.ToArray() });
        }
        return rutas.ToArray();
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

    /// Lo que el jugador 2 necesita ver al instante: las posiciones a 30 Hz y
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
                : new PuntoDeMapa { nombre = "jugador", x = jugador.position.x, z = jugador.position.z },
            camara_yaw = Mathf.Round(camara.Yaw * 10f) / 10f
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
            necesario = RecursosDeOperador.CircuitosParaHabilidad,
            habilidad_puppet = recursos.HabilidadPuppet,
            bloqueo_s = tareas.SegundosDeBloqueo(Time.time),
            ventana_rescate_s = rescate.SegundosRestantes(Time.time),
            multa_factor = rescate.FactorDeDescarga,
            segundos_caja = rescate.SegundosParaVaciarLaCaja
        });
    }

    /// Unity decide: valida la acción del jugador 2 con `FrenoDeOperador`, la
    /// aplica y avisa a todos. El navegador solo pide.
    private void AtenderAccionDelOperador(AccionOperadorMessage accion)
    {
        if (accion == null) return;
        switch (accion.accion)
        {
            case "retrasar": RetrasarAnimatronico(accion); break;
            case "trampa": ColocarTrampaDelOperador(accion); break;
            case "rescatar_puppet": RescatarAPuppet(accion); break;
        }
    }

    private void RetrasarAnimatronico(AccionOperadorMessage accion)
    {
        string objetivo = FrenoDeOperador.Normalizar(accion.objetivo);
        if (!partidaActiva)
        {
            Send(new AccionRechazadaMessage { objetivo = accion.objetivo, motivo = "sin_partida", id = accion.id });
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
                message = aviso,
                id = accion.id
            });
            PublicarEstadoDelMapa();
            return;
        }

        Send(new AccionRechazadaMessage
        {
            objetivo = accion.objetivo,
            motivo = resultado == FrenoDeOperador.Resultado.EnEnfriamiento ? "enfriamiento" : "desconocido",
            espera_s = objetivo == null ? 0f : freno.SegundosDeEspera(objetivo, Time.time),
            id = accion.id
        });
    }

    private void Rechazar(AccionOperadorMessage accion, string motivo)
    {
        Send(new AccionRechazadaMessage { objetivo = accion.objetivo ?? accion.punto, motivo = motivo, id = accion.id });
    }

    private void AvisarAlJugador1(string texto)
    {
        avisoDeOperador = texto;
        AmbienteDelJuego.PedirEstatica(Time.time, 0.8f);   // la pantalla «pierde señal» un instante
        avisoDeOperadorHasta = Time.time + 3f;
    }

    /// El jugador 1 debe saber si tiene compañero: con él, Freddy y Vixy
    /// avanzan más rápido (ver RecursosDeOperador.FactorDeAgresividad). Se
    /// avisa en el PC y en la tablet cada vez que entra o se cae.
    private void AvisarSiCambiaElCompanero()
    {
        PresenciaDeOperador.Cambio cambio = presencia.RevisarCambio(Time.time);
        if (cambio == PresenciaDeOperador.Cambio.SinCambio) return;
        bool conectado = cambio == PresenciaDeOperador.Cambio.Conectado;
        string aviso = conectado
            ? "Tu compañero se conectó: los animatrónicos van más rápido"
            : "Tu compañero se desconectó: los animatrónicos vuelven a su ritmo";
        AvisarAlJugador1(aviso);
        AmbienteDelJuego.PedirEstatica(Time.time, conectado ? 0.4f : 1.2f);
        Send(new EfectoMessage
        {
            animatronic = string.Empty,
            tipo = conectado ? "operador_conectado" : "operador_perdido",
            segundos = 0f,
            message = aviso
        });
    }

    /// Nombre y posición de cada punto de las rutas (los mismos que ve el
    /// jugador 2 en el mapa). Las rutas no cambian durante una partida.
    private Dictionary<string, Vector3> PuntosDeRuta()
    {
        if (puntosDeRuta != null) return puntosDeRuta;
        puntosDeRuta = new Dictionary<string, Vector3>();
        var nodosDelPlano = GameObject.Find("Mapa_V2/Nodos_Ruta");
        if (nodosDelPlano != null)
        {
            foreach (Transform punto in nodosDelPlano.transform)
                if (punto != jugador)
                    puntosDeRuta[punto.name] = punto.position;
            return puntosDeRuta;
        }

        foreach (AnimatronicUnityBrain brain in brains)
        {
            if (brain.ruta == null) continue;
            foreach (Transform punto in brain.ruta)
                if (punto != null && punto != jugador && !puntosDeRuta.ContainsKey(punto.name))
                    puntosDeRuta.Add(punto.name, punto.position);
        }
        return puntosDeRuta;
    }

    /// La cámara del juego gira hacia donde el jugador 1 mantiene apretado el
    /// botón de la tablet y vuelve sola al frente al soltarlo.
    private void ActualizarCamara()
    {
        camara.Actualizar(Time.deltaTime, Time.time);
        AplicarCamara();
    }

    private void CentrarCamara()
    {
        camara.Centrar();
        AplicarCamara();
    }

    private void AplicarCamara()
    {
        Camera principal = Camera.main;
        if (principal == null) return;
        // La orientación de partida es la que tiene la cámara la primera vez
        // (al empezar la partida, mirando al frente); el giro se suma a ella.
        if (principal.transform != camaraDeJuego)
        {
            camaraDeJuego = principal.transform;
            rotacionBaseDeCamara = camaraDeJuego.rotation;
        }
        // Respiración sutil de la cámara (AmbienteDelJuego): cabeceo de ±0,3°.
        (float cabeceo, float balanceo) = AmbienteDelJuego.RespiracionDeCamara(Time.time);
        camaraDeJuego.rotation = Quaternion.Euler(0f, camara.Yaw, 0f) * rotacionBaseDeCamara * Quaternion.Euler(cabeceo, 0f, balanceo);
    }

    /// La trampa se ve en el mapa del jugador 1 (un disco rojo que late).
    private void MostrarTrampa(string punto)
    {
        Vector3 lugar;
        if (!PuntosDeRuta().TryGetValue(punto, out lugar) || trampasVisibles.ContainsKey(punto)) return;
        trampasVisibles[punto] = TrampaVisible.Crear(punto, lugar);
    }

    private void QuitarTrampa(string punto)
    {
        GameObject objeto;
        if (!trampasVisibles.TryGetValue(punto, out objeto)) return;
        trampasVisibles.Remove(punto);
        if (objeto != null) Destroy(objeto);
    }

    private void QuitarTodasLasTrampas()
    {
        foreach (GameObject objeto in trampasVisibles.Values)
            if (objeto != null) Destroy(objeto);
        trampasVisibles.Clear();
    }

    private void ColocarTrampaDelOperador(AccionOperadorMessage accion)
    {
        if (!partidaActiva) { Rechazar(accion, "sin_partida"); return; }
        bool valido = !string.IsNullOrEmpty(accion.punto) && PuntosDeRuta().ContainsKey(accion.punto);
        switch (recursos.ColocarTrampa(accion.punto, valido))
        {
            case RecursosDeOperador.ResultadoTrampa.Colocada:
                string aviso = "Tu compañero puso una trampa";
                MostrarTrampa(accion.punto);
                AvisarAlJugador1(aviso);
                Send(new EfectoMessage { animatronic = string.Empty, tipo = "trampa_colocada", segundos = 0f, message = aviso, id = accion.id });
                PublicarEstadoOperador();
                break;
            case RecursosDeOperador.ResultadoTrampa.SinCargas: Rechazar(accion, "sin_cargas"); break;
            case RecursosDeOperador.ResultadoTrampa.PuntoOcupado: Rechazar(accion, "punto_ocupado"); break;
            default: Rechazar(accion, "punto_invalido"); break;
        }
    }

    /// Cada cuadro: si un animatrónico pisa una trampa, se queda quieto unos
    /// segundos y la trampa se gasta. Puppet dentro de su caja no cuenta.
    private void RevisarTrampas()
    {
        if (recursos.Trampas.Count == 0) return;
        Dictionary<string, Vector3> puntos = PuntosDeRuta();
        foreach (string punto in new List<string>(recursos.Trampas))
        {
            Vector3 lugar;
            if (!puntos.TryGetValue(punto, out lugar)) continue;
            foreach (AnimatronicUnityBrain brain in brains)
            {
                if (brain.EsPuppet && !brain.FueraDeCaja) continue;
                Vector3 a = new Vector3(brain.transform.position.x, 0f, brain.transform.position.z);
                Vector3 b = new Vector3(lugar.x, 0f, lugar.z);
                if (Vector3.Distance(a, b) > RadioDeTrampaMetros) continue;
                if (!recursos.DispararTrampa(punto)) break;
                QuitarTrampa(punto);
                brain.DetenerPor(RecursosDeOperador.SegundosDeTrampa);
                string nombre = char.ToUpperInvariant(brain.nombreAnimatronico[0]) + brain.nombreAnimatronico.Substring(1);
                string aviso = "Trampa: " + nombre + " atrapado";
                AvisarAlJugador1(aviso);
                Send(new EfectoMessage
                {
                    animatronic = brain.nombreAnimatronico.ToLowerInvariant(),
                    tipo = "trampa_disparada",
                    segundos = RecursosDeOperador.SegundosDeTrampa,
                    message = aviso
                });
                PublicarEstadoOperador();
                break;
            }
        }
    }

    private void RechazarTarea(string motivo, float espera = 0f)
    {
        Send(new AccionRechazadaMessage { objetivo = "tarea", motivo = motivo, espera_s = espera });
    }

    /// El jugador 2 abre una tarea de la terminal. Unity la inventa según la
    /// noche; si ya tenía una abierta del mismo tipo, es la misma.
    private void AtenderPedidoDeTarea(TareaPedirMessage pedido)
    {
        if (!partidaActiva) { RechazarTarea("sin_partida"); return; }
        TareasDeOperador.Tipo tipo;
        if (pedido == null || pedido.tipo == "calculo") tipo = TareasDeOperador.Tipo.Calculo;
        else if (pedido.tipo == "circuito") tipo = TareasDeOperador.Tipo.Circuito;
        else { RechazarTarea("tarea_desconocida"); return; }

        // No tiene sentido ganar lo que ya está lleno o ganado.
        if (tipo == TareasDeOperador.Tipo.Calculo && recursos.CargasLlenas) { RechazarTarea("cargas_llenas"); return; }
        if (tipo == TareasDeOperador.Tipo.Circuito && recursos.HabilidadPuppet) { RechazarTarea("habilidad_lista"); return; }

        TareasDeOperador.Tarea tarea = tareas.Pedir(tipo, noche, Time.time);
        if (tarea == null) { RechazarTarea("bloqueado", tareas.SegundosDeBloqueo(Time.time)); return; }

        var mensaje = new TareaMessage
        {
            id = tarea.id,
            tipo = tipo == TareasDeOperador.Tipo.Calculo ? "calculo" : "circuito",
            noche = tarea.noche,
            clase = tarea.clase,
            enunciado = tarea.enunciado,
            datos = tarea.datos,
            entradas = tarea.entradas,
            salida = tarea.salida,
            objetivo = tarea.objetivo
        };
        if (tarea.puertas != null)
        {
            mensaje.puertas = new PuertaMessage[tarea.puertas.Length];
            for (int i = 0; i < tarea.puertas.Length; i++)
                mensaje.puertas[i] = new PuertaMessage { id = tarea.puertas[i].id, op = tarea.puertas[i].op, a = tarea.puertas[i].a, b = tarea.puertas[i].b };
        }
        Send(mensaje);
    }

    /// Unity comprueba la respuesta. Acertar da la carga (cálculo) o el rescate
    /// (circuito); fallar bloquea la terminal unos segundos.
    private void AtenderRespuestaDeTarea(TareaRespuestaMessage respuesta)
    {
        if (!partidaActiva) { RechazarTarea("sin_partida"); return; }
        if (respuesta == null) { RechazarTarea("tarea_sin_pedir"); return; }

        TareasDeOperador.Tarea tarea = tareas.Pendiente;
        string tipo = tarea != null && tarea.tipo == TareasDeOperador.Tipo.Circuito ? "circuito" : "calculo";
        switch (tareas.Responder(respuesta.id, respuesta.valor, respuesta.interruptores, Time.time))
        {
            case TareasDeOperador.ResultadoRespuesta.SinTarea: RechazarTarea("tarea_sin_pedir"); return;
            case TareasDeOperador.ResultadoRespuesta.MuyRapida: RechazarTarea("tarea_muy_rapida"); return;
            case TareasDeOperador.ResultadoRespuesta.Correcta:
                if (tipo == "calculo") recursos.GanarCargaPorCalculo(); else recursos.GanarHabilidadPorCircuito();
                Send(new TareaResultadoMessage { id = respuesta.id, tipo = tipo, correcto = true });
                break;
            default:
                Send(new TareaResultadoMessage { id = respuesta.id, tipo = tipo, correcto = false, bloqueo_s = tareas.SegundosDeBloqueo(Time.time) });
                break;
        }
        PublicarEstadoOperador();
    }

    /// Cuando Puppet sale de su caja se abre la ventana de rescate y se queda
    /// quieto; si nadie lo rescata a tiempo, sigue su camino como siempre.
    private void GestionarRescateDePuppet()
    {
        foreach (AnimatronicUnityBrain brain in brains)
        {
            if (!brain.EsPuppet) continue;
            if (brain.FueraDeCaja && !puppetVentanaYaAbierta)
            {
                puppetVentanaYaAbierta = true;
                rescate.AbrirVentana(Time.time);
                brain.MantenerQuieto(true);
                PublicarEstadoOperador();
            }
            else if (!brain.FueraDeCaja)
            {
                puppetVentanaYaAbierta = false;
            }
            if (rescate.ConsumirVencimiento(Time.time))
            {
                brain.MantenerQuieto(false);
                PublicarEstadoOperador();
            }
        }
    }

    private void RescatarAPuppet(AccionOperadorMessage accion)
    {
        if (!partidaActiva) { Rechazar(accion, "sin_partida"); return; }
        switch (rescate.Rescatar(Time.time, recursos.HabilidadPuppet))
        {
            case RescateDePuppet.Resultado.SinVentana: Rechazar(accion, "sin_ventana"); return;
            case RescateDePuppet.Resultado.SinHabilidad: Rechazar(accion, "sin_habilidad"); return;
        }
        recursos.GastarHabilidadPuppet();
        foreach (AnimatronicUnityBrain brain in brains)
        {
            if (!brain.EsPuppet) continue;
            brain.VolverALaCaja();
            brain.MantenerQuieto(false);
        }
        valorCajaPuppet = MaxMusicBox;
        puppetEnPeligroPrevio = false;
        puppetCajaVaciaPrevio = false;
        string aviso = "Tu compañero te salvó de Puppet (la caja ahora dura " +
            rescate.SegundosParaVaciarLaCaja.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " s)";
        AvisarAlJugador1(aviso);
        Send(new EfectoMessage
        {
            animatronic = "puppet",
            tipo = "puppet_rescatado",
            segundos = 0f,
            message = aviso,
            id = accion.id,
            multa_factor = rescate.FactorDeDescarga,
            segundos_caja = rescate.SegundosParaVaciarLaCaja
        });
        PublicarEstadoOperador();
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
            multa_factor = rescate.FactorDeDescarga,
            segundos_caja = rescate.SegundosParaVaciarLaCaja,
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
