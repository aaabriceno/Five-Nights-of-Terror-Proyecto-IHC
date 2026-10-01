import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import '../models/task.dart';

/// Minijuego "Reparar Temperatura": mover un slider hasta ubicarlo
/// dentro de un rango objetivo y soltarlo ahí.
class TemperaturaGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, Map<String, dynamic> datos) onComplete;

  const TemperaturaGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<TemperaturaGameWidget> createState() => _TemperaturaGameWidgetState();
}

class _TemperaturaGameWidgetState extends State<TemperaturaGameWidget> {
  static const double _margenObjetivo = 5;

  late double _objetivo;
  double _valorActual = 50;
  int _segundosRestantes = 15;
  Timer? _contadorCuentaRegresiva;
  bool _finalizado = false;

  @override
  void initState() {
    super.initState();
    final Random random = Random();
    _objetivo = 20 + random.nextInt(60).toDouble(); // rango 20-80
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
      'objetivo': _objetivo.round(),
      'logrado': _valorActual.round(),
    });
  }

  bool _dentroDelRango() => (_valorActual - _objetivo).abs() <= _margenObjetivo;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text('Reparar Temperatura', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        Text('Tiempo restante: $_segundosRestantes s'),
        const SizedBox(height: 16),
        Text('Objetivo: ${_objetivo.round()}°C (±${_margenObjetivo.round()}°)'),
        const SizedBox(height: 24),
        Text('${_valorActual.round()}°C', style: Theme.of(context).textTheme.headlineMedium),
        Slider(
          value: _valorActual,
          min: 0,
          max: 100,
          onChanged: _finalizado
              ? null
              : (valor) => setState(() => _valorActual = valor),
          onChangeEnd: (_) {
            if (_dentroDelRango()) {
              _finalizar(exito: true);
            }
          },
        ),
      ],
    );
  }
}
