class AppConstants {
  /// Noches del juego. Debe coincidir con `MaxNight` en Unity
  /// (UnityGameSessionController.cs) y con `totalNoches` del mock.
  static const int totalNoches = 6;
  static const int reconnectMaxAttempts = 5;
  static const Duration reconnectDelay = Duration(seconds: 3);

  /// Cuánto espera la tablet la primera respuesta del juego (Unity la manda en
  /// menos de un segundo) antes de dar por perdida la conexión.
  static const Duration connectTimeout = Duration(seconds: 8);
}
