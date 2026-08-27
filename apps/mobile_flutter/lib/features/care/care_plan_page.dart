import 'package:flutter/material.dart';

import 'care_repository.dart';
import 'care_widgets.dart';
import 'check_in_pages.dart';
import 'exercise_pages.dart';

class CarePlanListPage extends StatefulWidget {
  const CarePlanListPage({required this.repository, super.key});
  final CareRepository repository;
  @override
  State<CarePlanListPage> createState() => _CarePlanListPageState();
}

class _CarePlanListPageState extends State<CarePlanListPage> {
  int _page = 1;
  @override
  Widget build(BuildContext context) => CarePageFrame<CareJson>(
    key: ValueKey(_page),
    title: careCopy('care.plans'),
    load: () => widget.repository.plans(_page),
    builder: (data, reload) => ListView(
      padding: const EdgeInsets.all(20),
      children: [
        careNotice('care.followUpSeparate'),
        if ((data['total'] as num) == 0) careNotice('care.noPlans'),
        ...careObjects(data['items']).map(
          (plan) => Card(
            child: ListTile(
              title: Text(plan['title'] as String),
              subtitle: Text(careCopy('care.status.${plan['status']}')),
              trailing: const Icon(Icons.chevron_right),
              onTap: () async {
                await Navigator.push(
                  context,
                  MaterialPageRoute<void>(
                    builder: (_) => CarePlanPage(
                      repository: widget.repository,
                      planId: plan['id'] as String,
                    ),
                  ),
                );
                reload();
              },
            ),
          ),
        ),
        CarePagination(
          page: _page,
          total: (data['total'] as num).toInt(),
          onChanged: (page) => setState(() => _page = page),
        ),
      ],
    ),
  );
}

class CarePlanPage extends StatelessWidget {
  const CarePlanPage({
    required this.repository,
    required this.planId,
    super.key,
  });
  final CareRepository repository;
  final String planId;
  @override
  Widget build(BuildContext context) => CarePageFrame<CareJson>(
    title: careCopy('care.plans'),
    load: () => repository.plan(planId),
    builder: (plan, reload) => ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Text(
          plan['title'] as String,
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        careNotice('care.feedbackNotice'),
        Text(careCopy('care.status.${plan['status']}')),
        ...careObjects(plan['tasks']).map(
          (task) => Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${task['dueDate']} · ${careCopy('care.task.${task['kind']}')}',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  if (task['exerciseId'] != null)
                    Text(
                      careCopy(switch (task['exerciseId']) {
                        'grounding' => 'care.exercise.grounding',
                        'pause' => 'care.exercise.pause',
                        _ => 'care.exercise.smallStep',
                      }),
                    ),
                  Text(careCopy('care.status.${task['status']}')),
                  if (task['feedback'] != null)
                    Text(task['feedback'] as String),
                  if (task['status'] == 'Pending' && plan['status'] == 'Active')
                    Wrap(
                      spacing: 8,
                      children: [
                        OutlinedButton(
                          onPressed: () async {
                            await _start(context, task);
                            reload();
                          },
                          child: Text(careCopy('care.doTask')),
                        ),
                        FilledButton(
                          onPressed: () async {
                            await _respond(context, task, 'Done');
                            reload();
                          },
                          child: Text(careCopy('care.done')),
                        ),
                        TextButton(
                          onPressed: () async {
                            await _respond(context, task, 'Skipped');
                            reload();
                          },
                          child: Text(careCopy('care.skip')),
                        ),
                      ],
                    ),
                ],
              ),
            ),
          ),
        ),
      ],
    ),
  );
  Future<void> _start(BuildContext context, CareJson task) async {
    if (task['kind'] == 'CheckIn') {
      await openTodayEntry(context, repository);
      return;
    }
    try {
      final exercises = await repository.exercises();
      final exercise = exercises.firstWhere(
        (item) => item['id'] == task['exerciseId'],
      );
      if (!context.mounted) return;
      await Navigator.of(context).push(
        MaterialPageRoute<bool>(
          builder: (_) =>
              ExercisePracticePage(repository: repository, exercise: exercise),
        ),
      );
    } catch (error) {
      if (context.mounted) careError(context, error);
    }
  }

  Future<void> _respond(BuildContext context, CareJson task, String status) =>
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => _FeedbackPage(
            repository: repository,
            planId: planId,
            taskId: task['id'] as String,
            status: status,
          ),
        ),
      );
}

class _FeedbackPage extends StatefulWidget {
  const _FeedbackPage({
    required this.repository,
    required this.planId,
    required this.taskId,
    required this.status,
  });
  final CareRepository repository;
  final String planId, taskId, status;
  @override
  State<_FeedbackPage> createState() => _FeedbackPageState();
}

class _FeedbackPageState extends State<_FeedbackPage> {
  final _feedback = TextEditingController();
  bool _acknowledged = false, _busy = false;
  @override
  void dispose() {
    _feedback.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_busy || !_acknowledged || _feedback.text.length > 500) return;
    setState(() => _busy = true);
    try {
      await widget.repository.feedback(
        widget.planId,
        widget.taskId,
        widget.status,
        _feedback.text.trim().isEmpty ? null : _feedback.text.trim(),
      );
      if (mounted) Navigator.pop(context);
    } catch (error) {
      if (mounted) careError(context, error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(careCopy('care.feedback'))),
    body: ListView(
      padding: const EdgeInsets.all(20),
      children: [
        careNotice('care.feedbackNotice'),
        TextFormField(
          controller: _feedback,
          maxLength: 500,
          minLines: 3,
          maxLines: 6,
          enabled: !_busy,
          decoration: InputDecoration(labelText: careCopy('care.feedback')),
        ),
        CheckboxListTile(
          value: _acknowledged,
          title: Text(careCopy('care.feedbackConfirm')),
          onChanged: _busy
              ? null
              : (value) => setState(() => _acknowledged = value ?? false),
        ),
        FilledButton(
          onPressed: _acknowledged && !_busy ? _save : null,
          child: Text(careCopy('care.save')),
        ),
      ],
    ),
  );
}
