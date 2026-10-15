import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/services/servicio_de_avisos.dart';

class VibradorFalso implements Vibrador {
  VibradorFalso({this.tiene = true});
  final bool tiene;
  final List<List<int>> patrones = <List<int>>[];
  int cancelaciones = 0;

  @override
  Future<bool> get tieneVibrador async => tiene;

  @override
  Future<void> vibrar(List<int> patron) async => patrones.add(patron);

  @override
  Future<void> cancelar() async => cancelaciones++;
}

void main() {
  final DateTime t0 = DateTime(2026, 10, 15, 12);

  Task tarea(int id, {required int segundosPendiente}) => Task(
        taskId: id,
        taskType: 'cables',
        duration: 30,
        description: 'x',
        difficulty: 1,
        createdAt: t0.subtract(Duration(seconds: segundosPendiente)),
        params: const {},
      );

  // Cuánto vibra el motor en total (los pares de la lista son espera, vibra...).
  int msVibrando(List<int> patron) {
    int total = 0;
    for (int i = 1; i < patron.length; i += 2) {
      total += patron[i];
    }
    return total;
  }

  int pulsos(List<int> patron) => patron.length ~/ 2;

  late VibradorFalso vibrador;
  late ServicioDeAvisos servicio;

  setUp(() async {
    vibrador = VibradorFalso();
    servicio = ServicioDeAvisos(vibrador: vibrador);
    await servicio.iniciar();
  });

  group('caja musical en peligro (< 20 %)', () {
    test('vibra fuerte ("trrr": varios pulsos rápidos, más de medio segundo) al entrar en peligro', () {
      servicio.revisar(ahora: t0, cajaEnPeligro: true, tareas: const []);
      expect(vibrador.patrones, hasLength(1));
      expect(pulsos(vibrador.patrones.first), greaterThanOrEqualTo(4));
      expect(msVibrando(vibrador.patrones.first), greaterThanOrEqualTo(600));
    });

    test('se repite cada 4 s mientras siga en peligro, no antes', () {
      servicio.revisar(ahora: t0, cajaEnPeligro: true, tareas: const []);
      servicio.revisar(ahora: t0.add(const Duration(seconds: 2)), cajaEnPeligro: true, tareas: const []);
      expect(vibrador.patrones, hasLength(1));
      servicio.revisar(ahora: t0.add(const Duration(seconds: 4)), cajaEnPeligro: true, tareas: const []);
      expect(vibrador.patrones, hasLength(2));
    });

    test('al restaurar la caja deja de vibrar y cancela la vibración en curso', () {
      servicio.revisar(ahora: t0, cajaEnPeligro: true, tareas: const []);
      servicio.revisar(ahora: t0.add(const Duration(seconds: 1)), cajaEnPeligro: false, tareas: const []);
      expect(vibrador.cancelaciones, 1);
      servicio.revisar(ahora: t0.add(const Duration(seconds: 9)), cajaEnPeligro: false, tareas: const []);
      expect(vibrador.patrones, hasLength(1));
    });

    test('si vuelve a entrar en peligro, vibra de inmediato otra vez', () {
      servicio.revisar(ahora: t0, cajaEnPeligro: true, tareas: const []);
      servicio.revisar(ahora: t0.add(const Duration(seconds: 1)), cajaEnPeligro: false, tareas: const []);
      servicio.revisar(ahora: t0.add(const Duration(seconds: 2)), cajaEnPeligro: true, tareas: const []);
      expect(vibrador.patrones, hasLength(2));
    });

    test('sin peligro no vibra nunca', () {
      for (int s = 0; s < 30; s++) {
        servicio.revisar(ahora: t0.add(Duration(seconds: s)), cajaEnPeligro: false, tareas: const []);
      }
      expect(vibrador.patrones, isEmpty);
      expect(vibrador.cancelaciones, 0, reason: 'no cancela lo que no empezó');
    });
  });

  group('tarea pendiente que pasa a rojo (40 s)', () {
    test('a los 39 s no avisa; a los 41 s da un "trin" corto, una sola vez', () {
      servicio.revisar(ahora: t0, cajaEnPeligro: false, tareas: <Task>[tarea(1, segundosPendiente: 39)]);
      expect(vibrador.patrones, isEmpty);
      servicio.revisar(ahora: t0, cajaEnPeligro: false, tareas: <Task>[tarea(1, segundosPendiente: 41)]);
      expect(vibrador.patrones, hasLength(1));
      expect(pulsos(vibrador.patrones.first), 1);
      expect(msVibrando(vibrador.patrones.first), inInclusiveRange(150, 450));
      // Sigue roja en la revisión siguiente: no vuelve a vibrar.
      servicio.revisar(
          ahora: t0.add(const Duration(seconds: 1)),
          cajaEnPeligro: false,
          tareas: <Task>[tarea(1, segundosPendiente: 42)]);
      expect(vibrador.patrones, hasLength(1));
    });

    test('una tarea en ámbar (25 s) no avisa', () {
      servicio.revisar(ahora: t0, cajaEnPeligro: false, tareas: <Task>[tarea(1, segundosPendiente: 25)]);
      expect(vibrador.patrones, isEmpty);
    });

    test('cada tarea que se pone roja avisa una vez', () {
      servicio.revisar(ahora: t0, cajaEnPeligro: false, tareas: <Task>[tarea(1, segundosPendiente: 41)]);
      servicio.revisar(
        ahora: t0.add(const Duration(seconds: 1)),
        cajaEnPeligro: false,
        tareas: <Task>[tarea(1, segundosPendiente: 42), tarea(2, segundosPendiente: 41)],
      );
      expect(vibrador.patrones, hasLength(2));
    });
  });

  test('si la caja está en peligro y a la vez una tarea se pone roja, manda el aviso de la caja', () {
    servicio.revisar(ahora: t0, cajaEnPeligro: true, tareas: <Task>[tarea(1, segundosPendiente: 50)]);
    expect(vibrador.patrones, hasLength(1));
    expect(pulsos(vibrador.patrones.first), greaterThanOrEqualTo(4), reason: 'el patrón fuerte de la caja');
  });

  test('sin motor de vibración no hace nada ni falla', () async {
    final VibradorFalso sin = VibradorFalso(tiene: false);
    final ServicioDeAvisos s = ServicioDeAvisos(vibrador: sin);
    await s.iniciar();
    s.revisar(ahora: t0, cajaEnPeligro: true, tareas: <Task>[tarea(1, segundosPendiente: 50)]);
    expect(sin.patrones, isEmpty);
  });
}
