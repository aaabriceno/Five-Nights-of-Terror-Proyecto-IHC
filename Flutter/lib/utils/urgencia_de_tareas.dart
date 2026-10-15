import '../models/task.dart';

/// Qué tan urgente es una tarea pendiente, según cuánto lleva sin resolverse.
/// Lo usan el menú de tareas (color, icono y texto) y el servicio de avisos
/// (vibración), para que ambos coincidan en el momento exacto del cambio.
enum NivelDeUrgencia { normal, ambar, roja }

const Duration umbralAmbar = Duration(seconds: 20);
const Duration umbralRojo = Duration(seconds: 40);

/// La antigüedad se calcula con `Task.createdAt` (la hora que mandó Unity), sin
/// ningún dato nuevo del servidor.
NivelDeUrgencia nivelDeUrgencia(Task tarea, DateTime ahora) {
  final Duration antiguedad = ahora.difference(tarea.createdAt);
  if (antiguedad >= umbralRojo) return NivelDeUrgencia.roja;
  if (antiguedad >= umbralAmbar) return NivelDeUrgencia.ambar;
  return NivelDeUrgencia.normal;
}
