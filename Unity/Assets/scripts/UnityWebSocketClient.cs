using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Threading.Tasks;
using System;

/// <summary>
/// Conexión del juego con el relay. Corre antes que los demás scripts
/// (orden -100): así un mensaje que llegó entre cuadro y cuadro se reparte en
/// el mismo Update en que el controlador lo va a usar, y no uno después (hasta
/// 16 ms menos de reacción a 60 FPS para la cámara, las tareas y las acciones
/// del jugador 2).
/// </summary>
[DefaultExecutionOrder(-100)]
public class UnityWebSocketClient : MonoBehaviour
{
    public static event Action<string> MessageReceived;

    [SerializeField]
    private string serverUrl = "ws://127.0.0.1:8000";

    /// Puerto del relay, leído de la misma URL a la que se conecta el
    /// cliente: así la pantalla de espera muestra siempre el que se usa.
    public int Puerto => new Uri(serverUrl).Port;

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
            // El socket de Mono nace con Nagle: cada mensaje chico esperaba el ACK
            // del anterior (hasta 40 ms en Windows; en Linux el relay lo esquiva
            // con TCP_QUICKACK). Se apaga aquí, apenas abre la conexión.
            Debug.Log(AjusteDeSocket.DesactivarNagle(websocket)
                ? "[Red] TCP_NODELAY activado: los mensajes salen sin esperar el ACK anterior"
                : "[Red] No se encontró el socket para apagar Nagle: los mensajes pueden demorar hasta 40 ms");

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
#if UNITY_EDITOR
            // Solo en el editor: en el juego exportado cada Debug.Log escribe en
            // Player.log con su pila de llamadas, en el hilo principal, por cada
            // orden de cámara o tarea que llega.
            Debug.Log("Mensaje recibido del servidor: " + mensaje);
#endif
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
