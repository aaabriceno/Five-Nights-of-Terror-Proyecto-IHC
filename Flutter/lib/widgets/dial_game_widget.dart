import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import '../models/task.dart';
import 'encabezado_de_minijuego.dart';

/// Margen de error por defecto de una perilla, en grados.
const double toleranciaDePerillaPorDefecto = 25;

/// Distancia angular más corta entre dos ángulos, entre 0° y 180°.
double diferenciaAngular(double a, double b) {
  final double d = (a - b).abs() % 360;
  return d > 180 ? 360 - d : d;
}

/// Qué tan cerca está la perilla de su objetivo, de 0 (lejos) a 1 (dentro de
/// la tolerancia: se puede soltar). Crece de forma continua desde los 90° de
/// distancia, para que el jugador vea que se acerca antes de llegar.
double proximidadAlObjetivo(double diferenciaGrados, double tolerancia) {
  const double distanciaDondeEmpieza = 90;
  if (diferenciaGrados <= tolerancia) return 1;
  if (diferenciaGrados >= distanciaDondeEmpieza) return 0;
  return 1 -
      (diferenciaGrados - tolerancia) / (distanciaDondeEmpieza - tolerancia);
}

/// Minijuego "Girar Perillas": arrastrar cada perilla hasta alcanzar el
/// ángulo objetivo, dentro de un margen de tolerancia.
class DialGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, List<Map<String, dynamic>> diales) onComplete;

  const DialGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<DialGameWidget> createState() => _DialGameWidgetState();
}

class _DialGameWidgetState extends State<DialGameWidget> {
  static const int _cantidadDialesPorDefecto = 2;

  late int _cantidadDiales;
  late double _tolerancia;
  late List<double> _angulosObjetivo;
  late List<double> _angulosActuales;
  late List<GlobalKey> _clavesPerillas;
  final Set<int> _dialesCompletados = {};
  int _segundosRestantes = 20;
  Timer? _contadorCuentaRegresiva;
  bool _finalizado = false;

