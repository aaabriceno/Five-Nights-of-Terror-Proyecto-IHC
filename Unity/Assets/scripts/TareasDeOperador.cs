using System;
using System.Collections.Generic;

/// <summary>
/// Las tareas de la terminal del jugador 2: lo que tiene que resolver para
/// ganar sus poderes. Unity es la autoridad (inventa el problema y comprueba la
/// respuesta); la página solo lo muestra.
///
/// * **Cálculo** (da 1 carga de trampa): pasar un binario o un hexadecimal a
///   decimal, una operación, una serie o una pregunta de memoria. De 8 a 12 s.
/// * **Circuito lógico** (da el rescate de Puppet): poner los interruptores para
///   que la salida de unas puertas Y / O / NO / O exclusivo sea la pedida. De
///   20 a 30 s.
///
/// Los problemas crecen con la noche. Un error bloquea la terminal unos
/// segundos y se pierde ese problema, así que no se puede probar a ciegas.
///
/// Sin `UnityEngine`, para probarla fuera del editor. El tiempo lo pone quien
/// la llama (p. ej. `Time.time`).
/// </summary>
public class TareasDeOperador
{
    public enum Tipo { Calculo, Circuito }
    public enum ResultadoRespuesta { Correcta, Incorrecta, MuyRapida, SinTarea }

    /// Lo que cuesta equivocarse: la terminal no entrega tareas durante este tiempo.
    public const float BloqueoPorErrorSegundos = 3f;
    /// Menos que esto desde que se entrega es imposible de hacer a mano: Unity lo
    /// rechaza para que la página no pueda fingir una tarea.
    public const float MinimoSegundosCalculo = 1.5f;
    public const float MinimoSegundosCircuito = 3f;
    /// Una tarea abandonada caduca. Mientras no caduca, volver a pedirla da el
    /// mismo problema (cerrar y abrir la ventana no sirve para buscar uno fácil).
    public const float SegundosMaximosDeTarea = 90f;

    /// Una puerta del circuito: `op` es AND, OR, XOR o NOT (esta usa solo `a`);
    /// `a` y `b` son entradas (A, B...) o la salida de otra puerta (G1, G2...).
    public class Puerta { public string id; public string op; public string a; public string b; }

    public class Tarea
    {
        public int id;
        public Tipo tipo;
        public int noche;
        public float inicio;

        // Cálculo
        public string clase;        // binario, hexadecimal, aritmetica, memoria o serie
        public string enunciado;
        public string[] datos;      // lo que pide el enunciado, para quien quiera resolverlo por código
        public int respuesta;

        // Circuito
        public string[] entradas;
        public Puerta[] puertas;    // en orden: cada una solo usa entradas o puertas anteriores
        public string salida;       // la última puerta
        public bool objetivo;       // el foco debe quedar así
    }

    private readonly Random azar;
    private int ultimoId;
    private string ultimaClase;
    private float bloqueadoHasta = float.NegativeInfinity;

    public Tarea Pendiente { get; private set; }

    public TareasDeOperador(Random azar) { this.azar = azar ?? new Random(); }

    public void Reiniciar()
    {
        Pendiente = null;
        ultimaClase = null;
        bloqueadoHasta = float.NegativeInfinity;
    }

    public bool EstaBloqueado(float ahora) { return ahora < bloqueadoHasta; }

    public float SegundosDeBloqueo(float ahora) { return Math.Max(0f, bloqueadoHasta - ahora); }

    /// Entrega una tarea. Devuelve null si la terminal está bloqueada por un
    /// error. Si ya hay una pendiente del mismo tipo (y no caducó), es esa.
    public Tarea Pedir(Tipo tipo, int noche, float ahora)
    {
        if (EstaBloqueado(ahora)) return null;
        if (Pendiente != null && Pendiente.tipo == tipo && !Caducada(Pendiente, ahora)) return Pendiente;

        noche = Math.Max(1, Math.Min(6, noche));
        Tarea tarea = tipo == Tipo.Calculo ? GenerarCalculo(noche) : GenerarCircuito(noche);
        tarea.id = ++ultimoId;
        tarea.tipo = tipo;
        tarea.noche = noche;
        tarea.inicio = ahora;
        Pendiente = tarea;
        return tarea;
    }

