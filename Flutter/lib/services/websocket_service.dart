import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:flutter/foundation.dart';
import 'package:web_socket_channel/io.dart';
import 'package:web_socket_channel/web_socket_channel.dart';
import '../utils/logger.dart';

class WebSocketService {
  WebSocketChannel? _channel;
  final StreamController<Map<String, dynamic>> _messageController =
      StreamController<Map<String, dynamic>>.broadcast();

  Stream<Map<String, dynamic>> get messages => _messageController.stream;

  /// Cada cuánto la tablet le manda un ping al relay. Si la respuesta no llega
  /// antes del siguiente, dart:io cierra el socket: así la tablet se entera de
  /// que se quedó sin red (de 3 a 6 s) aunque Android no avise nada.
  static const Duration intervaloDeLatido = Duration(seconds: 3);

  /// Cualquier final del socket que no pidió la app (error, el juego cerró la
  /// conexión, el latido no tuvo respuesta) llega a `messages` como un error,
  /// una sola vez. Antes el cierre solo se anotaba en el log y la tablet seguía
  /// diciendo «Conectado» sin juego del otro lado.
  void connect(String url) {
    try {
      final WebSocketChannel canal = IOWebSocketChannel.connect(
        Uri.parse(url),
        pingInterval: intervaloDeLatido,
      );
      _channel = canal;
      bool avisado = false;
      // Una conexión vieja (ya descartada con disconnect) no avisa nada: su
      // cierre llega tarde y tumbaría a la conexión nueva.
      void avisarCaida(Object motivo) {
        if (avisado || !identical(_channel, canal)) return;
        avisado = true;
        _messageController.addError(motivo);
      }

      canal.stream.listen(
        (dynamic raw) {
          if (identical(_channel, canal)) procesarCrudo(raw);
        },
        onError: (Object error) {
          appLogger.e('WebSocket error: $error');
          avisarCaida(error);
        },
        onDone: () {
          appLogger.w('WebSocket connection closed');
          avisarCaida(const SocketException('Se cerró la conexión con el juego'));
        },
      );
    } catch (e) {
      appLogger.e('Error connecting: $e');
      _messageController.addError(e);
    }
  }

  /// Convierte lo que llega por el socket en un mensaje. Solo pasan los objetos
  /// JSON (`{...}`): un texto que no es JSON, una lista, un número o un marco
  /// binario se ignoran y se anotan, sin tumbar la suscripción. Antes un marco
  /// así lanzaba una excepción dentro del `listen` y la app quedaba sorda.
  @visibleForTesting
  void procesarCrudo(dynamic raw) {
    if (raw is! String) {
      appLogger.w('Se ignora un marco que no es texto: ${raw.runtimeType}');
      return;
    }
    Object? decodificado;
    try {
      decodificado = jsonDecode(raw);
    } on FormatException {
      appLogger.w('Se ignora un mensaje que no es JSON: $raw');
      return;
    }
    if (decodificado is! Map<String, dynamic>) {
      appLogger.w('Se ignora un JSON que no es un objeto: $raw');
      return;
    }
    _messageController.add(decodificado);
  }

  void sendMessage(Map<String, dynamic> data) {
    _channel?.sink.add(jsonEncode(data));
  }

  void disconnect() {
    final WebSocketChannel? canal = _channel;
    _channel = null;            // primero: su cierre ya no cuenta como caída
    canal?.sink.close();
  }

  void dispose() {
    disconnect();
    _messageController.close();
  }
}
