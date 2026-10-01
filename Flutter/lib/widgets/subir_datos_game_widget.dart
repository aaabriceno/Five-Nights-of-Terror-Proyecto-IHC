import 'dart:async';
import 'package:flutter/material.dart';
import '../models/task.dart';

/// Minijuego "Subir Datos": el jugador mantiene el dedo apoyado en la
/// pantalla; mientras lo mantiene, una barra de progreso avanza. Si
/// suelta antes de completarse, el progreso se reinicia.
class SubirDatosGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, Map<String, dynamic> datos) onComplete;

  const SubirDatosGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<SubirDatosGameWidget> createState() => _SubirDatosGameWidgetState();
}

class _SubirDatosGameWidgetState extends State<SubirDatosGameWidget> {
  static const Duration _duracionSubida = Duration(seconds: 6);

  int _segundosRestantes = 12;
  Timer? _contadorCuentaRegresiva;
  Timer? _temporizadorSubida;
  double _progreso = 0;
  bool _finalizado = false;

  @override
  void initState() {
    super.initState();
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
    _temporizadorSubida?.cancel();
    super.dispose();
  }

  void _finalizar({required bool exito}) {
    if (_finalizado) return;
    _finalizado = true;
    _contadorCuentaRegresiva?.cancel();
    _temporizadorSubida?.cancel();
    widget.onComplete(exito, {'progreso_final': _progreso});
  }

  void _alMantenerPresionado() {
    const int pasosTotales = 60;
    int pasoActual = (_progreso * pasosTotales).round();
    _temporizadorSubida = Timer.periodic(_duracionSubida ~/ pasosTotales, (temporizador) {
      pasoActual++;
      setState(() => _progreso = pasoActual / pasosTotales);
      if (_progreso >= 1) {
        temporizador.cancel();
        _finalizar(exito: true);
      }
    });
  }

  void _alSoltar() {
    _temporizadorSubida?.cancel();
    setState(() => _progreso = 0);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text('Subir Datos', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        Text('Tiempo restante: $_segundosRestantes s'),
        const SizedBox(height: 32),
        const Text('Mantén presionado para subir el reporte'),
        const SizedBox(height: 16),
        GestureDetector(
          onTapDown: (_) => _alMantenerPresionado(),
          onTapUp: (_) => _alSoltar(),
          onTapCancel: _alSoltar,
          child: Container(
            width: 140,
            height: 140,
            decoration: const BoxDecoration(
              shape: BoxShape.circle,
              color: Colors.indigo,
            ),
            child: const Icon(Icons.cloud_upload, size: 56, color: Colors.white),
          ),
        ),
        const SizedBox(height: 16),
        LinearProgressIndicator(value: _progreso),
      ],
    );
  }
}
