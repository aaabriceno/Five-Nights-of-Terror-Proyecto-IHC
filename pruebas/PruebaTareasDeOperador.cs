using System;
using System.Collections.Generic;
using System.Linq;

// Pruebas de TareasDeOperador fuera de Unity (ver probar_reglas_de_operador.sh).
// El solucionador de aquí es independiente del generador: si los dos coinciden en
// miles de tareas, la respuesta que guarda Unity es la que el enunciado pide.
public static class PruebaTareasDeOperador
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    // ---------------------------------------------------------- solucionador del cálculo
    private static int Resolver(TareasDeOperador.Tarea t)
    {
        string[] d = t.datos;
        switch (t.clase)
        {
            case "binario": return Convert.ToInt32(d[0], 2);
            case "hexadecimal": return Convert.ToInt32(d[0], 16);
            case "aritmetica":
                {
                    // datos: a, op, b  ó  a, ×, b, +|−, c  (el producto va primero)
                    int a = int.Parse(d[0]), b = int.Parse(d[2]);
                    int valor = d[1] == "+" ? a + b : a * b;
                    if (d.Length == 5) valor += d[3] == "+" ? int.Parse(d[4]) : -int.Parse(d[4]);
                    return valor;
                }
            case "memoria":
                {
                    int n = int.Parse(d[1]);
                    return d[0] == "valores" ? 1 << n : 8 * n;
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
        }
        throw new Exception("clase desconocida: " + t.clase);
    }

    // ---------------------------------------------------------- evaluador independiente del circuito
    private static bool Salida(TareasDeOperador.Tarea t, int[] interruptores)
    {
        var v = new Dictionary<string, bool>();
        for (int i = 0; i < t.entradas.Length; i++) v[t.entradas[i]] = interruptores[i] != 0;
        foreach (var p in t.puertas)
        {
            bool a = v[p.a];
            switch (p.op)
            {
                case "NOT": v[p.id] = !a; break;
                case "AND": v[p.id] = a && v[p.b]; break;
                case "OR": v[p.id] = a || v[p.b]; break;
                case "XOR": v[p.id] = a ^ v[p.b]; break;
                default: throw new Exception("puerta desconocida: " + p.op);
            }
        }
        return v[t.salida];
    }

    private static IEnumerable<int[]> Combinaciones(int n)
    {
        for (int m = 0; m < (1 << n); m++) yield return Enumerable.Range(0, n).Select(i => (m >> i) & 1).ToArray();
    }

    public static int Main()
    {
        // --- Los números acordados ---
        Comprobar("un error bloquea la terminal 3 s", TareasDeOperador.BloqueoPorErrorSegundos == 3f);
        Comprobar("el cálculo no se puede responder en menos de 1,5 s", TareasDeOperador.MinimoSegundosCalculo == 1.5f);
        Comprobar("el circuito no se puede responder en menos de 3 s", TareasDeOperador.MinimoSegundosCircuito == 3f);

        // --- Cálculo: el generador y el solucionador independiente coinciden en todas las noches ---
        bool todasCoinciden = true, enunciados = true, rango = true;
        var clasesPorNoche = new Dictionary<int, HashSet<string>>();
        for (int noche = 1; noche <= 6; noche++)
        {
            clasesPorNoche[noche] = new HashSet<string>();
            for (int semilla = 0; semilla < 400; semilla++)
            {
                var tareas = new TareasDeOperador(new Random(semilla));
                var t = tareas.Pedir(TareasDeOperador.Tipo.Calculo, noche, 0f);
                clasesPorNoche[noche].Add(t.clase);
                if (Resolver(t) != t.respuesta) { todasCoinciden = false; Console.WriteLine($"    noche {noche} semilla {semilla}: {t.enunciado} -> {t.respuesta} y el solucionador da {Resolver(t)}"); }
                if (string.IsNullOrWhiteSpace(t.enunciado) || t.datos == null || t.datos.Length == 0) enunciados = false;
                if (t.respuesta < 0 || t.respuesta > 1500) rango = false;
            }
        }
        Comprobar("en las 6 noches la respuesta guardada es la que el enunciado pide (2 400 tareas)", todasCoinciden);
        Comprobar("todas traen enunciado y datos", enunciados);
        Comprobar("las respuestas son enteros entre 0 y 1 500 (se escriben a mano)", rango);
        Comprobar("la noche 1 solo pide binario corto y aritmética simple",
            clasesPorNoche[1].SetEquals(new[] { "binario", "aritmetica" }));
        Comprobar("la noche 2 añade series y memoria, sin hexadecimal",
            clasesPorNoche[2].SetEquals(new[] { "binario", "aritmetica", "serie", "memoria" }));
        Comprobar("de la noche 3 en adelante aparece el hexadecimal",
            Enumerable.Range(3, 4).All(n => clasesPorNoche[n].Contains("hexadecimal")));

        // El binario crece con la noche (3, 4 y 5 bits).
        Func<int, int> bitsMaximos = noche =>
        {
            int max = 0;
            for (int s = 0; s < 300; s++)
            {
                var t = new TareasDeOperador(new Random(s)).Pedir(TareasDeOperador.Tipo.Calculo, noche, 0f);
                if (t.clase == "binario") max = Math.Max(max, t.datos[0].Length);
            }
            return max;
        };
        Comprobar("el binario es de 3 bits en la noche 1, 4 en la 2 y 5 desde la 4",
            bitsMaximos(1) == 3 && bitsMaximos(2) == 4 && bitsMaximos(4) == 5);

        // No repite la misma clase dos veces seguidas cuando hay de dónde elegir.
        var seguidas = new TareasDeOperador(new Random(7));
        string anterior = null;
        bool sinRepetir = true;
        for (int i = 0; i < 300; i++)
        {
            var t = seguidas.Pedir(TareasDeOperador.Tipo.Calculo, 3, i * 100f);
            seguidas.Responder(t.id, t.respuesta, null, i * 100f + 5f);
            if (t.clase == anterior) sinRepetir = false;
            anterior = t.clase;
        }
        Comprobar("no repite la misma clase dos veces seguidas (300 tareas en la noche 3)", sinRepetir);

        // --- Circuito: siempre resoluble y nunca de suerte ---
        bool circuitosValidos = true, nuncaTrivial = true, bienFormados = true;
        for (int noche = 1; noche <= 6; noche++)
        {
            for (int semilla = 0; semilla < 250; semilla++)
            {
                var t = new TareasDeOperador(new Random(semilla)).Pedir(TareasDeOperador.Tipo.Circuito, noche, 0f);
                int esperadas = noche <= 2 ? 3 : 4;
                if (t.entradas.Length != esperadas) bienFormados = false;
                if (t.puertas.Length < 2 || t.salida != t.puertas[t.puertas.Length - 1].id) bienFormados = false;
                var conocidas = new HashSet<string>(t.entradas);
                foreach (var p in t.puertas)
                {
                    if (!conocidas.Contains(p.a) || (p.op != "NOT" && !conocidas.Contains(p.b))) bienFormados = false;
                    conocidas.Add(p.id);
                }
                // todas las puertas menos la última alimentan a otra (no hay puertas de adorno)
                foreach (var p in t.puertas.Take(t.puertas.Length - 1))
                    if (!t.puertas.Any(q => q.a == p.id || q.b == p.id)) bienFormados = false;
                int buenas = Combinaciones(t.entradas.Length).Count(c => Salida(t, c) == t.objetivo);
                int total = 1 << t.entradas.Length;
                if (buenas < 1) circuitosValidos = false;
                if (buenas > total / 2) nuncaTrivial = false;
            }
        }
        Comprobar("los circuitos están bien formados (3 entradas en las noches 1-2, 4 después, sin puertas de adorno)", bienFormados);
        Comprobar("todo circuito tiene al menos una solución (1 500 circuitos)", circuitosValidos);
        Comprobar("ninguno se resuelve a ciegas: aciertan a lo sumo la mitad de las combinaciones", nuncaTrivial);

        // --- Flujo de una tarea ---
        var f = new TareasDeOperador(new Random(1));
        Comprobar("al empezar no hay tarea pendiente ni bloqueo", f.Pendiente == null && !f.EstaBloqueado(0f));
        var primera = f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 10f);
        Comprobar("pedir una tarea la deja pendiente y con id", primera != null && f.Pendiente == primera && primera.id > 0);
        Comprobar("volver a pedir la misma devuelve la misma (cerrar y abrir no cambia el problema)",
            f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 11f) == primera);

        Comprobar("responder a una tarea que no es la pendiente se rechaza (sin_tarea)",
            f.Responder(primera.id + 99, primera.respuesta, null, 15f) == TareasDeOperador.ResultadoRespuesta.SinTarea);
        Comprobar("responder en menos de 1,5 s se rechaza por muy rápido",
            f.Responder(primera.id, primera.respuesta, null, 11.2f) == TareasDeOperador.ResultadoRespuesta.MuyRapida);
        Comprobar("y la tarea sigue pendiente para reintentar", f.Pendiente == primera);
        Comprobar("la respuesta correcta se acepta",
            f.Responder(primera.id, primera.respuesta, null, 12f) == TareasDeOperador.ResultadoRespuesta.Correcta);
        Comprobar("tras acertar ya no hay tarea pendiente ni bloqueo", f.Pendiente == null && !f.EstaBloqueado(12f));
        Comprobar("no se puede responder dos veces la misma",
            f.Responder(primera.id, primera.respuesta, null, 13f) == TareasDeOperador.ResultadoRespuesta.SinTarea);

        var segunda = f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 20f);
        Comprobar("la tarea siguiente tiene otro id", segunda.id != primera.id);
        Comprobar("una respuesta equivocada se rechaza como incorrecta",
            f.Responder(segunda.id, segunda.respuesta + 1, null, 22f) == TareasDeOperador.ResultadoRespuesta.Incorrecta);
        Comprobar("el error bloquea la terminal 3 s", f.EstaBloqueado(22f) && f.EstaBloqueado(24.9f) && !f.EstaBloqueado(25.1f));
        Comprobar("durante el bloqueo faltan los segundos que quedan", Math.Abs(f.SegundosDeBloqueo(23f) - 2f) < 0.01f);
        Comprobar("y no se entrega tarea nueva", f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 23f) == null);
        Comprobar("tras el error la tarea ya no está pendiente (hay que pedir otra)", f.Pendiente == null);
        Comprobar("pasado el bloqueo se entrega una tarea nueva",
            f.Pedir(TareasDeOperador.Tipo.Calculo, 1, 25.5f) != null);

        // --- Circuito: se responde con los interruptores ---
        var g = new TareasDeOperador(new Random(3));
        var circuito = g.Pedir(TareasDeOperador.Tipo.Circuito, 2, 0f);
        int[] solucion = Combinaciones(circuito.entradas.Length).First(c => Salida(circuito, c) == circuito.objetivo);
        int[] equivocada = Combinaciones(circuito.entradas.Length).First(c => Salida(circuito, c) != circuito.objetivo);
        Comprobar("responder el circuito en menos de 3 s se rechaza por muy rápido",
            g.Responder(circuito.id, 0, solucion, 2f) == TareasDeOperador.ResultadoRespuesta.MuyRapida);
        Comprobar("con interruptores que no dan el objetivo es incorrecto",
            g.Responder(circuito.id, 0, equivocada, 4f) == TareasDeOperador.ResultadoRespuesta.Incorrecta);
        var circuito2 = g.Pedir(TareasDeOperador.Tipo.Circuito, 2, 10f);
        int[] solucion2 = Combinaciones(circuito2.entradas.Length).First(c => Salida(circuito2, c) == circuito2.objetivo);
        Comprobar("con interruptores que dan el objetivo es correcto",
            g.Responder(circuito2.id, 0, solucion2, 14f) == TareasDeOperador.ResultadoRespuesta.Correcta);
        var circuito3 = g.Pedir(TareasDeOperador.Tipo.Circuito, 2, 20f);
        Comprobar("con un número equivocado de interruptores es incorrecto",
            g.Responder(circuito3.id, 0, new[] { 1 }, 25f) == TareasDeOperador.ResultadoRespuesta.Incorrecta);
        Comprobar("sin interruptores es incorrecto",
            g.Responder(g.Pedir(TareasDeOperador.Tipo.Circuito, 2, 40f).id, 0, null, 45f) == TareasDeOperador.ResultadoRespuesta.Incorrecta);

        // --- Cambiar de tipo, caducidad y reinicio ---
        var h = new TareasDeOperador(new Random(5));
        var calculo = h.Pedir(TareasDeOperador.Tipo.Calculo, 1, 0f);
        var otra = h.Pedir(TareasDeOperador.Tipo.Circuito, 1, 1f);
        Comprobar("pedir otro tipo reemplaza la tarea pendiente", otra.tipo == TareasDeOperador.Tipo.Circuito && h.Pendiente == otra);
        Comprobar("la tarea reemplazada ya no se puede responder",
            h.Responder(calculo.id, calculo.respuesta, null, 5f) == TareasDeOperador.ResultadoRespuesta.SinTarea);
        Comprobar("una tarea abandonada caduca a los 90 s",
            h.Responder(otra.id, 0, new[] { 1, 1, 1 }, 1f + TareasDeOperador.SegundosMaximosDeTarea + 1f) == TareasDeOperador.ResultadoRespuesta.SinTarea);
        var nueva = h.Pedir(TareasDeOperador.Tipo.Calculo, 1, 200f);
        Comprobar("y pedir pasado ese tiempo da una tarea nueva", nueva != null && nueva.id != otra.id);
        h.Pedir(TareasDeOperador.Tipo.Calculo, 1, 201f);
        h.Responder(nueva.id, nueva.respuesta + 1, null, 205f);
        h.Reiniciar();
        Comprobar("Reiniciar borra la tarea pendiente y el bloqueo", h.Pendiente == null && !h.EstaBloqueado(205f));

        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
