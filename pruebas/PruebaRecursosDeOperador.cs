using System;
using System.Linq;

// Pruebas de RecursosDeOperador y PresenciaDeOperador fuera de Unity.
public static class PruebaRecursosDeOperador
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    public static int Main()
    {
        var r = new RecursosDeOperador();

        Comprobar("al empezar hay 1 carga y el rescate sin desbloquear",
            r.Cargas == RecursosDeOperador.CargasIniciales && r.Progreso == 0 && !r.HabilidadPuppet);
        Comprobar("colocar una trampa con carga se acepta",
            r.ColocarTrampa("PuntoA", true) == RecursosDeOperador.ResultadoTrampa.Colocada);
        Comprobar("la trampa gasta la carga y queda registrada",
            r.Cargas == 0 && r.Trampas.Contains("PuntoA"));
        Comprobar("sin cargas se rechaza",
            r.ColocarTrampa("PuntoB", true) == RecursosDeOperador.ResultadoTrampa.SinCargas);

        r.Reiniciar();
        r.ColocarTrampa("PuntoA", true);
        r.GanarCargaPorCalculo();   // vuelve a haber 1 carga
        Comprobar("en un punto ocupado se rechaza sin gastar carga",
            r.ColocarTrampa("PuntoA", true) == RecursosDeOperador.ResultadoTrampa.PuntoOcupado && r.Cargas == 1);
        Comprobar("un punto que no existe se rechaza sin gastar carga",
            r.ColocarTrampa("Nada", false) == RecursosDeOperador.ResultadoTrampa.PuntoInvalido && r.Cargas == 1);
        Comprobar("disparar una trampa existente devuelve true y la quita",
            r.DispararTrampa("PuntoA") && !r.Trampas.Contains("PuntoA"));
        Comprobar("disparar dos veces la misma devuelve false", !r.DispararTrampa("PuntoA"));

        // Las tareas de la terminal (ver TareasDeOperador): el cálculo da cargas y la ecuación, el rescate.
        r.Reiniciar();
        Comprobar("al empezar las cargas no están llenas", !r.CargasLlenas);
        r.GanarCargaPorCalculo();
        Comprobar("un cálculo correcto da 1 carga y no toca el rescate",
            r.Cargas == RecursosDeOperador.CargasIniciales + 1 && r.Progreso == 0 && !r.HabilidadPuppet);
        for (int i = 0; i < 5; i++) r.GanarCargaPorCalculo();
        Comprobar("las cargas nunca pasan del máximo", r.Cargas == RecursosDeOperador.MaxCargas && r.CargasLlenas);
        Comprobar("solo con cálculos no se desbloquea el rescate de Puppet", !r.HabilidadPuppet);

        Comprobar("hace falta 1 ecuación para desbloquear el rescate", RecursosDeOperador.EcuacionesParaHabilidad == 1);
        r.GanarHabilidadPorEcuacion();
        Comprobar("una ecuación resuelta desbloquea la habilidad de Puppet", r.HabilidadPuppet);
        Comprobar("y no toca las cargas", r.Cargas == RecursosDeOperador.MaxCargas);
        r.GanarHabilidadPorEcuacion();
        Comprobar("otra ecuación no acumula más de lo necesario", r.Progreso == RecursosDeOperador.EcuacionesParaHabilidad);
        Comprobar("gastar la habilidad devuelve true", r.GastarHabilidadPuppet());
        Comprobar("tras gastarla no hay habilidad ni progreso", !r.HabilidadPuppet && r.Progreso == 0);
        Comprobar("gastarla sin tenerla devuelve false", !r.GastarHabilidadPuppet());

        var p = new PresenciaDeOperador();
        Comprobar("sin latidos el operador no está vivo", !p.EstaVivo(0f));
        p.Latir(10f);
        Comprobar("a los 5,9 s del último latido sigue vivo", p.EstaVivo(15.9f));
        Comprobar("a los 6,1 s se da por caído", !p.EstaVivo(16.1f));
        p.Reiniciar();
        Comprobar("tras reiniciar no está vivo", !p.EstaVivo(10.5f));

        // El jugador 1 se entera (en la tablet) de que su compañero entró o se cayó.
        var c = new PresenciaDeOperador();
        Comprobar("sin latidos no hay nada que avisar",
            c.RevisarCambio(0f) == PresenciaDeOperador.Cambio.SinCambio);
        c.Latir(10f);
        Comprobar("el primer latido avisa que el compañero se conectó",
            c.RevisarCambio(10f) == PresenciaDeOperador.Cambio.Conectado);
        Comprobar("el aviso de conexión sale una sola vez",
            c.RevisarCambio(10.5f) == PresenciaDeOperador.Cambio.SinCambio);
        c.Latir(12f);
        Comprobar("mientras llegan latidos no hay cambio",
            c.RevisarCambio(17f) == PresenciaDeOperador.Cambio.SinCambio);
        Comprobar("pasados 6 s sin latido avisa que se perdió",
            c.RevisarCambio(18.5f) == PresenciaDeOperador.Cambio.Perdido);
        Comprobar("el aviso de pérdida sale una sola vez",
            c.RevisarCambio(30f) == PresenciaDeOperador.Cambio.SinCambio);
        c.Latir(31f);
        Comprobar("si vuelve, avisa otra vez que se conectó",
            c.RevisarCambio(31f) == PresenciaDeOperador.Cambio.Conectado);
        c.OlvidarCambio();
        Comprobar("en una partida nueva, un compañero que ya estaba se anuncia de nuevo",
            c.RevisarCambio(32f) == PresenciaDeOperador.Cambio.Conectado);
        c.OlvidarCambio();
        Comprobar("en una partida nueva sin compañero no se avisa nada",
            c.RevisarCambio(60f) == PresenciaDeOperador.Cambio.SinCambio);

        Comprobar("con operador vivo, Freddy va 0,4 veces el intervalo (más rápido)",
            Math.Abs(RecursosDeOperador.FactorDeAgresividad("freddy", true) - 0.4f) < 0.001f);
        Comprobar("con operador vivo, Vixy va 0,7",
            Math.Abs(RecursosDeOperador.FactorDeAgresividad("vixy", true) - 0.7f) < 0.001f);
        Comprobar("Puppet no cambia", Math.Abs(RecursosDeOperador.FactorDeAgresividad("puppet", true) - 1f) < 0.001f);
        Comprobar("sin operador, Freddy es normal", Math.Abs(RecursosDeOperador.FactorDeAgresividad("freddy", false) - 1f) < 0.001f);
        Comprobar("el nombre en mayúsculas también vale", Math.Abs(RecursosDeOperador.FactorDeAgresividad("FREDDY", true) - 0.4f) < 0.001f);
        Comprobar("un nombre desconocido es normal", Math.Abs(RecursosDeOperador.FactorDeAgresividad("otro", true) - 1f) < 0.001f);

        Console.WriteLine(fallos == 0 ? "\nTodas las pruebas pasaron." : "\n" + fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
