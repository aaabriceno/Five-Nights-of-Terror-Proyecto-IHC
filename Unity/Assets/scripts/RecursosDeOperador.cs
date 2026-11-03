using System.Collections.Generic;

/// <summary>
/// Lo que el jugador 2 (operador) tiene y gasta: cargas de trampa, el progreso
/// hacia la habilidad de rescatar a Puppet y las trampas puestas en el mapa.
/// Vive aparte, sin UnityEngine, para probarla fuera del editor; el tiempo lo
/// pone quien la llama (p. ej. `Time.time`).
///
/// Los valores son una propuesta inicial para jugar y ajustar con el profesor.
/// </summary>
public class RecursosDeOperador
{
    public enum ResultadoTrampa { Colocada, SinCargas, PuntoOcupado, PuntoInvalido }
    public enum ResultadoMiniTarea { Aceptada, MuyRapida, SinInicio }

    /// Cuánto se queda quieto un animatrónico al pisar una trampa.
    public const float SegundosDeTrampa = 6f;
    public const int CargasIniciales = 1;
    public const int MaxCargas = 3;
    /// Mini-tareas que hacen falta para desbloquear el rescate de Puppet.
    public const int MiniTareasParaHabilidad = 3;
    /// Menos que esto desde el inicio es imposible de hacer a mano: Unity lo
    /// rechaza para que la página no pueda fingir la mini-tarea.
    public const float MinimoSegundosMiniTarea = 3f;

    /// Con un segundo jugador que ayuda, los animatrónicos son más agresivos:
    /// el intervalo entre avances se multiplica por este factor (menor = más
    /// rápido). Freddy pasa de unos 22 s a unos 9 s en la noche 1. Valores
    /// iniciales para ajustar con el profesor; Puppet no cambia.
    public const float AgresividadDeFreddy = 0.4f;
    public const float AgresividadDeVixy = 0.7f;

    /// Factor del intervalo de avance de un animatrónico según haya o no un
    /// operador conectado. 1 = sin cambio.
    public static float FactorDeAgresividad(string animatronico, bool operadorVivo)
    {
        if (!operadorVivo || animatronico == null) return 1f;
        switch (animatronico.ToLowerInvariant())
        {
            case "freddy": return AgresividadDeFreddy;
            case "vixy": return AgresividadDeVixy;
            default: return 1f;
        }
    }

    private readonly HashSet<string> trampas = new HashSet<string>();
    private float inicioDeMiniTarea = -1f;

    public int Cargas { get; private set; }
    public int Progreso { get; private set; }
    public bool HabilidadPuppet { get { return Progreso >= MiniTareasParaHabilidad; } }
    public IReadOnlyCollection<string> Trampas { get { return trampas; } }

    public RecursosDeOperador() { Reiniciar(); }

    public void Reiniciar()
    {
        Cargas = CargasIniciales;
        Progreso = 0;
        trampas.Clear();
        inicioDeMiniTarea = -1f;
    }

    public ResultadoTrampa ColocarTrampa(string punto, bool puntoValido)
    {
        if (!puntoValido || string.IsNullOrEmpty(punto)) return ResultadoTrampa.PuntoInvalido;
        if (trampas.Contains(punto)) return ResultadoTrampa.PuntoOcupado;
        if (Cargas <= 0) return ResultadoTrampa.SinCargas;
        Cargas--;
        trampas.Add(punto);
        return ResultadoTrampa.Colocada;
    }

    /// Quita la trampa si existe. Devuelve true si había una.
    public bool DispararTrampa(string punto) { return trampas.Remove(punto); }

    public void IniciarMiniTarea(float ahora) { inicioDeMiniTarea = ahora; }

    public ResultadoMiniTarea CompletarMiniTarea(float ahora)
    {
        if (inicioDeMiniTarea < 0f) return ResultadoMiniTarea.SinInicio;
        if (ahora - inicioDeMiniTarea < MinimoSegundosMiniTarea) return ResultadoMiniTarea.MuyRapida;
        inicioDeMiniTarea = -1f;
        if (Cargas < MaxCargas) Cargas++;
        Progreso++;
        return ResultadoMiniTarea.Aceptada;
    }

    /// Usa la habilidad de rescatar a Puppet: hay que volver a ganarla.
    public bool GastarHabilidadPuppet()
    {
        if (!HabilidadPuppet) return false;
        Progreso = 0;
        return true;
    }
}

/// <summary>
/// ¿Sigue conectado el jugador 2? La página manda un latido (`ping_operador`)
/// cada 2 s; si pasan más de 6 s sin latido se da por caído, porque el relay
/// no avisa a Unity cuando un navegador se desconecta.
/// </summary>
public class PresenciaDeOperador
{
    public const float SegundosParaDarloPorCaido = 6f;

    private float ultimoLatido = float.NegativeInfinity;

    public void Latir(float ahora) { ultimoLatido = ahora; }

    public bool EstaVivo(float ahora) { return ahora - ultimoLatido <= SegundosParaDarloPorCaido; }

    public void Reiniciar() { ultimoLatido = float.NegativeInfinity; }
}
