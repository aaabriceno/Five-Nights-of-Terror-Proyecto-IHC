import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:web_socket_channel/web_socket_channel.dart';
import '../utils/logger.dart';

class WebSocketService {
  WebSocketChannel? _channel;
  final StreamController<Map<String, dynamic>> _messageController =
      StreamController<Map<String, dynamic>>.broadcast();

  Stream<Map<String, dynamic>> get messages => _messageController.stream;

  void connect(String url) {
    try {
      _channel = WebSocketChannel.connect(Uri.parse(url));
      _channel!.stream.listen(
        procesarCrudo,
        onError: (Object error) {
          appLogger.e('WebSocket error: $error');
          _messageController.addError(error);
        },
        onDone: () {
          appLogger.w('WebSocket connection closed');
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
    _channel?.sink.close();
    _channel = null;
  }

  void dispose() {
    disconnect();
    _messageController.close();
  }
}
