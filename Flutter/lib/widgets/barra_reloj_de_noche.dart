import 'package:flutter/material.dart';
import '../utils/colors.dart';
import '../utils/constants.dart';

/// Muestra la noche actual ("Noche 3/6") y la hora simulada del reloj
/// (12 AM -> 6 AM). El total sale de [AppConstants.totalNoches], que debe ser el
/// mismo número de noches que tiene Unity.
///
/// Los minutos llegan en el mensaje pero no se muestran: un número que
/// cambia todo el tiempo atrae la mirada a la tablet, que es justo la
/// atención que el juego quiere disputar entre pantalla y tablet. Mostrando
/// solo la hora, el reloj cambia 6 veces por noche y se lee de un vistazo.
/// Puramente presentacional — GameScreen le pasa los valores leídos de
/// GameProvider.session.
class BarraRelojDeNoche extends StatelessWidget {
  final int nocheActual;
  final String horaEnJuego;

  static const TextStyle _estiloGrande =
      TextStyle(fontSize: 32, fontWeight: FontWeight.bold);

  const BarraRelojDeNoche({
    super.key,
    required this.nocheActual,
    required this.horaEnJuego,
  });

  /// Convierte "3:47 AM" en "3 AM". Si el texto no trae minutos, lo deja
  /// como está.
  String _soloLaHora(String hora) {
    final List<String> partes = hora.split(':');
    if (partes.length < 2) return hora;
    final List<String> restoTrasMinutos = partes[1].split(' ');
    if (restoTrasMinutos.length < 2) return partes[0];
    return '${partes[0]} ${restoTrasMinutos[1]}';
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
      decoration: BoxDecoration(
        color: AppColors.panel,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.panelBorde),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              const Icon(Icons.nightlight_round, size: 36, color: AppColors.acento),
              const SizedBox(width: 12),
              Text(
                'Noche $nocheActual/${AppConstants.totalNoches}',
                style: _estiloGrande,
              ),
            ],
          ),
          Row(
            children: [
              const Icon(Icons.access_time, size: 36, color: AppColors.acento),
              const SizedBox(width: 12),
              Text(
                _soloLaHora(horaEnJuego),
                style: _estiloGrande,
              ),
            ],
          ),
        ],
      ),
    );
  }
}
