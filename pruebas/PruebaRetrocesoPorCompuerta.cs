using System;

// Pruebas de RetrocesoPorCompuerta fuera de Unity (ver probar_reglas_de_operador.sh).
// La fase: el animatrónico llega a la boca del ducto con la compuerta cerrada,
// la empuja unos segundos y, si sigue cerrada, retrocede hasta la entrada del ducto.
public static class PruebaRetrocesoPorCompuerta
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    // Avanza la fase de a 1/8 s (exacto en float) durante `segundos`; cuenta cuántas veces pidió retroceder.
    private static int Avanzar(RetrocesoPorCompuerta fase, float segundos, bool enLaBoca, bool cerrada)
    {
        int veces = 0;
        int pasos = (int)Math.Round(segundos / 0.125f);
        for (int i = 0; i < pasos; i++)
            if (fase.Avanzar(0.125f, enLaBoca, cerrada)) veces++;
        return veces;
    }

    public static int Main()
    {
        float fase = RetrocesoPorCompuerta.SegundosEmpujando;
        Comprobar("la fase dura 4 s y retrocede 3 puntos",
            fase == 4f && RetrocesoPorCompuerta.PuntosQueRetrocede == 3);

        var r = new RetrocesoPorCompuerta();
        Comprobar("al empezar no está empujando", !r.Empujando);
        int antes = Avanzar(r, fase - 0.5f, true, true);
        Comprobar("en la boca con la compuerta cerrada empuja, y antes de 4 s no retrocede",
            antes == 0 && r.Empujando);
        int alCumplir = Avanzar(r, 0.5f, true, true);
        Comprobar("a los 4 s pide retroceder, una sola vez", alCumplir == 1);
        Comprobar("y la fase termina", !r.Empujando);

        var cortada = new RetrocesoPorCompuerta();
        Avanzar(cortada, fase - 0.5f, true, true);
        Avanzar(cortada, 0.125f, true, false);                       // el jugador 1 la abre
        Comprobar("abrir la compuerta corta la fase", !cortada.Empujando);
        Comprobar("y al cerrarla otra vez empieza de cero (4 s completos)",
            Avanzar(cortada, fase - 0.5f, true, true) == 0 && Avanzar(cortada, 0.5f, true, true) == 1);

        var lejos = new RetrocesoPorCompuerta();
        Comprobar("lejos de la boca no empuja aunque esté cerrada",
            Avanzar(lejos, fase * 2f, false, true) == 0 && !lejos.Empujando);
        Comprobar("en la boca con la compuerta abierta tampoco",
            Avanzar(lejos, fase * 2f, true, false) == 0 && !lejos.Empujando);

        var seguida = new RetrocesoPorCompuerta();
        Comprobar("si vuelve y la encuentra cerrada, cada vez necesita otros 4 s",
            Avanzar(seguida, fase * 2f, true, true) == 2);

        var reiniciada = new RetrocesoPorCompuerta();
        Avanzar(reiniciada, fase - 0.5f, true, true);
        reiniciada.Reiniciar();
        Comprobar("reiniciar (nueva noche) borra la fase",
            !reiniciada.Empujando && Avanzar(reiniciada, fase - 0.5f, true, true) == 0);

        // Con las rutas reales: desde la boca vuelve a la entrada de su ducto.
        string[] vixy = RecorridosDeAnimatronicos.Ruta("Vixy");
        string[] puppet = RecorridosDeAnimatronicos.Ruta("Puppet");
        Comprobar("Vixy vuelve de V04 a V01 (luz amarilla)",
            vixy[vixy.Length - 2] == "V04" && vixy[RetrocesoPorCompuerta.PuntoAlQueVuelve(vixy.Length - 2)] == "V01");
        Comprobar("Puppet vuelve de V08 a V05 (luz amarilla)",
            puppet[puppet.Length - 2] == "V08" && puppet[RetrocesoPorCompuerta.PuntoAlQueVuelve(puppet.Length - 2)] == "V05");
        Comprobar("nunca vuelve antes del primer punto", RetrocesoPorCompuerta.PuntoAlQueVuelve(1) == 0);

        Console.WriteLine(fallos == 0 ? "\nTodas las pruebas pasaron." : "\n" + fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
