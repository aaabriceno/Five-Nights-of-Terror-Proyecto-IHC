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

console.log("terminal: circuito lógico (lo que se ve mientras se mueven los interruptores)");
// (A Y B) O (NO C), con una puerta XOR al final: G1 = A Y B, G2 = NO C, G3 = G1 O G2, G4 = G3 XOR D
const circuito = {
  entradas: ["A", "B", "C", "D"],
  puertas: [
    { id: "G1", op: "AND", a: "A", b: "B" },
    { id: "G2", op: "NOT", a: "C", b: "" },
    { id: "G3", op: "OR", a: "G1", b: "G2" },
    { id: "G4", op: "XOR", a: "G3", b: "D" },
  ],
  salida: "G4",
  objetivo: true,
};
let c = Logica.evaluarCircuito(circuito, [1, 1, 1, 0]);
comprobar("A=1 B=1 C=1 D=0: G1=1, G2=0, G3=1 y la salida 1",
  c.valores.G1 === true && c.valores.G2 === false && c.valores.G3 === true && c.salida === true);
c = Logica.evaluarCircuito(circuito, [0, 0, 1, 0]);
comprobar("A=0 B=0 C=1 D=0: G1=0, G2=0, G3=0 y la salida 0", c.valores.G3 === false && c.salida === false);
c = Logica.evaluarCircuito(circuito, [0, 0, 1, 1]);
comprobar("el XOR con D=1 invierte la salida", c.salida === true);
comprobar("las entradas también salen en los valores", c.valores.A === false && c.valores.D === true);
// Solo A=1: G1=0, G2=NO 0=1, G3=1, G4=1 XOR 0=1.
comprobar("con interruptores de menos no falla (los que faltan valen 0)",
  Logica.evaluarCircuito(circuito, [1]).salida === true && Logica.evaluarCircuito(circuito, undefined).salida === true);
comprobar("el foco se enciende cuando la salida es el objetivo",
  Logica.circuitoResuelto(circuito, [0, 0, 1, 1]) === true && Logica.circuitoResuelto(circuito, [0, 0, 1, 0]) === false);
comprobar("con objetivo apagado se resuelve con la salida en 0",
  Logica.circuitoResuelto({ ...circuito, objetivo: false }, [0, 0, 1, 0]) === true);
comprobar("las puertas se leen en español",
  Logica.textoDePuerta(circuito.puertas[0]) === "G1 = A Y B" &&
  Logica.textoDePuerta(circuito.puertas[1]) === "G2 = NO C" &&
  Logica.textoDePuerta(circuito.puertas[2]) === "G3 = G1 O G2" &&
  Logica.textoDePuerta(circuito.puertas[3]) === "G4 = G3 O exclusivo D");

console.log("terminal: lo que se escribe en un cálculo");
comprobar("un número se lee tal cual", Logica.leerRespuesta("42") === 42);
comprobar("con espacios alrededor también", Logica.leerRespuesta("  7 ") === 7);
comprobar("ceros a la izquierda no importan", Logica.leerRespuesta("0042") === 42);
comprobar("vacío no es una respuesta", Logica.leerRespuesta("") === null && Logica.leerRespuesta("   ") === null);
comprobar("letras o símbolos no son una respuesta", Logica.leerRespuesta("4x") === null && Logica.leerRespuesta("1,5") === null && Logica.leerRespuesta("-3") === null);
comprobar("un número absurdo de largo se rechaza", Logica.leerRespuesta("12345678901") === null);

console.log("textoDeEfecto (lo que ve el jugador 2 de lo que él mismo hizo)");
const NOMBRES = { freddy: "Freddy", vixy: "Vixy", puppet: "Puppet" };
const t = (m) => Logica.textoDeEfecto(m, NOMBRES);
comprobar("retrasar: dice a quién y por cuánto, en segunda persona",
  t({ tipo: "ralentizado", animatronic: "freddy", segundos: 5 }) === "Retrasaste a Freddy por 5 s");
comprobar("sin tipo se toma como retraso (Unity viejo)",
  t({ animatronic: "vixy", segundos: 5 }) === "Retrasaste a Vixy por 5 s");
comprobar("trampa puesta", t({ tipo: "trampa_colocada" }) === "Trampa puesta");
comprobar("trampa disparada", t({ tipo: "trampa_disparada", animatronic: "vixy" }) === "Trampa: Vixy atrapado");
comprobar("rescate: cuenta la multa que paga el jugador 1",
  t({ tipo: "puppet_rescatado", multa_factor: 2 }) === "Rescataste a Puppet · la caja del jugador 1 se descarga ×2 más rápido");
comprobar("rescate sin multa en el mensaje (Unity viejo)", t({ tipo: "puppet_rescatado" }) === "Rescataste a Puppet");
comprobar("rescate con la duración nueva de la caja: lo dice en segundos",
  t({ tipo: "puppet_rescatado", multa_factor: 2, segundos_caja: 15 }) === "Rescataste a Puppet · la caja del jugador 1 ahora dura 15 s");
comprobar("y con medios segundos",
  t({ tipo: "puppet_rescatado", multa_factor: 4, segundos_caja: 7.5 }) === "Rescataste a Puppet · la caja del jugador 1 ahora dura 7,5 s");
comprobar("los segundos se escriben sin ceros de más",
  Logica.formatearSegundos(15) === "15" && Logica.formatearSegundos(7.5) === "7,5" && Logica.formatearSegundos(3.75) === "3,75");
