using UnityEngine;

/// <summary>
/// Pasos metálicos posicionales de un animatrónico: una fuente de audio 3D
/// sobre el modelo, así el jugador 1 oye de qué lado viene (y gira la cámara a
/// tiempo). El sonido se genera en código (golpe sordo + chasquido metálico +
/// chirrido) para no depender de un archivo; el volumen y el ritmo siguen a la
/// velocidad del cuerpo (ver BalanceoDeAnimatronico).
/// </summary>
public sealed class SonidoDeAnimatronico : MonoBehaviour
{
    [Range(0f, 1f)] public float volumenMaximo = 0.7f;
    public float tono = 1f;

    private const int Muestreo = 22050;
    private static AudioClip pasos;

    private AudioSource fuente;
    private BalanceoDeAnimatronico balanceo;

    private void Start()
    {
        balanceo = GetComponent<BalanceoDeAnimatronico>();
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = pasos ?? (pasos = GenerarPasos());
        fuente.loop = true;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Logarithmic;
        fuente.minDistance = 2f;
        fuente.maxDistance = 30f;
        fuente.dopplerLevel = 0f;
        fuente.spread = 20f;
        fuente.volume = 0f;
        fuente.playOnAwake = false;
        fuente.Play();
    }

    private void Update()
    {
        if (fuente == null) return;
        float velocidad = balanceo != null ? balanceo.Velocidad : 0f;
        float volumen = AmbienteDelJuego.PartidaActiva ? AmbienteDelJuego.VolumenDePasos(velocidad) * volumenMaximo : 0f;
        fuente.volume = Mathf.MoveTowards(fuente.volume, volumen, 2f * Time.deltaTime);
        // El clip trae dos pasos por segundo: el tono lo ajusta a la cadencia real.
        float cadencia = AmbienteDelJuego.CadenciaDePasos(velocidad);
        if (cadencia > 0f) fuente.pitch = tono * cadencia / 2f;
    }

    /// Un segundo de audio con dos pasos (uno a los 0 s y otro a los 0,5 s).
    private static AudioClip GenerarPasos()
    {
        int total = Muestreo;
        var muestras = new float[total];
        var azar = new System.Random(7);
        foreach (int inicio in new[] { 0, Muestreo / 2 })
        {
            for (int i = 0; i < Muestreo / 2 && inicio + i < total; i++)
            {
                float t = i / (float)Muestreo;
                // Golpe sordo (55 Hz, se apaga en ~0,12 s).
                float golpe = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 28f) * 0.9f;
                // Chasquido metálico: ruido muy corto.
                float chasquido = (float)(azar.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 180f) * 0.5f;
                // Chirrido: 400 Hz que sube un poco y se apaga en ~0,25 s.
                float chirrido = Mathf.Sin(2f * Mathf.PI * (380f + 160f * t) * t) * Mathf.Exp(-t * 14f) * 0.12f;
                muestras[inicio + i] += golpe + chasquido + chirrido;
            }
        }
        for (int i = 0; i < total; i++) muestras[i] = Mathf.Clamp(muestras[i], -1f, 1f);
        AudioClip clip = AudioClip.Create("pasos_metalicos", total, 1, Muestreo, false);
        clip.SetData(muestras, 0);
        return clip;
    }
}
