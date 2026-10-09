using UnityEngine;

public class HeadOrientationTracker : MonoBehaviour
{
    [Header("Configuración de Calibración")]
    [Tooltip("Si el ratio supera este número, asume que miras a la tablet. Ajustar según pruebas.")]
    public float umbralRatio = 1.3f;

    [Header("Estado Actual (Lectura)")]
    public float ratioActual;
    public bool mirandoPC = false; // <-- Cambiado: Por defecto inicia en false

    // Referencia al contenedor de los puntos verdes
    private Transform contenedorPuntos;
    private UnityGameSessionController controladorTutorial;

    void Update()
    {
        CalcularProporcionFacial();
    }

    private void CalcularProporcionFacial()
    {
        // 1. Buscar el objeto que tiene los 468 puntos faciales como hijos
        if (contenedorPuntos == null)
        {
            mirandoPC = false; // Si no hay contenedor, es false automáticamente

            Transform[] todosLosObjetos = FindObjectsOfType<Transform>();
            foreach (Transform t in todosLosObjetos)
            {
                // MediaPipe dibuja exactamente 468 puntos para la cara (o 478 si incluye pupilas)
                if (t.childCount == 468 || t.childCount == 478)
                {
                    contenedorPuntos = t;
                    break;
                }
            }
        }

        // 2. Calcular distancias
        if (contenedorPuntos != null)
        {
            // NUEVO: Verificamos si MediaPipe ocultó la malla al no encontrar un rostro en la cámara
            if (!contenedorPuntos.gameObject.activeInHierarchy || !contenedorPuntos.GetChild(0).gameObject.activeInHierarchy)
            {
                mirandoPC = false;
                return; // Salimos de la función sin calcular nada
            }

            try
            {
                // Extraer posiciones de los puntos clave
                // 10 = Frente, 1 = Punta de la Nariz, 152 = Base del Mentón
                Vector3 frente = contenedorPuntos.GetChild(10).position;
                Vector3 nariz = contenedorPuntos.GetChild(1).position;
                Vector3 menton = contenedorPuntos.GetChild(152).position;

                float distanciaFrenteNariz = Vector3.Distance(frente, nariz);
                float distanciaNarizMenton = Vector3.Distance(nariz, menton);

                // Evitar división por cero si se pierde el rastreo
                if (distanciaNarizMenton > 0.01f)
                {
                    // Calculamos la proporción
                    ratioActual = distanciaFrenteNariz / distanciaNarizMenton;

                    // Si la frente se alarga mucho respecto al mentón, estás mirando abajo
                    mirandoPC = (ratioActual < umbralRatio);
                }
            }
            catch
            {
                // NUEVO: Si hay un error de lectura por un corte de cámara, se marca como false
                mirandoPC = false;
            }
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (controladorTutorial == null)
            controladorTutorial = FindObjectOfType<UnityGameSessionController>();
        if (controladorTutorial != null && controladorTutorial.EnTutorial) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 25;
        style.normal.textColor = mirandoPC ? Color.green : Color.red;

        // NUEVO: Muestra un mensaje claro si la cara no está en pantalla
        if (contenedorPuntos == null || !contenedorPuntos.gameObject.activeInHierarchy || !contenedorPuntos.GetChild(0).gameObject.activeInHierarchy)
        {
            GUI.Label(new Rect(20, 20, 800, 50), "mirandoPC: False", style);
        }
        else
        {
            GUI.Label(new Rect(20, 20, 800, 50), $"mirandoPC: {mirandoPC} (Ratio: {ratioActual:F2})", style);
        }
    }
}
