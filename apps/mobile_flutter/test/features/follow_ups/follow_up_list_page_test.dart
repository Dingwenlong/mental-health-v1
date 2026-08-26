import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/follow_ups/follow_up_list_page.dart';
import 'package:mobile_flutter/features/follow_ups/local_reminder_service.dart';

void main() {
  testWidgets('shows the shared illustrated empty state without follow-ups', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: FollowUpListPage(
          repository: _EmptyFollowUpRepository(),
          reminders: _DeniedReminderService(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('empty-state-illustration')), findsOneWidget);
    expect(find.text('还没有随访安排'), findsOneWidget);
  });

  testWidgets('permission denial does not hide the follow-up task', (
    tester,
  ) async {
    final reminders = _DeniedReminderService();
    await tester.pumpWidget(
      MaterialApp(
        home: FollowUpListPage(
          repository: _FakeFollowUpRepository(),
          reminders: reminders,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('随访时间：2026年8月25日 14:30'), findsOneWidget);
    expect(find.text('没有通知权限，随访仍会显示在这里。'), findsOneWidget);
    expect(reminders.scheduledIds, <String>['follow-up-1']);
  });
}

class _EmptyFollowUpRepository implements FollowUpRepository {
  @override
  Future<List<UserFollowUp>> listFollowUps() async => const <UserFollowUp>[];
}

class _FakeFollowUpRepository implements FollowUpRepository {
  @override
  Future<List<UserFollowUp>> listFollowUps() async => <UserFollowUp>[
    UserFollowUp(
      id: 'follow-up-1',
      status: 'Scheduled',
      dueAt: DateTime(2026, 8, 25, 14, 30),
      deadline: DateTime(2026, 8, 26),
    ),
  ];
}

class _DeniedReminderService implements LocalReminderService {
  final List<String> scheduledIds = <String>[];

  @override
  Future<bool> schedule(UserFollowUp task) async {
    scheduledIds.add(task.id);
    return false;
  }
}
