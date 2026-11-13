using System;

// Pruebas de PlanoDelMapa fuera de Unity (ver probar_reglas_de_operador.sh): qué
// pieza del modelo del mapa es cada objeto (por su nombre, tal como viene del
// .obj de Marcelo) y cómo se rotula en el plano del jugador 2.
public static class PruebaPlanoDelMapa
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static bool Es(string nombre, string tipo, string etiqueta = "")
    {
        PlanoDelMapa.Pieza pieza = PlanoDelMapa.Clasificar(nombre);
        return pieza.Tipo == tipo && pieza.Etiqueta == etiqueta;
    }

    public static int Main()
    {
        // --- Pisos: cada sala con su nombre ---
        Comprobar("el piso del comedor es una sala rotulada COMEDOR", Es("Piso_Comedor_Piso_Calido", "sala", "COMEDOR"));
        Comprobar("el del escenario, ESCENARIO", Es("Piso_Escenario_Piso_Calido", "sala", "ESCENARIO"));
        Comprobar("el del rincón de premios, RINCÓN DE PREMIOS", Es("Piso_Rincon_Premios_Piso_Calido", "sala", "RINCÓN DE PREMIOS"));
        Comprobar("la oficina es su propio tipo, rotulada OFICINA", Es("Piso_Oficina_Oficina_Amarillo", "oficina", "OFICINA"));
        Comprobar("el pasillo izquierdo es pasillo, rotulado PASILLO IZQ.", Es("Piso_Pasillo_Izquierdo_Piso_Pasillos", "pasillo", "PASILLO IZQ."));
        Comprobar("el derecho, PASILLO DER.", Es("Piso_Pasillo_Derecho_Piso_Pasillos", "pasillo", "PASILLO DER."));
        Comprobar("el principal, PASILLO", Es("Piso_Pasillo_Principal_Piso_Pasillos", "pasillo", "PASILLO"));
        Comprobar("el tramo horizontal de un ducto es ducto, rotulado VENTILACIÓN",
            Es("Piso_Ducto_Izquierdo_Horizontal_Piso_Ventilaciones", "ducto", "VENTILACIÓN"));
        Comprobar("la entrada de un ducto es ducto sin rótulo (no repetir el texto)",
            Es("Piso_Ducto_Derecho_Entrada_Piso_Ventilaciones", "ducto", ""));

        // --- Muros ---
        Comprobar("un muro de sala es muro", Es("Muro_Exterior_Oeste_Muros_Gris_Oscuro", "muro"));
        Comprobar("un muro de ventilación también es muro", Es("Vent_Izq_Muro_Sur_Muros_Gris_Oscuro", "muro"));
        Comprobar("el nombre del mesh de un muro (sufijo _Mesh) también", Es("Muro_Fondo_Oficina_Muro_Fondo_Oficina_Mesh", "muro"));

        // --- Muebles y referencias ---
        Comprobar("la plataforma del escenario es tarima", Es("Plataforma_Escenario_Escenario_Morado", "tarima"));
        Comprobar("el tablero de una mesa es mesa", Es("Mesa_03_Tablero_Mesas_Madera", "mesa"));
        Comprobar("la base de la mesa no (queda debajo del tablero)", Es("Mesa_03_Base_Sillas_Madera", ""));
        Comprobar("la caja de música es caja", Es("Caja_Musica_Puppet_Caja_Musica", "caja"));

        // --- Lo que no va en el plano ---
        Comprobar("los techos no van (taparían todo desde arriba)", Es("Techo_Cuerpo_Central_Techo_Exterior_Grafito", ""));
        Comprobar("las sillas no van (ruido visual)", Es("Silla_08_2_Sillas_Madera", ""));
        Comprobar("los focos no van", Es("Foco_Comedor_01_Foco_Carcasa_Metal", ""));
        Comprobar("la cortina no va", Es("Cortina_Escenario_Cortina_Roja", ""));
        Comprobar("un nombre vacío o null no falla", Es("", "") && Es(null, ""));
        Comprobar("mayúsculas y minúsculas no importan", Es("piso_comedor_x", "sala", "COMEDOR") && Es("MURO_X", "muro"));

        // --- Rectángulo a partir de una caja (lo que da Renderer.bounds) ---
        PlanoDelMapa.Rectangulo r = PlanoDelMapa.RectanguloDeCaja(10f, 22f, 30f, 29f, 0.004f);
        Comprobar("la caja se redondea a centímetros y conserva sus bordes",
            r.x0 == 10f && r.z0 == 22f && r.x1 == 30f && r.z1 == 29f);
        PlanoDelMapa.Rectangulo fino = PlanoDelMapa.RectanguloDeCaja(5f, 6f, 5.18f, 29f, 0f);
        Comprobar("un muro de 18 cm de grosor sigue teniendo grosor", fino.x1 - fino.x0 > 0.1f);
        PlanoDelMapa.Rectangulo invertido = PlanoDelMapa.RectanguloDeCaja(30f, 29f, 10f, 22f, 0f);
        Comprobar("si llega con los bordes al revés, se ordenan", invertido.x0 == 10f && invertido.z0 == 22f);

        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
