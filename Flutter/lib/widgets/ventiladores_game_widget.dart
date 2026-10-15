import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import '../models/task.dart';
import 'encabezado_de_minijuego.dart';

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
  State<VentiladoresGameWidget> createState() => _VentiladoresGameWidgetState();
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
    setState(
      () =>
          _pinIngresado = _pinIngresado.substring(0, _pinIngresado.length - 1),
    );
  }

  Widget _buildBotonDigito(int digito, double lado) {
    return SizedBox(
      width: lado,
      height: lado,
      child: ElevatedButton(
        onPressed: () => _alPresionarDigito(digito),
        child: Text(
          '$digito',
          style: TextStyle(fontSize: lado * 0.45, fontWeight: FontWeight.bold),
        ),
      ),
    );
  }

  Widget _buildBotonBorrar(double lado) {
    return SizedBox(
      width: lado,
      height: lado,
      child: OutlinedButton(
        onPressed: _borrarUltimoDigito,
        child: Icon(Icons.backspace, size: lado * 0.42),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, restricciones) {
        // Los botones se agrandan hasta donde alcanza el alto: arriba van el
        // encabezado y la fila del código (~190 px con margen) y abajo 4 filas.
        const double separacion = 12;
        final double escala = MediaQuery.textScalerOf(context).scale(1);
        final double alto = restricciones.maxHeight.isFinite
            ? restricciones.maxHeight
            : 650;
        final double lado = ((alto - 190 * escala) / 4 - separacion).clamp(
          56.0,
          104.0,
        );

        Widget fila(List<Widget> botones) => Padding(
          padding: const EdgeInsets.only(bottom: separacion),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              for (int i = 0; i < botones.length; i++) ...<Widget>[
                if (i > 0) const SizedBox(width: separacion),
                botones[i],
              ],
            ],
          ),
        );

        return Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            EncabezadoDeMinijuego(
              titulo: 'Encender Ventiladores',
              segundosRestantes: _segundosRestantes,
            ),
            const SizedBox(height: 8),
            // El código a copiar y lo que va ingresando el jugador, en la misma
            // fila para dejar más alto a los botones.
            FittedBox(
              fit: BoxFit.scaleDown,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text(
                    'Código: $_pinObjetivo',
                    style: const TextStyle(
                      fontSize: 40,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(width: 56),
                  Text(
                    _pinIngresado.padRight(4, '_').split('').join(' '),
                    style: const TextStyle(
                      fontSize: 40,
                      fontWeight: FontWeight.bold,
                      letterSpacing: 4,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            // Teclado en cuadrícula: 1-2-3 / 4-5-6 / 7-8-9 / borrar-0.
            for (int f = 0; f < 3; f++)
              fila(<Widget>[
                for (int c = 0; c < 3; c++)
                  _buildBotonDigito(f * 3 + c + 1, lado),
              ]),
            fila(<Widget>[
              _buildBotonBorrar(lado),
              _buildBotonDigito(0, lado),
              SizedBox(width: lado, height: lado),
            ]),
          ],
        );
      },
    );
  }
}
