using System;

/// <summary>
/// Las compuertas de los dos ductos que dan a la oficina. El jugador 1 las
/// cierra y abre desde la tablet: con la compuerta cerrada, el animatrónico de
/// ese ducto no puede entrar ni atacar. Cerradas gastan batería (una sola para
/// las dos, llena al empezar cada noche); al llegar a 0 se abren solas y ya no
/// se pueden cerrar hasta que el jugador 2 recargue con un cálculo.
///
/// La linterna del jugador 1 (paso 8) usa la misma batería: se prende
/// mientras mantiene apretado su botón en la tablet. La tablet repite la orden
/// cada ~0,15 s; si pasa `SegundosSinOrdenParaApagar` sin orden se apaga sola
/// (un «soltar» perdido por la red o la tablet caída no la dejan prendida).
///
/// Sin UnityEngine, para probarla fuera del editor. El tiempo lo pone quien la
/// llama.
/// </summary>
public class CompuertasDeDuctos
{
    public const float BateriaInicial = 100f;
    /// Por cada compuerta cerrada: una sola cerrada todo el tiempo dura 50 s, y
    /// una noche dura de 120 a 165 s, así que no alcanza para tenerlas siempre
    /// cerradas: hay que cerrarlas cuando el semáforo está en rojo.
    public const float ConsumoPorSegundo = 2f;
    /// Lo que suma un cálculo bien resuelto del jugador 2.
    public const float RecargaPorCalculo = 20f;
    /// La linterna prendida gasta lo mismo que una compuerta cerrada.
    public const float ConsumoDeLinterna = 2f;
    public const float SegundosSinOrdenParaApagar = 0.5f;

    private bool cerradaIzquierda;
    private bool cerradaDerecha;
    private float ultimaOrdenDeLinterna;

    public bool LinternaEncendida { get; private set; }

    public float Bateria { get; private set; }
    public bool SinBateria { get { return Bateria <= 0f; } }
    public bool BateriaLlena { get { return Bateria >= BateriaInicial; } }

    public CompuertasDeDuctos() { Reiniciar(); }

    public void Reiniciar()
    {
        Bateria = BateriaInicial;
        cerradaIzquierda = false;
        cerradaDerecha = false;
        LinternaEncendida = false;
    }

    /// La tablet la prende (`encender` = true, repetido mientras el dedo sigue
    /// apoyado) o la apaga. Devuelve false si se quiere prender sin batería.
    public bool OrdenarLinterna(bool encender, float ahora)
    {
        if (encender && SinBateria) return false;
        LinternaEncendida = encender;
        ultimaOrdenDeLinterna = ahora;
        return true;
    }

    /// Cada cuadro: la apaga si pasó demasiado sin órdenes de la tablet.
    public void ActualizarLinterna(float ahora)
    {
        if (LinternaEncendida && ahora - ultimaOrdenDeLinterna > SegundosSinOrdenParaApagar)
            LinternaEncendida = false;
    }

    public bool EstaCerrada(string lado)
    {
        if (lado == SemaforoDeDuctos.Izquierda) return cerradaIzquierda;
        if (lado == SemaforoDeDuctos.Derecha) return cerradaDerecha;
        return false;
    }

    /// Cierra (`cerrar` = true) o abre una compuerta. Devuelve false si el lado
    /// no existe o si se quiere cerrar sin batería.
    public bool Fijar(string lado, bool cerrar)
    {
        if (lado != SemaforoDeDuctos.Izquierda && lado != SemaforoDeDuctos.Derecha) return false;
        if (cerrar && SinBateria) return false;
        if (lado == SemaforoDeDuctos.Izquierda) cerradaIzquierda = cerrar;
        else cerradaDerecha = cerrar;
        return true;
    }

    /// Gasta la batería de `segundos` con las compuertas y la linterna como
    /// están. Devuelve true solo en la llamada en que se acaba (y entonces abre
    /// las compuertas y apaga la linterna).
    public bool Avanzar(float segundos)
    {
        int cerradas = (cerradaIzquierda ? 1 : 0) + (cerradaDerecha ? 1 : 0);
        float consumo = ConsumoPorSegundo * cerradas + (LinternaEncendida ? ConsumoDeLinterna : 0f);
        if (segundos <= 0f || consumo <= 0f || SinBateria) return false;
        Bateria = Math.Max(0f, Bateria - consumo * segundos);
        if (!SinBateria) return false;
        cerradaIzquierda = false;
        cerradaDerecha = false;
        LinternaEncendida = false;
        return true;
    }

    public void Recargar(float porcentaje)
    {
        if (porcentaje <= 0f) return;
        Bateria = Math.Min(BateriaInicial, Bateria + porcentaje);
    }
}
