// Pruebas de logica.js: node UnityRelay/operador/probar_logica.js
const assert = require("assert");
const Logica = require("./logica.js");

let fallos = 0;
function comprobar(descripcion, condicion) {
  console.log(`  ${condicion ? "ok   " : "FALLA"} ${descripcion}`);
  if (!condicion) fallos++;
}

console.log("suavizar");
const muestras = [{ t: 0, x: 0, z: 0 }, { t: 100, x: 10, z: 20 }];
let p = Logica.suavizar(muestras, 110);          // 110 - 60 = 50 ms: a mitad de camino
comprobar("interpola a mitad de camino", p && p.x === 5 && p.z === 10);
p = Logica.suavizar(muestras, 5000);
comprobar("no extrapola más allá de la última muestra", p && p.x === 10 && p.z === 20);
p = Logica.suavizar([{ t: 7, x: 3, z: 4 }], 1000);
comprobar("con una sola muestra la devuelve", p && p.x === 3 && p.z === 4);
comprobar("sin muestras devuelve null", Logica.suavizar([], 100) === null);
p = Logica.suavizar(muestras, 20);               // 20 - 60 < 0: antes de la primera
comprobar("antes de la primera muestra devuelve la primera", p && p.x === 0);

console.log("crearMedidor");
const medidor = Logica.crearMedidor(200);
for (let i = 1; i <= 100; i++) medidor.registrar(i);
let r = medidor.resumen();
comprobar("n = 100", r.n === 100);
comprobar("p50 cerca de 50", Math.abs(r.p50 - 50) <= 1);
comprobar("p95 cerca de 95", Math.abs(r.p95 - 95) <= 1);
comprobar("max = 100", r.max === 100);
comprobar("último = 100", r.ultimo === 100);
for (let i = 0; i < 250; i++) medidor.registrar(5);
comprobar("guarda como máximo 200 registros", medidor.resumen().n === 200);
comprobar("medidor vacío no falla", Logica.crearMedidor().resumen().n === 0);

console.log("mini-tarea (secuencia de símbolos)");
let n = 0;
const azar = () => [0.0, 0.3, 0.6, 0.99][n++ % 4];
const secuencia = Logica.generarSecuencia(4, azar);
comprobar("genera 4 símbolos del 0 al 3", secuencia.length === 4 && secuencia.every((v) => v >= 0 && v <= 3));
comprobar("con ese azar sale 0, 1, 2, 3", secuencia.join() === "0,1,2,3");
comprobar("una entrada parcial correcta sigue en curso", Logica.evaluarEntrada([0, 1, 2, 3], [0, 1]) === "en curso");
comprobar("la entrada completa y correcta termina bien", Logica.evaluarEntrada([0, 1, 2, 3], [0, 1, 2, 3]) === "completa");
comprobar("un símbolo equivocado falla", Logica.evaluarEntrada([0, 1, 2, 3], [0, 2]) === "mal");
comprobar("una entrada vacía sigue en curso", Logica.evaluarEntrada([0, 1, 2, 3], []) === "en curso");

console.log(fallos === 0 ? "\nTodas las pruebas pasaron." : `\n${fallos} prueba(s) fallaron.`);
process.exit(fallos ? 1 : 0);
