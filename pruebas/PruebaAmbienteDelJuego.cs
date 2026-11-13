using System;

// Pruebas de AmbienteDelJuego fuera de Unity (ver probar_reglas_de_operador.sh):
// los números del ambiente (niebla por noche, parpadeos, balanceo, estática,
// respiración de la cámara) sin UnityEngine.
public static class PruebaAmbienteDelJuego
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    private static bool Cerca(float a, float b, float tolerancia = 1e-4f) => Math.Abs(a - b) <= tolerancia;

    public static int Main()
    {
        // --- Cada noche más oscura ---
        AmbienteDelJuego.ParametrosDeNoche n1 = AmbienteDelJuego.ParametrosDeLaNoche(1);
        AmbienteDelJuego.ParametrosDeNoche n6 = AmbienteDelJuego.ParametrosDeLaNoche(6);
        Comprobar("la noche 1 tiene luz entera (factor 1)", Cerca(n1.FactorDeLuz, 1f));
        Comprobar("la noche 6 tiene menos luz que la 1 pero no menos de 0,6",
            n6.FactorDeLuz < n1.FactorDeLuz && n6.FactorDeLuz >= 0.6f);
        Comprobar("la niebla crece con la noche", n6.DensidadDeNiebla > n1.DensidadDeNiebla);
        Comprobar("el parpadeo es más probable en la noche 6", n6.ProbabilidadDeParpadeo > n1.ProbabilidadDeParpadeo);
        Comprobar("una noche fuera de rango se trata como la 1 o la 6",
            Cerca(AmbienteDelJuego.ParametrosDeLaNoche(0).FactorDeLuz, n1.FactorDeLuz)
            && Cerca(AmbienteDelJuego.ParametrosDeLaNoche(9).FactorDeLuz, n6.FactorDeLuz));
        bool monotona = true;
        for (int noche = 1; noche < 6; noche++)
            if (AmbienteDelJuego.ParametrosDeLaNoche(noche + 1).FactorDeLuz > AmbienteDelJuego.ParametrosDeLaNoche(noche).FactorDeLuz) monotona = false;
        Comprobar("la luz nunca sube de una noche a la siguiente", monotona);

        // --- Estática en pantalla: baja de 1 a 0 mientras dura y después no hay ---
        AmbienteDelJuego.OlvidarEstatica();
        Comprobar("sin pedirla, la estática es 0", Cerca(AmbienteDelJuego.NivelDeEstatica(10f), 0f));
        AmbienteDelJuego.PedirEstatica(10f, 0.5f);
        Comprobar("recién pedida vale 1", Cerca(AmbienteDelJuego.NivelDeEstatica(10f), 1f));
        Comprobar("a la mitad vale 0,5", Cerca(AmbienteDelJuego.NivelDeEstatica(10.25f), 0.5f));
        Comprobar("terminada vale 0", Cerca(AmbienteDelJuego.NivelDeEstatica(10.6f), 0f));
        AmbienteDelJuego.PedirEstatica(10f, 1f);
        AmbienteDelJuego.PedirEstatica(10.2f, 0.3f);
        Comprobar("un pedido más corto no acorta uno más largo en curso", Cerca(AmbienteDelJuego.NivelDeEstatica(10.8f), 0.2f, 1e-3f));
        Comprobar("nunca pasa de 1 ni baja de 0",
            AmbienteDelJuego.NivelDeEstatica(9f) == 0f && AmbienteDelJuego.NivelDeEstatica(10f) <= 1f);

        // --- Balanceo procedural al caminar ---
        AmbienteDelJuego.Balanceo quieto = AmbienteDelJuego.CalcularBalanceo(0f, 3.7f);
        Comprobar("quieto no se balancea ni cabecea ni sube",
            Cerca(quieto.DesplazamientoY, 0f) && Cerca(quieto.BalanceoGrados, 0f) && Cerca(quieto.CabeceoGrados, 0f));
        float maximoBalanceo = 0f, maximoSubida = 0f, minimoSubida = 1f;
        for (float t = 0f; t < 4f; t += 0.01f)
        {
            AmbienteDelJuego.Balanceo b = AmbienteDelJuego.CalcularBalanceo(1.2f, t);
            maximoBalanceo = Math.Max(maximoBalanceo, Math.Abs(b.BalanceoGrados));
            maximoSubida = Math.Max(maximoSubida, b.DesplazamientoY);
            minimoSubida = Math.Min(minimoSubida, b.DesplazamientoY);
        }
        Comprobar("caminando a 1,2 m/s se balancea entre 2 y 6 grados", maximoBalanceo >= 2f && maximoBalanceo <= 6f);
        Comprobar("y sube hasta 2-5 cm sin hundirse en el piso", maximoSubida >= 0.02f && maximoSubida <= 0.05f && minimoSubida >= 0f);
        AmbienteDelJuego.Balanceo lento = AmbienteDelJuego.CalcularBalanceo(0.3f, 0.37f);
        AmbienteDelJuego.Balanceo rapido = AmbienteDelJuego.CalcularBalanceo(2f, 0.37f);
        Comprobar("más velocidad, más cadencia de pasos", AmbienteDelJuego.CadenciaDePasos(2f) > AmbienteDelJuego.CadenciaDePasos(0.3f));
        Comprobar("la cadencia parada es 0 y nunca pasa de 3 pasos por segundo",
            Cerca(AmbienteDelJuego.CadenciaDePasos(0f), 0f) && AmbienteDelJuego.CadenciaDePasos(50f) <= 3f);
        Comprobar("el volumen de pasos es 0 parado y sube con la velocidad hasta 1",
            Cerca(AmbienteDelJuego.VolumenDePasos(0f), 0f) && AmbienteDelJuego.VolumenDePasos(0.5f) > 0f
            && AmbienteDelJuego.VolumenDePasos(0.5f) < AmbienteDelJuego.VolumenDePasos(1.5f) && AmbienteDelJuego.VolumenDePasos(9f) <= 1f);
        Comprobar("el balanceo es continuo (sin saltos entre cuadros)",
            Math.Abs(AmbienteDelJuego.CalcularBalanceo(1f, 1.000f).BalanceoGrados - AmbienteDelJuego.CalcularBalanceo(1f, 1.016f).BalanceoGrados) < 1f);
        Comprobar("lento y rápido no son lo mismo", !Cerca(lento.BalanceoGrados, rapido.BalanceoGrados));

        // --- Respiración de la cámara: sutil y continua ---
        float maximoCabeceo = 0f;
        for (float t = 0f; t < 8f; t += 0.02f)
        {
            (float cabeceo, float balanceo) = AmbienteDelJuego.RespiracionDeCamara(t);
            maximoCabeceo = Math.Max(maximoCabeceo, Math.Max(Math.Abs(cabeceo), Math.Abs(balanceo)));
        }
        Comprobar("la cámara respira menos de medio grado", maximoCabeceo > 0.05f && maximoCabeceo <= 0.5f);

        // --- Luz de la oficina según lo cerca que está Freddy ---
        Comprobar("con Freddy lejos (20 m) la oficina está entera", Cerca(AmbienteDelJuego.LuzDeOficina(20f, 0.5f, 3f), 1f));
        bool parpadea = false;
        for (float t = 0f; t < 3f; t += 0.05f)
            if (AmbienteDelJuego.LuzDeOficina(6f, 0.9f, t) < 0.95f) parpadea = true;
        Comprobar("con Freddy a 6 m parpadea en algún momento", parpadea);
        bool seApaga = false, vuelve = false;
        for (float t = 0f; t < 6f; t += 0.05f)
        {
            float luz = AmbienteDelJuego.LuzDeOficina(3f, 0.1f, t);
            if (luz <= 0.05f) seApaga = true;
            if (luz >= 0.5f) vuelve = true;
        }
        Comprobar("con Freddy a 3 m la luz se apaga a ratos y vuelve", seApaga && vuelve);
        Comprobar("la luz nunca es negativa ni mayor que 1",
            AmbienteDelJuego.LuzDeOficina(3f, 1f, 1.3f) >= 0f && AmbienteDelJuego.LuzDeOficina(3f, 0f, 1.3f) <= 1f);

        // --- Pulso rojo de emergencia y alerta de ventilación ---
        float minimoPulso = 1f, maximoPulso = 0f;
        for (float t = 0f; t < 2f; t += 0.01f)
        {
            float p = AmbienteDelJuego.PulsoDeEmergencia(t);
            minimoPulso = Math.Min(minimoPulso, p);
            maximoPulso = Math.Max(maximoPulso, p);
        }
        Comprobar("el pulso de emergencia va de ~0,2 a 1", minimoPulso <= 0.25f && maximoPulso >= 0.95f);
        Comprobar("la alerta de ventilación está apagada con el animatrónico lejos",
            !AmbienteDelJuego.AlertaDeVentilacion(30f, 6f, 0.1f));
        bool encendida = false, apagada = false;
        for (float t = 0f; t < 1f; t += 0.02f)
        {
            if (AmbienteDelJuego.AlertaDeVentilacion(4f, 6f, t)) encendida = true; else apagada = true;
        }
        Comprobar("cerca, la alerta titila (hay instantes encendida y apagada en 1 s)", encendida && apagada);

        // --- Ojos: dos puntos a la altura de la cara, al frente ---
        AmbienteDelJuego.Ojos ojos = AmbienteDelJuego.PosicionDeOjos(ancho: 0.8f, alto: 2f, profundidad: 0.6f);
        Comprobar("los ojos quedan en el 78-86 % de la altura", ojos.Altura >= 1.56f && ojos.Altura <= 1.72f);
        Comprobar("separados entre sí menos que el ancho del cuerpo", ojos.Separacion > 0f && ojos.Separacion < 0.8f);
        Comprobar("y por delante de toda la caja del modelo (ninguna malla los tapa)",
            ojos.Adelante > 0.5f * 0.6f && ojos.Adelante < 0.5f * 0.6f + 0.1f);

        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