    /// Comprueba la respuesta: `valor` para un cálculo, `interruptores` (0 o 1 por
    /// entrada, en el orden de `entradas`) para un circuito. Acertar o fallar
    /// cierra la tarea; ir demasiado rápido la deja abierta.
    public ResultadoRespuesta Responder(int id, int valor, int[] interruptores, float ahora)
    {
        if (Pendiente == null || Pendiente.id != id) return ResultadoRespuesta.SinTarea;
        if (Caducada(Pendiente, ahora)) { Pendiente = null; return ResultadoRespuesta.SinTarea; }

        float minimo = Pendiente.tipo == Tipo.Calculo ? MinimoSegundosCalculo : MinimoSegundosCircuito;
        if (ahora - Pendiente.inicio < minimo) return ResultadoRespuesta.MuyRapida;

        bool acierto = Pendiente.tipo == Tipo.Calculo
            ? valor == Pendiente.respuesta
            : interruptores != null && interruptores.Length == Pendiente.entradas.Length &&
              SalidaDelCircuito(Pendiente, interruptores) == Pendiente.objetivo;

        Pendiente = null;
        if (acierto) return ResultadoRespuesta.Correcta;
        bloqueadoHasta = ahora + BloqueoPorErrorSegundos;
        return ResultadoRespuesta.Incorrecta;
    }

    private static bool Caducada(Tarea tarea, float ahora) { return ahora - tarea.inicio > SegundosMaximosDeTarea; }

    // =========================================================================== cálculo
    private Tarea GenerarCalculo(int noche)
    {
        var clases = new List<string> { "binario", "aritmetica" };
        if (noche >= 2) { clases.Add("serie"); clases.Add("memoria"); }
        if (noche >= 3) clases.Add("hexadecimal");
        if (clases.Count > 1) clases.Remove(ultimaClase);
        string clase = clases[azar.Next(clases.Count)];
        ultimaClase = clase;

        var t = new Tarea { clase = clase };
        switch (clase)
        {
            case "binario":
                {
                    int bits = noche <= 1 ? 3 : noche <= 3 ? 4 : 5;
                    int valor = azar.Next(3, 1 << bits);
                    string binario = Convert.ToString(valor, 2).PadLeft(bits, '0');
                    t.datos = new[] { binario };
                    t.respuesta = valor;
                    t.enunciado = "Pasa " + binario + " de binario a decimal";
                    break;
                }
            case "hexadecimal":
                {
                    int valor = azar.Next(0x10, noche <= 3 ? 0x40 : 0x80);
                    string hexadecimal = Convert.ToString(valor, 16).ToUpperInvariant();
                    t.datos = new[] { hexadecimal };
                    t.respuesta = valor;
                    t.enunciado = "Pasa " + hexadecimal + " de hexadecimal a decimal";
                    break;
                }
            case "aritmetica": GenerarAritmetica(t, noche); break;
            case "memoria":
                {
                    if (azar.Next(2) == 0)
                    {
                        int bits = azar.Next(3, 7);
                        t.datos = new[] { "valores", bits.ToString() };
                        t.respuesta = 1 << bits;
                        t.enunciado = "¿Cuántos valores distintos se pueden guardar con " + bits + " bits?";
                    }
                    else
                    {
                        int bytes = azar.Next(2, 10);
                        t.datos = new[] { "bits", bytes.ToString() };
                        t.respuesta = 8 * bytes;
                        t.enunciado = "¿Cuántos bits hay en " + bytes + " bytes?";
                    }
                    break;
                }
            case "serie": GenerarSerie(t, noche); break;
        }
        return t;
    }

