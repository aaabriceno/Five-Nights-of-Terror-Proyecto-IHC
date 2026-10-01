import 'dart:async';
import 'package:flutter/material.dart';
import '../models/task.dart';

/// Minijuego "Reiniciar WiFi": el jugador mantiene presionado un botón
/// unos segundos (simula sostener el botón físico del router), luego
/// una barra de progreso avanza sola hasta reconectar.
class WifiGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, Map<String, dynamic> datos) onComplete;

  const WifiGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<WifiGameWidget> createState() => _WifiGameWidgetState();
}

class _WifiGameWidgetState extends State<WifiGameWidget> {
  static const Duration _duracionMantenerPresionado = Duration(seconds: 3);
  static const Duration _duracionReconexion = Duration(seconds: 5);

  int _segundosRestantes = 20;
  Timer? _contadorCuentaRegresiva;
  Timer? _temporizadorMantenerPresionado;
  Timer? _temporizadorReconexion;
  bool _botonPresionado = false;
  double _progresoPresion = 0;
  double _progresoReconexion = 0;
  bool _reconectando = false;
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
    _temporizadorMantenerPresionado?.cancel();
    _temporizadorReconexion?.cancel();
    super.dispose();
  }

  void _finalizar({required bool exito}) {
    if (_finalizado) return;
    _finalizado = true;
    _contadorCuentaRegresiva?.cancel();
    _temporizadorMantenerPresionado?.cancel();
    _temporizadorReconexion?.cancel();
    widget.onComplete(exito, {'reconectado': exito});
  }

  void _alPresionar() {
    if (_reconectando || _finalizado) return;
    _botonPresionado = true;
    const int pasosTotales = 30;
    int pasoActual = 0;
    _temporizadorMantenerPresionado = Timer.periodic(
      _duracionMantenerPresionado ~/ pasosTotales,
      (temporizador) {
        if (!_botonPresionado) {
          temporizador.cancel();
          setState(() => _progresoPresion = 0);
          return;
        }
        pasoActual++;
        setState(() => _progresoPresion = pasoActual / pasosTotales);
        if (pasoActual >= pasosTotales) {
          temporizador.cancel();
          _iniciarReconexion();
        }
      },
    );
  }

  void _alSoltar() {
    _botonPresionado = false;
  }

  void _iniciarReconexion() {
    setState(() => _reconectando = true);
    const int pasosTotales = 50;
    int pasoActual = 0;
    _temporizadorReconexion = Timer.periodic(
      _duracionReconexion ~/ pasosTotales,
      (temporizador) {
        pasoActual++;
        setState(() => _progresoReconexion = pasoActual / pasosTotales);
        if (pasoActual >= pasosTotales) {
          temporizador.cancel();
          _finalizar(exito: true);
        }
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text('Reiniciar WiFi', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        Text('Tiempo restante: $_segundosRestantes s'),
        const SizedBox(height: 32),
        if (!_reconectando) ...[
          const Text('Mantén presionado para reiniciar el router'),
          const SizedBox(height: 16),
          GestureDetector(
            onTapDown: (_) => _alPresionar(),
            onTapUp: (_) => _alSoltar(),
            onTapCancel: () => _alSoltar(),
            child: Container(
              width: 120,
              height: 120,
              decoration: const BoxDecoration(
                shape: BoxShape.circle,
                color: Colors.blueGrey,
              ),
              child: Icon(Icons.wifi, size: 48, color: Colors.white),
            ),
          ),
          const SizedBox(height: 16),
          LinearProgressIndicator(value: _progresoPresion),
        ] else ...[
          const Text('Reconectando...'),
          const SizedBox(height: 16),
          LinearProgressIndicator(value: _progresoReconexion),
        ],
      ],
    );
  }
}
