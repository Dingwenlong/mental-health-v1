class UserFollowUp {
  const UserFollowUp({
    required this.id,
    required this.status,
    required this.dueAt,
    required this.deadline,
    this.conflictCode,
  });

  final String id;
  final String status;
  final DateTime? dueAt;
  final DateTime? deadline;
  final String? conflictCode;

  factory UserFollowUp.fromJson(Map<String, dynamic> json) => UserFollowUp(
    id: json['id'].toString(),
    status: json['status'].toString(),
    dueAt: _date(json['dueAt']),
    deadline: _date(json['deadline']),
    conflictCode: json['conflictCode']?.toString(),
  );

  static DateTime? _date(dynamic value) =>
      value == null ? null : DateTime.parse(value.toString()).toLocal();
}
