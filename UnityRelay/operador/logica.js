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

  return { suavizar, crearMedidor };
});
