import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/services/websocket_service.dart';

/// Un mensaje roto (un relay viejo, un Unity con otra versión, alguien que
/// manda cualquier cosa al puerto) no puede tumbar la partida: se ignora y el
/// siguiente mensaje bueno se procesa igual.
void main() {
  group('GameProvider.handleMessage', () {
    late GameProvider juego;
    late int avisos;

    setUp(() {
      juego = GameProvider();
      avisos = 0;
      juego.addListener(() => avisos++);
    });

    test('sin "type" no falla', () {
      expect(() => juego.handleMessage(<String, dynamic>{'night': 2}), returnsNormally);
    });

    test('con "type" que no es texto no falla', () {
      expect(() => juego.handleMessage(<String, dynamic>{'type': 7}), returnsNormally);
    });

    test('night_status con campos de otro tipo no falla y no cambia la sesión', () {
      juego.handleMessage(<String, dynamic>{'type': 'night_status', 'night': 'dos', 'in_game_time': 3, 'risk_percent': 'alto'});
      expect(juego.session.nocheActual, 1);
      expect(juego.session.horaEnJuego, '12:00 AM');
    });

    test('task_list con una tarea rota se ignora y el siguiente task_list bueno entra', () {
      juego.handleMessage(<String, dynamic>{
        'type': 'task_list',
        'tasks': <dynamic>[
          <String, dynamic>{'task_id': 'uno', 'task_type': 'cables'},
        ],
      });
      expect(juego.session.tareasPendientes, isEmpty);
      juego.handleMessage(<String, dynamic>{
        'type': 'task_list',
        'tasks': <dynamic>[
          <String, dynamic>{
            'task_id': 1,
            'task_type': 'cables',
            'duration': 30,
            'description': 'x',
            'difficulty': 1,
            'timestamp': DateTime.now().millisecondsSinceEpoch,
          },
        ],
      });
      expect(juego.session.tareasPendientes.length, 1);
    });

    test('attack sin campos no falla ni termina la partida', () {
      juego.handleMessage(<String, dynamic>{'type': 'attack'});
      expect(juego.isGameOver, isFalse);
    });

    test('un mensaje roto no avisa a las pantallas; uno bueno sí', () {
      juego.handleMessage(<String, dynamic>{'type': 'night_status', 'night': 'dos'});
      expect(avisos, 0);
      juego.handleMessage(<String, dynamic>{'type': 'night_status', 'night': 2, 'in_game_time': '1:00 AM', 'risk_percent': 10});
      expect(avisos, 1);
      expect(juego.session.nocheActual, 2);
    });
  });

  group('WebSocketService.procesarCrudo', () {
    test('solo pasa los objetos JSON; lo demás se ignora sin fallar', () async {
      final WebSocketService servicio = WebSocketService();
      final List<Map<String, dynamic>> recibidos = <Map<String, dynamic>>[];
      servicio.messages.listen(recibidos.add);
      for (final dynamic crudo in <dynamic>['no es json', '[1, 2]', '42', 42, null, <int>[1, 2], '"texto"']) {
        expect(() => servicio.procesarCrudo(crudo), returnsNormally, reason: '$crudo');
      }
      servicio.procesarCrudo('{"type": "night_status", "night": 3}');
      await Future<void>.delayed(Duration.zero);
      expect(recibidos, <Map<String, dynamic>>[
        <String, dynamic>{'type': 'night_status', 'night': 3},
      ]);
      servicio.dispose();
    });
  });
}
