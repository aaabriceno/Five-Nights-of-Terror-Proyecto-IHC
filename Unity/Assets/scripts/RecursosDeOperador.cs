using System.Collections.Generic;

/// <summary>
/// Lo que el jugador 2 (operador) tiene y gasta: cargas de trampa, la
/// habilidad de rescatar a Puppet y las trampas puestas en el mapa. Las gana
/// resolviendo las tareas de la terminal (ver TareasDeOperador): un cálculo da
/// 1 carga y un circuito lógico desbloquea el rescate.
/// Vive aparte, sin UnityEngine, para probarla fuera del editor; el tiempo lo
/// pone quien la llama (p. ej. `Time.time`).
///
/// Los valores son una propuesta inicial para jugar y ajustar con el profesor.
/// </summary>
public class RecursosDeOperador
{
    public enum ResultadoTrampa { Colocada, SinCargas, PuntoOcupado, PuntoInvalido }

    /// Cuánto se queda quieto un animatrónico al pisar una trampa.
    public const float SegundosDeTrampa = 6f;
    public const int CargasIniciales = 1;
    public const int MaxCargas = 3;
    /// Circuitos lógicos que hacen falta para desbloquear el rescate de Puppet
    /// (antes eran 3 mini-tareas iguales de memoria).
    public const int CircuitosParaHabilidad = 1;

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

    public int Cargas { get; private set; }
    /// Circuitos resueltos hacia el rescate de Puppet (0 hasta CircuitosParaHabilidad).
    public int Progreso { get; private set; }
    public bool CargasLlenas { get { return Cargas >= MaxCargas; } }
    public bool HabilidadPuppet { get { return Progreso >= CircuitosParaHabilidad; } }
    public IReadOnlyCollection<string> Trampas { get { return trampas; } }

    public RecursosDeOperador() { Reiniciar(); }

    public void Reiniciar()
    {
        Cargas = CargasIniciales;
        Progreso = 0;
        trampas.Clear();
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

    /// Un cálculo bien resuelto: 1 carga de trampa (sin pasar del máximo).
    public void GanarCargaPorCalculo() { if (Cargas < MaxCargas) Cargas++; }

    /// Un circuito bien resuelto: acerca (o da) el rescate de Puppet.
    public void GanarHabilidadPorCircuito() { if (Progreso < CircuitosParaHabilidad) Progreso++; }

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

    /// Qué le pasó al compañero desde la última vez que se preguntó. Sirve para
    /// avisar al jugador 1 una sola vez por cada entrada o caída.
    public enum Cambio { SinCambio, Conectado, Perdido }

    private float ultimoLatido = float.NegativeInfinity;
    private bool estabaVivo;

    public void Latir(float ahora) { ultimoLatido = ahora; }

    public bool EstaVivo(float ahora) { return ahora - ultimoLatido <= SegundosParaDarloPorCaido; }

    public void Reiniciar() { ultimoLatido = float.NegativeInfinity; estabaVivo = false; }

    /// Pregúntalo cada cuadro: devuelve Conectado o Perdido solo en el cuadro
    /// en que cambia el estado, y SinCambio el resto del tiempo.
    public Cambio RevisarCambio(float ahora)
    {
        bool vivo = EstaVivo(ahora);
        if (vivo == estabaVivo) return Cambio.SinCambio;
        estabaVivo = vivo;
        return vivo ? Cambio.Conectado : Cambio.Perdido;
    }

    /// Al empezar una partida: lo que ya había (un compañero conectado de
    /// antes) se vuelve a anunciar, para que la tablet de esa partida lo sepa.
    public void OlvidarCambio() { estabaVivo = false; }
}
