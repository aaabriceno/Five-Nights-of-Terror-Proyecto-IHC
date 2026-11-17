using System;

// Pruebas de PracticaDeTutorial fuera de Unity (ver probar_reglas_de_operador.sh).
public static class PruebaPracticaDeTutorial
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    public static int Main()
    {
        var p = new PracticaDeTutorial();
        Comprobar("empieza sin nada probado", !p.Camara && !p.Linterna && !p.Compuerta && !p.Cuerda && !p.Cables && !p.Mirada);
        Comprobar("sin nada probado no están listos los controles ni la práctica", !p.ControlesListos && !p.Completa);

        Comprobar("soltar la cámara (0) no cuenta como girarla", !p.UsarCamara(0) && !p.Camara);
        Comprobar("girar la cámara a la izquierda la marca (y avisa que cambió)", p.UsarCamara(-1) && p.Camara);
        Comprobar("girarla otra vez no avisa de nuevo (no hay que republicar)", !p.UsarCamara(1));

        Comprobar("apagar la linterna no cuenta", !p.UsarLinterna(false) && !p.Linterna);
        Comprobar("prender la linterna la marca", p.UsarLinterna(true) && p.Linterna);

        Comprobar("abrir una compuerta no cuenta (hay que cerrarla)", !p.UsarCompuerta(false) && !p.Compuerta);
        Comprobar("cerrar una compuerta la marca", p.UsarCompuerta(true) && p.Compuerta);

        Comprobar("con tres de cuatro controles aún no están listos", !p.ControlesListos);
        Comprobar("dar cuerda a la caja la marca", p.DarCuerda() && p.Cuerda);
        Comprobar("con los cuatro controles quedan listos", p.ControlesListos);
        Comprobar("pero la práctica no está completa sin cables ni mirada", !p.Completa);

        Comprobar("resolver los cables los marca", p.CompletarCables() && p.Cables);

        Comprobar("mirar 1 s no alcanza", !p.Mirar(true, 1f) && !p.Mirada);
        Comprobar("apartar la vista reinicia la cuenta", !p.Mirar(false, 0.1f) && !p.Mirar(true, 1.5f) && !p.Mirada);
        Comprobar($"mirar {PracticaDeTutorial.SegundosDeMirada:g} s seguidos verifica la mirada", p.Mirar(true, 0.6f) && p.Mirada);
        Comprobar("apartar la vista después ya no la desmarca", !p.Mirar(false, 1f) && p.Mirada);
        Comprobar("con todo hecho, la práctica está completa", p.Completa);

        p.Reiniciar();
        Comprobar("Reiniciar (al abrir el tutorial otra vez) deja todo sin probar",
                  !p.Camara && !p.Linterna && !p.Compuerta && !p.Cuerda && !p.Cables && !p.Mirada && !p.Mirar(true, 1.9f));

        Console.WriteLine(fallos == 0 ? "\nTodas las pruebas pasaron." : "\n" + fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
