using System;

// Pruebas de SemaforoDeDuctos fuera de Unity (ver probar_reglas_de_operador.sh):
// de qué color está la luz de cada ducto según por dónde va su animatrónico.
public static class PruebaSemaforoDeDuctos
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static SemaforoDeDuctos.Luz Luz(string lado, string punto) { return SemaforoDeDuctos.LuzDe(lado, punto); }

    public static int Main()
    {
        var verde = SemaforoDeDuctos.Luz.Verde;
        var amarillo = SemaforoDeDuctos.Luz.Amarillo;
        var rojo = SemaforoDeDuctos.Luz.Rojo;

        // --- Qué ducto usa cada animatrónico (por su ruta) ---
        Comprobar("Vixy usa el ducto izquierdo", SemaforoDeDuctos.LadoDeRuta(RecorridosDeAnimatronicos.Ruta("vixy")) == "izquierda");
        Comprobar("Puppet usa el ducto derecho", SemaforoDeDuctos.LadoDeRuta(RecorridosDeAnimatronicos.Ruta("puppet")) == "derecha");
        Comprobar("Freddy no usa ductos (viene por el pasillo)", SemaforoDeDuctos.LadoDeRuta(RecorridosDeAnimatronicos.Ruta("freddy")) == null);
        Comprobar("una ruta vacía o null no usa ductos",
            SemaforoDeDuctos.LadoDeRuta(new string[0]) == null && SemaforoDeDuctos.LadoDeRuta(null) == null);

        // --- Ducto izquierdo: V01 → V02 → V03 → V04 → oficina ---
        Comprobar("fuera del ducto (pasillo izquierdo, P12): verde", Luz("izquierda", "P12") == verde);
        Comprobar("todavía en el escenario o sin punto: verde", Luz("izquierda", "P02") == verde && Luz("izquierda", null) == verde);
        Comprobar("primera mitad del ducto (V01, V02): amarillo", Luz("izquierda", "V01") == amarillo && Luz("izquierda", "V02") == amarillo);
        Comprobar("último tramo, junto a la oficina (V03, V04): rojo", Luz("izquierda", "V03") == rojo && Luz("izquierda", "V04") == rojo);
        Comprobar("entrando a la oficina (J1): rojo", Luz("izquierda", "J1") == rojo);

        // --- Ducto derecho: V05 → V06 → V07 → V08 → oficina ---
        Comprobar("ducto derecho: V05 y V06 amarillo", Luz("derecha", "V05") == amarillo && Luz("derecha", "V06") == amarillo);
        Comprobar("ducto derecho: V07 y V08 rojo", Luz("derecha", "V07") == rojo && Luz("derecha", "V08") == rojo);
        Comprobar("un punto del otro ducto no enciende este", Luz("derecha", "V04") == verde && Luz("izquierda", "V08") == verde);
        Comprobar("sin ducto (Freddy) siempre verde", Luz(null, "V04") == verde && Luz(null, "J1") == verde);

        // --- Dos animatrónicos en el mismo ducto: manda el más cercano ---
        Comprobar("de dos luces gana la peor",
            SemaforoDeDuctos.LaPeor(verde, rojo) == rojo && SemaforoDeDuctos.LaPeor(amarillo, verde) == amarillo
            && SemaforoDeDuctos.LaPeor(verde, verde) == verde);

        // --- Cómo viaja por la red ---
        Comprobar("los colores se escriben en minúsculas: verde, amarillo, rojo",
            SemaforoDeDuctos.Texto(verde) == "verde" && SemaforoDeDuctos.Texto(amarillo) == "amarillo" && SemaforoDeDuctos.Texto(rojo) == "rojo");

        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
