using System;
using System.Collections.Generic;

/// <summary>
/// Regla de lo que puede hacer el jugador 2 (operador): retrasar a un
/// animatrónico por un rato, con un tiempo de espera antes de repetir. Vive
/// aparte, sin UnityEngine, para poder probarla fuera del editor; el tiempo lo
/// pone quien la llama (p. ej. `Time.time`).
///
/// Los tres valores son una propuesta inicial para jugar y ajustar con el
/// profesor, no un balance definitivo.
/// </summary>
public class FrenoDeOperador
{
    public enum Resultado { Aceptada, AnimatronicoDesconocido, EnEnfriamiento }

    /// Cuánto dura el retraso.
    public const float DuracionSegundos = 8f;
    /// Desde que se usa hasta que se puede volver a usar sobre el mismo animatrónico.
    public const float EnfriamientoSegundos = 20f;
    /// Fracción de la velocidad normal mientras dura el retraso.
    public const float FactorRalentizado = 0.5f;

    public static readonly string[] Animatronicos = { "freddy", "vixy", "puppet" };

    private readonly Dictionary<string, float> ultimoUso = new Dictionary<string, float>();

    /// Normaliza el nombre ("  PuPpEt " -> "puppet"), o null si no es uno de los tres.
    public static string Normalizar(string animatronico)
    {
        if (string.IsNullOrEmpty(animatronico)) return null;
        string limpio = animatronico.Trim().ToLowerInvariant();
        return Array.IndexOf(Animatronicos, limpio) >= 0 ? limpio : null;
    }

    public Resultado Intentar(string animatronico, float ahora)
    {
        string nombre = Normalizar(animatronico);
        if (nombre == null) return Resultado.AnimatronicoDesconocido;
        if (SegundosDeEspera(nombre, ahora) > 0f) return Resultado.EnEnfriamiento;
        ultimoUso[nombre] = ahora;
        return Resultado.Aceptada;
    }

    /// 1 si el animatrónico va normal; `FactorRalentizado` mientras dura el retraso.
    public float FactorDeVelocidad(string animatronico, float ahora) =>
        SegundosDeRetraso(animatronico, ahora) > 0f ? FactorRalentizado : 1f;

    /// Segundos que le quedan al retraso (0 si no está retrasado).
    public float SegundosDeRetraso(string animatronico, float ahora)
    {
        string nombre = Normalizar(animatronico);
        if (nombre == null || !ultimoUso.TryGetValue(nombre, out float inicio)) return 0f;
        return Math.Max(0f, DuracionSegundos - (ahora - inicio));
    }

    /// Segundos que faltan para poder volver a usarlo (0 si ya se puede).
    public float SegundosDeEspera(string animatronico, float ahora)
    {
        string nombre = Normalizar(animatronico);
        if (nombre == null || !ultimoUso.TryGetValue(nombre, out float inicio)) return 0f;
        return Math.Max(0f, EnfriamientoSegundos - (ahora - inicio));
    }

    /// Nueva noche: nadie queda retrasado ni en espera.
    public void Reiniciar() => ultimoUso.Clear();
}
