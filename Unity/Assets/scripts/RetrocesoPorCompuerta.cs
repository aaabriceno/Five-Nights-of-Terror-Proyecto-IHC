using System;

/// <summary>
/// La fase de la compuerta cerrada en la cara (paso 7): el animatrónico llega
/// a la boca de su ducto (su penúltimo punto) con la compuerta cerrada y la
/// empuja `SegundosEmpujando`. Si en ese tiempo el jugador 1 la abre, la fase
/// se corta y vuelve a sus reglas de siempre (puede atacar). Si aguanta
/// cerrada, el animatrónico retrocede `PuntosQueRetrocede` puntos de su ruta:
/// hasta la entrada del ducto (V01 Vixy, V05 Puppet), donde la luz se pone
/// amarilla. Desde ahí vuelve a avanzar como siempre.
///
/// Sin `UnityEngine`, para probarla fuera del editor; el cerebro del
/// animatrónico le dice cada frame si está en la boca y si está cerrada.
/// </summary>
public sealed class RetrocesoPorCompuerta
{
    public const float SegundosEmpujando = 4f;
    public const int PuntosQueRetrocede = 3;

    private float segundos;

    /// Está en la boca empujando la compuerta cerrada (la placa tiembla).
    public bool Empujando => segundos > 0f;

    /// Devuelve true una vez, cuando la compuerta aguantó la fase completa:
    /// es el momento de retroceder. Fuera de la boca o con la compuerta
    /// abierta la fase vuelve a cero.
    public bool Avanzar(float segundosTranscurridos, bool enLaBoca, bool cerrada)
    {
        if (!enLaBoca || !cerrada)
        {
            segundos = 0f;
            return false;
        }
        segundos += Math.Max(0f, segundosTranscurridos);
        if (segundos < SegundosEmpujando) return false;
        segundos = 0f;
        return true;
    }

    public void Reiniciar() { segundos = 0f; }

    /// El índice de la ruta al que vuelve desde la boca (`indiceDeLaBoca`, su penúltimo punto).
    public static int PuntoAlQueVuelve(int indiceDeLaBoca) => Math.Max(0, indiceDeLaBoca - PuntosQueRetrocede);
}
