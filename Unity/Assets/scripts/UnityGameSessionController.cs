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

    [Header("Jumpscare en la ventana de Unity")]
    [SerializeField] private Texture2D desktopJumpscareImage;
    [SerializeField] private AudioClip desktopJumpscareSound;
    [SerializeField, Min(0.1f)] private float desktopJumpscareDuration = 2.5f;
    private bool desktopJumpscareShown;

    private const float TickSeconds = 0.5f;
    private const float GameTimeScale = 3f;
    private const float MaxMusicBox = 2000f;
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
    private static readonly int[] PuppetDrainRates = { 40, 40, 60, 80, 100, 120 };

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

    private void Awake()
    {
        if (websocketClient == null) websocketClient = FindObjectOfType<UnityWebSocketClient>();
        if (coordinator == null) coordinator = FindObjectOfType<AnimatronicGameCoordinator>();
        if (detectorCabeza == null) detectorCabeza = FindObjectOfType<HeadOrientationTracker>();
        if (gestorSonido == null) gestorSonido = FindObjectOfType<GestorSonidoJuego>();
        if (jugador == null && Camera.main != null) jugador = Camera.main.transform;
        ConfigureSceneActors();
    }

    private void Start()
    {
        if (!partidaActiva && gestorSonido != null) gestorSonido.ReproducirMusicaMenu();
    }

    private void OnEnable() => UnityWebSocketClient.MessageReceived += OnMessage;
    private void OnDisable() => UnityWebSocketClient.MessageReceived -= OnMessage;

    private void Update()
    {
        if (!partidaActiva) return;
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
                case "disconnect": partidaActiva = false; break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("No se pudo procesar un mensaje de juego: " + exception.Message);
        }
    }

    private void StartGame(ConnectMessage message)
    {
        if (message == null) return;
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
        partidaActiva = true;
        desktopJumpscareShown = false;
        if (gestorSonido != null) gestorSonido.DetenerMusica();
        if (coordinator != null)
        {
            coordinator.partidaFinalizada = false;
            coordinator.noche = noche;
            coordinator.horaEnJuego = GetInGameTime();
            coordinator.porcentajeCajaPuppet = 100f;
            coordinator.puppetEnPeligro = false;
        }

        foreach (AnimatronicUnityBrain brain in brains)
        {
            brain.BeginSession(noche, GetInGameTime());
            brain.SetPuppetState(100f, false);
        }

        CreateTask(InitialTask, "Freddy");
        PublishState();
        PublishTaskList();
    }

    private void TickGame()
    {
        totalSeconds += TickSeconds;
        float gameDelta = TickSeconds * GameTimeScale;
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

        if (secondsIntoHour >= 60f)
        {
            secondsIntoHour -= 60f;
            if (hora < 6)
            {
                hora++;
                TryGenerateNightTasks();
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
                if ((type == "procesar_datos" || type == "subir_datos") && hora < 4) continue;
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
                if ((type == "procesar_datos" || type == "subir_datos") && hora < 4) continue;
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
        ShowDesktopJumpscare();
        string name = "Animatrónico";
        foreach (AnimatronicUnityBrain brain in brains)
            if (brain != null && brain.enabled && message.StartsWith(brain.nombreAnimatronico, StringComparison.OrdinalIgnoreCase))
            { name = brain.nombreAnimatronico; break; }

        Send(new AttackMessage
        {
            attack_id = "atk_" + Mathf.RoundToInt(totalSeconds * 1000f),
            animatronic = name,
            message = string.IsNullOrEmpty(message) ? name + " te encontró" : message,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        FinishGame(false, true, name);
    }

    private void ShowDesktopJumpscare()
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
            rawImage.texture = image;
            rawImage.color = Color.white;
            rawImage.raycastTarget = true;
        }
        else
        {
            Debug.LogError("No se encontró Resources/desktop_jumpscare.jpg para el jumpscare de Unity.");
        }

        AudioSource audioSource = overlay.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (sound != null)
            audioSource.PlayOneShot(sound);
        else
            Debug.LogError("No se encontró Resources/desktop_jumpscare.ogg para el jumpscare de Unity.");

        float visibleDuration = Mathf.Max(desktopJumpscareDuration, sound != null ? sound.length : 0f);
        Destroy(overlay, visibleDuration);
    }

    private void FinishGame(bool won, bool died, string attacker)
    {
        if (!partidaActiva) return;
        partidaActiva = false;
        foreach (AnimatronicUnityBrain brain in brains) brain.Detener();

        if (gestorSonido != null)
        {
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
        if (coordinator != null)
        {
            coordinator.partidaFinalizada = true;
            coordinator.alFinalizarPartida.Invoke(result);
        }
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
