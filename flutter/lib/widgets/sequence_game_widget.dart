import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import '../models/task.dart';

/// Minijuego de completar secuencias de números para poder terminar, en este caso
/// son 5 números, de prueba, podremos agregar más botones hasta en un máx de 10
class SequenceGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, List<int> secuenciaUsuario, int errores)
      onComplete;

  const SequenceGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<SequenceGameWidget> createState() => _SequenceGameWidgetState();
}

class _SequenceGameWidgetState extends State<SequenceGameWidget> {
  static const int _cantidadBotones = 5;

  late List<int> _ordenCorrecto;
  final List<int> _secuenciaUsuario = [];
  int _errores = 0;
  int _segundosRestantes = 25;
  Timer? _contadorCuentaRegresiva;
  bool _finalizado = false;

  @override
  void initState() {
    super.initState();
    _ordenCorrecto = List<int>.generate(_cantidadBotones, (i) => i + 1)
      ..shuffle(Random());
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
    widget.onComplete(exito, List<int>.from(_secuenciaUsuario), _errores);
  }

  void _alTocarBoton(int numero) {
    if (_finalizado) return;

    final int esperado = _ordenCorrecto[_secuenciaUsuario.length];
    if (numero != esperado) {
      setState(() {
        _errores++;
        _secuenciaUsuario.clear();
      });
      return;
    }

    setState(() {
      _secuenciaUsuario.add(numero);
    });

    if (_secuenciaUsuario.length == _ordenCorrecto.length) {
      _finalizar(exito: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          'Resolver Secuencia',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 4),
        Text('Tiempo restante: $_segundosRestantes s'),
        const SizedBox(height: 8),
        Text(
          'Orden: ${_ordenCorrecto.join(" → ")}',
          style: Theme.of(context).textTheme.bodyLarge,
        ),
        const SizedBox(height: 24),
        Wrap(
          spacing: 16,
          runSpacing: 16,
          alignment: WrapAlignment.center,
          children: List<Widget>.generate(_cantidadBotones, (index) {
            final int numero = index + 1;
            final bool yaTocado = _secuenciaUsuario.contains(numero);
            return SizedBox(
              width: 64,
              height: 64,
              child: ElevatedButton(
                onPressed: yaTocado ? null : () => _alTocarBoton(numero),
                style: ElevatedButton.styleFrom(
                  backgroundColor: yaTocado ? Colors.green : null,
                  shape: const CircleBorder(),
                ),
                child: Text('$numero', style: const TextStyle(fontSize: 20)),
              ),
            );
          }),
        ),
        const SizedBox(height: 16),
        Text('Progreso: ${_secuenciaUsuario.length}/${_ordenCorrecto.length}'),
        if (_errores > 0) Text('Errores: $_errores'),
      ],
    );
  }
}
