using System;

/// <summary>
/// Los números del ambiente del juego: cuánta niebla y luz tiene cada noche,
/// cómo parpadea la oficina cuando Freddy se acerca, el balanceo de un
/// animatrónico al caminar, la estática de la pantalla y la respiración de la
/// cámara. Todo puro (sin UnityEngine) para probarlo con Mono; quien lo aplica
/// a la escena es AjusteDeAmbiente.
///
/// También guarda el poco estado que el controlador del juego le cuenta al
/// ambiente (noche, partida activa, caja en peligro, pedidos de estática).
/// </summary>
public static class AmbienteDelJuego
{
    public const int NocheMinima = 1;
    public const int NocheMaxima = 6;

    // --- Lo que el controlador del juego le cuenta al ambiente ---
    public static int Noche = 1;
    public static bool PartidaActiva;
    public static bool CajaEnPeligro;

    public struct ParametrosDeNoche
    {
        /// Densidad de la niebla exponencial (más alta, pasillos más cortos).
        public float DensidadDeNiebla;
        /// Multiplica la intensidad de las luces de la escena (1 = como están).
        public float FactorDeLuz;
        /// Probabilidad por segundo de un parpadeo suelto en cualquier luz.
        public float ProbabilidadDeParpadeo;
    }

    /// Cada noche es un poco más oscura, con más niebla y más parpadeo. La luz no
    /// baja de 0,65 para que el mapa siga viéndose.
    public static ParametrosDeNoche ParametrosDeLaNoche(int noche)
    {
        int n = Math.Max(NocheMinima, Math.Min(NocheMaxima, noche)) - 1;
        return new ParametrosDeNoche
        {
            DensidadDeNiebla = 0.09f + 0.012f * n,
            FactorDeLuz = Math.Max(0.6f, 1f - 0.07f * n),
            ProbabilidadDeParpadeo = 0.02f + 0.012f * n,
        };
    }

    // --- Estática en la pantalla (cuando el jugador 2 actúa o se pierde señal) ---
    private static float estaticaDesde = float.NegativeInfinity;
    private static float estaticaHasta = float.NegativeInfinity;

    /// Pide `duracion` segundos de estática desde `ahora`. Un pedido más corto
    /// que uno en curso no lo acorta.
    public static void PedirEstatica(float ahora, float duracion)
    {
        if (duracion <= 0f) return;
        if (ahora + duracion <= estaticaHasta) return;
        estaticaDesde = ahora;
        estaticaHasta = ahora + duracion;
    }

    public static void OlvidarEstatica()
    {
        estaticaDesde = float.NegativeInfinity;
        estaticaHasta = float.NegativeInfinity;
    }

    /// 1 recién pedida, baja en línea recta hasta 0 cuando termina; 0 si no hay.
    public static float NivelDeEstatica(float ahora)
    {
        if (ahora < estaticaDesde || ahora >= estaticaHasta) return 0f;
        float duracion = estaticaHasta - estaticaDesde;
        if (duracion <= 0f) return 0f;
        return Acotar((estaticaHasta - ahora) / duracion, 0f, 1f);
    }

    // --- Balanceo procedural: los modelos no tienen esqueleto ---
    public struct Balanceo
    {
        /// Cuánto sube el cuerpo (metros), nunca negativo.
        public float DesplazamientoY;
        /// Inclinación lateral (grados), alterna de lado con cada paso.
        public float BalanceoGrados;
        /// Inclinación adelante/atrás (grados).
        public float CabeceoGrados;
    }

    /// Velocidad a la que el balanceo llega a su amplitud máxima (m/s).
    public const float VelocidadDePasoCompleto = 1.2f;
    public const float CadenciaMaxima = 3f;

    /// Pasos por segundo según la velocidad (0 parado, nunca más de 3).
    public static float CadenciaDePasos(float velocidad) =>
        Acotar(1.6f * Math.Max(0f, velocidad), 0f, CadenciaMaxima);

    /// Balanceo a partir de la velocidad y el tiempo (para una velocidad fija).
    /// Para una velocidad que cambia, acumula la fase y usa BalanceoConFase.
    public static Balanceo CalcularBalanceo(float velocidad, float tiempo) =>
        BalanceoConFase(AmplitudDeBalanceo(velocidad), 2f * (float)Math.PI * CadenciaDePasos(velocidad) * tiempo);

