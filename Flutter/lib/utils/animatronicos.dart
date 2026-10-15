import 'package:flutter/material.dart';
import 'colors.dart';

/// Cómo se presenta cada animatrónico en la tablet cuando te atrapa: nombre,
/// un icono propio (se distinguen por forma, no solo por color) y una línea que
/// explica qué hizo que apareciera, para que el jugador entienda qué falló.
class InfoDeAnimatronico {
  final String nombre;
  final IconData icono;
  final Color color;
  final String motivo;

  const InfoDeAnimatronico({
    required this.nombre,
    required this.icono,
    required this.color,
    required this.motivo,
  });
}

const InfoDeAnimatronico _generico = InfoDeAnimatronico(
  nombre: 'UN ANIMATRÓNICO',
  icono: Icons.warning_amber_rounded,
  color: AppColors.peligro,
  motivo: 'Te encontró.',
);

/// [nombre] es el que manda Unity en el mensaje `attack` (el
/// `nombreAnimatronico` del animatrónico). Un nombre ausente o desconocido da
/// una presentación genérica en vez de fallar.
InfoDeAnimatronico infoDeAnimatronico(String? nombre) {
  switch (nombre?.trim().toLowerCase()) {
    case 'freddy':
      return const InfoDeAnimatronico(
        nombre: 'FREDDY',
        icono: Icons.visibility_off,
        color: Color(0xFFD35400),
        motivo: 'Se acerca cuando no miras la pantalla de la computadora.',
      );
    case 'vixy':
      return const InfoDeAnimatronico(
        nombre: 'VIXY',
        icono: Icons.assignment_late,
        color: Color(0xFFC2185B),
        motivo: 'Avanza cuando dejas tareas pendientes.',
      );
    case 'puppet':
      return const InfoDeAnimatronico(
        nombre: 'PUPPET',
        icono: Icons.music_off,
        color: Color(0xFF7B1FA2),
        motivo: 'Sale de su caja cuando la caja musical se queda sin cuerda.',
      );
    default:
      return _generico;
  }
}
