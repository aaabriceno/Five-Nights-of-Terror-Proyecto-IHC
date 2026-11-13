using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

// Pruebas de AjusteDeSocket fuera de Unity (ver probar_reglas_de_operador.sh).
// La segunda parte abre el relay de verdad (UnityRelay/relay.py) y conecta un
// ClientWebSocket como el que usa el juego: comprueba que el socket que Mono
// esconde adentro queda con TCP_NODELAY (lo lee el kernel con getsockopt).
public static class PruebaAjusteDeSocket
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private class Envoltorio { public object adentro; }
    private class ConLista { public object[] cosas; }

    public static int Main()
    {
        // --- Un socket escondido dentro de objetos anidados ---
        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        int puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        var cliente = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        cliente.Connect(IPAddress.Loopback, puerto);
        using (Socket servidor = escucha.AcceptSocket())
        {
            Comprobar("un socket recién abierto tiene Nagle encendido (NoDelay = false)", !cliente.NoDelay);
            object raiz = new Envoltorio { adentro = new ConLista { cosas = new object[] { 7, "texto", new Envoltorio { adentro = new NetworkStream(cliente) } } } };
            Comprobar("BuscarSocket lo encuentra entre campos, arreglos y un NetworkStream", ReferenceEquals(AjusteDeSocket.BuscarSocket(raiz), cliente));
            Comprobar("DesactivarNagle devuelve true", AjusteDeSocket.DesactivarNagle(raiz));
            Comprobar("y el socket queda con NoDelay = true (getsockopt)", cliente.NoDelay);
            Comprobar("sin socket adentro, BuscarSocket devuelve null", AjusteDeSocket.BuscarSocket(new Envoltorio { adentro = "nada" }) == null);
            Comprobar("y DesactivarNagle devuelve false", !AjusteDeSocket.DesactivarNagle(new Envoltorio { adentro = 3 }));
            Comprobar("con null no falla", !AjusteDeSocket.DesactivarNagle(null));
            // Un ciclo de referencias no cuelga la búsqueda.
            var ciclo = new Envoltorio();
            ciclo.adentro = ciclo;
            Comprobar("un ciclo de referencias no cuelga", AjusteDeSocket.BuscarSocket(ciclo) == null);
        }
        cliente.Close();
        escucha.Stop();

        // --- El ClientWebSocket de Mono, el mismo que usa NativeWebSocket en el juego ---
        string raizDelRepo = Environment.GetEnvironmentVariable("RAIZ");
        if (string.IsNullOrEmpty(raizDelRepo))
        {
            Console.WriteLine("  (sin RAIZ: no se prueba contra el relay)");
        }
        else
        {
            ProbarConElRelay(raizDelRepo).GetAwaiter().GetResult();
        }

        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }

    private static async Task ProbarConElRelay(string raizDelRepo)
    {
        string python = Path.Combine(raizDelRepo, "UnityRelay", ".venv", "bin", "python");
        string relay = Path.Combine(raizDelRepo, "UnityRelay", "relay.py");
        if (!File.Exists(python) || !File.Exists(relay))
        {
            Console.WriteLine("  (no está el relay o su Python: no se prueba contra el relay)");
            return;
        }
        using (var proceso = Process.Start(new ProcessStartInfo(python, "-u \"" + relay + "\"")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        }))
        {
            try
            {
                var socket = new ClientWebSocket();
                Exception ultimoError = null;
                for (int intento = 0; intento < 40; intento++)
                {
                    try
                    {
                        await socket.ConnectAsync(new Uri("ws://127.0.0.1:8000"), CancellationToken.None);
                        ultimoError = null;
                        break;
                    }
                    catch (Exception error)
                    {
                        ultimoError = error;
                        socket = new ClientWebSocket();
                        await Task.Delay(250);
                    }
                }
                Comprobar("se conecta al relay con ClientWebSocket" + (ultimoError == null ? "" : " (" + ultimoError.Message + ")"), ultimoError == null);
                if (ultimoError != null) return;

                Socket interno = AjusteDeSocket.BuscarSocket(socket);
                Comprobar("BuscarSocket encuentra el socket TCP dentro del ClientWebSocket de Mono", interno != null);
                if (interno != null)
                    Console.WriteLine("  (antes del ajuste, NoDelay = " + interno.NoDelay + ")");
                Comprobar("DesactivarNagle(ClientWebSocket) devuelve true", AjusteDeSocket.DesactivarNagle(socket));
                Comprobar("el socket de Mono queda con TCP_NODELAY (NoDelay = true)", interno != null && interno.NoDelay);

                byte[] mensaje = System.Text.Encoding.UTF8.GetBytes("{\"type\":\"unity_connect\"}");
                await socket.SendAsync(new ArraySegment<byte>(mensaje), WebSocketMessageType.Text, true, CancellationToken.None);
                Comprobar("y la conexión sigue abierta después de mandar un mensaje", socket.State == WebSocketState.Open);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "fin", CancellationToken.None);
            }
            finally
            {
                try { proceso.Kill(); } catch (Exception) { }
                proceso.WaitForExit();
            }
        }
    }
}
