import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import '../models/task.dart';
import 'encabezado_de_minijuego.dart';

/// Coloca [cantidad] puntos de radio [radio] dentro de [area] (centros en
/// píxeles) sin que se encimen ni se salgan. Con la misma [semilla] da siempre
/// la misma colocación, para que no salten al redibujar la pantalla.
///
/// Prueba posiciones al azar con una separación mínima de `2 * radio + 8`; si
/// no logra colocarlos todos, usa una cuadrícula, que siempre cabe.
List<Offset> colocarPuntosSinSolaparse(
  int cantidad,
  Size area,
  double radio,
  int semilla,
) {
  const double holgura = 8;
  final double separacionMinima = 2 * radio + holgura;
  final double ancho = area.width - 2 * radio;
  final double alto = area.height - 2 * radio;
  final Random azar = Random(semilla);
  final List<Offset> puntos = <Offset>[];

  for (int intento = 0; intento < 400 && puntos.length < cantidad; intento++) {
    final Offset candidato = Offset(
      radio + azar.nextDouble() * ancho,
      radio + azar.nextDouble() * alto,
    );
    if (puntos.every(
      (Offset p) => (p - candidato).distance >= separacionMinima,
    )) {
      puntos.add(candidato);
    }
  }
  if (puntos.length == cantidad) return puntos;

  // Respaldo: cuadrícula de 3 columnas, un punto por celda, mezclada.
  const int columnas = 3;
  final int filas = (cantidad / columnas).ceil();
  final List<Offset> celdas = <Offset>[
    for (int i = 0; i < cantidad; i++)
      Offset(
        radio + (ancho * ((i % columnas) / (columnas - 1))),
        radio +
            (filas == 1 ? alto / 2 : alto * ((i ~/ columnas) / (filas - 1))),
      ),
  ]..shuffle(azar);
  return celdas;
}

/// Minijuego "Trazar Curso": puntos numerados dispersos en pantalla; el
/// jugador debe tocarlos en orden (1, 2, 3...) para completar el
/// recorrido, como un "conecta los puntos".
class TrazarCursoGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, Map<String, dynamic> datos) onComplete;

  const TrazarCursoGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<TrazarCursoGameWidget> createState() => _TrazarCursoGameWidgetState();
}

class _TrazarCursoGameWidgetState extends State<TrazarCursoGameWidget> {
  static const int _cantidadPuntos = 6;

  // Semilla de la colocación: fija durante la tarea para que los puntos no
  // cambien de lugar al redibujar, y distinta en cada tarea.
  late int _semilla;
  int _siguientePuntoEsperado = 0;
  int _segundosRestantes = 20;
  Timer? _contadorCuentaRegresiva;
  bool _finalizado = false;

  @override
  void initState() {
    super.initState();
    _semilla = Random().nextInt(1 << 30);
    _segundosRestantes = widget.task.duration;
    _contadorCuentaRegresiva = Timer.periodic(const Duration(seconds: 1), (_) {
      if (_finalizado) return;
      setState(() => _segundosRestantes--);
      if (_segundosRestantes <= 0) {
        _finalizar(exito: false);
      }
    });
  }

  @override
  void dispose() {
    _contadorCuentaRegresiva?.cancel();
    super.dispose();
  }

  void _finalizar({required bool exito}) {
    if (_finalizado) return;
    _finalizado = true;
    _contadorCuentaRegresiva?.cancel();
    widget.onComplete(exito, {
      'puntos_totales': _cantidadPuntos,
      'puntos_conectados': _siguientePuntoEsperado,
    });
  }

  void _alTocarPunto(int indice) {
    if (_finalizado) return;
    if (indice != _siguientePuntoEsperado) return;
    setState(() => _siguientePuntoEsperado++);
    if (_siguientePuntoEsperado == _cantidadPuntos) {
      _finalizar(exito: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        EncabezadoDeMinijuego(
          titulo: 'Trazar Curso',
          segundosRestantes: _segundosRestantes,
        ),
        const SizedBox(height: 4),
        Text(
          'Conectados: $_siguientePuntoEsperado/$_cantidadPuntos',
          style: EstilosDeMinijuego.estado,
        ),
        Expanded(
          child: LayoutBuilder(
            builder: (context, restricciones) {
              final Size area = Size(
                restricciones.maxWidth,
                restricciones.maxHeight,
              );
              // Círculos lo más grandes que permite el espacio (antes 56 px de
              // diámetro con el número en el tamaño por defecto): entre 80 y
              // 128 px, y el número ocupa casi todo el círculo.
              final double radio = (min(area.width, area.height) * 0.1).clamp(
                36.0,
                64.0,
              );
              final List<Offset> centros = colocarPuntosSinSolaparse(
                _cantidadPuntos,
                area,
                radio,
                _semilla,
              );
              return Stack(
                children: List<Widget>.generate(_cantidadPuntos, (i) {
                  final bool conectado = i < _siguientePuntoEsperado;
                  final bool esSiguiente = i == _siguientePuntoEsperado;
                  return Positioned(
                    left: centros[i].dx - radio,
                    top: centros[i].dy - radio,
                    child: GestureDetector(
                      onTap: () => _alTocarPunto(i),
                      child: Container(
                        width: radio * 2,
                        height: radio * 2,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          color: conectado
                              ? Colors.green
                              : (esSiguiente
                                    ? Colors.amber
                                    : Colors.grey.shade700),
                          // El que toca ahora lleva un aro blanco grueso: se
                          // distingue por forma y no solo por el color.
                          border: Border.all(
                            color: esSiguiente
                                ? Colors.white
                                : Colors.transparent,
                            width: 6,
                          ),
                        ),
                        child: Center(
                          child: Text(
                            '${i + 1}',
                            style: TextStyle(
                              color: esSiguiente ? Colors.black : Colors.white,
                              fontSize: radio * 0.95,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ),
                    ),
                  );
                }),
              );
            },
          ),
        ),
      ],
    );
  }
}
