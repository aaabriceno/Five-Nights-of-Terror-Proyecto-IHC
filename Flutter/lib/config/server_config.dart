import 'package:shared_preferences/shared_preferences.dart';

/// Configuración de conexión con la PC, persistida en el dispositivo
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

  /// "192.168.1.100:8000": a dónde va a buscar el juego la tablet.
  static String get direccion => '$host:$port';

  static final RegExp _ipv4 = RegExp(
    r'^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}$',
  );

  /// True si `texto` es una IPv4 completa (cuatro números de 0 a 255). Los
  /// espacios alrededor no cuentan.
  static bool esIpValida(String texto) => _ipv4.hasMatch(texto.trim());

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
