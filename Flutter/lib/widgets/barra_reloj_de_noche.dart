import 'package:flutter/material.dart';
import '../utils/colors.dart';

/// Muestra la noche actual y la hora simulada del reloj (12 AM -> 6 AM).
/// Hay 5 noches base más una 6ta noche extra ("Custom Night", mucho más
/// difícil, no cuenta como parte de las 5 base) — por eso el texto no fija
/// el total ("Noche 6/5" se vería mal), solo dice "Noche X" seguido de
/// "(Extra)" cuando corresponde a esa 6ta noche.
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
                nocheActual > 5 ? 'Noche $nocheActual (Extra)' : 'Noche $nocheActual/5',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ],
          ),
          Row(
            children: [
              const Icon(Icons.access_time, size: 20, color: AppColors.acento),
              const SizedBox(width: 8),
              Text(
                _soloLaHora(horaEnJuego),
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ],
          ),
        ],
      ),
    );
  }
}
