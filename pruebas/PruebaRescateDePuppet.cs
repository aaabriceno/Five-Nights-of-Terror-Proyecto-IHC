using System;

// Pruebas de RescateDePuppet fuera de Unity.
public static class PruebaRescateDePuppet
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
        var r = new RescateDePuppet();

        Comprobar("sin ventana abierta no se puede rescatar",
            r.Rescatar(0f, true) == RescateDePuppet.Resultado.SinVentana);
        Comprobar("el factor de descarga empieza en 1", Cerca(r.FactorDeDescarga, 1f));

        r.AbrirVentana(10f);
        Comprobar("la ventana está abierta a los 5,9 s", r.VentanaAbierta(15.9f));
        Comprobar("quedan unos 4 s a los 12 s", Cerca(r.SegundosRestantes(12f), 4f));
        Comprobar("a los 6,1 s ya no está abierta", !r.VentanaAbierta(16.1f));

        Comprobar("sin la habilidad se rechaza",
            r.Rescatar(12f, false) == RescateDePuppet.Resultado.SinHabilidad);
        Comprobar("rechazar por habilidad deja la ventana abierta", r.VentanaAbierta(12f));
        Comprobar("con la habilidad se rescata",
            r.Rescatar(12f, true) == RescateDePuppet.Resultado.Rescatado);
        Comprobar("el rescate dobla la descarga de la caja", Cerca(r.FactorDeDescarga, 2f));
        Comprobar("tras rescatar la ventana se cierra", !r.VentanaAbierta(12.1f));
        Comprobar("rescatar dos veces la misma ventana se rechaza",
            r.Rescatar(12.2f, true) == RescateDePuppet.Resultado.SinVentana);
        Comprobar("rescatado no cuenta como vencido", !r.ConsumirVencimiento(30f));

        r.AbrirVentana(40f);
        r.Rescatar(41f, true);
        Comprobar("otro rescate multiplica otra vez la descarga", Cerca(r.FactorDeDescarga, 4f));

        r.AbrirVentana(50f);
        Comprobar("antes de vencer no se consume el vencimiento", !r.ConsumirVencimiento(55f));
        Comprobar("al vencer sin rescate se consume una vez", r.ConsumirVencimiento(56.1f));
        Comprobar("y no una segunda vez", !r.ConsumirVencimiento(57f));

        // La caja dura 30 s cada noche; cada rescate del jugador 2 la deja en la mitad.
        var c = new RescateDePuppet();
        Comprobar("sin rescates la caja se vacía en 30 s", Cerca(c.SegundosParaVaciarLaCaja, 30f));
        c.AbrirVentana(0f); c.Rescatar(1f, true);
        Comprobar("tras el primer rescate dura 15 s", Cerca(c.SegundosParaVaciarLaCaja, 15f));
        c.AbrirVentana(10f); c.Rescatar(11f, true);
        Comprobar("tras el segundo, 7,5 s", Cerca(c.SegundosParaVaciarLaCaja, 7.5f));
        c.AbrirVentana(20f);
        Comprobar("un tercer rescate se acepta", c.Rescatar(21f, true) == RescateDePuppet.Resultado.Rescatado);
        Comprobar("pero la caja no baja de 7,5 s (el mínimo)", Cerca(c.SegundosParaVaciarLaCaja, 7.5f));
        Comprobar("y la multa se queda en ×4", Cerca(c.FactorDeDescarga, 4f));
        c.AbrirVentana(30f); c.Rescatar(31f, true);
        Comprobar("ni con más rescates", Cerca(c.SegundosParaVaciarLaCaja, 7.5f) && Cerca(c.FactorDeDescarga, 4f));
        c.Reiniciar();
        Comprobar("al empezar otra noche vuelve a 30 s", Cerca(c.SegundosParaVaciarLaCaja, 30f));

        const float cajaLlena = 2000f;
        Comprobar("la descarga por segundo no depende de la noche: llena la caja se vacía en 30 s",
            Cerca(c.DescargaPorSegundo(cajaLlena) * 30f, cajaLlena));
        c.AbrirVentana(0f); c.Rescatar(1f, true);
        Comprobar("tras un rescate se descarga al doble por segundo",
            Cerca(c.DescargaPorSegundo(cajaLlena), 2f * cajaLlena / 30f));
        Comprobar("darle cuerda la llena en 15 s y no se ve afectado por la multa",
            Cerca(c.RecargaPorSegundo(cajaLlena) * 15f, cajaLlena));

        // Simulación con ticks de 0,5 s: de llena a vacía.
        var s2 = new RescateDePuppet();
        float caja = cajaLlena; float t = 0f;
        while (caja > 0.01f && t < 100f) { caja -= s2.DescargaPorSegundo(cajaLlena) * 0.5f; t += 0.5f; }
        Comprobar("simulada, la caja llega a 0 a los 30 s", Cerca(t, 30f));
        s2.AbrirVentana(0f); s2.Rescatar(1f, true);
        caja = cajaLlena; t = 0f;
        while (caja > 0.01f && t < 100f) { caja -= s2.DescargaPorSegundo(cajaLlena) * 0.5f; t += 0.5f; }
        Comprobar("simulada tras un rescate, a los 15 s", Cerca(t, 15f));

        r.AbrirVentana(60f);
        r.Reiniciar();
        Comprobar("reiniciar cierra la ventana", !r.VentanaAbierta(61f));
        Comprobar("reiniciar no deja vencimiento pendiente", !r.ConsumirVencimiento(100f));
        Comprobar("reiniciar devuelve el factor a 1", Cerca(r.FactorDeDescarga, 1f));

        Console.WriteLine(fallos == 0 ? "\nTodas las pruebas pasaron." : "\n" + fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
