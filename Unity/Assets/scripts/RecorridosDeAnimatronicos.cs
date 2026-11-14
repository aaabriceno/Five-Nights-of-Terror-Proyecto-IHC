using System;

/// <summary>
/// Por dónde va cada animatrónico: su ruta fija (nombres de los puntos de la
/// escena, en orden, terminando en la posición del jugador 1) y los desvíos
/// ocasionales que puede hacer entre dos puntos de esa ruta. Vive aparte, sin
/// UnityEngine, para poder probar las rutas fuera del editor;
/// UnityGameSessionController busca estos nombres en la escena.
/// </summary>
public static class RecorridosDeAnimatronicos
{
    /// Un desvío: entre el punto `despuesDe` de la ruta (índice) y el siguiente,
    /// a veces pasa por `punto` y vuelve a la ruta.
    public struct Desvio
    {
        public int despuesDe;
        public string punto;
        public Desvio(int despuesDe, string punto) { this.despuesDe = despuesDe; this.punto = punto; }
    }

    /// Metros por segundo al caminar entre dos puntos (los tres). Era 1,5 (el
    /// valor de la escena); se subió a 2 el 2026-11-13 porque iban muy lentos.
    /// Retrasar del jugador 2 la sigue bajando a la mitad.
    public const float VelocidadAlCaminar = 2f;

    private static readonly string[] RutaDeFreddy ={ "P01", "P03", "P04", "P05", "P06", "P07", "J1" };
    // Del escenario baja por el comedor al pasillo izquierdo y entra al ducto izquierdo.
    private static readonly string[] RutaDeVixy = { "P02", "P11", "P12", "V01", "V02", "V03", "V04", "J1" };
    private static readonly string[] RutaDePuppet = { "P13", "P14", "P15", "P16", "V05", "V06", "V07", "V08", "J1" };

    // Freddy a veces se asoma a un lado del comedor. Vixy ya no se desvía:
    // su desvío era P10, arriba del todo en el pasillo izquierdo, y como el
    // escenario no tiene puerta a ese pasillo la hacía subir 35 m hasta la
    // esquina y bajar otra vez antes de entrar al ducto.
    private static readonly Desvio[] DesviosDeFreddy = { new Desvio(2, "P08"), new Desvio(2, "P09") };

    /// Una copia de la ruta (vacía si no se conoce al animatrónico).
    public static string[] Ruta(string animatronico)
    {
        switch (Normalizar(animatronico))
        {
            case "freddy": return (string[])RutaDeFreddy.Clone();
            case "vixy": return (string[])RutaDeVixy.Clone();
            case "puppet": return (string[])RutaDePuppet.Clone();
            default: return new string[0];
        }
    }

    /// Una copia de sus desvíos (vacía si no tiene).
    public static Desvio[] Desvios(string animatronico)
    {
        switch (Normalizar(animatronico))
        {
            case "freddy": return (Desvio[])DesviosDeFreddy.Clone();
            default: return new Desvio[0];
        }
    }

    private static string Normalizar(string nombre)
    {
        return string.IsNullOrEmpty(nombre) ? "" : nombre.Trim().ToLowerInvariant();
    }
}
