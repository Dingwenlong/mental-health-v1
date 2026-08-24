import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/catalog/catalog_repository.dart';
import 'package:mobile_flutter/features/data_rights/data_rights_page.dart';
import 'package:mobile_flutter/features/follow_ups/follow_up_list_page.dart';
import 'package:mobile_flutter/features/follow_ups/local_reminder_service.dart';
import 'package:mobile_flutter/features/home/patient_home_page.dart';
import 'package:mobile_flutter/features/results/analysis_progress_page.dart';
import 'package:mobile_flutter/features/results/result_page.dart';

void main() {
  testWidgets(
    'patient can reach consultation, result and follow-up from bottom navigation',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: PatientHomePage(
            catalogRepository: _CatalogRepository(),
            resultGateway: _ResultGateway(),
            followUpRepository: _FollowUpRepository(),
            reminders: _ReminderService(),
            dataRightsGateway: _DataRightsGateway(),
            dataExportSaver: _DataExportSaver(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('咨询'), findsOneWidget);
      expect(find.text('结果'), findsOneWidget);
      expect(find.text('回访'), findsOneWidget);
      expect(find.text('我的数据'), findsOneWidget);

      await tester.tap(find.text('结果'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'session-1');
      await tester.tap(find.text('查看结果'));
      await tester.pumpAndSettle();
      expect(find.text('正在整理这次分析'), findsOneWidget);
      expect(find.text('咨询'), findsOneWidget);
      expect(find.text('回访'), findsOneWidget);

      await tester.tap(find.text('回访'));
      await tester.pumpAndSettle();
      expect(find.text('我的随访'), findsOneWidget);
    },
  );
}

class _CatalogRepository implements CatalogRepository {
  @override
  Future<List<ServicePlanSummary>> listPlans() async => const [];

  @override
  Future<DemoOrderSummary> createOrder({
    required String planId,
    required Set<ConsentChoice> consents,
  }) => throw UnimplementedError();
}

class _ResultGateway implements ResultGateway {
  @override
  Future<AssessmentResult?> getResult(String sessionId) async => null;
}

class _FollowUpRepository implements FollowUpRepository {
  @override
  Future<List<UserFollowUp>> listFollowUps() async => const [];
}

class _ReminderService implements LocalReminderService {
  @override
  Future<bool> schedule(UserFollowUp task) async => true;
}

class _DataRightsGateway implements DataRightsGateway {
  @override
  Future<void> deleteDemoData() async {}

  @override
  Future<DataExportPackage> export({
    required bool includeRawMedia,
    required bool confirmRawMedia,
  }) => throw UnimplementedError();
}

class _DataExportSaver implements DataExportSaver {
  @override
  Future<String> save(DataExportPackage dataPackage) =>
      throw UnimplementedError();
}
