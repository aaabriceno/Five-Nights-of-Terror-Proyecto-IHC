using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Threading.Tasks;
using System;

public class UnityWebSocketClient : MonoBehaviour
{
    public static event Action<string> MessageReceived;

    [SerializeField]
    private string serverUrl = "ws://127.0.0.1:8000";

    private WebSocket websocket;

    public async void SendJsonMessage(string message)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("No se pudo enviar el mensaje: Unity no está conectado al relay.");
            return;
        }

        try
        {
            await websocket.SendText(message);
        }
        catch (Exception error)
        {
            Debug.LogError("Error al enviar mensaje WebSocket: " + error.Message);
        }
    }

    private async void Start()
    {
        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += async () =>
        {
            Debug.Log("Unity conectado al servidor");

            string mensaje = "{\"type\":\"unity_connect\"}";
            await websocket.SendText(mensaje);
        };

        websocket.OnError += (error) =>
        {
            Debug.LogError("Error WebSocket: " + error);
        };

        websocket.OnClose += (code) =>
        {
            Debug.Log("Unity desconectado del servidor");
        };

        websocket.OnMessage += (bytes) =>
        {
            string mensaje = Encoding.UTF8.GetString(bytes);
            Debug.Log("Mensaje recibido del servidor: " + mensaje);
            MessageReceived?.Invoke(mensaje);
        };

        try
        {
            await websocket.Connect();
        }
        catch (Exception error)
        {
            Debug.LogError("No se pudo conectar con el servidor WebSocket: " + error.Message);
        }
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }
}
