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

console.log("terminal: título y para qué sirve cada tarea");
// El jugador 2 tiene que saber por qué resuelve cada cosa, no solo qué resolver.
const deCalculo = Logica.textoDeTarea("calculo"), deEcuacion = Logica.textoDeTarea("ecuacion");
comprobar("el cálculo es la recarga de trampa", deCalculo.titulo === "Terminal · recarga de trampa");
comprobar("y dice que da 1 carga de trampa", deCalculo.porque === "Resuélvelo y ganas 1 carga de trampa.");
comprobar("la ecuación es el rescate de Puppet", deEcuacion.titulo === "Terminal · rescate de Puppet");
comprobar("y dice que reinicia la caja de música y salva al jugador 1",
  deEcuacion.porque === "Resuélvela para reiniciar la caja de música de Puppet: así salvas al jugador 1.");
comprobar("un tipo desconocido no inventa nada", Logica.textoDeTarea("circuito").titulo === "Terminal" &&
  Logica.textoDeTarea("circuito").porque === "");

console.log("ductos: copia del semáforo del jugador 1 y sus compuertas");
// El color va también en palabras: no se depende solo del color (WCAG 1.4.1).
comprobar("rojo y cerrada", Logica.textoDeDucto("rojo", true) === "Rojo · compuerta cerrada");
comprobar("amarillo y abierta", Logica.textoDeDucto("amarillo", false) === "Amarillo · abierta");
comprobar("verde y abierta", Logica.textoDeDucto("verde", false) === "Verde · abierta");
comprobar("una luz desconocida no inventa nada", Logica.textoDeDucto("azul", false) === "–" && Logica.textoDeDucto(undefined, true) === "–");

console.log("recargar: el cálculo da carga y batería");
comprobar("con cargas y batería llenas no hace falta", Logica.puedeRecargar({ cargas: 3, max_cargas: 3 }, 100) === false);
comprobar("con cargas llenas pero batería gastada sí", Logica.puedeRecargar({ cargas: 3, max_cargas: 3 }, 80) === true);
comprobar("con cargas por llenar sí", Logica.puedeRecargar({ cargas: 1, max_cargas: 3 }, 100) === true);
comprobar("sin dato de batería (Unity viejo) manda solo las cargas",
  Logica.puedeRecargar({ cargas: 3, max_cargas: 3 }, undefined) === false && Logica.puedeRecargar({ cargas: 2, max_cargas: 3 }, null) === true);
comprobar("sin estado de recursos todavía no se bloquea", Logica.puedeRecargar(null, 50) === true);

console.log("cupo de cálculos por noche (2026-11-22)");
comprobar("sin cálculos esta noche no se puede, aunque falte batería y cargas",
  Logica.puedeRecargar({ cargas: 0, max_cargas: 3, calculos_restantes: 0, calculos_de_la_noche: 6 }, 10) === false);
comprobar("con cupo y algo por llenar sí",
  Logica.puedeRecargar({ cargas: 0, max_cargas: 3, calculos_restantes: 2, calculos_de_la_noche: 6 }, 10) === true);
comprobar("un Unity sin el campo no bloquea (como antes)",
  Logica.puedeRecargar({ cargas: 1, max_cargas: 3 }, 50) === true);
comprobar("el cupo se lee «4 / 6»",
  Logica.textoDeCalculos({ calculos_restantes: 4, calculos_de_la_noche: 6 }) === "4 / 6");
comprobar("sin dato de cupo se lee «–», nunca «undefined»",
  Logica.textoDeCalculos({ cargas: 1 }) === "–" && Logica.textoDeCalculos(null) === "–");
comprobar("quedanCalculos: 0 es false, sin dato es true",
  Logica.quedanCalculos({ calculos_restantes: 0 }) === false && Logica.quedanCalculos({}) === true &&
  Logica.quedanCalculos(null) === true && Logica.quedanCalculos({ calculos_restantes: 3 }) === true);

comprobar("el aviso dice lo que se ganó: las dos cosas",
  Logica.textoDeRecarga({ cargas: 1, max_cargas: 3 }, 60) === "Correcto: +1 carga de trampa y +20 % de batería");