comprobar("su propia conexión no se anuncia a sí mismo", t({ tipo: "operador_conectado" }) === "");
comprobar("ni su caída", t({ tipo: "operador_perdido" }) === "");
comprobar("un tipo nuevo usa el texto de Unity", t({ tipo: "algo", message: "Pasó algo" }) === "Pasó algo");
comprobar("un tipo nuevo sin texto no muestra nada", t({ tipo: "algo" }) === "");
comprobar("la multa se escribe sin decimales sobrantes", Logica.formatearMulta(2) === "×2" && Logica.formatearMulta(1.5) === "×1,5");

console.log("textoDeRuta (la guía de cada animatrónico para el jugador 2)");
comprobar("une los puntos con flechas", Logica.textoDeRuta(["P01", "P03", "P07"]) === "P01 → P03 → P07");
comprobar("una ruta vacía o ausente no dice nada", Logica.textoDeRuta([]) === "" && Logica.textoDeRuta(undefined) === "");

console.log("textoDeCamara (hacia dónde mira el jugador 1)");
comprobar("al frente dentro de ±5°", Logica.textoDeCamara(0) === "Al frente" && Logica.textoDeCamara(-4.9) === "Al frente");
comprobar("a la izquierda con los grados", Logica.textoDeCamara(-45.4) === "Izquierda 45°");
comprobar("a la derecha con los grados", Logica.textoDeCamara(74.6) === "Derecha 75°");
comprobar("sin dato no inventa", Logica.textoDeCamara(undefined) === "–" && Logica.textoDeCamara(null) === "–");


console.log("plano del edificio");
const plano = [
  { tipo: "sala", etiqueta: "COMEDOR", x0: 10, z0: 10, x1: 30, z1: 22 },
  { tipo: "muro", etiqueta: "", x0: 5, z0: 6, x1: 5.18, z1: 29 },
  { tipo: "muro", etiqueta: "", x0: 37.82, z0: 22, x1: 38, z1: 29 },
  { tipo: "muro", etiqueta: "", x0: 17, z0: 0, x1: 23, z1: 0.18 },
];
let L = Logica.limitesDelMapa([{ x: 20, z: 15 }], plano, 1);
comprobar("los límites abarcan todo el edificio, no solo los puntos de ruta",
  L.x0 === 4 && L.x1 === 39 && L.z0 === -1 && L.z1 === 30);
L = Logica.limitesDelMapa([{ x: 20, z: 15 }, { x: 0, z: 40 }], plano, 2);
comprobar("y también los puntos que quedan fuera del plano", L.x0 === -2 && L.z1 === 42);
comprobar("sin plano ni puntos no hay límites", Logica.limitesDelMapa([], [], 2) === null);
comprobar("sin plano usa solo los puntos (como antes)",
  JSON.stringify(Logica.limitesDelMapa([{ x: 1, z: 2 }, { x: 5, z: 8 }], undefined, 2)) ===
  JSON.stringify({ x0: -1, x1: 7, z0: 0, z1: 10 }));
const validas = Logica.piezasValidas([
  ...plano,
  { tipo: "muro", x0: "a", z0: 0, x1: 1, z1: 1 },
  { tipo: "muro", x0: 0, z0: 0, x1: Infinity, z1: 1 },
  null,
  { tipo: "<img src=x>", x0: 0, z0: 0, x1: 1, z1: 1 },
]);
comprobar("piezasValidas descarta números rotos, nulos y tipos desconocidos", validas.length === 4);
comprobar("con algo que no es lista devuelve una lista vacía", Logica.piezasValidas("x").length === 0);


console.log("ruta por zonas");
const planoZonas = [
  { tipo: "sala", etiqueta: "ESCENARIO", x0: 10, z0: 22, x1: 30, z1: 29 },
  { tipo: "sala", etiqueta: "COMEDOR", x0: 10, z0: 10, x1: 30, z1: 22 },
  { tipo: "pasillo", etiqueta: "PASILLO", x0: 18, z0: 6, x1: 22, z1: 10 },
  { tipo: "ducto", etiqueta: "VENTILACIÓN", x0: 6.5, z0: 2, x1: 17, z1: 4 },
  { tipo: "ducto", etiqueta: "", x0: 6.5, z0: 4, x1: 8.5, z1: 6 },
  { tipo: "muro", etiqueta: "", x0: 10, z0: 10, x1: 30, z1: 10.18 },
];
const puntosZonas = [
  { nombre: "P01", x: 16.5, z: 24.3 }, { nombre: "P03", x: 20, z: 19.5 }, { nombre: "P04", x: 20, z: 15.8 },
  { nombre: "P06", x: 20, z: 8 }, { nombre: "V01", x: 7.5, z: 5 }, { nombre: "V02", x: 7.5, z: 3 },
];
comprobar("la ruta se cuenta por zonas, sin repetir la misma seguida",
  Logica.rutaPorZonas(["P01", "P03", "P04", "P06"], puntosZonas, planoZonas) === "Escenario → Comedor → Pasillo");
comprobar("una entrada de ducto sin rótulo cuenta como ventilación",
  Logica.rutaPorZonas(["V01", "V02"], puntosZonas, planoZonas) === "Ventilación");
comprobar("sin plano vuelve a los nombres de los puntos",
  Logica.rutaPorZonas(["P01", "P03"], puntosZonas, []) === "P01 → P03");
comprobar("un punto fuera de toda zona se salta",
  Logica.rutaPorZonas(["P01", "X"], [...puntosZonas, { nombre: "X", x: 99, z: 99 }], planoZonas) === "Escenario");

console.log(fallos === 0 ? "\nTodas las pruebas pasaron." : `\n${fallos} prueba(s) fallaron.`);
process.exit(fallos ? 1 : 0);
