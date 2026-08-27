import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/catalog/catalog_repository.dart';
import 'package:mobile_flutter/features/data_rights/data_rights_page.dart';
import 'package:mobile_flutter/features/follow_ups/follow_up_list_page.dart';
import 'package:mobile_flutter/features/follow_ups/local_reminder_service.dart';
import 'package:mobile_flutter/features/home/patient_home_page.dart';
import 'package:mobile_flutter/features/care/care_repository.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
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
            careRepository: _CareRepository(),
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
      expect(find.text('记录'), findsOneWidget);
      expect(find.text('回访'), findsOneWidget);
      expect(find.text('我的'), findsOneWidget);

      await tester.tap(find.text('记录'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('咨询报告'));
      await tester.pumpAndSettle();
      expect(find.byType(TextField), findsNothing);
      await tester.tap(find.text('查看报告'));
      await tester.pumpAndSettle();
      expect(find.text('正在整理这次分析'), findsOneWidget);
      await tester.tap(find.byIcon(Icons.arrow_back));
      await tester.pumpAndSettle();
      await tester.pageBack();
      await tester.pumpAndSettle();
      await tester.tap(find.text('我的'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('回访'));
      await tester.pumpAndSettle();
      expect(find.text('我的随访'), findsOneWidget);
    },
  );
}

class _CareRepository extends CareRepository {
  _CareRepository()
    : super(
        ApiClient(
          baseUrl: 'http://127.0.0.1/',
          readAccessToken: () async => null,
        ),
      );
  @override
  Future<CareJson> entries(int page) async => {'total': 0, 'items': []};
  @override
  Future<CareJson> plans(int page) async => {'total': 0, 'items': []};
  @override
  Future<CareJson> consultations({
    required int page,
    bool reports = false,
    String? status,
    String? from,
    String? to,
  }) async => {
    'total': 1,
    'items': [
      {
        'id': 'session-1',
        'status': 'Completed',
        'scheduledAt': null,
        'practitionerName': null,
        'analysisStatus': 'Pending',
      },
    ],
  };
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