    /// 0 parado, 1 a la velocidad de paso completo o más.
    public static float AmplitudDeBalanceo(float velocidad) =>
        Acotar(Math.Max(0f, velocidad) / VelocidadDePasoCompleto, 0f, 1f);

    /// `fase` en radianes: una vuelta completa son dos pasos (uno por lado).
    public static Balanceo BalanceoConFase(float amplitud, float fase)
    {
        float a = Acotar(amplitud, 0f, 1f);
        return new Balanceo
        {
            BalanceoGrados = 4.5f * a * (float)Math.Sin(fase),
            DesplazamientoY = 0.035f * a * (0.5f - 0.5f * (float)Math.Cos(2f * fase)),
            CabeceoGrados = 2f * a * (float)Math.Sin(2f * fase + 0.5f),
        };
    }

    /// Volumen de los pasos (0 parado, 1 a 1,5 m/s o más).
    public static float VolumenDePasos(float velocidad) =>
        Acotar(Math.Max(0f, velocidad) / 1.5f, 0f, 1f);

    // --- Cámara ---
    /// Respiración sutil: cabeceo de ±0,3° cada 4 s y balanceo de ±0,15° cada 6,3 s.
    public static (float cabeceo, float balanceo) RespiracionDeCamara(float tiempo) =>
        (0.3f * (float)Math.Sin(2.0 * Math.PI * tiempo / 4.0),
         0.15f * (float)Math.Sin(2.0 * Math.PI * tiempo / 6.3));

    // --- Luces que reaccionan ---
    /// Multiplicador de la luz de la oficina (0 a 1) según lo cerca que está
    /// Freddy: entera de lejos, parpadea desde 8 m y, a menos de 5 m, se apaga
    /// un tercio de segundo cada 3 s. `aleatorio` es un número 0-1 que cambia
    /// de cuadro en cuadro (lo pone quien llama).
    public static float LuzDeOficina(float distanciaDeFreddy, float aleatorio, float tiempo)
    {
        if (distanciaDeFreddy >= 8f) return 1f;
        float cercania = Acotar((8f - distanciaDeFreddy) / 5f, 0f, 1f);
        float temblor = 0.5f + 0.5f * (float)Math.Sin(37.0 * tiempo);
        float luz = 1f - 0.3f * temblor * Acotar(aleatorio, 0f, 1f) * cercania;
        if (distanciaDeFreddy < 5f && Fraccion(tiempo / 3f) < 0.12f) luz = 0f;
        return Acotar(luz, 0f, 1f);
    }

    /// Pulso de la luz roja de emergencia (caja de Puppet en peligro): 0,2 a 1, una vez por segundo.
    public static float PulsoDeEmergencia(float tiempo) =>
        0.2f + 0.8f * (0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * tiempo));

    /// La luz de alerta de un ducto titila (2 veces por segundo) cuando hay un
    /// animatrónico a menos de 1,5 veces su alcance.
    public static bool AlertaDeVentilacion(float distanciaMasCercana, float alcance, float tiempo) =>
        distanciaMasCercana <= alcance * 1.5f && Fraccion(tiempo * 2f) < 0.5f;

    // --- Ojos brillantes ---
    public struct Ojos
    {
        public float Altura;      // desde el piso del modelo
        public float Separacion;  // entre un ojo y el otro
        public float Adelante;    // desde el centro hacia el frente
    }

    /// Dónde poner dos puntos de luz como ojos en un modelo del que solo se
    /// conoce su caja (ancho, alto, profundidad): a la altura de la cara y
    /// apenas por delante de toda la caja, para que ninguna malla los tape.
    public static Ojos PosicionDeOjos(float ancho, float alto, float profundidad) => new Ojos
    {
        Altura = 0.82f * alto,
        Separacion = 0.22f * ancho,
        Adelante = 0.5f * profundidad + 0.03f,
    };

    private static float Acotar(float valor, float minimo, float maximo) =>
        valor < minimo ? minimo : (valor > maximo ? maximo : valor);

    private static float Fraccion(float valor) => valor - (float)Math.Floor(valor);
}
