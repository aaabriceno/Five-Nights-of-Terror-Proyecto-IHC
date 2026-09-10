import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import '../models/task.dart';

/// Minijuego "Encender Ventiladores": el jugador debe ingresar un PIN de
/// 4 dígitos (mostrado en pantalla) usando un teclado numérico táctil.
class VentiladoresGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, Map<String, dynamic> datos) onComplete;

  const VentiladoresGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<VentiladoresGameWidget> createState() =>
      _VentiladoresGameWidgetState();
}

class _VentiladoresGameWidgetState extends State<VentiladoresGameWidget> {
  late String _pinObjetivo;
  String _pinIngresado = '';
  int _segundosRestantes = 15;
  Timer? _contadorCuentaRegresiva;
  bool _finalizado = false;

  @override
  void initState() {
    super.initState();
    final Random random = Random();
    _pinObjetivo = List<int>.generate(4, (_) => random.nextInt(10)).join();
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
    widget.onComplete(exito, {'pin_correcto': _pinObjetivo});
  }

  void _alPresionarDigito(int digito) {
    if (_finalizado || _pinIngresado.length >= 4) return;
    setState(() => _pinIngresado += digito.toString());
    if (_pinIngresado.length == 4) {
      if (_pinIngresado == _pinObjetivo) {
        _finalizar(exito: true);
      } else {
        _finalizar(exito: false);
      }
    }
  }

  void _borrarUltimoDigito() {
    if (_pinIngresado.isEmpty) return;
    setState(() => _pinIngresado = _pinIngresado.substring(0, _pinIngresado.length - 1));
  }

  Widget _buildBotonDigito(int digito) {
    return SizedBox(
      width: 64,
      height: 64,
      child: ElevatedButton(
        onPressed: () => _alPresionarDigito(digito),
        child: Text('$digito', style: const TextStyle(fontSize: 20)),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text('Encender Ventiladores', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        Text('Tiempo restante: $_segundosRestantes s'),
        const SizedBox(height: 16),
        Text('Código: $_pinObjetivo', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 16),
        Text(
          _pinIngresado.padRight(4, '_').split('').join(' '),
          style: const TextStyle(fontSize: 28, letterSpacing: 4),
        ),
        const SizedBox(height: 24),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          alignment: WrapAlignment.center,
          children: [
            for (int fila = 0; fila < 3; fila++)
              for (int columna = 0; columna < 3; columna++)
                _buildBotonDigito(fila * 3 + columna + 1),
            SizedBox(
              width: 64,
              height: 64,
              child: OutlinedButton(
                onPressed: _borrarUltimoDigito,
                child: const Icon(Icons.backspace),
              ),
            ),
            _buildBotonDigito(0),
          ],
        ),
      ],
    );
  }
}
