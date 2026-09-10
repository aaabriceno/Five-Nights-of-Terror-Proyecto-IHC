import 'package:flutter/material.dart';
import '../utils/colors.dart';

/// Muestra el riesgo global de ataque (0-100%). A diferencia de una
/// barra de vida, AQUÍ SUBIR ES PELIGRO: 0% es seguro, 100% significa
/// que el próximo roll de ataque del servidor casi con certeza mata al
/// jugador. Ver docs/superpowers/specs/2026-09-09-sistema-de-riesgo-y-tareas-nuevas-design.md.
class BarraDeRiesgo extends StatelessWidget {
  final int riesgo;

  const BarraDeRiesgo({super.key, required this.riesgo});

  Color _colorPorRiesgo() {
    if (riesgo < 34) return AppColors.exito;
    if (riesgo < 67) return AppColors.advertencia;
    return AppColors.peligro;
  }

  @override
  Widget build(BuildContext context) {
    final double fraccion = (riesgo.clamp(0, 100)) / 100;
    final Color color = _colorPorRiesgo();

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: AppColors.panel,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.panelBorde),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.warning_amber_rounded, size: 18, color: color),
              const SizedBox(width: 8),
              Text(
                'Riesgo: $riesgo%',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ],
          ),
          const SizedBox(height: 8),
          ClipRRect(
            borderRadius: BorderRadius.circular(4),
            child: LinearProgressIndicator(
              value: fraccion,
              minHeight: 12,
              backgroundColor: AppColors.panelBorde,
              color: color,
            ),
          ),
        ],
      ),
    );
  }
}
