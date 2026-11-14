using System;

/// <summary>
/// El semáforo de cada ducto de ventilación: qué tan cerca de la oficina está
/// el animatrónico que entra por ahí. Vixy usa el ducto izquierdo
/// (V01 → V04) y Puppet el derecho (V05 → V08); Freddy no usa ductos.
///
/// * Verde: fuera del ducto (salas, pasillos, la caja de música).
/// * Amarillo: en la primera mitad del ducto.
/// * Rojo: en el último tramo, junto a la boca que da a la oficina.
///
/// La luz depende del punto de la ruta hacia el que va (o en el que está).
/// Sin UnityEngine, para probarla fuera del editor.
/// </summary>
public static class SemaforoDeDuctos
{
    public enum Luz { Verde, Amarillo, Rojo }

    public const string Izquierda = "izquierda";
    public const string Derecha = "derecha";

    private static readonly string[] DuctoIzquierdo = { "V01", "V02", "V03", "V04" };
    private static readonly string[] DuctoDerecho = { "V05", "V06", "V07", "V08" };
    /// El último punto de cada ruta: la posición del jugador 1, dentro de la oficina.
    private const string Oficina = "J1";

    /// Por qué ducto entra un animatrónico con esta ruta, o null si no usa ninguno.
    public static string LadoDeRuta(string[] ruta)
    {
        if (ruta == null) return null;
        foreach (string punto in ruta)
        {
            if (Array.IndexOf(DuctoIzquierdo, punto) >= 0) return Izquierda;
            if (Array.IndexOf(DuctoDerecho, punto) >= 0) return Derecha;
        }
        return null;
    }

    /// La luz del ducto `lado` con su animatrónico yendo a (o parado en) `punto`.
    public static Luz LuzDe(string lado, string punto)
    {
        string[] ducto = lado == Izquierda ? DuctoIzquierdo : lado == Derecha ? DuctoDerecho : null;
        if (ducto == null || punto == null) return Luz.Verde;
        if (punto == Oficina) return Luz.Rojo;
        int i = Array.IndexOf(ducto, punto);
        if (i < 0) return Luz.Verde;
        return i < ducto.Length / 2 ? Luz.Amarillo : Luz.Rojo;
    }

    /// Si dos animatrónicos usaran el mismo ducto, manda el más cercano.
    public static Luz LaPeor(Luz a, Luz b) { return a > b ? a : b; }

    /// Cómo viaja en `estado_oficina`.
    public static string Texto(Luz luz)
    {
        switch (luz)
        {
            case Luz.Rojo: return "rojo";
            case Luz.Amarillo: return "amarillo";
            default: return "verde";
        }
    }
}
