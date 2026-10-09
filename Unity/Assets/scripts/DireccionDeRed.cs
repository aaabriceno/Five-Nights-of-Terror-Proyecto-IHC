using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>
/// Averigua la IP de esta PC en la red local: la que la tablet debe usar para
/// conectarse al relay. No usa UnityEngine para poder probarse fuera del
/// editor (pruebas/probar_direccion_de_red.sh).
/// </summary>
public static class DireccionDeRed
{
    /// IP como texto, o null si la PC no está en ninguna red.
    public static string ObtenerIpLocal()
    {
        string porRuta = IpDeLaRutaPorDefecto();
        if (porRuta != null) return porRuta;

        // Sin ruta por defecto (red sin salida a internet): se toma la primera
        // interfaz activa con una IPv4 privada que la tablet pueda alcanzar.
        foreach (NetworkInterface interfaz in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (interfaz.OperationalStatus != OperationalStatus.Up) continue;
            foreach (UnicastIPAddressInformation informacion in interfaz.GetIPProperties().UnicastAddresses)
            {
                IPAddress direccion = informacion.Address;
                if (EsUtilParaLaTablet(direccion, interfaz.Name) && EsPrivada(direccion))
                    return direccion.ToString();
            }
        }
        return null;
    }

    /// Un socket UDP "conectado" no envía ningún paquete, pero obliga al
    /// sistema a elegir la interfaz por la que saldría el tráfico: esa es la de
    /// la red real (WiFi o cable), no la de Docker ni la de loopback.
    private static string IpDeLaRutaPorDefecto()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint local = socket.LocalEndPoint as IPEndPoint;
                if (local != null && EsUtilParaLaTablet(local.Address, string.Empty))
                    return local.Address.ToString();
            }
        }
        catch (SocketException)
        {
        }
        return null;
    }

    /// La tablet solo puede llegar a una IPv4 de la red física: se descartan
    /// loopback, link-local (169.254.x.x, señal de que no hubo DHCP) y las
    /// redes virtuales de Docker, que existen en la PC pero no hacia afuera.
    public static bool EsUtilParaLaTablet(IPAddress direccion, string nombreInterfaz)
    {
        if (direccion.AddressFamily != AddressFamily.InterNetwork) return false;
        if (IPAddress.IsLoopback(direccion)) return false;

        string nombre = nombreInterfaz ?? string.Empty;
        if (nombre.StartsWith("docker") || nombre.StartsWith("br-") || nombre.StartsWith("veth")) return false;

        byte[] bytes = direccion.GetAddressBytes();
        if (bytes[0] == 169 && bytes[1] == 254) return false;
        if (bytes[0] == 172 && bytes[1] == 17) return false;
        return true;
    }

    /// Texto que ve el jugador en la pantalla de espera. Sin red no hay IP que
    /// mostrar, así que se le dice qué hacer en vez de dejar el campo vacío.
    public static string TextoParaConectar(string ip, int puerto) =>
        ip != null
            ? $"Conecta la tablet a {ip} : {puerto}"
            : "Sin red — conecta la PC al WiFi";

    public static bool EsPrivada(IPAddress direccion)
    {
        if (direccion.AddressFamily != AddressFamily.InterNetwork) return false;
        byte[] bytes = direccion.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
