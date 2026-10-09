using System;
using System.Net;

// Pruebas de DireccionDeRed fuera de Unity: está fuera de Assets/ para que el
// editor no la compile (tiene su propio Main).
public static class PruebaDireccionDeRed
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static bool Util(string ip, string interfaz) =>
        DireccionDeRed.EsUtilParaLaTablet(IPAddress.Parse(ip), interfaz);

    public static int Main()
    {
        Comprobar("acepta la IP del WiFi", Util("192.168.1.7", "wlo1"));
        Comprobar("acepta una red 10.x", Util("10.20.30.40", "eth0"));
        Comprobar("descarta loopback", !Util("127.0.0.1", "lo"));
        Comprobar("descarta el puente de Docker por dirección", !Util("172.17.0.1", "docker0"));
        Comprobar("descarta otras redes de Docker por nombre", !Util("172.18.0.1", "br-1a2b3c4d"));
        Comprobar("descarta link-local sin DHCP", !Util("169.254.10.20", "wlo1"));
        Comprobar("descarta IPv6", !Util("fe80::1", "wlo1"));
        Comprobar("192.168.x es privada", DireccionDeRed.EsPrivada(IPAddress.Parse("192.168.0.5")));
        Comprobar("172.20.x es privada", DireccionDeRed.EsPrivada(IPAddress.Parse("172.20.1.1")));
        Comprobar("8.8.8.8 no es privada", !DireccionDeRed.EsPrivada(IPAddress.Parse("8.8.8.8")));

        Comprobar("texto con IP indica a dónde conectar la tablet",
            DireccionDeRed.TextoParaConectar("192.168.1.7", 8000) == "Conecta la tablet a 192.168.1.7 : 8000");
        Comprobar("texto sin red pide conectar la PC",
            DireccionDeRed.TextoParaConectar(null, 8000) == "Sin red — conecta la PC al WiFi");

        string ip = DireccionDeRed.ObtenerIpLocal();
        Console.WriteLine("  info  IP detectada en esta PC: " + (ip ?? "(ninguna)"));
        Comprobar("la IP detectada no es la de Docker", ip == null || !ip.StartsWith("172.17."));
        Comprobar("la IP detectada no es loopback", ip == null || !ip.StartsWith("127."));

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
