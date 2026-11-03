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

        Comprobar("al empezar hay 1 carga y 0 progreso",
            r.Cargas == RecursosDeOperador.CargasIniciales && r.Progreso == 0 && !r.HabilidadPuppet);
        Comprobar("colocar una trampa con carga se acepta",
            r.ColocarTrampa("PuntoA", true) == RecursosDeOperador.ResultadoTrampa.Colocada);
        Comprobar("la trampa gasta la carga y queda registrada",
            r.Cargas == 0 && r.Trampas.Contains("PuntoA"));
        Comprobar("sin cargas se rechaza",
            r.ColocarTrampa("PuntoB", true) == RecursosDeOperador.ResultadoTrampa.SinCargas);

        r.Reiniciar();
        r.ColocarTrampa("PuntoA", true);
        r.IniciarMiniTarea(0f);
        r.CompletarMiniTarea(3f);   // vuelve a haber 1 carga
        Comprobar("en un punto ocupado se rechaza sin gastar carga",
            r.ColocarTrampa("PuntoA", true) == RecursosDeOperador.ResultadoTrampa.PuntoOcupado && r.Cargas == 1);
        Comprobar("un punto que no existe se rechaza sin gastar carga",
            r.ColocarTrampa("Nada", false) == RecursosDeOperador.ResultadoTrampa.PuntoInvalido && r.Cargas == 1);
        Comprobar("disparar una trampa existente devuelve true y la quita",
            r.DispararTrampa("PuntoA") && !r.Trampas.Contains("PuntoA"));
        Comprobar("disparar dos veces la misma devuelve false", !r.DispararTrampa("PuntoA"));

        r.Reiniciar();
        Comprobar("completar sin haber iniciado se rechaza",
            r.CompletarMiniTarea(10f) == RecursosDeOperador.ResultadoMiniTarea.SinInicio);
        r.IniciarMiniTarea(10f);
        Comprobar("completar a los 2 s se rechaza por muy rápida",
            r.CompletarMiniTarea(12f) == RecursosDeOperador.ResultadoMiniTarea.MuyRapida);
        Comprobar("la mini-tarea rechazada no cambia cargas ni progreso",
            r.Cargas == RecursosDeOperador.CargasIniciales && r.Progreso == 0);
        Comprobar("pasados los 3 s se acepta",
            r.CompletarMiniTarea(13.2f) == RecursosDeOperador.ResultadoMiniTarea.Aceptada);
        Comprobar("aceptada sube 1 carga y 1 progreso",
            r.Cargas == RecursosDeOperador.CargasIniciales + 1 && r.Progreso == 1);
        Comprobar("tras aceptarse hace falta iniciar otra vez",
            r.CompletarMiniTarea(20f) == RecursosDeOperador.ResultadoMiniTarea.SinInicio);

        for (int i = 0; i < 5; i++) { r.IniciarMiniTarea(100f + i * 10); r.CompletarMiniTarea(104f + i * 10); }
        Comprobar("las cargas nunca pasan del máximo", r.Cargas == RecursosDeOperador.MaxCargas);
        Comprobar("con 3 o más mini-tareas se desbloquea la habilidad de Puppet", r.HabilidadPuppet);
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

        Console.WriteLine(fallos == 0 ? "\nTodas las pruebas pasaron." : "\n" + fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
