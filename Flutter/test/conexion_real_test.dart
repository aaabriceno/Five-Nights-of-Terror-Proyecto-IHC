import 'dart:async';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/services/websocket_service.dart';

/// El `WebSocketService` de verdad contra un servidor WebSocket local. El falso
/// de las otras pruebas cierra su stream y eso escondía que el real, al
/// cerrarse el socket, solo lo anotaba en el log: la tablet seguía diciendo
/// «Conectado» sin juego del otro lado.
void main() {
  late HttpServer servidor;
  late List<WebSocket> delServidor;

  setUp(() async {
    delServidor = <WebSocket>[];
    servidor = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    servidor.listen((HttpRequest pedido) async {
      final WebSocket socket = await WebSocketTransformer.upgrade(pedido);
      delServidor.add(socket);
      socket.listen((_) {});
    });
  });

  tearDown(() async {
    for (final WebSocket socket in delServidor) {
      await socket.close();
    }
    await servidor.close(force: true);
  });

  test('si el juego cierra la conexión, el servicio lo avisa como error', () async {
    final WebSocketService servicio = WebSocketService();
    final Completer<Object> fallo = Completer<Object>();
    servicio.messages.listen((_) {}, onError: (Object e) {
      if (!fallo.isCompleted) fallo.complete(e);
    });
    servicio.connect('ws://127.0.0.1:${servidor.port}');
    while (delServidor.isEmpty) {
      await Future<void>.delayed(const Duration(milliseconds: 20));
    }
    await delServidor.first.close();
    await expectLater(fallo.future.timeout(const Duration(seconds: 3)), completes);
    servicio.dispose();
  });

  test('cerrar a propósito no se avisa como error (ni la conexión vieja pisa a la nueva)', () async {
    final WebSocketService servicio = WebSocketService();
    final List<Object> errores = <Object>[];
    servicio.messages.listen((_) {}, onError: errores.add);
    servicio.connect('ws://127.0.0.1:${servidor.port}');
    while (delServidor.isEmpty) {
      await Future<void>.delayed(const Duration(milliseconds: 20));
    }
    servicio.disconnect();
    servicio.connect('ws://127.0.0.1:${servidor.port}');
    await Future<void>.delayed(const Duration(milliseconds: 600));
    expect(errores, isEmpty);
    servicio.dispose();
  });

  test('si la red se corta sin cerrar el socket, el latido lo detecta en pocos segundos', () async {
    // Proxy TCP entre la tablet y el juego: al «cortar la red» deja de pasar
    // datos sin cerrar nada, como cuando se apaga el hotspot.
    bool cortado = false;
    final ServerSocket proxy = await ServerSocket.bind(InternetAddress.loopbackIPv4, 0);
    final List<Socket> abiertos = <Socket>[];
    proxy.listen((Socket cliente) async {
      final Socket juego = await Socket.connect(InternetAddress.loopbackIPv4, servidor.port);
      abiertos.addAll(<Socket>[cliente, juego]);
      cliente.listen((datos) { if (!cortado) juego.add(datos); }, onError: (_) {});
      juego.listen((datos) { if (!cortado) cliente.add(datos); }, onError: (_) {});
    });

    final WebSocketService servicio = WebSocketService();
    final Completer<void> fallo = Completer<void>();
    servicio.messages.listen((_) {}, onError: (Object _) {
      if (!fallo.isCompleted) fallo.complete();
    });
    servicio.connect('ws://127.0.0.1:${proxy.port}');
    while (delServidor.isEmpty) {
      await Future<void>.delayed(const Duration(milliseconds: 20));
    }
    final Stopwatch reloj = Stopwatch()..start();
    cortado = true;
    await expectLater(fallo.future.timeout(const Duration(seconds: 8)), completes);
    expect(reloj.elapsed, lessThan(const Duration(seconds: 8)));
    servicio.dispose();
    for (final Socket socket in abiertos) {
      socket.destroy();
    }
    await proxy.close();
  }, timeout: const Timeout(Duration(seconds: 20)));
}
