using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Receives the existing backend protocol and shares its task/night state with
/// the Unity animatronic controllers. The backend remains unchanged.
/// </summary>
public class AnimatronicGameCoordinator : MonoBehaviour
{
    [Serializable]
    public class StringEvent : UnityEvent<string> { }

    [Serializable]
    private class MessageHeader
    {
        public string type;
    }

    [Serializable]
    public class TaskState
    {
        public int task_id;
        public string task_type;
        public string description;
        public int duration;
        public int difficulty;
    }

    [Serializable]
    private class TaskListMessage
    {
        public string type;
        public TaskState[] tasks;
    }

    [Serializable]
    private class NightStatusMessage
    {
        public string type;
        public int night;
        public string in_game_time;
    }

    [Serializable]
    private class PuppetStatusMessage
    {
        public string type;
        public bool en_peligro;
        public float valor_caja_porcentaje;
    }

    [Serializable]
    private class AttackMessage
    {
        public string type;
        public string animatronic;
        public string message;
    }

    [Serializable]
    private class GameOverMessage
    {
        public string type;
        public string result;
    }

    [Header("Estado compartido recibido del servidor")]
    [Min(1)] public int noche = 1;
    public string horaEnJuego = "12:00 AM";
    public bool partidaFinalizada;
    [Range(0f, 100f)] public float porcentajeCajaPuppet = 100f;
    public bool puppetEnPeligro;
    public List<TaskState> tareasActivas = new List<TaskState>();

    [Header("Eventos para conectar UI/efectos desde el Inspector")]
    public StringEvent alRecibirTarea = new StringEvent();
    public StringEvent alRetirarTarea = new StringEvent();
    public StringEvent alRecibirAtaque = new StringEvent();
    public StringEvent alFinalizarPartida = new StringEvent();

    private readonly List<AnimatronicUnityBrain> brains = new List<AnimatronicUnityBrain>();

    private void Awake()
    {
        brains.AddRange(FindObjectsOfType<AnimatronicUnityBrain>(true));
    }

    private void OnEnable()
    {
        UnityWebSocketClient.MessageReceived += OnServerMessage;
    }

    private void OnDisable()
    {
        UnityWebSocketClient.MessageReceived -= OnServerMessage;
    }

    private void OnServerMessage(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            MessageHeader header = JsonUtility.FromJson<MessageHeader>(json);
            if (header == null) return;

            // JsonUtility ignores fields not declared on each message DTO.
            if (header.type == "task_list")
            {
                TaskListMessage message = JsonUtility.FromJson<TaskListMessage>(json);
                ApplyTaskList(message);
            }
            else if (header.type == "night_status")
            {
                NightStatusMessage message = JsonUtility.FromJson<NightStatusMessage>(json);
                noche = Mathf.Clamp(message.night, 1, 6);
                horaEnJuego = message.in_game_time ?? horaEnJuego;
                foreach (AnimatronicUnityBrain brain in brains)
                    if (brain != null) brain.SetNight(noche, horaEnJuego);
            }
            else if (header.type == "estado_puppet")
            {
                PuppetStatusMessage message = JsonUtility.FromJson<PuppetStatusMessage>(json);
                porcentajeCajaPuppet = Mathf.Clamp(message.valor_caja_porcentaje, 0f, 100f);
                puppetEnPeligro = message.en_peligro;
                foreach (AnimatronicUnityBrain brain in brains)
                    if (brain != null && brain.EsPuppet) brain.SetPuppetState(porcentajeCajaPuppet, puppetEnPeligro);
            }
            else if (header.type == "attack")
            {
                AttackMessage message = JsonUtility.FromJson<AttackMessage>(json);
                alRecibirAtaque.Invoke(message.animatronic ?? string.Empty);
                foreach (AnimatronicUnityBrain brain in brains)
                    if (brain != null && string.Equals(brain.nombreAnimatronico, message.animatronic, StringComparison.OrdinalIgnoreCase))
                        brain.RecibirAtaqueDelServidor(message.message);
            }
            else if (header.type == "game_over")
            {
                GameOverMessage message = JsonUtility.FromJson<GameOverMessage>(json);
                partidaFinalizada = true;
                alFinalizarPartida.Invoke(message.result ?? string.Empty);
                foreach (AnimatronicUnityBrain brain in brains)
                    if (brain != null) brain.Detener();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"No se pudo procesar un evento del juego: {exception.Message}");
        }
    }

    private void ApplyTaskList(TaskListMessage message)
    {
        if (message == null || message.tasks == null) return;

        HashSet<string> previous = new HashSet<string>();
        foreach (TaskState task in tareasActivas)
            if (task != null) previous.Add(task.task_type);

        tareasActivas = new List<TaskState>(message.tasks);
        HashSet<string> current = new HashSet<string>();
        foreach (TaskState task in tareasActivas)
        {
            if (task == null || string.IsNullOrEmpty(task.task_type)) continue;
            current.Add(task.task_type);
            if (!previous.Contains(task.task_type)) alRecibirTarea.Invoke(task.task_type);
        }

        foreach (string oldTask in previous)
            if (!current.Contains(oldTask)) alRetirarTarea.Invoke(oldTask);

        foreach (AnimatronicUnityBrain brain in brains)
            if (brain != null) brain.SetTasks(tareasActivas);
    }
}
