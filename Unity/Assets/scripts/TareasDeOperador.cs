using System;
using System.Collections.Generic;

/// <summary>
/// Las tareas de la terminal del jugador 2: lo que tiene que resolver para
/// ganar sus poderes. Unity es la autoridad (inventa el problema y comprueba la
/// respuesta); la página solo lo muestra.
///
/// Son para cualquier persona, no solo para quien estudió computación
/// (2026-11-13: antes había binario, hexadecimal y circuitos lógicos):
///
/// * **Cálculo** (da 1 carga de trampa): una operación, una serie o un problema
///   corto de la vida diaria (cuántos minutos faltan, cuánto vuelto te dan).
/// * **Ecuación** (da el rescate de Puppet): encontrar x, por ejemplo
///   3x + 5 = 20. De un paso en las noches 1 y 2, de dos pasos en la 3 y la 4,
///   y con paréntesis desde la 5.
///
/// Las dos se responden con un número. Un error bloquea la terminal unos
/// segundos y se pierde ese problema, así que no se puede probar a ciegas.
///
/// Sin `UnityEngine`, para probarla fuera del editor. El tiempo lo pone quien
/// la llama (p. ej. `Time.time`).
/// </summary>
public class TareasDeOperador
{
    public enum Tipo { Calculo, Ecuacion }
    public enum ResultadoRespuesta { Correcta, Incorrecta, MuyRapida, SinTarea }

    /// Lo que cuesta equivocarse: la terminal no entrega tareas durante este tiempo.
    public const float BloqueoPorErrorSegundos = 3f;
    /// Menos que esto desde que se entrega es imposible de hacer a mano: Unity lo
    /// rechaza para que la página no pueda fingir una tarea.
    public const float MinimoSegundosCalculo = 1.5f;
    public const float MinimoSegundosEcuacion = 3f;
    /// Una tarea abandonada caduca. Mientras no caduca, volver a pedirla da el
    /// mismo problema (cerrar y abrir la ventana no sirve para buscar uno fácil).
    public const float SegundosMaximosDeTarea = 90f;

    public class Tarea
    {
        public int id;
        public Tipo tipo;
        public int noche;
        public float inicio;

        public string clase;        // aritmetica, serie, problema o ecuacion
        public string enunciado;
        public string[] datos;      // lo que pide el enunciado, para quien quiera resolverlo por código
        public int respuesta;
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
        Tarea tarea = tipo == Tipo.Calculo ? GenerarCalculo(noche) : GenerarEcuacion(noche);
        tarea.id = ++ultimoId;
        tarea.tipo = tipo;
        tarea.noche = noche;
        tarea.inicio = ahora;
        Pendiente = tarea;
        return tarea;
    }

    /// Comprueba la respuesta (el número que escribió el jugador 2). Acertar o
    /// fallar cierra la tarea; ir demasiado rápido la deja abierta.
    public ResultadoRespuesta Responder(int id, int valor, float ahora)
    {
        if (Pendiente == null || Pendiente.id != id) return ResultadoRespuesta.SinTarea;
        if (Caducada(Pendiente, ahora)) { Pendiente = null; return ResultadoRespuesta.SinTarea; }

        float minimo = Pendiente.tipo == Tipo.Calculo ? MinimoSegundosCalculo : MinimoSegundosEcuacion;
        if (ahora - Pendiente.inicio < minimo) return ResultadoRespuesta.MuyRapida;

        bool acierto = valor == Pendiente.respuesta;
        Pendiente = null;
        if (acierto) return ResultadoRespuesta.Correcta;
        bloqueadoHasta = ahora + BloqueoPorErrorSegundos;
        return ResultadoRespuesta.Incorrecta;
    }

    private static bool Caducada(Tarea tarea, float ahora) { return ahora - tarea.inicio > SegundosMaximosDeTarea; }

