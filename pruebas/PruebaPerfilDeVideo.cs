using System;

// Pruebas de PerfilDeVideo fuera de Unity (ver probar_reglas_de_operador.sh).
public static class PruebaPerfilDeVideo
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static readonly string[] SinOpciones = { "/juego/FiveNightsOfTerror.x86_64" };

    public static int Main()
    {
        // --- Los números que el equipo acordó para el juego ---
        Comprobar("la resolución estándar es 1920x1080", PerfilDeVideo.Ancho == 1920 && PerfilDeVideo.Alto == 1080);
        Comprobar("el juego no pasa de 60 cuadros por segundo", PerfilDeVideo.FpsMaximos == 60);
        Comprobar("las posiciones salen 30 veces por segundo", PerfilDeVideo.PosicionesPorSegundo == 30);
        Comprobar("entre posiciones pasa 1/30 s (33,3 ms)", Math.Abs(PerfilDeVideo.SegundosEntrePosiciones - 1f / 30f) < 1e-6f);

        // --- Pantalla: la línea de comandos manda sobre el perfil ---
        Comprobar("sin opciones, el perfil fija la pantalla", !PerfilDeVideo.LaLineaDeComandosFijaLaPantalla(SinOpciones));
        Comprobar("con -screen-width lo decide quien lanzó el juego",
            PerfilDeVideo.LaLineaDeComandosFijaLaPantalla(new[] { "juego", "-screen-width", "1280" }));
        Comprobar("con -screen-height también",
            PerfilDeVideo.LaLineaDeComandosFijaLaPantalla(new[] { "juego", "-screen-height", "720" }));
        Comprobar("con -screen-fullscreen también (así lo lanzan las pruebas)",
            PerfilDeVideo.LaLineaDeComandosFijaLaPantalla(new[] { "juego", "-screen-fullscreen", "0" }));
        Comprobar("con -popupwindow también",
            PerfilDeVideo.LaLineaDeComandosFijaLaPantalla(new[] { "juego", "-popupwindow" }));
        Comprobar("sin argumentos no falla", !PerfilDeVideo.LaLineaDeComandosFijaLaPantalla(null));

        // --- MediaPipe: GPU salvo donde se sabe que no sirve ---
        Comprobar("con una tarjeta integrada AMD, MediaPipe usa la GPU",
            !PerfilDeVideo.MediaPipeDebeUsarLaCpu("AMD Radeon 610M (radeonsi, raphael_mendocino, LLVM 20.1.2, DRM 3.64, 7.0.0-42-generic)", SinOpciones));
        Comprobar("con una tarjeta integrada Intel, MediaPipe usa la GPU",
            !PerfilDeVideo.MediaPipeDebeUsarLaCpu("Mesa Intel(R) UHD Graphics 620 (KBL GT2)", SinOpciones));
        Comprobar("en una PC sin tarjeta (render por software llvmpipe) usa la CPU",
            PerfilDeVideo.MediaPipeDebeUsarLaCpu("llvmpipe (LLVM 20.1.2, 256 bits)", SinOpciones));
        Comprobar("con render por software softpipe usa la CPU",
            PerfilDeVideo.MediaPipeDebeUsarLaCpu("softpipe", SinOpciones));
        Comprobar("con -mediapipe-cpu se fuerza la CPU aunque haya GPU",
            PerfilDeVideo.MediaPipeDebeUsarLaCpu("AMD Radeon 610M", new[] { "juego", "-mediapipe-cpu" }));
        Comprobar("sin nombre de tarjeta no se adivina: GPU y el respaldo se ocupa si falla",
            !PerfilDeVideo.MediaPipeDebeUsarLaCpu(null, SinOpciones));

        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
