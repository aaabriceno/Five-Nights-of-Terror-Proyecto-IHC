// Sonidos de la sala del jugador 2 (2026-11-24): bips de consola hechos con
// Web Audio, sin archivos. Son la respuesta que el jugador 1 tiene en la
// tablet (vibra y suena): bip al confirmar una acción, dos bips al acertar,
// zumbido al fallar, alarma cuando Puppet sale y estática cuando un
// animatrónico llega a «señal perdida».
//
// Volumen bajo: los dos jugadores suelen estar en el mismo salón y no debe
// tapar el juego de la PC. Se apaga en Opciones («Sonidos de la sala»). Si la
// computadora no tiene sonido o el navegador no lo deja, no suena y no pasa
// nada más. En el navegador queda como window.Sonidos.
(function () {
  "use strict";

  const CLAVE = "sala.sonidos";
  const VOLUMEN = 0.12;
  const MAXIMO_HISTORIAL = 50;

  let contexto = null;
  let salida = null;
  let activo = leer();
  const historial = [];     // lo que se mandó a sonar con los sonidos activos (pruebas y depuración)

  function leer() {
    try { return localStorage.getItem(CLAVE) !== "no"; } catch (e) { return true; }
  }

  function guardar() {
    try { localStorage.setItem(CLAVE, activo ? "si" : "no"); } catch (e) { /* sin almacenamiento: solo esta visita */ }
  }

  // El navegador solo deja sonar audio tras un gesto: se prepara en el primer
  // clic o tecla (normalmente «Entrar a la sala») y se reanuda si se suspendió.
  function preparar() {
    if (contexto) {
      if (contexto.state === "suspended") contexto.resume().catch(() => {});
      return contexto;
    }
    const Clase = window.AudioContext || window.webkitAudioContext;
    if (!Clase) return null;
    try {
      contexto = new Clase();
      salida = contexto.createGain();
      salida.gain.value = VOLUMEN;
      salida.connect(contexto.destination);
    } catch (e) {
      contexto = null;
    }
    return contexto;
  }
  document.addEventListener("pointerdown", preparar, true);
  document.addEventListener("keydown", preparar, true);

  function tono(frecuencia, inicio, duracion, forma) {
    const t = contexto.currentTime + inicio;
    const oscilador = contexto.createOscillator();
    const envolvente = contexto.createGain();
    oscilador.type = forma;
    oscilador.frequency.setValueAtTime(frecuencia, t);
    envolvente.gain.setValueAtTime(0.0001, t);
    envolvente.gain.exponentialRampToValueAtTime(1, t + 0.01);
    envolvente.gain.exponentialRampToValueAtTime(0.0001, t + duracion);
    oscilador.connect(envolvente);
    envolvente.connect(salida);
    oscilador.start(t);
    oscilador.stop(t + duracion + 0.02);
  }

  function estatica(duracion) {
    const muestras = Math.floor(contexto.sampleRate * duracion);
    const buffer = contexto.createBuffer(1, muestras, contexto.sampleRate);
    const datos = buffer.getChannelData(0);
    for (let i = 0; i < muestras; i++) datos[i] = (Math.random() * 2 - 1) * (1 - i / muestras);
    const fuente = contexto.createBufferSource();
    fuente.buffer = buffer;
    const filtro = contexto.createBiquadFilter();
    filtro.type = "bandpass";
    filtro.frequency.value = 1800;
    fuente.connect(filtro);
    filtro.connect(salida);
    fuente.start();
  }

  const RECETAS = {
    accion: () => tono(880, 0, 0.08, "square"),
    acierto: () => { tono(660, 0, 0.09, "square"); tono(990, 0.1, 0.12, "square"); },
    error: () => tono(110, 0, 0.3, "sawtooth"),
    alarma: () => { for (let i = 0; i < 4; i++) tono(i % 2 ? 620 : 880, i * 0.18, 0.16, "square"); },
    estatica: () => estatica(0.35),
  };

  function sonar(nombre) {
    if (!activo || !RECETAS[nombre]) return;
    historial.push(nombre);
    if (historial.length > MAXIMO_HISTORIAL) historial.shift();
    const c = preparar();
    if (!c || c.state !== "running") return;      // sin sonido disponible: no suena y listo
    try { RECETAS[nombre](); } catch (e) { /* un navegador raro no rompe la sala */ }
  }

  window.Sonidos = {
    sonar,
    activo: () => activo,
    activar(si) { activo = !!si; guardar(); },
    historial: () => historial.slice(),
  };
})();
