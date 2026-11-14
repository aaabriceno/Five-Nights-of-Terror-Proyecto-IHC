using System;

// Pruebas de LinternaDeOficina fuera de Unity (ver probar_reglas_de_operador.sh).
// La linterna alumbra hacia donde mira la cámara del jugador 1; alumbrar a Freddy
// 2 s seguidos lo hace retroceder 2 puntos. Coordenadas del plano real (vistas
// desde arriba, x y z): cámara en (20, 0,55); el pasillo del frente va por x = 20
// con P07 (20, 6,2), P06 (20, 8) y P05 (20, 11,8); la boca izquierda V04 (16,3, 3).
public static class PruebaLinternaDeOficina
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private const float CamaraX = 20f, CamaraZ = 0.55f;

    // ¿Alumbra el punto (x, z) con la cámara girada `yaw` grados (0 = al frente, + = derecha)?
    private static bool Alumbra(float yaw, float x, float z)
    {
        double r = yaw * Math.PI / 180.0;
        return LinternaDeOficina.Alumbra(CamaraX, CamaraZ, (float)Math.Sin(r), (float)Math.Cos(r), x, z);
    }

    // Avanza de a 1/8 s (exacto en float); cuenta cuántas veces pidió ahuyentar.
    private static int Avanzar(LinternaDeOficina luz, float segundos, bool alumbrado)
    {
        int veces = 0;
        int pasos = (int)Math.Round(segundos / 0.125f);
        for (int i = 0; i < pasos; i++)
            if (luz.Avanzar(0.125f, alumbrado)) veces++;
        return veces;
    }

    public static int Main()
    {
        Comprobar("alcance 10 m, cono de 40° (20° a cada lado), 2 s para ahuyentar, retrocede 2 puntos",
            LinternaDeOficina.Alcance == 10f && LinternaDeOficina.MitadDelAngulo == 20f
            && LinternaDeOficina.SegundosParaAhuyentar == 2f && LinternaDeOficina.PuntosQueRetrocede == 2);

        // --- El cono ---
        Comprobar("mirando al frente alumbra a Freddy en P07 y en P06", Alumbra(0f, 20f, 6.2f) && Alumbra(0f, 20f, 8f));
        Comprobar("P05 queda fuera de alcance (11,25 m)", !Alumbra(0f, 20f, 11.8f));
        Comprobar("un poco corrido del centro (15°) todavía lo alumbra", Alumbra(0f, 21.5f, 6.2f));
        Comprobar("muy corrido (28°) ya no", !Alumbra(0f, 23f, 6.2f));
        Comprobar("con la cámara girada a la izquierda (-75°) no alumbra el pasillo", !Alumbra(-75f, 20f, 6.2f));
        Comprobar("mirando al frente no alumbra la boca del ducto izquierdo (V04, a ~56°)", !Alumbra(0f, 16.3f, 3f));
        Comprobar("la dirección no necesita ser unitaria",
            LinternaDeOficina.Alumbra(CamaraX, CamaraZ, 0f, 5f, 20f, 6.2f));
        Comprobar("una dirección nula no alumbra nada",
            !LinternaDeOficina.Alumbra(CamaraX, CamaraZ, 0f, 0f, 20f, 6.2f));

        // --- Ahuyentar: 2 s seguidos de luz ---
        var luz = new LinternaDeOficina();
        Comprobar("antes de 2 s no lo ahuyenta", Avanzar(luz, 1.875f, true) == 0);
        Comprobar("a los 2 s lo ahuyenta, una sola vez", Avanzar(luz, 0.125f, true) == 1);
        var cortada = new LinternaDeOficina();
        Avanzar(cortada, 1.5f, true);
        Avanzar(cortada, 0.125f, false);                            // se le fue la luz
        Comprobar("si la luz se le va, la cuenta vuelve a cero",
            Avanzar(cortada, 1.875f, true) == 0 && Avanzar(cortada, 0.125f, true) == 1);
        Comprobar("sin luz nunca lo ahuyenta", Avanzar(new LinternaDeOficina(), 10f, false) == 0);
        var reiniciada = new LinternaDeOficina();
        Avanzar(reiniciada, 1.5f, true);
        reiniciada.Reiniciar();
        Comprobar("Reiniciar (noche nueva) borra la cuenta", Avanzar(reiniciada, 1.875f, true) == 0);

        // --- Con la ruta real de Freddy ---
        string[] freddy = RecorridosDeAnimatronicos.Ruta("Freddy");
        int p07 = Array.IndexOf(freddy, "P07"), p06 = Array.IndexOf(freddy, "P06");
        Comprobar("desde P07 vuelve a P05 y desde P06 a P04",
            freddy[p07 - LinternaDeOficina.PuntosQueRetrocede] == "P05"
            && freddy[p06 - LinternaDeOficina.PuntosQueRetrocede] == "P04");
        Comprobar("y ahí ya está fuera del alcance: la linterna no lo empuja hasta el escenario",
            !Alumbra(0f, 20f, 11.8f) && !Alumbra(0f, 20f, 15.8f));

        Console.WriteLine(fallos == 0 ? "\nTodas las pruebas pasaron." : "\n" + fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
