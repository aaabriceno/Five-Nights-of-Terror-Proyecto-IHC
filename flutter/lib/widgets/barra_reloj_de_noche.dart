import 'package:flutter/material.dart';
import '../utils/colors.dart';

/// Muestra la noche actual (1-5) y la hora simulada del reloj (12:00 AM ->
/// 6:00 AM). Puramente presentacional — GameScreen le pasa los valores
/// leídos de GameProvider.session.
class BarraRelojDeNoche extends StatelessWidget {
  final int nocheActual;
  final String horaEnJuego;

  const BarraRelojDeNoche({
    super.key,
    required this.nocheActual,
    required this.horaEnJuego,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      decoration: BoxDecoration(
        color: AppColors.panel,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.panelBorde),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              const Icon(Icons.nightlight_round, size: 20, color: AppColors.acento),
              const SizedBox(width: 8),
              Text(
                'Noche $nocheActual/5',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ],
          ),
          Row(
            children: [
              const Icon(Icons.access_time, size: 20, color: AppColors.acento),
              const SizedBox(width: 8),
              Text(horaEnJuego, style: Theme.of(context).textTheme.titleMedium),
            ],
          ),
        ],
      ),
    );
  }
}
