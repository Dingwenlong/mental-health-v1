import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import '../../generated/ui_copy.g.dart';
import '../../widgets/empty_state_panel.dart';
import 'follow_up_model.dart';
import 'local_reminder_service.dart';

export 'follow_up_model.dart';

abstract interface class FollowUpRepository {
  Future<List<UserFollowUp>> listFollowUps();
}

class ApiFollowUpRepository implements FollowUpRepository {
  const ApiFollowUpRepository(this._client);

  final ApiClient _client;

  @override
  Future<List<UserFollowUp>> listFollowUps() async =>
      (await _client.getList('follow-ups'))
          .map(
            (raw) =>
                UserFollowUp.fromJson(Map<String, dynamic>.from(raw as Map)),
          )
          .toList();
}

class FollowUpListPage extends StatefulWidget {
  const FollowUpListPage({
    required this.repository,
    required this.reminders,
    super.key,
  });

  final FollowUpRepository repository;
  final LocalReminderService reminders;

  @override
  State<FollowUpListPage> createState() => _FollowUpListPageState();
}

class _FollowUpListPageState extends State<FollowUpListPage> {
  List<UserFollowUp> _tasks = const <UserFollowUp>[];
  bool _loading = true;
  bool _failed = false;
  bool _permissionDenied = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: Text(UiCopy.get('followUp.title')),
      actions: <Widget>[
        IconButton(
          tooltip: UiCopy.get('common.refresh'),
          onPressed: _loading ? null : _load,
          icon: const Icon(Icons.refresh),
        ),
      ],
    ),
    body: _buildBody(),
  );

  Widget _buildBody() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_failed) {
      return Center(
        child: FilledButton(
          onPressed: _load,
          child: Text(UiCopy.get('catalog.retry')),
        ),
      );
    }
    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
      children: <Widget>[
        if (_permissionDenied)
          Container(
            margin: const EdgeInsets.only(bottom: 16),
            padding: const EdgeInsets.symmetric(vertical: 12),
            decoration: BoxDecoration(
              border: Border(
                bottom: BorderSide(color: Theme.of(context).dividerColor),
              ),
            ),
            child: Text(UiCopy.get('followUp.permissionDenied')),
          ),
        if (_tasks.isEmpty)
          EmptyStatePanel(message: UiCopy.get('followUp.none'))
        else
          for (final task in _tasks) _FollowUpRow(task: task),
      ],
    );
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _failed = false;
    });
    try {
      final tasks = await widget.repository.listFollowUps();
      var permissionDenied = false;
      for (final task in tasks.where((item) => item.status == 'Scheduled')) {
        if (!await widget.reminders.schedule(task)) permissionDenied = true;
      }
      if (!mounted) return;
      setState(() {
        _tasks = tasks;
        _permissionDenied = permissionDenied;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _failed = true;
      });
    }
  }
}

class _FollowUpRow extends StatelessWidget {
  const _FollowUpRow({required this.task});

  final UserFollowUp task;

  @override
  Widget build(BuildContext context) {
    final dueAt = task.dueAt;
    final conflict = task.conflictCode == 'NO_QUALIFIED_SLOT_BEFORE_SLA';
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 16),
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(color: Theme.of(context).dividerColor),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            _statusName(task.status),
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 6),
          if (dueAt != null)
            Text('${UiCopy.get('followUp.timePrefix')}${_format(dueAt)}'),
          if (conflict)
            Text(
              UiCopy.get('followUp.manualQueue'),
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
        ],
      ),
    );
  }

  static String _statusName(String status) => switch (status) {
    'Proposed' => UiCopy.get('followUp.status.proposed'),
    'Scheduled' => UiCopy.get('followUp.status.scheduled'),
    'Due' => UiCopy.get('followUp.status.due'),
    'Overdue' => UiCopy.get('followUp.status.overdue'),
    'Completed' => UiCopy.get('followUp.status.completed'),
    'Cancelled' => UiCopy.get('followUp.status.cancelled'),
    _ => status,
  };

  static String _format(DateTime value) {
    String two(int number) => number.toString().padLeft(2, '0');
    return '${value.year}${UiCopy.get('date.year')}'
        '${value.month}${UiCopy.get('date.month')}'
        '${value.day}${UiCopy.get('date.day')} '
        '${two(value.hour)}:${two(value.minute)}';
  }
}
