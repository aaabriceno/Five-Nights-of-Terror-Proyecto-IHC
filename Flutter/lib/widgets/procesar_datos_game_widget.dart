import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../models/task.dart';
import 'encabezado_de_minijuego.dart';

enum DireccionDeFlecha { arriba, abajo, izquierda, derecha }

/// Minijuego "Procesar Datos": se muestra una secuencia de flechas de
/// dirección; el jugador debe repetirla haciendo swipe en cada
/// dirección mostrada, en orden (tipo "Simon dice" táctil).
class ProcesarDatosGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, Map<String, dynamic> datos) onComplete;

  /// Secuencia fija de flechas. Si es null se genera una al azar; existe para
  /// poder probar el minijuego con una secuencia conocida.
  final List<DireccionDeFlecha>? secuenciaInicial;

  const ProcesarDatosGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
    this.secuenciaInicial,
  });

  @override
  State<ProcesarDatosGameWidget> createState() =>
      _ProcesarDatosGameWidgetState();
}

class _ProcesarDatosGameWidgetState extends State<ProcesarDatosGameWidget> {
  static const int _longitudSecuencia = 5;
  // Cuánto debe moverse el dedo para contar como un deslizamiento. Con 20 px
  // se decidía la dirección con el primer tramo del movimiento, que casi
  // siempre sale en curva, y se leía mal.
  static const double _distanciaMinimaDelSwipe = 56;
  // Un deslizamiento solo cuenta si un eje domina claramente sobre el otro;
  // una diagonal se ignora sin penalizar.
  static const double _dominanciaMinima = 1.4;

  late List<DireccionDeFlecha> _secuenciaObjetivo;
  int _indiceActual = 0;
  int _segundosRestantes = 15;
  Timer? _contadorCuentaRegresiva;
  bool _finalizado = false;
  int _errores = 0;
  // Solo cuenta el primer dedo, y cada toque decide como mucho una vez.
  int? _punteroActivo;
  Offset? _inicioDelToque;
  bool _toqueYaDecidido = false;

  @override
  void initState() {
    super.initState();
    final Random random = Random();
    _secuenciaObjetivo =
        widget.secuenciaInicial ??
        List<DireccionDeFlecha>.generate(
          _longitudSecuencia,
          (_) =>
              DireccionDeFlecha.values[random.nextInt(
                DireccionDeFlecha.values.length,
              )],
        );
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
      'longitud_secuencia': _longitudSecuencia,
      'aciertos': _indiceActual,
      'errores': _errores,
    });
  }

  IconData _iconoPorDireccion(DireccionDeFlecha direccion) {
    switch (direccion) {
      case DireccionDeFlecha.arriba:
        return Icons.arrow_upward;
      case DireccionDeFlecha.abajo:
        return Icons.arrow_downward;
      case DireccionDeFlecha.izquierda:
        return Icons.arrow_back;
      case DireccionDeFlecha.derecha:
        return Icons.arrow_forward;
    }
  }

  void _alRecibirSwipe(DireccionDeFlecha direccion) {
    if (_finalizado) return;
    if (direccion == _secuenciaObjetivo[_indiceActual]) {
      setState(() => _indiceActual++);
      if (_indiceActual == _secuenciaObjetivo.length) {
        _finalizar(exito: true);
      }
    } else {
      // Equivocarse no termina la tarea (antes sí, y un deslizamiento mal
      // leído hacía perderla): se vuelve a empezar la secuencia y se sigue
      // hasta que se acabe el tiempo, igual que en "Resolver Secuencia".
      HapticFeedback.heavyImpact();
      setState(() {
        _errores++;
        _indiceActual = 0;
      });
    }
  }

  void _alBajarDedo(PointerDownEvent evento) {
    if (_punteroActivo != null || _finalizado) return;
    _punteroActivo = evento.pointer;
    _inicioDelToque = evento.localPosition;
    _toqueYaDecidido = false;
  }

  void _alMoverDedo(PointerMoveEvent evento) {
    if (evento.pointer != _punteroActivo || _toqueYaDecidido || _finalizado) {
      return;
    }
    final Offset delta = evento.localPosition - _inicioDelToque!;
    if (delta.distance < _distanciaMinimaDelSwipe) return;

    final double horizontal = delta.dx.abs();
    final double vertical = delta.dy.abs();
    final DireccionDeFlecha direccion;
    if (horizontal >= vertical * _dominanciaMinima) {
      direccion = delta.dx > 0
          ? DireccionDeFlecha.derecha
          : DireccionDeFlecha.izquierda;
    } else if (vertical >= horizontal * _dominanciaMinima) {
      direccion = delta.dy > 0
          ? DireccionDeFlecha.abajo
          : DireccionDeFlecha.arriba;
    } else {
      return; // diagonal: se sigue esperando a que el dedo defina un eje
    }
    _toqueYaDecidido = true;
    _alRecibirSwipe(direccion);
  }

  void _alSubirODeshacerDedo(PointerEvent evento) {
    if (evento.pointer != _punteroActivo) return;
    _punteroActivo = null;
    _inicioDelToque = null;
  }

  Widget _flecha(int i, double lado) {
    final bool completada = i < _indiceActual;
    final bool esLaQueToca = i == _indiceActual;
    // La que toca ahora lleva un aro blanco grueso y las ya hechas un fondo
    // verde con la flecha oscura: se distinguen por forma y brillo, no solo
    // por el color.
    return Container(
      width: lado,
      height: lado,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: completada ? Colors.green : Colors.transparent,
        border: Border.all(
          color: esLaQueToca ? Colors.white : Colors.grey.shade700,
          width: esLaQueToca ? 6 : 2,
        ),
      ),
      child: Icon(
        _iconoPorDireccion(_secuenciaObjetivo[i]),
        size: lado * 0.7,
        color: completada
            ? Colors.black
            : (esLaQueToca ? Colors.white : Colors.grey),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        EncabezadoDeMinijuego(
          titulo: 'Procesar Datos',
          segundosRestantes: _segundosRestantes,
        ),
        const SizedBox(height: 16),
        // Flechas lo más grandes posible: se reparten el ancho disponible.
        LayoutBuilder(
          builder: (context, restricciones) {
            final double lado =
                ((restricciones.maxWidth / _secuenciaObjetivo.length) - 24)
                    .clamp(72.0, 120.0)
                    .toDouble();
            return Wrap(
              alignment: WrapAlignment.center,
              spacing: 20,
              runSpacing: 12,
              children: List<Widget>.generate(
                _secuenciaObjetivo.length,
                (i) => _flecha(i, lado),
              ),
            );
          },
        ),
        const SizedBox(height: 12),
        Text(
          'Progreso: $_indiceActual/${_secuenciaObjetivo.length}',
          style: EstilosDeMinijuego.estado,
        ),
        if (_errores > 0)
          Text('Errores: $_errores', style: EstilosDeMinijuego.estado),
        const SizedBox(height: 8),
        Expanded(
          // Listener y no GestureDetector.onPan: este último espera a superar
          // una holgura de ~36 px antes de empezar y la dirección se decidía
          // con el tramo inicial. Aquí se mide desde que el dedo toca.
          child: Listener(
            behavior: HitTestBehavior.opaque,
            onPointerDown: _alBajarDedo,
            onPointerMove: _alMoverDedo,
            onPointerUp: _alSubirODeshacerDedo,
            onPointerCancel: _alSubirODeshacerDedo,
            child: const SizedBox.expand(
              child: Center(
                child: Text(
                  'Desliza en la dirección mostrada',
                  style: EstilosDeMinijuego.detalle,
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
