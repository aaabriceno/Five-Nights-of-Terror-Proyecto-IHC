import 'package:shared_preferences/shared_preferences.dart';

/// Configuración de conexión al servidor, persistida en el dispositivo
/// (SharedPreferences) para que el jugador pueda cambiar la IP de la PC
/// desde la pantalla de Opciones sin reinstalar la app. `cargar()` debe
/// llamarse una vez al inicio (antes de MenuPrincipalScreen) para leer
/// los valores guardados; mientras tanto se usan los valores por
/// defecto de abajo.
class ServerConfig {
  static const String _claveUseMock = 'server_use_mock';
  static const String _claveHost = 'server_host';
  static const String _clavePuerto = 'server_port';

  static bool useMock = true;
  static String host = '192.168.1.100';
  static int port = 8000;

  static String get wsUrl => 'ws://$host:$port';

  static Future<void> cargar() async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    useMock = prefs.getBool(_claveUseMock) ?? true;
    host = prefs.getString(_claveHost) ?? host;
    port = prefs.getInt(_clavePuerto) ?? port;
  }

  static Future<void> guardar({
    required bool useMock,
    required String host,
    required int port,
  }) async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_claveUseMock, useMock);
    await prefs.setString(_claveHost, host);
    await prefs.setInt(_clavePuerto, port);
    ServerConfig.useMock = useMock;
    ServerConfig.host = host;
    ServerConfig.port = port;
  }
}
