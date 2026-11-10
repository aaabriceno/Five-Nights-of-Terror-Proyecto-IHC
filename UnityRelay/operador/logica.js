// Lógica pura de la página del jugador 2: sin DOM ni red, para poder probarla
// con Node (probar_logica.js). En el navegador queda como window.Logica.
(function (raiz, fabrica) {
  if (typeof module === "object" && module.exports) module.exports = fabrica();
  else raiz.Logica = fabrica();
})(typeof self !== "undefined" ? self : this, function () {

  // Posición mostrada de un animatrónico. Se dibuja un poco "en el pasado"
  // (retrasoMs) para tener siempre dos muestras entre las que interpolar:
  // el movimiento sale continuo aunque lleguen 20 mensajes por segundo.
  // muestras: [{t, x, z}] ordenadas por t (milisegundos).
  function suavizar(muestras, ahoraMs, retrasoMs) {
    if (retrasoMs === undefined) retrasoMs = 60;
    if (!muestras || muestras.length === 0) return null;
    const objetivo = ahoraMs - retrasoMs;
    const primera = muestras[0];
    const ultima = muestras[muestras.length - 1];
    if (objetivo <= primera.t) return { x: primera.x, z: primera.z };
    if (objetivo >= ultima.t) return { x: ultima.x, z: ultima.z };
    for (let i = 1; i < muestras.length; i++) {
      const b = muestras[i];
      if (objetivo <= b.t) {
        const a = muestras[i - 1];
        const k = (objetivo - a.t) / (b.t - a.t);
        return { x: a.x + (b.x - a.x) * k, z: a.z + (b.z - a.z) * k };
      }
    }
    return { x: ultima.x, z: ultima.z };
  }

  // Guarda las últimas `maximo` latencias (ms) y resume: último, p50, p95, max.
  function crearMedidor(maximo) {
    if (maximo === undefined) maximo = 200;
    const valores = [];
    function percentil(ordenados, p) {
      const posicion = Math.ceil((p / 100) * ordenados.length) - 1;
      return ordenados[Math.max(0, Math.min(ordenados.length - 1, posicion))];
    }
    return {
      registrar(ms) {
        valores.push(ms);
        if (valores.length > maximo) valores.shift();
      },
      resumen() {
        if (valores.length === 0) return { n: 0, ultimo: 0, p50: 0, p95: 0, max: 0 };
        const ordenados = valores.slice().sort((a, b) => a - b);
        return {
          n: valores.length,
          ultimo: valores[valores.length - 1],
          p50: percentil(ordenados, 50),
          p95: percentil(ordenados, 95),
          max: ordenados[ordenados.length - 1],
        };
      },
    };
  }

  // Mini-tarea de recarga: repetir una secuencia de símbolos (0 a 3).
  function generarSecuencia(largo, aleatorio) {
    if (!aleatorio) aleatorio = Math.random;
    const secuencia = [];
    for (let i = 0; i < largo; i++) secuencia.push(Math.min(3, Math.floor(aleatorio() * 4)));
    return secuencia;
  }

  // "en curso" mientras lo tecleado coincida con el principio de la secuencia,
  // "completa" si coincide entera, "mal" al primer símbolo equivocado.
  function evaluarEntrada(secuencia, entrada) {
    for (let i = 0; i < entrada.length; i++) if (entrada[i] !== secuencia[i]) return "mal";
    return entrada.length === secuencia.length ? "completa" : "en curso";
  }

  // "×2", "×4", "×1,5": el factor de la multa sin decimales sobrantes.
  function formatearMulta(factor) {
    const n = Number(factor);
    return "×" + (Number.isInteger(n) ? String(n) : n.toFixed(1).replace(".", ","));
  }

  // "15", "7,5", "3,75": los segundos sin ceros de más.
  function formatearSegundos(segundos) {
    return String(Math.round(Number(segundos) * 100) / 100).replace(".", ",");
  }

  // Lo que el jugador 2 lee de un `efecto` de Unity, en segunda persona (el
  // mismo mensaje lo lee el jugador 1 en su tablet como "tu compañero..."). Las
  // palabras se arman aquí, con el tipo y los datos, no con el `message` de
  // Unity, que está escrito para el jugador 1. "" = no hay nada que decirle.
  function textoDeEfecto(m, nombres) {
    const nombre = (nombres && nombres[m.animatronic]) || "un animatrónico";
    switch (m.tipo || "ralentizado") {
      case "ralentizado":
        return "Retrasaste a " + nombre + (m.segundos > 0 ? " por " + Math.round(m.segundos) + " s" : "");
      case "trampa_colocada":
        return "Trampa puesta";
      case "trampa_disparada":
        return "Trampa: " + nombre + " atrapado";
      case "puppet_rescatado":
        if (m.segundos_caja > 0)
          return "Rescataste a Puppet · la caja del jugador 1 ahora dura " + formatearSegundos(m.segundos_caja) + " s";
        return "Rescataste a Puppet" + (m.multa_factor > 1
          ? " · la caja del jugador 1 se descarga " + formatearMulta(m.multa_factor) + " más rápido" : "");
      case "operador_conectado":
      case "operador_perdido":
        return "";
    }
    return m.message || "";
  }

  // La ruta de un animatrónico como texto: "P01 → P03 → P07".
  function textoDeRuta(puntos) {
    return Array.isArray(puntos) ? puntos.join(" → ") : "";
  }

  // Hacia dónde mira la cámara del jugador 1: 0 = al frente, negativo = izquierda.
  function textoDeCamara(yaw) {
    if (typeof yaw !== "number" || !isFinite(yaw)) return "–";
    if (Math.abs(yaw) < 5) return "Al frente";
    return (yaw < 0 ? "Izquierda " : "Derecha ") + Math.round(Math.abs(yaw)) + "°";
  }

  return { suavizar, crearMedidor, generarSecuencia, evaluarEntrada, textoDeEfecto, formatearMulta, textoDeRuta, textoDeCamara, formatearSegundos };
});
