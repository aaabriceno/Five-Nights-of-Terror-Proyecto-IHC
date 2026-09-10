class AppConstants {
  static const int reconnectMaxAttempts = 5;
  static const Duration reconnectDelay = Duration(seconds: 3);
  static const Duration connectTimeout = Duration(seconds: 10);
}