    private void GenerarAritmetica(Tarea t, int noche)
    {
        string[] datos;
        int respuesta;
        if (noche <= 1)
        {
            if (azar.Next(2) == 0)
            {
                int a = azar.Next(12, 50), b = azar.Next(12, 50);
                datos = new[] { a.ToString(), "+", b.ToString() };
                respuesta = a + b;
            }
            else
            {
                int a = azar.Next(3, 10), b = azar.Next(3, 10);
                datos = new[] { a.ToString(), "×", b.ToString() };
                respuesta = a * b;
            }
        }
        else if (noche <= 3)
        {
            if (azar.Next(2) == 0)
            {
                int a = azar.Next(6, 13), b = azar.Next(6, 13);
                datos = new[] { a.ToString(), "×", b.ToString() };
                respuesta = a * b;
            }
            else
            {
                int a = azar.Next(3, 10), b = azar.Next(3, 10), c = azar.Next(10, 31);
                datos = new[] { a.ToString(), "×", b.ToString(), "+", c.ToString() };
                respuesta = a * b + c;
            }
        }
        else
        {
            int a = azar.Next(7, 16), b = azar.Next(7, 16);
            int c = azar.Next(10, Math.Min(61, a * b - 9));   // el resultado nunca baja de 10
            datos = new[] { a.ToString(), "×", b.ToString(), "−", c.ToString() };
            respuesta = a * b - c;
        }
        t.datos = datos;
        t.respuesta = respuesta;
        t.enunciado = "Calcula " + string.Join(" ", datos);
    }

    private void GenerarSerie(Tarea t, int noche)
    {
        var tipos = new List<string> { "aritmetica", "geometrica" };
        if (noche >= 4) { tipos.Add("cuadrados"); tipos.Add("fibonacci"); }
        string tipo = tipos[azar.Next(tipos.Count)];

        int[] serie;
        int siguiente;
        switch (tipo)
        {
            case "geometrica":
                {
                    int inicio = azar.Next(1, 5), razon = azar.Next(2, 4);
                    serie = new[] { inicio, inicio * razon, inicio * razon * razon, inicio * razon * razon * razon };
                    siguiente = serie[3] * razon;
                    break;
                }
            case "cuadrados":
                serie = new[] { 1, 4, 9, 16 };
                siguiente = 25;
                break;
            case "fibonacci":
                {
                    int a = azar.Next(1, 5), b = azar.Next(1, 6);
                    serie = new[] { a, b, a + b, a + 2 * b, 2 * a + 3 * b };
                    siguiente = 3 * a + 5 * b;
                    break;
                }
            default:
                {
                    int inicio = azar.Next(1, 21), paso = azar.Next(2, 10);
                    serie = new[] { inicio, inicio + paso, inicio + 2 * paso, inicio + 3 * paso };
                    siguiente = inicio + 4 * paso;
                    break;
                }
        }
        var datos = new string[serie.Length];
        for (int i = 0; i < serie.Length; i++) datos[i] = serie[i].ToString();
        t.datos = datos;
        t.respuesta = siguiente;
        t.enunciado = "Completa la serie: " + string.Join(", ", datos) + ", ?";
    }

