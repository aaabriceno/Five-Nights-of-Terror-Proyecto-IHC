using System;

// Pruebas de CompuertasDeDuctos fuera de Unity (ver probar_reglas_de_operador.sh):
// cerrar y abrir las compuertas de los ductos y la batería que gastan.
public static class PruebaCompuertasDeDuctos
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static bool Cerca(float a, float b) { return Math.Abs(a - b) < 0.01f; }

    public static int Main()
    {
        // --- Los números acordados ---
        Comprobar("la batería empieza en 100 %", CompuertasDeDuctos.BateriaInicial == 100f);
        Comprobar("cada compuerta cerrada gasta 2 % por segundo", CompuertasDeDuctos.ConsumoPorSegundo == 2f);
        Comprobar("un cálculo del jugador 2 recarga 20 %", CompuertasDeDuctos.RecargaPorCalculo == 20f);

        // --- Cerrar y abrir ---
        var c = new CompuertasDeDuctos();
        Comprobar("al empezar las dos están abiertas y la batería llena",
            !c.EstaCerrada("izquierda") && !c.EstaCerrada("derecha") && c.Bateria == 100f && c.BateriaLlena);
        Comprobar("cerrar la izquierda se acepta", c.Fijar("izquierda", true));
        Comprobar("y solo cierra esa", c.EstaCerrada("izquierda") && !c.EstaCerrada("derecha"));
        Comprobar("un lado que no existe no se acepta ni cambia nada",
            !c.Fijar("arriba", true) && !c.Fijar(null, true) && !c.EstaCerrada("arriba") && !c.EstaCerrada(null));
        Comprobar("abrir se acepta", c.Fijar("izquierda", false) && !c.EstaCerrada("izquierda"));

        // --- La batería ---
        c = new CompuertasDeDuctos();
        c.Avanzar(10f);
        Comprobar("con las dos abiertas no se gasta batería", c.Bateria == 100f);
        c.Fijar("izquierda", true);
        c.Avanzar(10f);
        Comprobar("una compuerta cerrada 10 s gasta 20 %", Cerca(c.Bateria, 80f));
        c.Fijar("derecha", true);
        c.Avanzar(10f);
        Comprobar("las dos cerradas 10 s gastan 40 %", Cerca(c.Bateria, 40f));
        c.Avanzar(-5f);
        Comprobar("un tiempo negativo no cambia nada", Cerca(c.Bateria, 40f));
        Comprobar("con batería a medias no está llena", !c.BateriaLlena);

        // --- Se acaba ---
        c = new CompuertasDeDuctos();
        c.Fijar("derecha", true);
        Comprobar("una compuerta cerrada todo el tiempo dura 50 s", !c.Avanzar(49.9f) && c.EstaCerrada("derecha"));
        Comprobar("al llegar a 0 avisa una vez que se acabó", c.Avanzar(0.2f));
        Comprobar("la batería queda en 0, no negativa", c.Bateria == 0f && c.SinBateria);
        Comprobar("y las compuertas se abren solas", !c.EstaCerrada("derecha") && !c.EstaCerrada("izquierda"));
        Comprobar("no vuelve a avisar", !c.Avanzar(1f));
        Comprobar("sin batería no se puede cerrar", !c.Fijar("izquierda", true) && !c.EstaCerrada("izquierda"));
        Comprobar("abrir sin batería sí se acepta (ya están abiertas)", c.Fijar("izquierda", false));

        // --- El jugador 2 recarga con un cálculo ---
        c.Recargar(CompuertasDeDuctos.RecargaPorCalculo);
        Comprobar("recargar desde 0 deja 20 %", Cerca(c.Bateria, 20f) && !c.SinBateria);
        Comprobar("y se puede volver a cerrar", c.Fijar("izquierda", true));
        c.Recargar(500f);
        Comprobar("recargar nunca pasa de 100 %", c.Bateria == 100f && c.BateriaLlena);
        c.Recargar(-30f);
        Comprobar("una recarga negativa no quita batería", c.Bateria == 100f);

        // --- Noche nueva ---
        c.Avanzar(20f);
        c.Reiniciar();
        Comprobar("Reiniciar abre las dos y llena la batería",
            !c.EstaCerrada("izquierda") && !c.EstaCerrada("derecha") && c.Bateria == 100f);

        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
