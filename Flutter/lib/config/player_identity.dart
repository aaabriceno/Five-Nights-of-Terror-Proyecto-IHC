import 'dart:math';

import 'package:shared_preferences/shared_preferences.dart';

/// Identificador único del jugador, generado una sola vez por instalación
/// y persistido en el dispositivo (SharedPreferences). Reemplaza el
/// literal fijo 'player_1' que se usaba antes en el mensaje `connect` —
/// necesario para que el servidor pueda distinguir una reconexión del
/// mismo jugador de una conexión de un jugador nuevo (`reconnect_success`).
class PlayerIdentity {
  static const String _clavePlayerId = 'player_id';

  static String playerId = '';

  static Future<void> cargar() async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    String? guardado = prefs.getString(_clavePlayerId);
    guardado ??= _generarNuevoId();
    playerId = guardado;
    await prefs.setString(_clavePlayerId, guardado);
  }

  static String _generarNuevoId() {
    final Random random = Random.secure();
    final String sufijo = List<int>.generate(16, (_) => random.nextInt(16))
        .map((n) => n.toRadixString(16))
        .join();
    return 'player_$sufijo';
  }
}
