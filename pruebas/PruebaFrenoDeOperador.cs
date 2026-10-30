using System;

// Pruebas de FrenoDeOperador fuera de Unity (mismo esquema que
// PruebaRutaDeJumpscare): está fuera de Assets/ porque tiene su propio Main.
public static class PruebaFrenoDeOperador
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static bool Cerca(float a, float b) => Math.Abs(a - b) < 0.001f;

    public static int Main()
    {
        var freno = new FrenoDeOperador();

        Comprobar("sin acción, la velocidad es normal",
            Cerca(freno.FactorDeVelocidad("vixy", 0f), 1f));
        Comprobar("retrasar a Vixy se acepta",
            freno.Intentar("vixy", 10f) == FrenoDeOperador.Resultado.Aceptada);
        Comprobar("durante el retraso, Vixy va a la mitad",
            Cerca(freno.FactorDeVelocidad("vixy", 12f), FrenoDeOperador.FactorRalentizado));
        Comprobar("el retraso de Vixy no afecta a Freddy",
            Cerca(freno.FactorDeVelocidad("freddy", 12f), 1f));
        Comprobar("al cumplirse la duración, vuelve a la normalidad",
            Cerca(freno.FactorDeVelocidad("vixy", 10f + FrenoDeOperador.DuracionSegundos), 1f));
        Comprobar("quedan 5 s de retraso a los 3 s de empezar",
            Cerca(freno.SegundosDeRetraso("vixy", 13f), FrenoDeOperador.DuracionSegundos - 3f));

        Comprobar("repetir antes del enfriamiento se rechaza",
            freno.Intentar("vixy", 15f) == FrenoDeOperador.Resultado.EnEnfriamiento);
        Comprobar("el rechazo no alarga el retraso ya activo",
            Cerca(freno.SegundosDeRetraso("vixy", 15f), FrenoDeOperador.DuracionSegundos - 5f));
        Comprobar("la espera restante cuenta desde el uso",
            Cerca(freno.SegundosDeEspera("vixy", 15f), FrenoDeOperador.EnfriamientoSegundos - 5f));
        Comprobar("otro animatrónico sí se puede retrasar en ese momento",
            freno.Intentar("freddy", 15f) == FrenoDeOperador.Resultado.Aceptada);
        Comprobar("pasado el enfriamiento, se acepta de nuevo",
            freno.Intentar("vixy", 10f + FrenoDeOperador.EnfriamientoSegundos) == FrenoDeOperador.Resultado.Aceptada);
        Comprobar("sin uso previo no hay espera",
            Cerca(freno.SegundosDeEspera("puppet", 100f), 0f));

        Comprobar("un animatrónico desconocido se rechaza",
            freno.Intentar("bonnie", 0f) == FrenoDeOperador.Resultado.AnimatronicoDesconocido);
        Comprobar("vacío o null se rechaza",
            freno.Intentar("", 0f) == FrenoDeOperador.Resultado.AnimatronicoDesconocido &&
            freno.Intentar(null, 0f) == FrenoDeOperador.Resultado.AnimatronicoDesconocido);
        Comprobar("no distingue mayúsculas ni espacios",
            new FrenoDeOperador().Intentar("  PuPpEt ", 0f) == FrenoDeOperador.Resultado.Aceptada);

        freno.Reiniciar();
        Comprobar("reiniciar (nueva noche) quita retrasos y esperas",
            Cerca(freno.FactorDeVelocidad("freddy", 16f), 1f) &&
            Cerca(freno.SegundosDeEspera("freddy", 16f), 0f));
        Comprobar("tras reiniciar se puede volver a usar enseguida",
            freno.Intentar("freddy", 16f) == FrenoDeOperador.Resultado.Aceptada);

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
