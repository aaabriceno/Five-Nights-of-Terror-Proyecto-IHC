import 'package:flutter/material.dart' hide ConnectionState;
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/config/server_config.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/utils/constants.dart';

import 'ayudas/servicio_falso.dart';

/// "Conectado" no debe aparecer porque la app *intentó* conectarse, sino porque
/// el juego de la PC respondió. Si la IP está mal o el juego está cerrado, el
/// jugador tiene que enterarse.
void main() {
  late WebSocketFalso ws;
  late ConnectionProvider conexion;
  late List<Map<String, dynamic>> recibidos;

  const Map<String, dynamic> respuestaDeUnity = <String, dynamic>{
    'type': 'night_status',
    'night': 1,
    'in_game_time': '12:00 AM',
    'risk_percent': 0,
  };

  setUp(() {
    ServerConfig.useMock = false;
    ws = WebSocketFalso();
    recibidos = <Map<String, dynamic>>[];
    conexion = ConnectionProvider(wsService: ws)..onMessage = recibidos.add;
  });

  tearDown(() {
    conexion.disconnect();
    ServerConfig.useMock = true;
  });

  test('el tiempo de espera es de 8 s', () {
    expect(AppConstants.connectTimeout, const Duration(seconds: 8));
  });

  testWidgets('al conectar queda "conectando", NO "conectado"', (tester) async {
    conexion.connect(modo: 'nuevo');
    expect(conexion.state, ConnectionState.connecting);
    expect(ws.enviados.first['type'], 'connect');
    await tester.pump(const Duration(seconds: 2));
    expect(conexion.state, ConnectionState.connecting, reason: 'sin respuesta sigue esperando');
    conexion.disconnect();
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('pasa a conectado cuando llega el primer mensaje de Unity, y lo reenvía', (tester) async {
    conexion.connect();
    await tester.pump(const Duration(seconds: 1));
    ws.llega(respuestaDeUnity);
    await tester.pump();
    expect(conexion.state, ConnectionState.connected);
    expect(recibidos, <Map<String, dynamic>>[respuestaDeUnity]);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('sin respuesta en 8 s: error, y corta el socket', (tester) async {
    conexion.connect();
    await tester.pump(const Duration(seconds: 7));
    expect(conexion.state, ConnectionState.connecting);
    await tester.pump(const Duration(seconds: 2));
    expect(conexion.state, ConnectionState.error);
    expect(ws.desconexiones, greaterThanOrEqualTo(1));
    // No se queda reintentando en silencio.
    await tester.pump(const Duration(seconds: 30));
    expect(conexion.state, ConnectionState.error);
    expect(ws.conexiones, 1);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('si responde a los 7 s, el tiempo de espera ya no lo tumba después', (tester) async {
    conexion.connect();
    await tester.pump(const Duration(seconds: 7));
    ws.llega(respuestaDeUnity);
    await tester.pump(const Duration(seconds: 20));
    expect(conexion.state, ConnectionState.connected);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('conexión rechazada al conectar: error de inmediato, sin reintentos de 15 s', (tester) async {
    conexion.connect();
    ws.falla();
    await tester.pump();
    expect(conexion.state, ConnectionState.error);
    await tester.pump(const Duration(seconds: 30));
    expect(ws.conexiones, 1);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('si ya estaba conectado y el socket se cae: reconecta (como antes)', (tester) async {
    conexion.connect();
    ws.llega(respuestaDeUnity);
    await tester.pump();
    ws.seCierra();
    await tester.pump();
    expect(conexion.state, ConnectionState.reconnecting);
    await tester.pump(AppConstants.reconnectDelay + const Duration(milliseconds: 100));
    expect(ws.conexiones, 2, reason: 'volvió a intentar');
    conexion.disconnect();
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('disconnect() cancela la espera: no pasa a error después', (tester) async {
    conexion.connect();
    conexion.disconnect();
    await tester.pump(const Duration(seconds: 30));
    expect(conexion.state, ConnectionState.idle);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('en modo simulado conecta al instante (no hay red de por medio)', (tester) async {
    ServerConfig.useMock = true;
    final ConnectionProvider simulado = ConnectionProvider(wsService: ws);
    simulado.connect();
    expect(simulado.state, ConnectionState.connected);
    simulado.disconnect();
    await tester.pumpWidget(const SizedBox());
  });
}
