import 'package:flutter/material.dart';

/// Tamaños de texto compartidos por todos los minijuegos. Antes cada uno usaba
/// el estilo por defecto (muy chico para leerlo de reojo mientras se vigila la
/// pantalla de la PC); centralizarlos permite agrandarlos todos a la vez.
class EstilosDeMinijuego {
  EstilosDeMinijuego._();

  static const TextStyle titulo = TextStyle(fontSize: 36, fontWeight: FontWeight.bold);
  static const TextStyle estado = TextStyle(fontSize: 28, fontWeight: FontWeight.w600);
  static const TextStyle detalle = TextStyle(fontSize: 24);
}

/// Título del minijuego y tiempo restante, con el mismo tamaño en todos.
class EncabezadoDeMinijuego extends StatelessWidget {
  final String titulo;
  final int segundosRestantes;

  const EncabezadoDeMinijuego({
    super.key,
    required this.titulo,
    required this.segundosRestantes,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(titulo, style: EstilosDeMinijuego.titulo, textAlign: TextAlign.center),
        const SizedBox(height: 4),
        Text('Tiempo restante: $segundosRestantes s', style: EstilosDeMinijuego.estado),
      ],
    );
  }
}
