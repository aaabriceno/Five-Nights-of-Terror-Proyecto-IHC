import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/widgets/cable_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/dial_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/procesar_datos_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/puppet_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/rhythm_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/sequence_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/subir_datos_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/temperatura_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/trazar_curso_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/ventiladores_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/wifi_game_widget.dart';

/// El área de juego de la tablet (Lenovo TB350XU, densidad 200) mide unos
/// 1568×700 px lógicos una vez descontadas las barras. Con textos y botones
/// grandes, ningún minijuego debe desbordarse ahí ni en una tablet más chica,
/// ni con el texto del sistema agrandado: esa tablet tiene la escala en 1.15 y
/// otras pueden tenerla más alta, así que se prueba con 1.3.
void main() {
  Task tarea(String tipo) => Task(
        taskId: 1,
        taskType: tipo,
        duration: 90,
        description: 'prueba',
        difficulty: 1,
        createdAt: DateTime(2026),
        params: const {},
      );

  final Map<String, Widget Function()> minijuegos = <String, Widget Function()>{
    'cables': () => CableGameWidget(task: tarea('cables'), onComplete: (_, __) {}),
    'perillas': () => DialGameWidget(task: tarea('dials'), onComplete: (_, __) {}),
    'secuencia': () => SequenceGameWidget(task: tarea('sequence'), onComplete: (_, __, ___) {}),
    'ritmo': () => RhythmGameWidget(task: tarea('rhythm'), onComplete: (_, __) {}),
    'wifi': () => WifiGameWidget(task: tarea('wifi'), onComplete: (_, __) {}),
    'subir datos': () => SubirDatosGameWidget(task: tarea('subir_datos'), onComplete: (_, __) {}),
    'ventiladores': () => VentiladoresGameWidget(task: tarea('ventiladores'), onComplete: (_, __) {}),
    'temperatura': () => TemperaturaGameWidget(task: tarea('temperatura'), onComplete: (_, __) {}),
    'procesar datos': () => ProcesarDatosGameWidget(task: tarea('procesar'), onComplete: (_, __) {}),
    'trazar curso': () => TrazarCursoGameWidget(task: tarea('trazar_curso'), onComplete: (_, __) {}),
    'caja musical': () => PuppetGameWidget(
          enPeligro: true,
          valorCajaPorcentaje: 40,
          alEmpezarASostener: () {},
          alSoltar: () {},
        ),
  };

  for (final Size area in const <Size>[Size(1568, 700), Size(1000, 520)]) {
    for (final MapEntry<String, Widget Function()> m in minijuegos.entries) {
      testWidgets('${m.key} no se desborda en ${area.width.toInt()}×${area.height.toInt()}', (tester) async {
        tester.view.physicalSize = area;
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.reset);
        await tester.pumpWidget(MaterialApp(
          builder: (context, child) => MediaQuery(
            data: MediaQuery.of(context).copyWith(textScaler: const TextScaler.linear(1.3)),
            child: child!,
          ),
          home: Scaffold(body: Center(child: SizedBox.fromSize(size: area, child: m.value()))),
        ));
        await tester.pump(const Duration(milliseconds: 200));
        // Cualquier "RenderFlex overflowed" haría fallar la prueba por sí solo.
        await tester.pumpWidget(const SizedBox()); // cancela temporizadores
      });
    }
  }
}
