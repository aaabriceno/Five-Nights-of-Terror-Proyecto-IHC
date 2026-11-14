using System;
using System.Collections.Generic;
using System.Linq;

// Pruebas de TareasDeOperador fuera de Unity (ver probar_reglas_de_operador.sh).
// El solucionador de aquí es independiente del generador: si los dos coinciden en
// miles de tareas, la respuesta que guarda Unity es la que el enunciado pide.
//
// Desde el 2026-11-13 las tareas son para cualquier persona: nada de binario,
// hexadecimal, bits ni puertas lógicas. El rescate de Puppet se gana con una
// ecuación sencilla.
public static class PruebaTareasDeOperador
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    // ---------------------------------------------------------- solucionador del cálculo
    private static int Minutos(string hora)
    {
        // "12:05" es pasada la medianoche: 5 minutos.
        string[] partes = hora.Split(':');
        int h = int.Parse(partes[0]) % 12;
        return h * 60 + int.Parse(partes[1]);
    }

    private static int Resolver(TareasDeOperador.Tarea t)
    {
        string[] d = t.datos;
        switch (t.clase)
        {
            case "aritmetica":
                {
                    // datos: a, op, b  ó  a, ×, b, +|−, c  (el producto va primero)
                    int a = int.Parse(d[0]), b = int.Parse(d[2]);
                    int valor = d[1] == "+" ? a + b : a * b;
                    if (d.Length == 5) valor += d[3] == "+" ? int.Parse(d[4]) : -int.Parse(d[4]);
                    return valor;
                }
            case "serie":
                {
                    int[] s = d.Select(int.Parse).ToArray();
                    int paso = s[1] - s[0];
                    if (s.Zip(s.Skip(1), (x, y) => y - x).All(p => p == paso)) return s[s.Length - 1] + paso;
                    var razones = s.Zip(s.Skip(1), (x, y) => y % x == 0 ? y / x : -1).ToArray();
                    if (razones.All(r => r > 0) && razones.Distinct().Count() == 1) return s[s.Length - 1] * razones[0];
                    if (Enumerable.Range(0, s.Length).All(i => s[i] == (i + 1) * (i + 1))) return (s.Length + 1) * (s.Length + 1);
                    return s[s.Length - 1] + s[s.Length - 2];   // tipo Fibonacci
                }
            case "problema":
                if (d[0] == "reloj") return Minutos(d[2]) - Minutos(d[1]);
                if (d[0] == "vuelto") return int.Parse(d[3]) - int.Parse(d[1]) * int.Parse(d[2]);
                break;
        }
        throw new Exception("clase desconocida: " + t.clase + " " + string.Join(",", d));
    }

    // ---------------------------------------------------------- la ecuación, armada aparte
    // datos: lineal, a, op, b, c  →  a·x op b = c   |   parentesis, a, op, b, c  →  a(x op b) = c
    private static string TextoDeEcuacion(string[] d)
    {
        string a = d[1], op = d[2], b = d[3], c = d[4];
        if (d[0] == "parentesis") return a + "(x " + op + " " + b + ") = " + c;
        string termino = a == "1" ? "x" : a + "x";
        return b == "0" ? termino + " = " + c : termino + " " + op + " " + b + " = " + c;
    }

    private static bool Cumple(string[] d, int x)
    {
        int a = int.Parse(d[1]), b = int.Parse(d[3]), c = int.Parse(d[4]);
        int conB = d[2] == "+" ? b : -b;
        return d[0] == "parentesis" ? a * (x + conB) == c : a * x + conB == c;
    }

    public static int Main()
    {
        // --- Los números acordados ---
        Comprobar("un error bloquea la terminal 3 s", TareasDeOperador.BloqueoPorErrorSegundos == 3f);
        Comprobar("el cálculo no se puede responder en menos de 1,5 s", TareasDeOperador.MinimoSegundosCalculo == 1.5f);
        Comprobar("la ecuación no se puede responder en menos de 3 s", TareasDeOperador.MinimoSegundosEcuacion == 3f);

        // --- Cálculo: el generador y el solucionador independiente coinciden en todas las noches ---
        bool todasCoinciden = true, enunciados = true, rango = true, sinComputacion = true;
        var clasesPorNoche = new Dictionary<int, HashSet<string>>();
        var problemas = new HashSet<string>();
        for (int noche = 1; noche <= 6; noche++)
        {
            clasesPorNoche[noche] = new HashSet<string>();
            for (int semilla = 0; semilla < 400; semilla++)
            {
                var tareas = new TareasDeOperador(new Random(semilla));
                var t = tareas.Pedir(TareasDeOperador.Tipo.Calculo, noche, 0f);
                clasesPorNoche[noche].Add(t.clase);
                if (t.clase == "problema") problemas.Add(t.datos[0]);
                if (Resolver(t) != t.respuesta) { todasCoinciden = false; Console.WriteLine($"    noche {noche} semilla {semilla}: {t.enunciado} -> {t.respuesta} y el solucionador da {Resolver(t)}"); }
                if (string.IsNullOrWhiteSpace(t.enunciado) || t.datos == null || t.datos.Length == 0) enunciados = false;
                if (t.respuesta < 1 || t.respuesta > 1500) rango = false;
                string texto = t.enunciado.ToLowerInvariant();
                if (texto.Contains("binario") || texto.Contains("hexadecimal") || texto.Contains("bit")) sinComputacion = false;
            }
        }
        Comprobar("en las 6 noches la respuesta guardada es la que el enunciado pide (2 400 tareas)", todasCoinciden);
        Comprobar("todas traen enunciado y datos", enunciados);
        Comprobar("las respuestas son enteros entre 1 y 1 500 (se escriben a mano)", rango);
        Comprobar("ningún cálculo pide saber computación (sin binario, hexadecimal ni bits)", sinComputacion);
        Comprobar("solo hay operaciones, series y problemas de la vida diaria",
            Enumerable.Range(1, 6).All(n => clasesPorNoche[n].IsSubsetOf(new[] { "aritmetica", "serie", "problema" })));
        Comprobar("la noche 1 pide operaciones y problemas",
            clasesPorNoche[1].SetEquals(new[] { "aritmetica", "problema" }));
        Comprobar("desde la noche 2 se añaden las series",
            Enumerable.Range(2, 5).All(n => clasesPorNoche[n].SetEquals(new[] { "aritmetica", "serie", "problema" })));
        Comprobar("los problemas son de la hora (minutos que faltan) y del vuelto",
            problemas.SetEquals(new[] { "reloj", "vuelto" }));

        // La hora del problema cae dentro del turno (12 a 6 de la mañana) y en múltiplos de 5.
        bool horasDelTurno = true;
        for (int s = 0; s < 600; s++)
        {
            var t = new TareasDeOperador(new Random(s)).Pedir(TareasDeOperador.Tipo.Calculo, 1 + s % 6, 0f);
            if (t.clase != "problema" || t.datos[0] != "reloj") continue;
            int desde = Minutos(t.datos[1]), hasta = Minutos(t.datos[2]);
            if (desde < 0 || hasta > 6 * 60 || hasta <= desde || t.respuesta % 5 != 0 || !t.enunciado.Contains(t.datos[1] + " AM"))
                horasDelTurno = false;
        }
        Comprobar("las horas van de 12:00 a 6:00 AM, en múltiplos de 5 minutos", horasDelTurno);

        // No repite la misma clase dos veces seguidas cuando hay de dónde elegir.
        var seguidas = new TareasDeOperador(new Random(7));
        string anterior = null;
        bool sinRepetir = true;
        for (int i = 0; i < 300; i++)
        {
            var t = seguidas.Pedir(TareasDeOperador.Tipo.Calculo, 3, i * 100f);
            seguidas.Responder(t.id, t.respuesta, i * 100f + 5f);
            if (t.clase == anterior) sinRepetir = false;
            anterior = t.clase;
        }
        Comprobar("no repite la misma clase dos veces seguidas (300 tareas en la noche 3)", sinRepetir);

        // --- Ecuación (el rescate de Puppet): una x entera que cumple lo que dice el enunciado ---
        bool ecuacionesValidas = true, enunciadoCoincide = true, unPaso = true, dosPasos = true, conParentesis = false, xRazonable = true;
        for (int noche = 1; noche <= 6; noche++)
        {
            for (int semilla = 0; semilla < 300; semilla++)
            {
                var t = new TareasDeOperador(new Random(semilla)).Pedir(TareasDeOperador.Tipo.Ecuacion, noche, 0f);
                string[] d = t.datos;
                if (t.clase != "ecuacion" || d == null || d.Length != 5 || !Cumple(d, t.respuesta)) { ecuacionesValidas = false; continue; }
                if (t.enunciado != "Encuentra x: " + TextoDeEcuacion(d)) { enunciadoCoincide = false; Console.WriteLine($"    {t.enunciado} | {TextoDeEcuacion(d)}"); }
                if (t.respuesta < 1 || t.respuesta > 20 || int.Parse(d[4]) < 1) xRazonable = false;
                bool esDeUnPaso = d[0] == "lineal" && (d[1] == "1" || d[3] == "0");
                if (noche <= 2 && !esDeUnPaso) unPaso = false;
                if (noche >= 3 && esDeUnPaso) dosPasos = false;
                if (noche <= 4 && d[0] == "parentesis") dosPasos = false;
                if (noche >= 5 && d[0] == "parentesis") conParentesis = true;
            }
        }
        Comprobar("la x guardada cumple la ecuación en las 6 noches (1 800 ecuaciones)", ecuacionesValidas);
        Comprobar("el enunciado muestra justo esa ecuación («Encuentra x: 3x + 5 = 20»)", enunciadoCoincide);
        Comprobar("x es un entero de 1 a 20 y el resultado es positivo", xRazonable);
        Comprobar("noches 1 y 2: de un solo paso (x + 7 = 15 ó 3x = 12)", unPaso);
        Comprobar("noches 3 y 4: de dos pasos (2x − 4 = 10), sin paréntesis", dosPasos);
        Comprobar("noches 5 y 6: aparecen con paréntesis (2(x − 3) = 10)", conParentesis);

        // --- Flujo de una tarea ---
        var f = new TareasDeOperador(new Random(1));
        Comprobar("al empezar no hay tarea pendiente ni bloqueo", f.Pendiente == null && !f.EstaBloqueado(0f));
        var primera = f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 10f);
        Comprobar("pedir una tarea la deja pendiente y con id", primera != null && f.Pendiente == primera && primera.id > 0);
        Comprobar("volver a pedir la misma devuelve la misma (cerrar y abrir no cambia el problema)",
            f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 11f) == primera);

        Comprobar("responder a una tarea que no es la pendiente se rechaza (sin_tarea)",
            f.Responder(primera.id + 99, primera.respuesta, 15f) == TareasDeOperador.ResultadoRespuesta.SinTarea);
        Comprobar("responder en menos de 1,5 s se rechaza por muy rápido",
            f.Responder(primera.id, primera.respuesta, 11.2f) == TareasDeOperador.ResultadoRespuesta.MuyRapida);
        Comprobar("y la tarea sigue pendiente para reintentar", f.Pendiente == primera);
        Comprobar("la respuesta correcta se acepta",
            f.Responder(primera.id, primera.respuesta, 12f) == TareasDeOperador.ResultadoRespuesta.Correcta);
        Comprobar("tras acertar ya no hay tarea pendiente ni bloqueo", f.Pendiente == null && !f.EstaBloqueado(12f));
        Comprobar("no se puede responder dos veces la misma",
            f.Responder(primera.id, primera.respuesta, 13f) == TareasDeOperador.ResultadoRespuesta.SinTarea);

        var segunda = f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 20f);
        Comprobar("la tarea siguiente tiene otro id", segunda.id != primera.id);
        Comprobar("una respuesta equivocada se rechaza como incorrecta",
            f.Responder(segunda.id, segunda.respuesta + 1, 22f) == TareasDeOperador.ResultadoRespuesta.Incorrecta);
        Comprobar("el error bloquea la terminal 3 s", f.EstaBloqueado(22f) && f.EstaBloqueado(24.9f) && !f.EstaBloqueado(25.1f));
        Comprobar("durante el bloqueo faltan los segundos que quedan", Math.Abs(f.SegundosDeBloqueo(23f) - 2f) < 0.01f);
        Comprobar("y no se entrega tarea nueva", f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 23f) == null);
        Comprobar("tras el error la tarea ya no está pendiente (hay que pedir otra)", f.Pendiente == null);
        Comprobar("pasado el bloqueo se entrega una tarea nueva",
            f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 25.5f) != null);

        // --- Ecuación: se responde con el valor de x ---
        var g = new TareasDeOperador(new Random(3));
        var ecuacion = g.Pedir(TareasDeOperador.Tipo.Ecuacion, 2, 0f);
        Comprobar("responder la ecuación en menos de 3 s se rechaza por muy rápido",
            g.Responder(ecuacion.id, ecuacion.respuesta, 2f) == TareasDeOperador.ResultadoRespuesta.MuyRapida);
        Comprobar("con una x equivocada es incorrecto",
            g.Responder(ecuacion.id, ecuacion.respuesta + 1, 4f) == TareasDeOperador.ResultadoRespuesta.Incorrecta);
        var ecuacion2 = g.Pedir(TareasDeOperador.Tipo.Ecuacion, 2, 10f);
        Comprobar("con la x correcta es correcto",
            g.Responder(ecuacion2.id, ecuacion2.respuesta, 14f) == TareasDeOperador.ResultadoRespuesta.Correcta);

        // --- Cambiar de tipo, caducidad y reinicio ---
        var h = new TareasDeOperador(new Random(5));
        var calculo = h.Pedir(TareasDeOperador.Tipo.Calculo, 1, 0f);
        var otra = h.Pedir(TareasDeOperador.Tipo.Ecuacion, 1, 1f);
        Comprobar("pedir otro tipo reemplaza la tarea pendiente", otra.tipo == TareasDeOperador.Tipo.Ecuacion && h.Pendiente == otra);
        Comprobar("la tarea reemplazada ya no se puede responder",
            h.Responder(calculo.id, calculo.respuesta, 5f) == TareasDeOperador.ResultadoRespuesta.SinTarea);
        Comprobar("una tarea abandonada caduca a los 90 s",
            h.Responder(otra.id, otra.respuesta, 1f + TareasDeOperador.SegundosMaximosDeTarea + 1f) == TareasDeOperador.ResultadoRespuesta.SinTarea);
        var nueva = h.Pedir(TareasDeOperador.Tipo.Calculo, 1, 200f);
        Comprobar("y pedir pasado ese tiempo da una tarea nueva", nueva != null && nueva.id != otra.id);
        h.Pedir(TareasDeOperador.Tipo.Calculo, 1, 201f);
        h.Responder(nueva.id, nueva.respuesta + 1, 205f);
        h.Reiniciar();
        Comprobar("Reiniciar borra la tarea pendiente y el bloqueo", h.Pendiente == null && !h.EstaBloqueado(205f));

        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
