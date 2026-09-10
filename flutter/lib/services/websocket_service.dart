import 'dart:async';
import 'dart:convert';
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
        (dynamic raw) {
          final Map<String, dynamic> decoded =
              jsonDecode(raw as String) as Map<String, dynamic>;
          _messageController.add(decoded);
        },
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
