using System;

/// <summary>
/// El plano 2D del mapa para el jugador 2: qué pieza del modelo es cada objeto
/// (por su nombre, tal como viene del .obj del mapa) y cómo se rotula. Unity lo
/// arma en el momento con la geometría real (Renderer.bounds), así que si el
/// mapa cambia, el plano cambia con él. Sin UnityEngine para probarlo con Mono.
///
/// Tipos: "sala", "oficina", "pasillo", "ducto" (pisos), "muro", "tarima",
/// "mesa", "caja". Cadena vacía = no va en el plano (techos, sillas, focos...).
/// </summary>
public static class PlanoDelMapa
{
    public struct Pieza
    {
        public string Tipo;
        public string Etiqueta;
    }

    public struct Rectangulo
    {
        public float x0, z0, x1, z1;
    }

    private static readonly Pieza Nada = new Pieza { Tipo = "", Etiqueta = "" };

    public static Pieza Clasificar(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return Nada;
        string n = nombre.ToLowerInvariant();

        if (n.StartsWith("piso_")) return ClasificarPiso(n);
        if (n.StartsWith("muro_") || n.StartsWith("vent_")) return Crear("muro");
        if (n.StartsWith("plataforma_escenario")) return Crear("tarima");
        if (n.StartsWith("mesa_")) return n.Contains("_base") ? Nada : Crear("mesa");
        if (n.StartsWith("caja_musica")) return Crear("caja");
        return Nada;
    }

    private static Pieza ClasificarPiso(string n)
    {
        if (n.Contains("ducto")) return Crear("ducto", n.Contains("horizontal") ? "VENTILACIÓN" : "");
        if (n.Contains("oficina")) return Crear("oficina", "OFICINA");
        if (n.Contains("pasillo_izquierdo")) return Crear("pasillo", "PASILLO IZQ.");
        if (n.Contains("pasillo_derecho")) return Crear("pasillo", "PASILLO DER.");
        if (n.Contains("pasillo")) return Crear("pasillo", "PASILLO");
        if (n.Contains("comedor")) return Crear("sala", "COMEDOR");
        if (n.Contains("escenario")) return Crear("sala", "ESCENARIO");
        if (n.Contains("rincon")) return Crear("sala", "RINCÓN DE PREMIOS");
        return Crear("sala");
    }

    private static Pieza Crear(string tipo, string etiqueta = "") => new Pieza { Tipo = tipo, Etiqueta = etiqueta };

    /// Rectángulo en el plano XZ a partir de los bordes de una caja (los de
    /// Renderer.bounds), ordenados y redondeados a centímetros para que el
    /// mensaje pese poco. `holgura` se ignora por debajo de un centímetro.
    public static Rectangulo RectanguloDeCaja(float xa, float za, float xb, float zb, float holgura)
    {
        return new Rectangulo
        {
            x0 = Centimetros(Math.Min(xa, xb)),
            z0 = Centimetros(Math.Min(za, zb)),
            x1 = Centimetros(Math.Max(xa, xb)),
            z1 = Centimetros(Math.Max(za, zb)),
        };
    }

    private static float Centimetros(float metros) => (float)Math.Round(metros, 2);
}
