// Práctica del «Cómo jugar» del jugador 2 (2026-11-20): una sala de control de
// mentira donde se prueba Retrasar, la trampa y la terminal sin el juego. Es
// lógica pura, sin DOM ni red (probar_practica.js la prueba con Node); el
// tiempo entra como parámetro, en segundos. En el navegador queda como
// window.Practica. Los valores son los de Unity, para que lo practicado sea
// lo mismo que pasa en la partida.
(function (raiz, fabrica) {
  if (typeof module === "object" && module.exports) module.exports = fabrica();
  else raiz.Practica = fabrica();
})(typeof self !== "undefined" ? self : this, function () {

  const VALORES = {
    retrasoS: 8,          // FrenoDeOperador.DuracionSegundos
    factorLento: 0.5,     // FrenoDeOperador.FactorRalentizado
    enfriamientoS: 20,    // FrenoDeOperador.EnfriamientoSegundos (desde que se usa)
    trampaS: 6,           // RecursosDeOperador.SegundosDeTrampa
    bloqueoS: 3,          // TareasDeOperador.BloqueoPorErrorSegundos
    maxCargas: 3,
    velocidad: 9,         // unidades del mini mapa por segundo (una vuelta dura ~13 s)
  };

  // El mini mapa mide 100 × 64. Freddy sale del escenario, cruza el comedor y
  // baja por el pasillo izquierdo hasta la oficina; el pasillo derecho queda
  // fuera de su camino (sirve para aprender a mirar por dónde viene).
  const RUTA_FREDDY = [
    { id: "escenario", x: 50, y: 8 },
    { id: "comedor", x: 50, y: 29 },
    { id: "pasillo-izq", x: 14, y: 29 },
    { id: "pasillo-izq-bajo", x: 14, y: 53 },
    { id: "puerta", x: 38, y: 53 },
    { id: "oficina", x: 50, y: 56 },
  ];

  const PUNTOS = [
    { id: "comedor", nombre: "Comedor", x: 50, y: 29, enRuta: true },
    { id: "pasillo-izq", nombre: "Pasillo izquierdo", x: 14, y: 29, enRuta: true },
    { id: "pasillo-izq-bajo", nombre: "Pasillo izquierdo, abajo", x: 14, y: 53, enRuta: true },
    { id: "pasillo-der", nombre: "Pasillo derecho", x: 86, y: 29, enRuta: false },
    { id: "pasillo-der-bajo", nombre: "Pasillo derecho, abajo", x: 86, y: 53, enRuta: false },
  ];

  const PASOS = [
    { id: "conocer", texto: "Conociste a Freddy, Vixy y Puppet", diapositiva: 2 },
    { id: "panel", texto: "Leíste el panel del jugador 1", diapositiva: 3 },
    { id: "retrasar", texto: "Retrasaste a Freddy", diapositiva: 4 },
    { id: "trampa", texto: "Atrapaste a Freddy con una trampa", diapositiva: 5 },
    { id: "calculo", texto: "Resolviste un cálculo (recarga)", diapositiva: 6 },
    { id: "ecuacion", texto: "Resolviste una ecuación (rescate)", diapositiva: 6 },
    { id: "rescate", texto: "Rescataste a Puppet", diapositiva: 6 },
  ];

  const PREGUNTAS_PANEL = [
    { fila: "mirada", texto: "Toca la fila que dice si el jugador 1 está mirando la pantalla." },
    { fila: "caja", texto: "Ahora toca la que dice cuánta cuerda le queda a la caja de Puppet." },
  ];

  const ANIMATRONICOS = ["freddy", "vixy", "puppet"];

  // Distancia acumulada desde el escenario hasta cada punto de la ruta.
  const TRAMOS = [0];
  for (let i = 1; i < RUTA_FREDDY.length; i++) {
    const a = RUTA_FREDDY[i - 1], b = RUTA_FREDDY[i];
    TRAMOS.push(TRAMOS[i - 1] + Math.hypot(b.x - a.x, b.y - a.y));
  }
  const LARGO_RUTA = TRAMOS[TRAMOS.length - 1];
  const distanciaEnRuta = (id) => TRAMOS[RUTA_FREDDY.findIndex((p) => p.id === id)];

  function crear() {
    return {
      avance: 0,                 // cuánto camino lleva Freddy desde el escenario
      ultimoS: null,
      retrasoHasta: -Infinity,
      esperaHasta: -Infinity,
      atrapadoHasta: -Infinity,
      trampa: null,              // id del punto con la trampa puesta
      cargas: 1,
      bloqueadoHasta: -Infinity,
      rescateListo: false,
      conocidos: {},
      pregunta: 0,               // índice en PREGUNTAS_PANEL
      hechos: {},
    };
  }

  function marcar(estado, id) {
    if (PASOS.some((p) => p.id === id)) estado.hechos[id] = true;
  }

  function pendientes(estado) {
    return PASOS.filter((p) => !estado.hechos[p.id]).map((p) => p.id);
  }

  function avanzar(estado, ahoraS) {
    const dt = estado.ultimoS === null ? 0 : Math.min(0.25, Math.max(0, ahoraS - estado.ultimoS));
    estado.ultimoS = ahoraS;
    if (ahoraS < estado.atrapadoHasta) return;
    const velocidad = VALORES.velocidad * (ahoraS < estado.retrasoHasta ? VALORES.factorLento : 1);
    const antes = estado.avance;
    let despues = antes + velocidad * dt;
    if (estado.trampa) {
      const enTrampa = distanciaEnRuta(estado.trampa);
      if (enTrampa > antes && enTrampa <= despues) {
        estado.avance = enTrampa;
        estado.atrapadoHasta = ahoraS + VALORES.trampaS;
        estado.trampa = null;
        marcar(estado, "trampa");
        return;
      }
    }
    if (despues >= LARGO_RUTA) despues = 0;      // llegó a la oficina: vuelve a salir del escenario
    estado.avance = despues;
  }

  function posicion(estado) {
    for (let i = 1; i < RUTA_FREDDY.length; i++) {
      if (estado.avance <= TRAMOS[i]) {
        const a = RUTA_FREDDY[i - 1], b = RUTA_FREDDY[i];
        const k = (estado.avance - TRAMOS[i - 1]) / (TRAMOS[i] - TRAMOS[i - 1]);
        return { x: a.x + (b.x - a.x) * k, y: a.y + (b.y - a.y) * k };
      }
    }
    const ultimo = RUTA_FREDDY[RUTA_FREDDY.length - 1];
    return { x: ultimo.x, y: ultimo.y };
  }

  function freddy(estado, ahoraS) {
    const atrapado = ahoraS < estado.atrapadoHasta;
    return {
      atrapado,
      lento: !atrapado && ahoraS < estado.retrasoHasta,
      retrasoRestante: Math.max(0, estado.retrasoHasta - ahoraS),
      esperaRestante: Math.max(0, estado.esperaHasta - ahoraS),
    };
  }

  function retrasar(estado, ahoraS) {
    const espera = Math.max(0, estado.esperaHasta - ahoraS);
    if (espera > 0) return { ok: false, esperaRestante: espera };
    estado.retrasoHasta = ahoraS + VALORES.retrasoS;
    estado.esperaHasta = ahoraS + VALORES.enfriamientoS;
    marcar(estado, "retrasar");
    return { ok: true, esperaRestante: VALORES.enfriamientoS };
  }

  function ponerTrampa(estado, idPunto) {
    const punto = PUNTOS.find((p) => p.id === idPunto);
    if (!punto) return { ok: false, motivo: "desconocido" };
    if (estado.cargas <= 0) return { ok: false, motivo: "sin_cargas" };
    if (!punto.enRuta) return { ok: false, motivo: "fuera_de_ruta" };
    estado.trampa = punto.id;
    estado.cargas -= 1;
    return { ok: true };
  }

  // Como TareasDeOperador.cs: «Calcula a × b» con factores de 6 a 12 y
  // «Encuentra x: ax + b = c» con a de 2 a 4, x de 2 a 10 y b de 2 a 12.
  function crearTarea(tipo, azar) {
    const entre = (min, max) => min + Math.floor((azar || Math.random)() * (max - min + 1));
    if (tipo === "ecuacion") {
      const a = entre(2, 4), x = entre(2, 10), b = entre(2, 12);
      return { tipo, enunciado: `Encuentra x: ${a}x + ${b} = ${a * x + b}`, respuesta: x };
    }
    const a = entre(6, 12), b = entre(6, 12);
    return { tipo: "calculo", enunciado: `Calcula ${a} × ${b}`, respuesta: a * b };
  }

  function bloqueoRestante(estado, ahoraS) {
    return Math.max(0, estado.bloqueadoHasta - ahoraS);
  }

  function responder(estado, tarea, valor, ahoraS) {
    const restante = bloqueoRestante(estado, ahoraS);
    if (restante > 0) return { correcto: false, bloqueado: true, bloqueoS: restante };
    if (valor !== tarea.respuesta) {
      estado.bloqueadoHasta = ahoraS + VALORES.bloqueoS;
      return { correcto: false, bloqueoS: VALORES.bloqueoS };
    }
    if (tarea.tipo === "calculo") estado.cargas = Math.min(VALORES.maxCargas, estado.cargas + 1);
    else estado.rescateListo = true;
    marcar(estado, tarea.tipo);
    return { correcto: true, bloqueoS: 0 };
  }

  function rescatar(estado) {
    if (!estado.rescateListo) return false;
    marcar(estado, "rescate");
    return true;
  }

  // Devuelve cuántos de los tres conoce ya.
  function conocer(estado, nombre) {
    if (ANIMATRONICOS.includes(nombre)) estado.conocidos[nombre] = true;
    const cuantos = ANIMATRONICOS.filter((n) => estado.conocidos[n]).length;
    if (cuantos === ANIMATRONICOS.length) marcar(estado, "conocer");
    return cuantos;
  }

  function preguntaDelPanel(estado) {
    return PREGUNTAS_PANEL[estado.pregunta] || null;
  }

  function tocarFila(estado, fila) {
    const pregunta = preguntaDelPanel(estado);
    if (!pregunta) return { correcto: false, terminado: true };
    if (pregunta.fila !== fila) return { correcto: false, terminado: false };
    estado.pregunta++;
    const terminado = estado.pregunta >= PREGUNTAS_PANEL.length;
    if (terminado) marcar(estado, "panel");
    return { correcto: true, terminado };
  }

  return {
    VALORES, RUTA_FREDDY, PUNTOS, PASOS, crear, avanzar, posicion, freddy, retrasar, ponerTrampa,
    crearTarea, responder, bloqueoRestante, rescatar, conocer, preguntaDelPanel, tocarFila, marcar, pendientes,
  };
});
