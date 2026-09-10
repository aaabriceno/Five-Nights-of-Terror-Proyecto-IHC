class Task {
  final int taskId;
  final String taskType;
  final int duration;
  final String description;
  final int difficulty;
  final DateTime createdAt;
  final Map<String, dynamic> params;

  Task({
    required this.taskId,
    required this.taskType,
    required this.duration,
    required this.description,
    required this.difficulty,
    required this.createdAt,
    required this.params,
  });

  factory Task.fromJson(Map<String, dynamic> json) {
    return Task(
      taskId: json['task_id'] as int,
      taskType: json['task_type'] as String,
      duration: json['duration'] as int,
      description: json['description'] as String,
      difficulty: json['difficulty'] as int,
      createdAt: DateTime.fromMillisecondsSinceEpoch(json['timestamp'] as int),
      params: (json['task_params'] as Map<String, dynamic>?) ?? {},
    );
  }
}
