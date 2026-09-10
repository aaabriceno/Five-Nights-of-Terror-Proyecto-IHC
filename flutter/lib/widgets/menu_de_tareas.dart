import 'package:flutter/material.dart';
import '../models/task.dart';
import '../utils/colors.dart';

/// Muestra la lista de tareas pendientes de la noche como tarjetas
/// tocables. Al tocar una, llama `alElegirTarea` con esa tarea — la
/// pantalla que use este widget decide qué hacer (normalmente,
/// GameProvider.elegirTarea).
class MenuDeTareas extends StatelessWidget {
  final List<Task> tareas;
  final void Function(Task tarea) alElegirTarea;
  final bool wifiActivo;

  const MenuDeTareas({
    super.key,
    required this.tareas,
    required this.alElegirTarea,
    required this.wifiActivo,
  });

  String _nombreLegible(String taskType) {
    switch (taskType) {
      case 'cables':
        return 'Conectar Cables';
      case 'dials':
        return 'Girar Perillas';
      case 'sequence':
        return 'Resolver Secuencia';
      case 'rhythm':
        return 'Ritmo Crítico';
      case 'wifi':
        return 'Reiniciar WiFi';
      case 'ventiladores':
        return 'Encender Ventiladores';
      case 'temperatura':
        return 'Reparar Temperatura';
      case 'procesar_datos':
        return 'Procesar Datos';
      case 'subir_datos':
        return 'Subir Datos';
      case 'trazar_curso':
        return 'Trazar Curso';
      default:
        return taskType;
    }
  }

  // Ícono provisional por tipo de tarea — se reemplazará por una imagen
  // real cuando estén listos los assets del minijuego correspondiente.
  IconData _iconoPorTipo(String taskType) {
    switch (taskType) {
      case 'cables':
        return Icons.cable;
      case 'dials':
        return Icons.tune;
      case 'sequence':
        return Icons.format_list_numbered;
      case 'rhythm':
        return Icons.graphic_eq;
      case 'wifi':
        return Icons.wifi;
      case 'ventiladores':
        return Icons.air;
      case 'temperatura':
        return Icons.thermostat;
      case 'procesar_datos':
        return Icons.data_usage;
      case 'subir_datos':
        return Icons.cloud_upload;
      case 'trazar_curso':
        return Icons.timeline;
      default:
        return Icons.help_outline;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (tareas.isEmpty) {
      return Center(
        child: Text(
          'Sin tareas pendientes — vigila la pantalla',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
      );
    }

    return GridView.builder(
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        mainAxisSpacing: 12,
        crossAxisSpacing: 12,
        childAspectRatio: 2.2,
      ),
      itemCount: tareas.length,
      itemBuilder: (context, indice) {
        final Task tarea = tareas[indice];
        final bool bloqueadaPorWifi =
            tarea.taskType == 'subir_datos' && !wifiActivo;
        return Opacity(
          opacity: bloqueadaPorWifi ? 0.4 : 1.0,
          child: Card(
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: bloqueadaPorWifi ? null : () => alElegirTarea(tarea),
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Row(
                  children: [
                    Container(
                      width: 56,
                      height: 56,
                      decoration: BoxDecoration(
                        color: AppColors.acento.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Icon(
                        _iconoPorTipo(tarea.taskType),
                        color: AppColors.acento,
                        size: 28,
                      ),
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            _nombreLegible(tarea.taskType),
                            style: Theme.of(context).textTheme.titleMedium,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${tarea.duration}s',
                            style: Theme.of(context).textTheme.bodySmall,
                          ),
                          if (bloqueadaPorWifi)
                            Text(
                              'Requiere WiFi activo',
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}
