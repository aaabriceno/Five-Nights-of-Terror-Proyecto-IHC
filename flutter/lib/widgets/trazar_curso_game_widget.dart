import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import '../models/task.dart';

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
  static const double _radioToque = 28;

  late List<Offset> _posicionesRelativas;
  int _siguientePuntoEsperado = 0;
  int _segundosRestantes = 20;
  Timer? _contadorCuentaRegresiva;
  bool _finalizado = false;

  @override
  void initState() {
    super.initState();
    final Random random = Random();
    _posicionesRelativas = List<Offset>.generate(
      _cantidadPuntos,
      (_) => Offset(
        0.1 + random.nextDouble() * 0.8,
        0.1 + random.nextDouble() * 0.8,
      ),
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
        Text('Trazar Curso', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text('Tiempo restante: $_segundosRestantes s'),
        const SizedBox(height: 4),
        Text('Conectados: $_siguientePuntoEsperado/$_cantidadPuntos'),
        Expanded(
          child: LayoutBuilder(
            builder: (context, restricciones) {
              return Stack(
                children: List<Widget>.generate(_cantidadPuntos, (i) {
                  final Offset relativa = _posicionesRelativas[i];
                  final bool conectado = i < _siguientePuntoEsperado;
                  final bool esSiguiente = i == _siguientePuntoEsperado;
                  return Positioned(
                    left: relativa.dx * restricciones.maxWidth - _radioToque,
                    top: relativa.dy * restricciones.maxHeight - _radioToque,
                    child: GestureDetector(
                      onTap: () => _alTocarPunto(i),
                      child: Container(
                        width: _radioToque * 2,
                        height: _radioToque * 2,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          color: conectado
                              ? Colors.green
                              : (esSiguiente ? Colors.amber : Colors.grey),
                        ),
                        child: Center(
                          child: Text(
                            '${i + 1}',
                            style: const TextStyle(
                              color: Colors.white,
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
