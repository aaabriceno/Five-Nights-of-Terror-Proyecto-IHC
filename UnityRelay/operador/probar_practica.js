// Pruebas de practica.js (la práctica del «Cómo jugar» del jugador 2):
// node UnityRelay/operador/probar_practica.js
const Practica = require("./practica.js");

let fallos = 0;
function comprobar(descripcion, condicion) {
  console.log(`  ${condicion ? "ok   " : "FALLA"} ${descripcion}`);
  if (!condicion) fallos++;
}

// Avanza la simulación de `desde` a `hasta` (segundos) en pasos de 0,1 s,
// como lo haría la página cuadro a cuadro.
function correr(estado, desde, hasta) {
  for (let t = desde; t <= hasta + 1e-9; t += 0.1) Practica.avanzar(estado, Math.round(t * 1000) / 1000);
}
const distancia = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const oficina = Practica.RUTA_FREDDY[Practica.RUTA_FREDDY.length - 1];

console.log("valores reales de Unity");
const V = Practica.VALORES;
comprobar("Retrasar dura 8 s a la mitad de velocidad y se reusa a los 20 s",
  V.retrasoS === 8 && V.factorLento === 0.5 && V.enfriamientoS === 20);
comprobar("la trampa detiene 6 s, un error bloquea la terminal 3 s y caben 3 cargas",
  V.trampaS === 6 && V.bloqueoS === 3 && V.maxCargas === 3);

console.log("Freddy camina hacia la oficina");
let e = Practica.crear();
const inicio = Practica.posicion(e);
correr(e, 0, 3);
comprobar("se mueve con el tiempo", distancia(Practica.posicion(e), inicio) > 1);
comprobar("se acerca a la oficina", distancia(Practica.posicion(e), oficina) < distancia(inicio, oficina));
comprobar("sin retrasar va a velocidad normal", !Practica.freddy(e, 3).lento && !Practica.freddy(e, 3).atrapado);

console.log("Retrasar");
const normal = Practica.crear();
correr(normal, 0, 2);
const lento = Practica.crear();
const r1 = Practica.retrasar(lento, 0);
correr(lento, 0, 2);
comprobar("Retrasar se acepta la primera vez", r1.ok === true);
comprobar("queda lento", Practica.freddy(lento, 2).lento === true);
const recorridoNormal = distancia(Practica.posicion(normal), inicio);
const recorridoLento = distancia(Practica.posicion(lento), inicio);
comprobar(`lento recorre la mitad en el mismo tiempo (${recorridoLento.toFixed(2)} de ${recorridoNormal.toFixed(2)})`,
  Math.abs(recorridoLento - recorridoNormal / 2) < 0.3);
comprobar("marca el paso «retrasar»", !Practica.pendientes(lento).includes("retrasar"));
const r2 = Practica.retrasar(lento, 5);
comprobar("un segundo Retrasar antes de 20 s no se acepta y dice cuánto falta (15 s)",
  r2.ok === false && Math.abs(r2.esperaRestante - 15) < 1e-6);
correr(lento, 2.1, 9);
comprobar("a los 8 s deja de estar lento", Practica.freddy(lento, 9).lento === false);
comprobar("y aún falta para volver a usarlo", Practica.freddy(lento, 9).esperaRestante > 0);
comprobar("a los 20 s del uso se puede volver a retrasar", Practica.retrasar(lento, 20).ok === true);

console.log("trampa");
e = Practica.crear();
const enRuta = Practica.PUNTOS.filter((p) => p.enRuta);
const fueraDeRuta = Practica.PUNTOS.filter((p) => !p.enRuta);
comprobar("hay puntos en el camino de Freddy y fuera de él", enRuta.length >= 2 && fueraDeRuta.length >= 1);
comprobar("ni el escenario ni la oficina son puntos para trampa",
  !Practica.PUNTOS.some((p) => p.id === Practica.RUTA_FREDDY[0].id || p.id === oficina.id));
const fuera = Practica.ponerTrampa(e, fueraDeRuta[0].id, 0);
comprobar("fuera del camino dice «fuera_de_ruta»", fuera.ok === false && fuera.motivo === "fuera_de_ruta");
comprobar("y no gasta la carga", e.cargas === 1);
comprobar("un punto que no existe dice «desconocido»", Practica.ponerTrampa(e, "no-existe", 0).motivo === "desconocido");
const blanco = enRuta[0];
const puesta = Practica.ponerTrampa(e, blanco.id, 0);
comprobar("en el camino se acepta y gasta la carga", puesta.ok === true && e.cargas === 0);
let atrapadoEn = null;
for (let t = 0; t <= 30 && atrapadoEn === null; t = Math.round((t + 0.1) * 1000) / 1000) {
  Practica.avanzar(e, t);
  if (Practica.freddy(e, t).atrapado) atrapadoEn = t;
}
comprobar("Freddy cae en la trampa al pasar por ese punto", atrapadoEn !== null);
comprobar("se detiene justo en el punto de la trampa", atrapadoEn !== null && distancia(Practica.posicion(e), blanco) < 0.01);
const dondeCayo = Practica.posicion(e);
correr(e, atrapadoEn + 0.1, atrapadoEn + 5.5);
comprobar("sigue detenido 5,5 s después", distancia(Practica.posicion(e), dondeCayo) < 0.01);
correr(e, atrapadoEn + 5.6, atrapadoEn + 8);
comprobar("a los 6 s vuelve a caminar", distancia(Practica.posicion(e), dondeCayo) > 1);
comprobar("marca el paso «trampa»", !Practica.pendientes(e).includes("trampa"));
comprobar("sin cargas dice «sin_cargas»", Practica.ponerTrampa(e, enRuta[1].id, 40).motivo === "sin_cargas");

