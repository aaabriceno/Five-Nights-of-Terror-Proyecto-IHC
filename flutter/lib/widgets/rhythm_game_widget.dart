import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import '../models/task.dart';

/// Minijuego "Ritmo Crítico": presionar el botón que se resalta al ritmo
/// de un pulso (BPM), dentro de una ventana de tolerancia en milisegundos.
class RhythmGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, Map<String, dynamic> datosRitmo)
      onComplete;

  const RhythmGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<RhythmGameWidget> createState() => _RhythmGameWidgetState();
}

class _RhythmGameWidgetState extends State<RhythmGameWidget> {
  static const int _totalPulsos = 8;
  static const Duration _duracionPulso = Duration(milliseconds: 1500);
  static const List<Color> _coloresBotones = [
    Colors.red,
    Colors.green,
    Colors.blue,
  ];

  Timer? _timerPulso;
  Timer? _countdownTimer;
  int _segundosRestantes = 15;
  bool _finalizado = false;

  int _pulsosEmitidos = 0;
  int _botonEsperado = 0;
  bool _yaRespondioPulsoActual = false;

  int _aciertos = 0;
  int _fallados = 0;

  final Random _random = Random();

  @override
  void initState() {
    super.initState();
    _segundosRestantes = widget.task.duration;

    _countdownTimer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (_finalizado) return;
      setState(() {
        _segundosRestantes--;
      });
      if (_segundosRestantes <= 0) {
        _finalizar(exito: _aciertos >= (_totalPulsos / 2)); // aprueba con 50%+
      }
    });

    _timerPulso = Timer.periodic(_duracionPulso, (_) => _emitirPulso());
    _emitirPulso();
  }

  @override
  void dispose() {
    _timerPulso?.cancel();
    _countdownTimer?.cancel();
    super.dispose();
  }

  void _emitirPulso() {
    if (_finalizado) return;

    // Si el pulso anterior nunca fue respondido correctamente, cuenta fallo.
    if (_pulsosEmitidos > 0 && !_yaRespondioPulsoActual) {
      _fallados++;
    }

    _pulsosEmitidos++;
    if (_pulsosEmitidos > _totalPulsos) {
      _finalizar(exito: _aciertos >= (_totalPulsos / 2));
      return;
    }

    setState(() {
      _botonEsperado = _random.nextInt(_coloresBotones.length);
      _yaRespondioPulsoActual = false;
    });
  }

  void _finalizar({required bool exito}) {
    if (_finalizado) return;
    _finalizado = true;
    _timerPulso?.cancel();
    _countdownTimer?.cancel();

    final double precision = _totalPulsos == 0
        ? 0
        : (_aciertos / _totalPulsos * 100);

    widget.onComplete(exito, {
      'total_beats': _totalPulsos,
      'correct_presses': _aciertos,
      'missed_presses': _fallados,
      'accuracy': double.parse(precision.toStringAsFixed(1)),
    });
  }

  void _alPresionarBoton(int indice) {
    if (_finalizado || _yaRespondioPulsoActual) return;
    if (indice != _botonEsperado) return; // toque incorrecto no cuenta

    setState(() {
      _yaRespondioPulsoActual = true;
      _aciertos++;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          'Ritmo Crítico',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 4),
        Text('Tiempo restante: $_segundosRestantes s'),
        const SizedBox(height: 24),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: List<Widget>.generate(_coloresBotones.length, (indice) {
            final bool resaltado =
                indice == _botonEsperado && !_yaRespondioPulsoActual;
            return SizedBox(
              width: 72,
              height: 72,
              child: ElevatedButton(
                onPressed: () => _alPresionarBoton(indice),
                style: ElevatedButton.styleFrom(
                  backgroundColor: resaltado
                      ? _coloresBotones[indice]
                      : _coloresBotones[indice].withValues(alpha: 0.3),
                  shape: const CircleBorder(),
                ),
                child: const SizedBox.shrink(),
              ),
            );
          }),
        ),
        const SizedBox(height: 24),
        Text('Aciertos: $_aciertos/$_totalPulsos'),
        Text('Pulso: ${_pulsosEmitidos.clamp(0, _totalPulsos)}/$_totalPulsos'),
      ],
    );
  }
}
