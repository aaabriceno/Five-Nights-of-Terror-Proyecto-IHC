using System;
using System.Collections.Generic;

/// <summary>
/// Lo que el jugador 2 (operador) tiene y gasta: cargas de trampa, la
/// habilidad de rescatar a Puppet y las trampas puestas en el mapa. Las gana
/// resolviendo las tareas de la terminal (ver TareasDeOperador): un cálculo da
/// 1 carga y una ecuación desbloquea el rescate.
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

    /// Cuántos cálculos puede resolver el jugador 2 en cada noche (2026-11-22,
    /// decidido por Anthony). Antes eran infinitos y con ellos las trampas y la
    /// batería del jugador 1 tampoco se acababan. Solo gastan cupo los cálculos
    /// bien resueltos; el cupo vuelve a llenarse al empezar cada noche.
    private static readonly int[] CalculosPorNocheTabla = { 6, 5, 4, 3, 3, 3 };

    /// Cupo de cálculos de una noche (las noches fuera de 1-6 se ajustan).
    public static int CalculosPorNoche(int noche)
    {
        int indice = Math.Max(1, Math.Min(CalculosPorNocheTabla.Length, noche)) - 1;
        return CalculosPorNocheTabla[indice];
    }
    /// Ecuaciones que hacen falta para desbloquear el rescate de Puppet
    /// (antes eran 3 mini-tareas iguales de memoria).
    public const int EcuacionesParaHabilidad = 1;

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
    /// Cupo de cálculos de esta noche y cuántos quedan.
    public int CalculosDeLaNoche { get; private set; }
    public int CalculosRestantes { get; private set; }
    public bool QuedanCalculos { get { return CalculosRestantes > 0; } }
    /// Ecuaciones resueltas hacia el rescate de Puppet (0 hasta EcuacionesParaHabilidad).
    public int Progreso { get; private set; }
    public bool CargasLlenas { get { return Cargas >= MaxCargas; } }
    public bool HabilidadPuppet { get { return Progreso >= EcuacionesParaHabilidad; } }
    public IReadOnlyCollection<string> Trampas { get { return trampas; } }

    public RecursosDeOperador() { Reiniciar(); }

    /// Al empezar una noche: 1 carga, sin rescate, sin trampas y el cupo de
    /// cálculos de esa noche.
    public void Reiniciar(int noche = 1)
    {
        Cargas = CargasIniciales;
        CalculosDeLaNoche = CalculosPorNoche(noche);
        CalculosRestantes = CalculosDeLaNoche;
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

    /// Un cálculo bien resuelto: gasta 1 del cupo de la noche y da 1 carga de
    /// trampa (sin pasar del máximo). Sin cupo no cuenta: devuelve false y no
    /// da nada (quien llama tampoco debe recargar la batería).
    public bool GanarCargaPorCalculo()
    {
        if (!QuedanCalculos) return false;
        CalculosRestantes--;
        if (Cargas < MaxCargas) Cargas++;
        return true;
    }

    /// Una ecuación bien resuelta: acerca (o da) el rescate de Puppet.
    public void GanarHabilidadPorEcuacion() { if (Progreso < EcuacionesParaHabilidad) Progreso++; }

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