console.log("al llegar a la oficina vuelve a empezar");
e = Practica.crear();
let volvio = false, cerca = false;
for (let t = 0; t <= 60 && !volvio; t = Math.round((t + 0.1) * 1000) / 1000) {
  Practica.avanzar(e, t);
  const p = Practica.posicion(e);
  if (distancia(p, oficina) < 3) cerca = true;
  if (cerca && distancia(p, inicio) < 1) volvio = true;
}
comprobar("llega a la oficina y aparece de nuevo en el escenario", cerca && volvio);

console.log("terminal");
const cero = () => 0;
const calculo = Practica.crearTarea("calculo", cero);
comprobar("cálculo como el de Unity: «Calcula 6 × 6» = 36", calculo.enunciado === "Calcula 6 × 6" && calculo.respuesta === 36);
const alto = Practica.crearTarea("calculo", () => 0.999);
comprobar("los factores llegan hasta 12", alto.enunciado === "Calcula 12 × 12" && alto.respuesta === 144);
const ecuacion = Practica.crearTarea("ecuacion", cero);
comprobar("ecuación como la de Unity: «Encuentra x: 2x + 2 = 6» → 2",
  ecuacion.enunciado === "Encuentra x: 2x + 2 = 6" && ecuacion.respuesta === 2);
let sana = true;
for (let i = 0; i < 200; i++) {
  const t = Practica.crearTarea("ecuacion", Math.random);
  const m = /^Encuentra x: (\d)x \+ (\d+) = (\d+)$/.exec(t.enunciado);
  if (!m || Number(m[1]) * t.respuesta + Number(m[2]) !== Number(m[3]) || t.respuesta < 2 || t.respuesta > 10) sana = false;
}
comprobar("200 ecuaciones al azar tienen solución entera de 2 a 10", sana);

e = Practica.crear();
comprobar("el rescate no se puede antes de la ecuación", Practica.rescatar(e) === false);
let r = Practica.responder(e, calculo, 35, 0);
comprobar("respuesta incorrecta: bloquea la terminal 3 s", r.correcto === false && r.bloqueoS === 3);
comprobar("bloqueoRestante cuenta hacia atrás", Math.abs(Practica.bloqueoRestante(e, 1) - 2) < 1e-6);
r = Practica.responder(e, calculo, 36, 2);
comprobar("bloqueada no acepta ni la correcta", r.correcto === false && r.bloqueado === true);
r = Practica.responder(e, calculo, 36, 3.1);
comprobar("pasado el bloqueo la correcta se acepta", r.correcto === true);
comprobar("el cálculo da +1 carga", e.cargas === 2);
Practica.responder(e, Practica.crearTarea("calculo", cero), 36, 4);
Practica.responder(e, Practica.crearTarea("calculo", cero), 36, 5);
comprobar("las cargas no pasan de 3", e.cargas === 3);
comprobar("marca el paso «calculo»", !Practica.pendientes(e).includes("calculo"));
r = Practica.responder(e, ecuacion, 2, 6);
comprobar("la ecuación correcta se acepta y marca «ecuacion»", r.correcto && !Practica.pendientes(e).includes("ecuacion"));
comprobar("después de la ecuación el rescate funciona", Practica.rescatar(e) === true);
comprobar("y marca «rescate»", !Practica.pendientes(e).includes("rescate"));

console.log("conocer a los animatrónicos y leer el panel");
e = Practica.crear();
comprobar("al empezar faltan los 7 pasos", Practica.pendientes(e).length === 7 && Practica.PASOS.length === 7);
comprobar("cada paso dice en qué diapositiva se practica",
  Practica.PASOS.every((p) => typeof p.texto === "string" && p.texto && p.diapositiva >= 2 && p.diapositiva <= 6));
Practica.conocer(e, "freddy"); Practica.conocer(e, "freddy"); Practica.conocer(e, "vixy");
comprobar("tocar dos (uno repetido) no basta", Practica.pendientes(e).includes("conocer"));
Practica.conocer(e, "puppet");
comprobar("tocar a los tres marca «conocer»", !Practica.pendientes(e).includes("conocer"));
comprobar("la primera pregunta del panel es la fila de la mirada", Practica.preguntaDelPanel(e).fila === "mirada");
let t1 = Practica.tocarFila(e, "caja");
comprobar("tocar otra fila no cuenta", t1.correcto === false && Practica.preguntaDelPanel(e).fila === "mirada");
t1 = Practica.tocarFila(e, "mirada");
comprobar("tocar «mirada» cuenta y pasa a la caja", t1.correcto === true && Practica.preguntaDelPanel(e).fila === "caja");
t1 = Practica.tocarFila(e, "caja");
comprobar("tocar «caja» termina el panel y marca «panel»",
  t1.correcto && t1.terminado && Practica.preguntaDelPanel(e) === null && !Practica.pendientes(e).includes("panel"));
for (const id of ["retrasar", "trampa", "calculo", "ecuacion", "rescate"]) Practica.marcar(e, id);
comprobar("con todo hecho no falta nada", Practica.pendientes(e).length === 0);
Practica.marcar(e, "inventado");
comprobar("marcar un paso que no existe no rompe nada", Practica.pendientes(e).length === 0);

console.log(fallos ? `\n${fallos} prueba(s) fallaron.` : "\nTodas las pruebas pasaron.");
process.exit(fallos ? 1 : 0);
