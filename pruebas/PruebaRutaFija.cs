using System;
using System.Collections.Generic;

// Pruebas de RutaFija fuera de Unity (ver probar_reglas_de_operador.sh).
public static class PruebaRutaFija
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    // Recorre la ruta entera y devuelve los ids en el orden en que se visitan.
    private static List<int> Recorrer(RutaFija ruta, float probabilidad, Random azar)
    {
        var visitados = new List<int>();
        for (int i = 0; i < 100; i++)
        {
            int siguiente = ruta.Siguiente(probabilidad, () => (float)azar.NextDouble());
            if (siguiente < 0) break;
            visitados.Add(siguiente);
        }
        return visitados;
    }

    public static int Main()
    {
        // 6 puntos principales (0..5, el 5 es el penúltimo). Los desvíos tienen id 10, 11...
        // 10 sale después del punto 2; 11 y 12 salen después del 3.
        var sinDesvios = new RutaFija(6, 10, new int[0]);
        var visitas = Recorrer(sinDesvios, 1f, new Random(1));
        Comprobar("sin desvíos recorre los puntos en orden, sin saltarse ninguno",
            string.Join(",", visitas) == "1,2,3,4,5");
        Comprobar("al llegar al penúltimo ya no hay siguiente",
            sinDesvios.Siguiente(1f, () => 0f) == -1);

        var conDesvios = new RutaFija(6, 10, new[] { 2, 3, 3 });
        visitas = Recorrer(conDesvios, 0f, new Random(1));
        Comprobar("con probabilidad 0 nunca hace un desvío", string.Join(",", visitas) == "1,2,3,4,5");

        conDesvios.Reiniciar();
        visitas = Recorrer(conDesvios, 1f, new Random(1));
        Comprobar("con probabilidad 1 hace un desvío en cada tramo que lo tiene y siempre vuelve a la ruta",
            visitas.Count == 7 && visitas[0] == 1 && visitas[1] == 2 && visitas[2] == 10 && visitas[3] == 3
            && (visitas[4] == 11 || visitas[4] == 12) && visitas[5] == 4 && visitas[6] == 5);

        conDesvios.Reiniciar();
        int primeroDeLosDos = conDesvios.Siguiente(1f, () => 0f);   // 1
        conDesvios.Siguiente(1f, () => 0f);                          // 2
        conDesvios.Siguiente(1f, () => 0f);                          // desvío 10
        conDesvios.Siguiente(1f, () => 0f);                          // 3
        int elegidoConAzarBajo = conDesvios.Siguiente(1f, () => 0f);
        conDesvios.Reiniciar();
        conDesvios.Siguiente(1f, () => 0f); conDesvios.Siguiente(1f, () => 0f);
        conDesvios.Siguiente(1f, () => 0f); conDesvios.Siguiente(1f, () => 0f);
        int elegidoConAzarAlto = conDesvios.Siguiente(1f, () => 0.99f);
        Comprobar("entre varios desvíos del mismo tramo, el azar decide cuál",
            primeroDeLosDos == 1 && elegidoConAzarBajo == 11 && elegidoConAzarAlto == 12);

        // Con mucho azar: nunca retrocede, nunca repite, un solo desvío por tramo, y termina en el penúltimo.
        bool nuncaRetrocede = true, terminaBien = true, unoPorTramo = true;
        var azarGeneral = new Random(7);
        for (int vuelta = 0; vuelta < 2000; vuelta++)
        {
            var ruta = new RutaFija(6, 10, new[] { 2, 3, 3 });
            var v = Recorrer(ruta, 0.5f, azarGeneral);
            int ultimoPrincipal = 0, desviosSeguidos = 0;
            foreach (int id in v)
            {
                if (id >= 10) { desviosSeguidos++; if (desviosSeguidos > 1) unoPorTramo = false; continue; }
                desviosSeguidos = 0;
                if (id != ultimoPrincipal + 1) nuncaRetrocede = false;
                ultimoPrincipal = id;
            }
            if (ultimoPrincipal != 5) terminaBien = false;
        }
        Comprobar("en 2000 recorridos al azar nunca se salta ni repite un punto principal", nuncaRetrocede);
        Comprobar("siempre termina en el penúltimo punto", terminaBien);
        Comprobar("nunca hace dos desvíos seguidos", unoPorTramo);

        // Un desvío que después del penúltimo o fuera de la ruta no se usa.
        var raros = new RutaFija(4, 10, new[] { 3, 9 });
        visitas = Recorrer(raros, 1f, new Random(1));
        Comprobar("un desvío colgado del penúltimo (o de un punto que no existe) se ignora",
            string.Join(",", visitas) == "1,2,3");

        // Si un desvío no se puede recorrer (sin malla), se abandona y se sigue por la ruta.
        var r = new RutaFija(6, 10, new[] { 2 });
        r.Siguiente(1f, () => 0f); r.Siguiente(1f, () => 0f);
        int d = r.Siguiente(1f, () => 0f);
        r.AbandonarDesvio();
        int tras = r.Siguiente(0f, () => 0f);
        Comprobar("abandonar el desvío deja seguir con el punto principal siguiente", d == 10 && tras == 3);

        // Reiniciar vuelve al inicio.
        r.Reiniciar();
        Comprobar("reiniciar empieza otra vez desde el primer punto", r.Siguiente(0f, () => 0f) == 1);

        Console.WriteLine(fallos == 0 ? "\nTodas las pruebas pasaron." : "\n" + fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
