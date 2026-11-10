using System;

/// <summary>
/// Segunda oportunidad contra Puppet: cuando sale de su caja se abre una
/// ventana corta. Si el jugador 2 (con la habilidad ganada) lo rescata dentro
/// de ella, Puppet vuelve a la caja y el jugador 1 paga una multa: la caja se
/// descarga más rápido. Sin `UnityEngine`, para probarla fuera del editor.
///
/// Valores iniciales para jugar y ajustar con el profesor.
/// </summary>
public class RescateDePuppet
{
    public enum Resultado { Rescatado, SinVentana, SinHabilidad }

    public const float VentanaSegundos = 6f;
    /// Cuántas veces más rápido se descarga la caja por cada rescate.
    public const float MultaDeDescarga = 2f;

    /// Lo que dura la caja de música llena, en segundos, cada noche. No baja
    /// de noche en noche: lo único que la acorta son los rescates del jugador
    /// 2 (30 s, 15 s, 7,5 s), y se vuelve a 30 s al empezar otra noche.
    public const float SegundosDeLaCaja = 30f;

    /// Por muy seguido que se rescate a Puppet, la caja no dura menos que esto
    /// (más corta sería imposible de sostener). Se llega con dos rescates.
    public const float SegundosMinimosDeLaCaja = 7.5f;

    private bool abierta;
    private float apertura;

    /// 1 al inicio de la noche; se multiplica por MultaDeDescarga en cada rescate.
    public float FactorDeDescarga { get; private set; }

    /// Lo que dura ahora la caja llena si nadie le da cuerda.
    public float SegundosParaVaciarLaCaja { get { return SegundosDeLaCaja / FactorDeDescarga; } }

    /// Cuánto baja la caja por segundo (en las mismas unidades que `maximo`).
    public float DescargaPorSegundo(float maximo) { return maximo / SegundosParaVaciarLaCaja; }

    /// Cuánto sube por segundo mientras el jugador 1 le da cuerda: llena la
    /// caja en la mitad de su duración base (15 s) y la multa no lo cambia.
    public float RecargaPorSegundo(float maximo) { return 2f * maximo / SegundosDeLaCaja; }

    public RescateDePuppet() { Reiniciar(); }

    public void Reiniciar()
    {
        abierta = false;
        FactorDeDescarga = 1f;
    }

    public void AbrirVentana(float ahora)
    {
        abierta = true;
        apertura = ahora;
    }

    public bool VentanaAbierta(float ahora)
    {
        return abierta && ahora - apertura <= VentanaSegundos;
    }

    public float SegundosRestantes(float ahora)
    {
        if (!VentanaAbierta(ahora)) return 0f;
        return VentanaSegundos - (ahora - apertura);
    }

    /// True UNA sola vez cuando la ventana venció sin que nadie rescatara a
    /// Puppet: ahí Puppet sigue su camino como siempre.
    public bool ConsumirVencimiento(float ahora)
    {
        if (!abierta || ahora - apertura <= VentanaSegundos) return false;
        abierta = false;
        return true;
    }

    public Resultado Rescatar(float ahora, bool habilidad)
    {
        if (!VentanaAbierta(ahora)) return Resultado.SinVentana;
        if (!habilidad) return Resultado.SinHabilidad;
        abierta = false;
        FactorDeDescarga = Math.Min(FactorDeDescarga * MultaDeDescarga, SegundosDeLaCaja / SegundosMinimosDeLaCaja);
        return Resultado.Rescatado;
    }
}
