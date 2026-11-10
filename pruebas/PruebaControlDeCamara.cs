using System;

// Pruebas de ControlDeCamara fuera de Unity (ver probar_reglas_de_operador.sh).
public static class PruebaControlDeCamara
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static bool Cerca(float a, float b, float margen = 0.5f) { return Math.Abs(a - b) <= margen; }

    // Avanza el reloj `segundos`. Si `direccion` no es null, la tablet repite la orden cada 0,15 s (como el dedo apoyado).
    private static float ahora;
    private static void Simular(ControlDeCamara c, float segundos, int? direccion)
    {
        const float paso = 0.01f;
        float proximaOrden = ahora;
        for (float t = 0f; t < segundos - 1e-4f; t += paso)
        {
            if (direccion.HasValue && ahora >= proximaOrden - 1e-4f)
            {
                c.Ordenar(direccion.Value, ahora);
                proximaOrden = ahora + 0.15f;
            }
            ahora += paso;
            c.Actualizar(paso, ahora);
        }
    }

    public static int Main()
    {
        var c = new ControlDeCamara();
        Comprobar("empieza mirando al frente", c.Yaw == 0f);

        Simular(c, 0.5f, 1);
        Comprobar("mantener la derecha medio segundo gira unos 35°", Cerca(c.Yaw, 35f, 1.5f));
        Simular(c, 5f, 1);
        Comprobar("no pasa de 75° a la derecha", Cerca(c.Yaw, ControlDeCamara.GradosMaximos, 0.01f));

        c.Ordenar(0, ahora);
        Simular(c, 0.5f, null);
        Comprobar("al soltar vuelve al centro a 90°/s: a los 0,5 s van unos 30°", Cerca(c.Yaw, 30f, 1.5f));
        Simular(c, 2f, null);
        Comprobar("y llega al centro sin pasarse", c.Yaw == 0f);

        Simular(c, 5f, -1);
        Comprobar("mantener la izquierda llega a -75° y no pasa", Cerca(c.Yaw, -ControlDeCamara.GradosMaximos, 0.01f));

        Simular(c, 0.5f, 1);
        Comprobar("cambiar de lado sin soltar: la última orden manda", c.Yaw > -ControlDeCamara.GradosMaximos + 30f);

        // La tablet se pierde sin avisar que soltó el botón: una orden vieja no deja la cámara girando.
        c.Centrar();
        Comprobar("Centrar deja la cámara al frente de golpe", c.Yaw == 0f);
        c.Ordenar(1, ahora);
        Simular(c, 3f, null);
        Comprobar("sin nuevas órdenes (se perdió el «soltar») la cámara vuelve sola al centro", c.Yaw == 0f);

        c.Ordenar(1, ahora);
        float antes = ahora;
        Simular(c, 0.3f, null);
        Comprobar("durante los primeros 0,5 s de una orden sigue girando", c.Yaw > 15f && ahora - antes < 0.5f);

        c.Centrar();
        c.Ordenar(7, ahora);
        Simular(c, 0.1f, null);
        Comprobar("una dirección fuera de -1..1 se toma como el lado que indica, no como velocidad", Cerca(c.Yaw, 7f, 1f));
        c.Centrar();
        c.Ordenar(-9, ahora);
        Simular(c, 0.1f, null);
        Comprobar("lo mismo hacia la izquierda", Cerca(c.Yaw, -7f, 1f));

        Console.WriteLine(fallos == 0 ? "\nTodas las pruebas pasaron." : "\n" + fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