    // =========================================================================== circuito
    private Tarea GenerarCircuito(int noche)
    {
        int nEntradas = noche <= 2 ? 3 : 4;
        string[] entradas = new[] { "A", "B", "C", "D" };
        Array.Resize(ref entradas, nEntradas);
        var binarias = noche >= 3 ? new[] { "AND", "OR", "XOR" } : new[] { "AND", "OR" };

        for (int intento = 0; intento < 400; intento++)
        {
            int nPuertas = noche <= 1 ? azar.Next(2, 4) : noche == 2 ? 3 : noche == 3 ? azar.Next(3, 5) : azar.Next(4, 6);
            var puertas = new List<Puerta>();
            var sinUsarPuertas = new List<string>();
            var sinUsarEntradas = new List<string>(entradas);
            var usadas = new HashSet<string>();

            for (int i = 0; i < nPuertas; i++)
            {
                bool ultima = i == nPuertas - 1;
                string op = !ultima && azar.Next(4) == 0 ? "NOT" : binarias[azar.Next(binarias.Length)];
                string a = Tomar(sinUsarPuertas, sinUsarEntradas, entradas, puertas, null);
                string b = op == "NOT" ? null : Tomar(sinUsarPuertas, sinUsarEntradas, entradas, puertas, a);
                foreach (string operando in new[] { a, b })
                {
                    if (operando == null) continue;
                    sinUsarPuertas.Remove(operando);
                    sinUsarEntradas.Remove(operando);
                    usadas.Add(operando);
                }
                string id = "G" + (i + 1);
                puertas.Add(new Puerta { id = id, op = op, a = a, b = b });
                sinUsarPuertas.Add(id);
            }

            // Ninguna puerta de adorno: todas menos la última alimentan a otra.
            if (sinUsarPuertas.Count != 1) continue;
            int entradasUsadas = 0;
            foreach (string e in entradas) if (usadas.Contains(e)) entradasUsadas++;
            if (entradasUsadas < 2) continue;

            bool objetivo = azar.Next(2) == 1;
            var t = new Tarea { entradas = entradas, puertas = puertas.ToArray(), salida = puertas[puertas.Count - 1].id, objetivo = objetivo };
            int buenas = 0, total = 1 << nEntradas;
            for (int m = 0; m < total; m++)
            {
                var interruptores = new int[nEntradas];
                for (int i = 0; i < nEntradas; i++) interruptores[i] = (m >> i) & 1;
                if (SalidaDelCircuito(t, interruptores) == objetivo) buenas++;
            }
            // Con solución, y que acertar a ciegas no sea más probable que fallar.
            if (buenas >= 1 && buenas <= total / 2) return t;
        }

        // Respaldo (no debería hacer falta): (A Y B) Y (NO C), solo se enciende con A=1, B=1, C=0.
        return new Tarea
        {
            entradas = new[] { "A", "B", "C" },
            puertas = new[]
            {
                new Puerta { id = "G1", op = "AND", a = "A", b = "B" },
                new Puerta { id = "G2", op = "NOT", a = "C" },
                new Puerta { id = "G3", op = "AND", a = "G1", b = "G2" },
            },
            salida = "G3",
            objetivo = true
        };
    }

    /// Prefiere la salida de una puerta que nadie usa aún, luego una entrada sin usar, y al final cualquiera.
    private string Tomar(List<string> puertasSinUsar, List<string> entradasSinUsar, string[] entradas, List<Puerta> puertas, string excluir)
    {
        var candidatas = new List<string>();
        foreach (string s in puertasSinUsar) if (s != excluir) candidatas.Add(s);
        if (candidatas.Count == 0) foreach (string s in entradasSinUsar) if (s != excluir) candidatas.Add(s);
        if (candidatas.Count == 0)
        {
            foreach (string s in entradas) if (s != excluir) candidatas.Add(s);
            foreach (Puerta p in puertas) if (p.id != excluir) candidatas.Add(p.id);
        }
        return candidatas[azar.Next(candidatas.Count)];
    }

    /// Lo que sale de la última puerta con esos interruptores (0 o 1 por entrada).
    public static bool SalidaDelCircuito(Tarea tarea, int[] interruptores)
    {
        var valores = new Dictionary<string, bool>();
        for (int i = 0; i < tarea.entradas.Length; i++) valores[tarea.entradas[i]] = interruptores[i] != 0;
        foreach (Puerta p in tarea.puertas)
        {
            bool a = valores[p.a];
            switch (p.op)
            {
                case "NOT": valores[p.id] = !a; break;
                case "AND": valores[p.id] = a && valores[p.b]; break;
                case "OR": valores[p.id] = a || valores[p.b]; break;
                default: valores[p.id] = a ^ valores[p.b]; break;   // XOR
            }
        }
        return valores[tarea.salida];
    }
}