    // =========================================================================== cálculo
    private Tarea GenerarCalculo(int noche)
    {
        var clases = new List<string> { "aritmetica", "problema" };
        if (noche >= 2) clases.Add("serie");
        if (clases.Count > 1) clases.Remove(ultimaClase);
        string clase = clases[azar.Next(clases.Count)];
        ultimaClase = clase;

        var t = new Tarea { clase = clase };
        switch (clase)
        {
            case "aritmetica": GenerarAritmetica(t, noche); break;
            case "serie": GenerarSerie(t, noche); break;
            default:
                if (azar.Next(2) == 0) GenerarProblemaDeReloj(t, noche);
                else GenerarProblemaDeVuelto(t, noche);
                break;
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

    /// "Son las 2:45 AM. ¿Cuántos minutos faltan para las 3:30 AM?" Dentro del
    /// turno (12:00 a 6:00 AM) y en múltiplos de 5; la espera crece con la noche.
    private void GenerarProblemaDeReloj(Tarea t, int noche)
    {
        int maximo = noche <= 2 ? 40 : noche <= 4 ? 70 : 95;
        int minutos = 5 * azar.Next(2, maximo / 5 + 1);
        int desde = 5 * azar.Next(0, (6 * 60 - minutos) / 5 + 1);   // minutos pasada la medianoche
        int hasta = desde + minutos;
        t.clase = "problema";
        t.datos = new[] { "reloj", Hora(desde), Hora(hasta) };
        t.respuesta = minutos;
        t.enunciado = "Son las " + Hora(desde) + " AM. ¿Cuántos minutos faltan para las " + Hora(hasta) + " AM?";
    }

    private static string Hora(int minutosDesdeMedianoche)
    {
        int h = minutosDesdeMedianoche / 60;
        return (h == 0 ? 12 : h) + ":" + (minutosDesdeMedianoche % 60).ToString("00");
    }

    /// "Compras 3 pilas a 4 soles cada una y pagas con 20 soles. ¿Cuánto vuelto te dan?"
    private void GenerarProblemaDeVuelto(Tarea t, int noche)
    {
        int cantidad = azar.Next(2, noche <= 2 ? 4 : 7);
        int precio = azar.Next(2, noche <= 2 ? 6 : 10);
        int gasto = cantidad * precio;
        int pago = 10;
        while (pago <= gasto) pago = pago == 10 ? 20 : pago == 20 ? 50 : 100;
        t.clase = "problema";
        t.datos = new[] { "vuelto", cantidad.ToString(), precio.ToString(), pago.ToString() };
        t.respuesta = pago - gasto;
        t.enunciado = "Compras " + cantidad + " pilas a " + precio + " soles cada una y pagas con " + pago +
                      " soles. ¿Cuánto vuelto te dan?";
    }

    // =========================================================================== ecuación
    /// datos: lineal, a, op, b, c  →  a·x op b = c   |   parentesis, a, op, b, c  →  a(x op b) = c
    private Tarea GenerarEcuacion(int noche)
    {
        int x = azar.Next(2, 16);
        string forma = "lineal", op = "+";
        int a, b, c;
        if (noche <= 2)
        {
            // Un solo paso: x + 7 = 15, x − 4 = 9 ó 3x = 12.
            switch (azar.Next(3))
            {
                case 0: a = 1; b = azar.Next(2, 16); c = x + b; break;
                case 1: a = 1; op = "−"; b = azar.Next(1, x); c = x - b; break;
                default: a = azar.Next(2, 6); b = 0; x = azar.Next(2, 11); c = a * x; break;
            }
        }
        else if (noche <= 4 || azar.Next(2) == 0)
        {
            // Dos pasos: 2x − 4 = 10 ó 3x + 5 = 20.
            a = azar.Next(2, noche <= 4 ? 5 : 7);
            x = azar.Next(2, 11);
            if (azar.Next(2) == 0) { b = azar.Next(2, 13); c = a * x + b; }
            else { op = "−"; b = azar.Next(1, Math.Min(13, a * x)); c = a * x - b; }
        }
        else
        {
            // Con paréntesis: 2(x − 3) = 10 ó 3(x + 2) = 21.
            forma = "parentesis";
            a = azar.Next(2, 5);
            if (azar.Next(2) == 0) { b = azar.Next(1, 6); x = azar.Next(b + 1, b + 9); op = "−"; c = a * (x - b); }
            else { b = azar.Next(1, 6); x = azar.Next(1, 11); c = a * (x + b); }
        }

        var t = new Tarea { clase = "ecuacion" };
        t.datos = new[] { forma, a.ToString(), op, b.ToString(), c.ToString() };
        t.respuesta = x;
        t.enunciado = "Encuentra x: " + TextoDeEcuacion(forma, a, op, b, c);
        return t;
    }

    private static string TextoDeEcuacion(string forma, int a, string op, int b, int c)
    {
        if (forma == "parentesis") return a + "(x " + op + " " + b + ") = " + c;
        string termino = a == 1 ? "x" : a + "x";
        return b == 0 ? termino + " = " + c : termino + " " + op + " " + b + " = " + c;
    }
}
