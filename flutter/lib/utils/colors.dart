import 'package:flutter/material.dart';

/// Paleta tipo "sala de vigilancia" (FNAF): fondo casi negro, acento
/// ámbar/dorado como color principal (animatrónicos, luces de alerta),
/// rojo reservado solo para peligro real (vida baja, ataque).
class AppColors {
  static const Color fondo = Color(0xFF0D0D0D);
  static const Color panel = Color(0xFF1A1712);
  static const Color panelBorde = Color(0xFF3A3226);

  static const Color acento = Color(0xFFE0A94A);
  static const Color acentoOscuro = Color(0xFF8A6A2E);

  static const Color peligro = Color(0xFFD32F2F);
  static const Color exito = Color(0xFF4CAF50);
  static const Color advertencia = Color(0xFFE0A94A);

  static const Color textoPrimario = Color(0xFFF2EAD9);
  static const Color textoSecundario = Color(0xFFA79C87);

  // Alias retenidos por compatibilidad semántica con el resto del código.
  static const Color primary = acento;
  static const Color danger = peligro;
  static const Color background = fondo;
}