comprobar("solo batería si las cargas ya estaban llenas",
  Logica.textoDeRecarga({ cargas: 3, max_cargas: 3 }, 60) === "Correcto: +20 % de batería");
comprobar("solo la carga si la batería estaba llena o no hay dato",
  Logica.textoDeRecarga({ cargas: 1, max_cargas: 3 }, 100) === "Correcto: +1 carga de trampa" &&
  Logica.textoDeRecarga({ cargas: 1, max_cargas: 3 }, undefined) === "Correcto: +1 carga de trampa");

console.log("terminal: lo que se escribe como respuesta");
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
comprobar("batería agotada: le dice al jugador 2 que puede recargarla con un cálculo",
  t({ tipo: "bateria_agotada", message: "Sin batería: se abrieron las compuertas y se apagó la linterna" })
    === "El jugador 1 se quedó sin batería: resuelve un cálculo para recargarla (+20 %)");
comprobar("un tipo nuevo usa el texto de Unity", t({ tipo: "algo", message: "Pasó algo" }) === "Pasó algo");
comprobar("un tipo nuevo sin texto no muestra nada", t({ tipo: "algo" }) === "");
comprobar("la multa se escribe sin decimales sobrantes", Logica.formatearMulta(2) === "×2" && Logica.formatearMulta(1.5) === "×1,5");

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

console.log("archivoDeSusto");
comprobar("Freddy → jumpscare_freddy.webm", Logica.archivoDeSusto("Freddy") === "jumpscare_freddy.webm");
comprobar("« vixy » (espacios) → jumpscare_vixy.webm", Logica.archivoDeSusto(" vixy ") === "jumpscare_vixy.webm");
comprobar("PUPPET → jumpscare_puppet.webm", Logica.archivoDeSusto("PUPPET") === "jumpscare_puppet.webm");
comprobar("el genérico «Animatrónico» no tiene video", Logica.archivoDeSusto("Animatrónico") === null);
comprobar("vacío, undefined o un número no tienen video",
  Logica.archivoDeSusto("") === null && Logica.archivoDeSusto(undefined) === null && Logica.archivoDeSusto(5) === null);
comprobar("un nombre con ruta no arma un archivo", Logica.archivoDeSusto("../freddy") === null);

console.log("imagenDeSusto");
comprobar("Freddy → su cara, jumpscare_freddy.jpg", Logica.imagenDeSusto("Freddy") === "jumpscare_freddy.jpg");
comprobar("« vixy » → jumpscare_vixy.jpg", Logica.imagenDeSusto(" vixy ") === "jumpscare_vixy.jpg");
comprobar("PUPPET → jumpscare_puppet.jpg", Logica.imagenDeSusto("PUPPET") === "jumpscare_puppet.jpg");
comprobar("el genérico «Animatrónico» usa la imagen de respaldo general",
  Logica.imagenDeSusto("Animatrónico") === "desktop_jumpscare.jpg");
comprobar("vacío, undefined o con ruta → respaldo general",
  Logica.imagenDeSusto("") === "desktop_jumpscare.jpg" && Logica.imagenDeSusto(undefined) === "desktop_jumpscare.jpg"
  && Logica.imagenDeSusto("../freddy") === "desktop_jumpscare.jpg");

console.log("duracionDelSusto");
const cerca = (a, b) => Math.abs(a - b) < 1e-6;
comprobar("video 1,5 s y grito 1,6155 s → dura lo que el grito", cerca(Logica.duracionDelSusto(1.5, 1.6155), 1.6155));
comprobar("video 3 s y grito 1,6 s → dura lo que el video", Logica.duracionDelSusto(3, 1.6) === 3);
comprobar("sin video: lo que dura el grito si pasa de 1,6 s", cerca(Logica.duracionDelSusto(0, 1.6155), 1.6155));
comprobar("sin video y grito corto: 1,6 s como mínimo", Logica.duracionDelSusto(0, 0.5) === 1.6);
comprobar("duraciones rotas (NaN) cuentan como sin video ni grito", Logica.duracionDelSusto(NaN, 0) === 1.6);


console.log(fallos === 0 ? "\nTodas las pruebas pasaron." : `\n${fallos} prueba(s) fallaron.`);
process.exit(fallos ? 1 : 0);
