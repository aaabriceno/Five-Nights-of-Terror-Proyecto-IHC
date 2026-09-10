import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import '../models/task.dart';

enum _Direccion { arriba, abajo, izquierda, derecha }

/// Minijuego "Procesar Datos": se muestra una secuencia de flechas de
/// dirección; el jugador debe repetirla haciendo swipe en cada
/// dirección mostrada, en orden (tipo "Simon dice" táctil).
class ProcesarDatosGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, Map<String, dynamic> datos) onComplete;

  const ProcesarDatosGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<ProcesarDatosGameWidget> createState() =>
      _ProcesarDatosGameWidgetState();
}

class _ProcesarDatosGameWidgetState extends State<ProcesarDatosGameWidget> {
  static const int _longitudSecuencia = 5;
  static const double _umbralSwipe = 20;

  late List<_Direccion> _secuenciaObjetivo;
  int _indiceActual = 0;
  int _segundosRestantes = 15;
  Timer? _contadorCuentaRegresiva;
  bool _finalizado = false;
  Offset? _inicioArrastre;

  @override
  void initState() {
    super.initState();
    final Random random = Random();
    _secuenciaObjetivo = List<_Direccion>.generate(
      _longitudSecuencia,
      (_) => _Direccion.values[random.nextInt(_Direccion.values.length)],
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
    });
  }

  IconData _iconoPorDireccion(_Direccion direccion) {
    switch (direccion) {
      case _Direccion.arriba:
        return Icons.arrow_upward;
      case _Direccion.abajo:
        return Icons.arrow_downward;
      case _Direccion.izquierda:
        return Icons.arrow_back;
      case _Direccion.derecha:
        return Icons.arrow_forward;
    }
  }

  void _alRecibirSwipe(_Direccion direccion) {
    if (_finalizado) return;
    if (direccion == _secuenciaObjetivo[_indiceActual]) {
      setState(() => _indiceActual++);
      if (_indiceActual == _secuenciaObjetivo.length) {
        _finalizar(exito: true);
      }
    } else {
      _finalizar(exito: false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text('Procesar Datos', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        Text('Tiempo restante: $_segundosRestantes s'),
        const SizedBox(height: 24),
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: List<Widget>.generate(_secuenciaObjetivo.length, (i) {
            final bool completado = i < _indiceActual;
            return Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Icon(
                _iconoPorDireccion(_secuenciaObjetivo[i]),
                color: completado ? Colors.green : Colors.grey,
                size: 28,
              ),
            );
          }),
        ),
        const SizedBox(height: 32),
        Expanded(
          child: GestureDetector(
            onPanStart: (detalles) => _inicioArrastre = detalles.localPosition,
            onPanEnd: (_) {
              _inicioArrastre = null;
            },
            onPanUpdate: (detalles) {
              if (_inicioArrastre == null || _finalizado) return;
              final Offset delta = detalles.localPosition - _inicioArrastre!;
              if (delta.distance < _umbralSwipe) return;
              final _Direccion direccion;
              if (delta.dx.abs() > delta.dy.abs()) {
                direccion = delta.dx > 0 ? _Direccion.derecha : _Direccion.izquierda;
              } else {
                direccion = delta.dy > 0 ? _Direccion.abajo : _Direccion.arriba;
              }
              _inicioArrastre = null;
              _alRecibirSwipe(direccion);
            },
            child: Container(
              color: Colors.transparent,
              child: const Center(child: Text('Desliza en la dirección mostrada')),
            ),
          ),
        ),
      ],
    );
  }
}
