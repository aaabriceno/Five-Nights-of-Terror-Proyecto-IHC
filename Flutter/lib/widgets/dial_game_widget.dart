import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import '../models/task.dart';

/// Minijuego "Girar Perillas": arrastrar cada perilla hasta alcanzar el
/// ángulo objetivo, dentro de un margen de tolerancia.
class DialGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, List<Map<String, dynamic>> diales)
      onComplete;

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
  static const double _toleranciaPorDefecto = 15;

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
    _cantidadDiales = (params['num_dials'] as int?) ?? _cantidadDialesPorDefecto;
    _tolerancia =
        ((params['tolerance'] as num?) ?? _toleranciaPorDefecto).toDouble();

    final List<dynamic>? objetivosDelServidor = params['targets'] as List<dynamic>?;
    if (objetivosDelServidor != null && objetivosDelServidor.length == _cantidadDiales) {
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
    _clavesPerillas = List<GlobalKey>.generate(_cantidadDiales, (_) => GlobalKey());
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

  bool _dentroDeTolerancia(int indice) {
    final double diferencia =
        (_angulosActuales[indice] - _angulosObjetivo[indice]).abs() % 360;
    final double diferenciaCorta =
        diferencia > 180 ? 360 - diferencia : diferencia;
    return diferenciaCorta <= _tolerancia;
  }

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

  Widget _buildDial(int indice) {
    final bool completado = _dialesCompletados.contains(indice);
    return Column(
      children: [
        Text('Perilla ${indice + 1}: girar a ${_angulosObjetivo[indice].round()}°'),
        const SizedBox(height: 8),
        Builder(
          builder: (context) {
            const double tamano = 120;
            return GestureDetector(
              onPanUpdate: (details) {
                final RenderBox? caja = _clavesPerillas[indice]
                    .currentContext
                    ?.findRenderObject() as RenderBox?;
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
                    width: 3,
                  ),
                ),
                child: Transform.rotate(
                  angle: _angulosActuales[indice] * pi / 180,
                  child: const Align(
                    alignment: Alignment(0, -0.8),
                    child: Icon(Icons.circle, size: 16, color: Colors.red),
                  ),
                ),
              ),
            );
          },
        ),
        const SizedBox(height: 4),
        Text('Actual: ${_angulosActuales[indice].round()}°'),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          'Girar Perillas',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 4),
        Text('Tiempo restante: $_segundosRestantes s'),
        const SizedBox(height: 8),
        Text('Tolerancia: ±${_tolerancia.round()}°'),
        const SizedBox(height: 24),
        Expanded(
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceEvenly,
            children: List<Widget>.generate(
              _cantidadDiales,
              (i) => _buildDial(i),
            ),
          ),
        ),
        const SizedBox(height: 16),
        Text('Completadas: ${_dialesCompletados.length}/$_cantidadDiales'),
      ],
    );
  }
}
