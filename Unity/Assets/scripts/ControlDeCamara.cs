using System;

/// <summary>
/// Hacia dónde mira la cámara del jugador 1. La tablet tiene dos botones
/// grandes (izquierda y derecha) que se mantienen apretados: mientras sigan
/// apretados la cámara gira; al soltarlos vuelve sola al frente. Así mirar un
/// ducto cuesta atención (hay que mantener el botón) y no se puede acampar ahí.
///
/// Sin `UnityEngine`, para probarla fuera del editor. El tiempo lo pone quien
/// la llama (p. ej. `Time.time`).
/// </summary>
public class ControlDeCamara
{
    /// Hasta dónde puede girar a cada lado (grados). Con 75° la boca de un
    /// ducto de la oficina (a unos 51°-56° del frente) queda casi al centro.
    public const float GradosMaximos = 75f;
    public const float GradosPorSegundoAlGirar = 70f;
    public const float GradosPorSegundoAlVolver = 90f;
    /// La tablet repite la orden cada ~0,15 s mientras el dedo sigue apoyado.
    /// Si pasa más tiempo sin orden se da por soltado el botón: así un «soltar»
    /// perdido por la red no deja la cámara girando.
    public const float SegundosSinOrdenParaSoltar = 0.5f;

    private int direccion;
    private float ultimaOrden = float.NegativeInfinity;

    /// Giro actual: 0 = al frente, positivo = a la derecha, negativo = a la izquierda.
    public float Yaw { get; private set; }

    /// -1 izquierda, 0 soltó, 1 derecha. Cualquier otro número vale por su signo.
    public void Ordenar(int lado, float ahora)
    {
        direccion = Math.Sign(lado);
        ultimaOrden = ahora;
    }

    public void Actualizar(float segundos, float ahora)
    {
        int lado = ahora - ultimaOrden <= SegundosSinOrdenParaSoltar ? direccion : 0;
        if (lado != 0)
        {
            Yaw = Math.Max(-GradosMaximos, Math.Min(GradosMaximos, Yaw + lado * GradosPorSegundoAlGirar * segundos));
            return;
        }
        float vuelta = GradosPorSegundoAlVolver * segundos;
        if (Math.Abs(Yaw) <= vuelta) Yaw = 0f;
        else Yaw -= Math.Sign(Yaw) * vuelta;
    }

    /// Al empezar o terminar una partida: de golpe al frente y sin órdenes pendientes.
    public void Centrar()
    {
        Yaw = 0f;
        direccion = 0;
        ultimaOrden = float.NegativeInfinity;
    }
}
