using System;
using System.Collections.Generic;

/// <summary>
/// El recorrido de un animatrónico: una ruta fija (los puntos principales, en
/// orden, hasta el punto penúltimo) con desvíos ocasionales. Cada animatrónico
/// tiene su camino y el jugador 2 puede aprenderlo; el azar solo mete algún
/// desvío entre dos puntos, nunca se salta un punto ni retrocede.
///
/// Sin `UnityEngine`, para probarla fuera del editor. Trabaja con números:
/// los puntos principales son 0..n-1 (el n-1 es el penúltimo) y cada desvío
/// tiene un id propio, `primerIdDeDesvio + k`. Quien la usa (el cerebro del
/// animatrónico) traduce los ids a puntos del mapa y llama a `Siguiente` solo
/// cuando el animatrónico ya llegó al destino anterior.
/// </summary>
public class RutaFija
{
    private readonly int cantidadDePuntos;
    private readonly int primerIdDeDesvio;
    private readonly List<int> despuesDe;

    private int posicion;
    private int desvioEnCurso = -1;

    /// <param name="cantidadDePuntos">Puntos principales, contando el penúltimo.</param>
    /// <param name="primerIdDeDesvio">Id del primer desvío; no debe chocar con los puntos principales.</param>
    /// <param name="desviosDespuesDe">Para cada desvío k, el punto principal tras el que se hace (entre ese y el siguiente).</param>
    public RutaFija(int cantidadDePuntos, int primerIdDeDesvio, int[] desviosDespuesDe)
    {
        this.cantidadDePuntos = cantidadDePuntos;
        this.primerIdDeDesvio = primerIdDeDesvio;
        despuesDe = new List<int>(desviosDespuesDe ?? new int[0]);
        Reiniciar();
    }

    /// Último punto principal que alcanzó (0 = donde empieza).
    public int Posicion { get { return posicion; } }

    public bool EsDesvio(int id) { return id >= primerIdDeDesvio; }

    public void Reiniciar()
    {
        posicion = 0;
        desvioEnCurso = -1;
    }

    /// El siguiente destino: el punto principal que sigue, o un desvío con la
    /// probabilidad dada (si el tramo tiene alguno). -1 si ya está en el
    /// penúltimo. `azar` devuelve un número en [0, 1).
    public int Siguiente(float probabilidadDeDesvio, Func<float> azar)
    {
        if (desvioEnCurso >= 0)
        {
            // Ya llegó al desvío: vuelve a la ruta.
            desvioEnCurso = -1;
            return Avanzar();
        }
        if (posicion >= cantidadDePuntos - 1) return -1;

        List<int> candidatos = DesviosDelTramo();
        if (candidatos.Count > 0 && azar() < probabilidadDeDesvio)
        {
            int elegido = (int)(azar() * candidatos.Count);
            if (elegido >= candidatos.Count) elegido = candidatos.Count - 1;
            desvioEnCurso = candidatos[elegido];
            return desvioEnCurso;
        }
        return Avanzar();
    }

    /// El desvío elegido no se puede recorrer (sin malla de navegación): se
    /// queda en el punto en que estaba y la siguiente llamada sigue la ruta.
    public void AbandonarDesvio() { desvioEnCurso = -1; }

    private int Avanzar()
    {
        if (posicion >= cantidadDePuntos - 1) return -1;
        posicion++;
        return posicion;
    }

    private List<int> DesviosDelTramo()
    {
        var ids = new List<int>();
        for (int k = 0; k < despuesDe.Count; k++)
            if (despuesDe[k] == posicion) ids.Add(primerIdDeDesvio + k);
        return ids;
    }
}
