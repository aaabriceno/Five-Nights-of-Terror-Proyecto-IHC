class AppConstants {
  /// Noches del juego. Debe coincidir con `MaxNight` en Unity
  /// (UnityGameSessionController.cs) y con `totalNoches` del mock.
  static const int totalNoches = 6;
  static const int reconnectMaxAttempts = 5;
  static const Duration reconnectDelay = Duration(seconds: 3);
  static const Duration connectTimeout = Duration(seconds: 10);
}
