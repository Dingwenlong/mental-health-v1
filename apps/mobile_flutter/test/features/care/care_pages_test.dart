import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/features/care/care_repository.dart';
import 'package:mobile_flutter/features/care/check_in_pages.dart';
import 'package:mobile_flutter/features/care/exercise_pages.dart';
import 'package:mobile_flutter/features/care/sharing_page.dart';
import 'package:mobile_flutter/features/care/care_plan_page.dart';

void main() {
  testWidgets(
    'record editor validates before saving and preserves version on edit',
    (tester) async {
      final repository = _Repository();
      await tester.pumpWidget(
        MaterialApp(
          home: CheckInEditorPage(
            repository: repository,
            entry: {
              'date': careToday(),
              'mood': 3,
              'sleepHours': 7,
              'note': '原记录',
              'version': 2,
            },
          ),
        ),
      );
      await tester.enterText(find.byKey(const Key('sleep-hours')), '25');
      await tester.ensureVisible(find.text('保存'));
      await tester.tap(find.text('保存'));
      await tester.pumpAndSettle();
      expect(repository.saved, isNull);
      await tester.enterText(find.byKey(const Key('sleep-hours')), '8');
      await tester.ensureVisible(find.text('保存'));
      await tester.tap(find.text('保存'));
      await tester.pumpAndSettle();
      expect(repository.saved?['version'], 2);
      expect(repository.saved?['sleepHours'], 8);
    },
  );
  testWidgets(
    'sharing is opt in and a revoked record loses its active action',
    (tester) async {
      final repository = _Repository();
      await tester.pumpWidget(
        MaterialApp(home: SharingPage(repository: repository)),
      );
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<FilledButton>(find.widgetWithText(FilledButton, '授权共享'))
            .onPressed,
        isNull,
      );
      await tester.tap(find.byType(Checkbox));
      await tester.pumpAndSettle();
      await tester.tap(find.text('授权共享'));
      await tester.pumpAndSettle();
      expect(repository.shared, isTrue);
      await tester.ensureVisible(find.text('撤回共享'));
      await tester.tap(find.text('撤回共享'));
      await tester.pumpAndSettle();
      expect(repository.shared, isFalse);
      expect(find.text('撤回共享'), findsNothing);
    },
  );
  testWidgets('stopping an exercise does not record completion', (
    tester,
  ) async {
    final repository = _Repository();
    await tester.pumpWidget(
      MaterialApp(
        home: ExercisePracticePage(
          repository: repository,
          exercise: {
            'id': 'grounding',
            'title': '留意身边',
            'instruction': '留意周围的声音。',
            'durationSeconds': 90,
          },
        ),
      ),
    );
    await tester.tap(find.text('开始练习'));
    await tester.pump(const Duration(seconds: 1));
    expect(
      tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, '记录完成'))
          .onPressed,
      isNull,
    );
    await tester.tap(find.text('停止练习'));
    await tester.pumpAndSettle();
    expect(repository.exerciseCompleted, isFalse);
  });
  testWidgets(
    'plan feedback requires acknowledgement and does not write private records',
    (tester) async {
      final repository = _Repository();
      await tester.pumpWidget(
        MaterialApp(
          home: CarePlanPage(repository: repository, planId: 'plan-1'),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, '已完成'));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<FilledButton>(find.widgetWithText(FilledButton, '保存'))
            .onPressed,
        isNull,
      );
      await tester.tap(find.byType(Checkbox));
      await tester.pumpAndSettle();
      await tester.tap(find.text('保存'));
      await tester.pumpAndSettle();
      expect(repository.responded, isTrue);
      expect(repository.saved, isNull);
    },
  );
  testWidgets('trend keeps missing dates and reloads on retry', (tester) async {
    final repository = _Repository()..fail = true;
    await tester.pumpWidget(
      MaterialApp(home: TrendsPage(repository: repository)),
    );
    await tester.pumpAndSettle();
    expect(find.text('没能加载，请重试。'), findsOneWidget);
    repository.fail = false;
    await tester.tap(find.widgetWithText(FilledButton, '刷新'));
    await tester.pumpAndSettle();
    expect(find.text('2026-08-26'), findsOneWidget);
    expect(find.text('未记录'), findsOneWidget);
  });
}

class _Repository extends CareRepository {
  _Repository()
    : super(
        ApiClient(
          baseUrl: 'http://127.0.0.1/',
          readAccessToken: () async => null,
        ),
      );
  CareJson? saved;
  bool shared = false,
      exerciseCompleted = false,
      responded = false,
      fail = false;
  @override
  Future<void> saveEntry(String date, CareJson body) async {
    saved = body;
  }

  @override
  Future<List<CareJson>> candidates() async => [
    {
      'followUpId': 'follow-1',
      'doctorId': 'doctor-1',
      'doctorName': '回访医生',
      'dueAt': null,
    },
  ];
  @override
  Future<CareJson> grants(int page) async => {
    'total': shared ? 1 : 0,
    'items': shared
        ? [
            {'id': 'grant-1', 'doctorName': '回访医生', 'active': true},
          ]
        : [],
  };
  @override
  Future<void> grant(String followUpId) async {
    shared = true;
  }

  @override
  Future<void> revoke(String id) async {
    shared = false;
  }

  @override
  Future<void> completeExercise(String id, String exerciseId) async {
    exerciseCompleted = true;
  }

  @override
  Future<CareJson> plan(String id) async => {
    'id': id,
    'title': '这周的安排',
    'status': responded ? 'Completed' : 'Active',
    'tasks': [
      {
        'id': 'task-1',
        'kind': 'CheckIn',
        'dueDate': careToday(),
        'status': responded ? 'Done' : 'Pending',
        'feedback': null,
      },
    ],
  };
  @override
  Future<void> feedback(
    String planId,
    String taskId,
    String status,
    String? feedback,
  ) async {
    responded = true;
  }

  @override
  Future<List<CareJson>> trends(int days) async {
    if (fail) throw Exception('offline');
    return [
      {
        'date': '2026-08-26',
        'mood': null,
        'sleepHours': null,
        'exerciseCount': 0,
      },
      {'date': '2026-08-27', 'mood': 3, 'sleepHours': 7, 'exerciseCount': 1},
    ];
  }
}
