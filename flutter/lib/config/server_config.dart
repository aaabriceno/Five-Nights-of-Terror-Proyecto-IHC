class ServerConfig {
  // Backend Python no existe todavía — usar mock hasta que el equipo lo entregue.
  static const bool useMock = true;

  static const String host = '192.168.1.100';
  static const int port = 8000;

  static String get wsUrl => 'ws://$host:$port';
}
