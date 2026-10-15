import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import '../models/task.dart';
import 'encabezado_de_minijuego.dart';

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
        EncabezadoDeMinijuego(titulo: 'Resolver Secuencia', segundosRestantes: _segundosRestantes),
        const SizedBox(height: 8),
        const SizedBox(height: 8),
        // El orden a seguir es lo más importante de la pantalla: grande y en
        // negrita, no un texto de apoyo.
        Text(
          'Orden: ${_ordenCorrecto.join(" → ")}',
          style: const TextStyle(fontSize: 44, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 16),
        LayoutBuilder(
          builder: (context, restricciones) {
            // Botones lo más grandes posible, sin pasar de una fila de alto
            // razonable: se reparten el ancho entre todos.
            final double lado = ((restricciones.maxWidth / _cantidadBotones) - 24)
                .clamp(88.0, 128.0)
                .toDouble();
            return Wrap(
              spacing: 20,
              runSpacing: 20,
              alignment: WrapAlignment.center,
              children: List<Widget>.generate(_cantidadBotones, (index) {
                final int numero = index + 1;
                final bool yaTocado = _secuenciaUsuario.contains(numero);
                return SizedBox(
                  width: lado,
                  height: lado,
                  child: ElevatedButton(
                    onPressed: yaTocado ? null : () => _alTocarBoton(numero),
                    style: ElevatedButton.styleFrom(
                      padding: EdgeInsets.zero,
                      backgroundColor: yaTocado ? Colors.green : null,
                      shape: const CircleBorder(),
                    ),
                    // El check repite en forma lo que el fondo verde indica por
                    // color, para quien no distingue el verde del gris apagado.
                    child: yaTocado
                        ? Icon(Icons.check, size: lado * 0.5)
                        : Text('$numero',
                            style: TextStyle(
                                fontSize: lado * 0.5, fontWeight: FontWeight.bold)),
                  ),
                );
              }),
            );
          },
        ),
        const SizedBox(height: 16),
        Text('Progreso: ${_secuenciaUsuario.length}/${_ordenCorrecto.length}', style: EstilosDeMinijuego.estado),
        if (_errores > 0) Text('Errores: $_errores', style: EstilosDeMinijuego.estado),
      ],
    );
  }
}
