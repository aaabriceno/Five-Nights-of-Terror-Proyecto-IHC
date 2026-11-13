using System;

// Pruebas de RecorridosDeAnimatronicos fuera de Unity (ver probar_reglas_de_operador.sh):
// por qué puntos va cada animatrónico y a qué puntos puede desviarse.
public static class PruebaRecorridosDeAnimatronicos
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static bool Igual(string[] a, params string[] b)
    {
        if (a == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    public static int Main()
    {
        // --- Rutas ---
        Comprobar("Freddy: escenario, comedor, pasillo principal y la oficina",
            Igual(RecorridosDeAnimatronicos.Ruta("freddy"), "P01", "P03", "P04", "P05", "P06", "P07", "J1"));
        Comprobar("Vixy: escenario, pasillo izquierdo y el ducto izquierdo hasta la oficina",
            Igual(RecorridosDeAnimatronicos.Ruta("vixy"), "P02", "P11", "P12", "V01", "V02", "V03", "V04", "J1"));
        Comprobar("Puppet: rincón de premios, pasillo derecho y el ducto derecho hasta la oficina",
            Igual(RecorridosDeAnimatronicos.Ruta("puppet"), "P13", "P14", "P15", "P16", "V05", "V06", "V07", "V08", "J1"));
        Comprobar("el nombre no distingue mayúsculas (Unity los llama Freddy, Vixy y Puppet)",
            Igual(RecorridosDeAnimatronicos.Ruta("Vixy"), RecorridosDeAnimatronicos.Ruta("vixy")));
        Comprobar("un animatrónico desconocido no tiene ruta", RecorridosDeAnimatronicos.Ruta("chica").Length == 0
            && RecorridosDeAnimatronicos.Ruta(null).Length == 0);

        // --- Desvíos ---
        // P10 está arriba del todo en el pasillo izquierdo y el escenario no tiene
        // puerta hacia ese pasillo: el desvío la hacía subir 35 m hasta la esquina
        // y bajar de nuevo antes de entrar al ducto.
        Comprobar("Vixy no tiene desvíos (ya no sube hasta P10)", RecorridosDeAnimatronicos.Desvios("vixy").Length == 0);
        Comprobar("ningún animatrónico se desvía a P10",
            Array.TrueForAll(new[] { "freddy", "vixy", "puppet" },
                a => Array.TrueForAll(RecorridosDeAnimatronicos.Desvios(a), d => d.punto != "P10")));
        var freddy = RecorridosDeAnimatronicos.Desvios("freddy");
        Comprobar("Freddy conserva sus desvíos: se asoma a P08 o P09 después de P04",
            freddy.Length == 2 && freddy[0].despuesDe == 2 && freddy[0].punto == "P08"
            && freddy[1].despuesDe == 2 && freddy[1].punto == "P09");
        Comprobar("Puppet no tiene desvíos", RecorridosDeAnimatronicos.Desvios("puppet").Length == 0);
        Comprobar("cada desvío sale de un tramo de su ruta (antes del punto penúltimo)",
            Array.TrueForAll(new[] { "freddy", "vixy", "puppet" }, a =>
                Array.TrueForAll(RecorridosDeAnimatronicos.Desvios(a),
                    d => d.despuesDe >= 0 && d.despuesDe < RecorridosDeAnimatronicos.Ruta(a).Length - 2)));
        Comprobar("cambiar el arreglo devuelto no cambia la ruta guardada", CopiaIndependiente());

        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }

    private static bool CopiaIndependiente()
    {
        string[] ruta = RecorridosDeAnimatronicos.Ruta("vixy");
        ruta[0] = "X";
        return RecorridosDeAnimatronicos.Ruta("vixy")[0] == "P02";
    }
}