  @override
  void initState() {
    super.initState();
    final Map<String, dynamic> params = widget.task.params;
    _cantidadDiales =
        (params['num_dials'] as int?) ?? _cantidadDialesPorDefecto;
    _tolerancia =
        ((params['tolerance'] as num?) ?? toleranciaDePerillaPorDefecto)
            .toDouble();

    final List<dynamic>? objetivosDelServidor =
        params['targets'] as List<dynamic>?;
    if (objetivosDelServidor != null &&
        objetivosDelServidor.length == _cantidadDiales) {
      _angulosObjetivo = objetivosDelServidor
          .map((valor) => (valor as num).toDouble())
          .toList();
    } else {
      final Random random = Random();
      _angulosObjetivo = List<double>.generate(
        _cantidadDiales,
        (_) => (random.nextInt(12) * 30).toDouble(), // múltiplos de 30°
      );
    }

    _angulosActuales = List<double>.filled(_cantidadDiales, 0);
    _clavesPerillas = List<GlobalKey>.generate(
      _cantidadDiales,
      (_) => GlobalKey(),
    );
    _segundosRestantes = widget.task.duration;
    _contadorCuentaRegresiva = Timer.periodic(const Duration(seconds: 1), (_) {
      if (_finalizado) return;
      setState(() {
        _segundosRestantes--;
      });
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

    final List<Map<String, dynamic>> diales = [];
    for (int i = 0; i < _cantidadDiales; i++) {
      diales.add({
        'dial_id': i + 1,
        'target': _angulosObjetivo[i].round(),
        'achieved': _angulosActuales[i].round(),
        'correct': _dialesCompletados.contains(i),
      });
    }

    widget.onComplete(exito, diales);
  }

  double _diferenciaConElObjetivo(int indice) =>
      diferenciaAngular(_angulosActuales[indice], _angulosObjetivo[indice]);

  bool _dentroDeTolerancia(int indice) =>
      _diferenciaConElObjetivo(indice) <= _tolerancia;

  void _actualizarAngulo(int indice, Offset centro, Offset posicionToque) {
    final double dx = posicionToque.dx - centro.dx;
    final double dy = posicionToque.dy - centro.dy;
    double angulo = atan2(dy, dx) * 180 / pi + 90; // 0° arriba
    if (angulo < 0) angulo += 360;

    setState(() {
      _angulosActuales[indice] = angulo;
      if (_dentroDeTolerancia(indice)) {
        _dialesCompletados.add(indice);
      } else {
        _dialesCompletados.remove(indice);
      }
    });
  }

  void _alSoltarPerilla() {
    // Se verifica la victoria solo al soltar el dedo, no en cada frame de
    // arrastre — de lo contrario, si ambas perillas pasan por su ángulo
    // correcto al mismo tiempo mientras tanteas la segunda (sin haber
    // soltado ahí a propósito), el juego terminaba de golpe.
    if (_dialesCompletados.length == _cantidadDiales) {
      _finalizar(exito: true);
    }
  }

  Widget _buildDial(int indice, double tamano) {
    final bool completado = _dialesCompletados.contains(indice);
    final double proximidad = proximidadAlObjetivo(
      _diferenciaConElObjetivo(indice),
      _tolerancia,
    );
    const TextStyle negrita = TextStyle(
      fontSize: 22,
      fontWeight: FontWeight.bold,
    );
    // Cada columna ocupa a lo sumo su parte del ancho; si un texto no cabe se
    // reduce en vez de desbordar.
    return SizedBox(
      width: tamano + 24,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          FittedBox(
            fit: BoxFit.scaleDown,
            child: Text(
              'Perilla ${indice + 1}: girar a ${_angulosObjetivo[indice].round()}°',
              style: negrita,
            ),
          ),
          const SizedBox(height: 8),
          GestureDetector(
            onPanUpdate: (details) {
              final RenderBox? caja =
                  _clavesPerillas[indice].currentContext?.findRenderObject()
                      as RenderBox?;
              if (caja == null) return;
              final Offset local = caja.globalToLocal(details.globalPosition);
              final Offset centro = Offset(tamano / 2, tamano / 2);
              _actualizarAngulo(indice, centro, local);
            },
            onPanEnd: (_) => _alSoltarPerilla(),
            child: Container(
              key: _clavesPerillas[indice],
              width: tamano,
              height: tamano,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(
                  color: completado ? Colors.green : Colors.grey,
                  width: completado ? 10 : 6,
                ),
              ),
              child: Stack(
                alignment: Alignment.center,
                children: [
                  // El círculo verde crece a medida que te acercas al ángulo y
                  // llena la perilla cuando ya puedes soltar. El tamaño (y el
                  // check y el texto) avisan igual que el color: no depende de
                  // distinguir verde de gris.
                  AnimatedContainer(
                    duration: const Duration(milliseconds: 80),
                    width: tamano * 0.9 * proximidad,
                    height: tamano * 0.9 * proximidad,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: Colors.green.withValues(
                        alpha: 0.25 + 0.45 * proximidad,
                      ),
                    ),
                  ),
                  // Marca fija del ángulo objetivo, sobre el aro de la perilla.
                  Transform.rotate(
                    angle: _angulosObjetivo[indice] * pi / 180,
                    child: Align(
                      alignment: const Alignment(0, -0.97),
                      child: Icon(
                        Icons.arrow_drop_down,
                        size: tamano * 0.2,
                        color: Colors.green,
                      ),
                    ),
                  ),
                  // Punto que el jugador arrastra.
                  Transform.rotate(
                    angle: _angulosActuales[indice] * pi / 180,
                    child: Align(
                      alignment: const Alignment(0, -0.72),
                      child: Icon(
                        Icons.circle,
                        size: tamano * 0.14,
                        color: Colors.redAccent,
                      ),
                    ),
                  ),
                  if (completado)
                    Icon(Icons.check, size: tamano * 0.3, color: Colors.white),
                ],
              ),
            ),
          ),
          const SizedBox(height: 8),
          FittedBox(
            fit: BoxFit.scaleDown,
            child: Text(
              completado
                  ? '¡LISTO, SUELTA! ${_angulosActuales[indice].round()}°'
                  : 'Actual: ${_angulosActuales[indice].round()}°',
              style: negrita.copyWith(color: completado ? Colors.green : null),
            ),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        EncabezadoDeMinijuego(
          titulo: 'Girar Perillas',
          segundosRestantes: _segundosRestantes,
        ),
        const SizedBox(height: 8),
        // Arriba y no al final: un texto pegado al borde inferior se recortaba
        // en la tablet con el texto del sistema agrandado.
        Text(
          'Completadas: ${_dialesCompletados.length}/$_cantidadDiales',
          style: EstilosDeMinijuego.estado,
        ),
        const SizedBox(height: 8),
        Expanded(
          child: LayoutBuilder(
            builder: (context, restricciones) {
              // Lo más grande que cabe: limitado por el ancho de cada perilla
              // y por el alto (menos los textos de arriba y abajo).
              final double espacio = min(
                restricciones.maxWidth / _cantidadDiales - 24,
                restricciones.maxHeight - 100,
              );
              final double tamano = espacio.clamp(110.0, 260.0).toDouble();
              return Row(
                mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                crossAxisAlignment: CrossAxisAlignment.center,
                children: List<Widget>.generate(
                  _cantidadDiales,
                  (i) => _buildDial(i, tamano),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}
