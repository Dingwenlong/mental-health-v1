import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';
import 'package:mobile_flutter/features/ai_consultation/ai_session_launcher.dart';
import 'package:mobile_flutter/features/care/care_repository.dart';
import 'package:mobile_flutter/features/catalog/catalog_repository.dart';
import 'package:mobile_flutter/features/data_rights/data_rights_page.dart';
import 'package:mobile_flutter/features/follow_ups/follow_up_list_page.dart';
import 'package:mobile_flutter/features/follow_ups/local_reminder_service.dart';
import 'package:mobile_flutter/features/results/analysis_progress_page.dart';
import 'package:mobile_flutter/features/results/result_page.dart';
import 'package:mobile_flutter/main.dart';
import 'package:mobile_flutter/providers/speech/chime_fallback_speech_provider.dart';

// Uses the existing test-only credential injection pattern. No product login bypass.
// Run "record" before the doctor browser flow, then "feedback", then "readback"
// after restarting the isolated API. Only synthetic, disposable local data.
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  testWidgets('Android care flow uses persisted API data', (tester) async {
    const baseUrl = String.fromEnvironment('API_BASE_URL');
    const token = String.fromEnvironment('USER_ACCESS_TOKEN');
    const phase = String.fromEnvironment('CARE_PHASE', defaultValue: 'record');
    expect(token, isNotEmpty);
    expect(Uri.parse(baseUrl).path, '/api/v1/');
    expect(
      Uri.parse(baseUrl).host,
      isIn(['10.0.2.2', '127.0.0.1', 'localhost']),
    );
    final storage = _TokenStorage(token);
    final client = ApiClient(
      baseUrl: baseUrl,
      readAccessToken: storage.readAccessToken,
    );
    final auth = AuthStore(gateway: ApiAuthGateway(client), storage: storage);
    await auth.restore();
    final care = CareRepository(client);
    await tester.pumpWidget(
      MentalHealthApp(
        authStore: auth,
        catalogRepository: ApiCatalogRepository(client),
        careRepository: care,
        resultGateway: ApiResultGateway(client),
        followUpRepository: ApiFollowUpRepository(client),
        reminders: DeviceLocalReminderService(),
        dataRightsGateway: ApiDataRightsGateway(client),
        dataExportSaver: const AndroidDownloadsDataExportSaver(),
        aiLauncher: ApiAiSessionLauncher(
          client,
          speechFactory: () =>
              ChimeFallbackSpeechProvider(player: AssetChimePlayer()),
        ),
      ),
    );
    await _until(tester, find.byType(NavigationBar));
    if (phase == 'record') {
      await _until(tester, find.text(careCopy('care.recordToday')));
      await _tap(tester, find.text(careCopy('care.recordToday')));
      await tester.enterText(find.byKey(const Key('sleep-hours')), '7.5');
      await tester.enterText(
        find.byKey(const Key('daily-note')),
        '合成日常记录：今天散步十分钟。',
      );
      await _tap(tester, find.text('保存'));
      await _until(tester, find.text('修改'));
      expect((await care.todayEntry())?['note'], '合成日常记录：今天散步十分钟。');
      await _nav(tester, '咨询');
      await _tap(tester, find.text('我的咨询'));
      await _until(tester, find.text('进入咨询'));
      await _tap(tester, find.text('进入咨询'));
      await _until(tester, find.byKey(const Key('ai-input')));
      await tester.enterText(find.byKey(const Key('ai-input')), '最近很难过');
      await _tap(tester, find.byKey(const Key('ai-send')));
      await _until(tester, find.text('这几天不好过。哪件事最让你难受？'));
      await _back(tester);
      await _until(tester, find.text('查看报告'));
      await _tap(tester, find.text('查看报告'));
      await _until(tester, find.byType(ResultPage));
      await _back(tester);
      await _back(tester);
      await _nav(tester, '我的');
      await _tap(tester, find.text('资料共享'));
      await _until(tester, find.byType(CheckboxListTile));
      await _tap(tester, find.byType(CheckboxListTile).first);
      await _tap(tester, find.text('授权共享'));
      await _until(tester, find.text('撤回共享'));
      expect((await care.grants(1))['total'], 1);
    } else if (phase == 'feedback') {
      await _until(tester, find.text('本周跟进安排'));
      await _tap(tester, find.text('本周跟进安排'));
      await _until(tester, find.text('已完成'));
      await _tap(tester, find.text('已完成').first);
      await _until(tester, find.byType(TextFormField));
      await tester.enterText(find.byType(TextFormField), '合成任务反馈：已完成记录。');
      await _tap(tester, find.byType(CheckboxListTile));
      await _tap(tester, find.text('保存'));
      await _until(tester, find.text('跳过'));
      await _tap(tester, find.text('跳过'));
      await _until(tester, find.byType(TextFormField));
      await tester.enterText(find.byType(TextFormField), '合成任务反馈：今天先休息。');
      await _tap(tester, find.byType(CheckboxListTile));
      await _tap(tester, find.text('保存'));
      await _until(tester, find.text('已跳过'));
      expect((await care.plans(1))['items'][0]['status'], 'Completed');
    } else if (phase == 'readback') {
      expect((await care.todayEntry())?['sleepHours'], 7.5);
      await _until(tester, find.text('本周跟进安排'));
      await _tap(tester, find.text('本周跟进安排'));
      await _until(tester, find.text('合成任务反馈：已完成记录。'));
      expect(find.text('合成任务反馈：今天先休息。'), findsOneWidget);
    } else {
      fail('Unknown care test phase');
    }
    await tester.pumpWidget(const SizedBox.shrink());
    auth.dispose();
  });
}

Future<void> _until(WidgetTester tester, Finder finder) async {
  for (var attempt = 0; attempt < 120; attempt++) {
    await tester.pump(const Duration(milliseconds: 250));
    if (finder.evaluate().isNotEmpty) return;
  }
  expect(finder, findsWidgets);
}

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _back(WidgetTester tester) =>
    _tap(tester, find.byIcon(Icons.arrow_back).first);
Future<void> _nav(WidgetTester tester, String label) => _tap(
  tester,
  find.descendant(of: find.byType(NavigationBar), matching: find.text(label)),
);

class _TokenStorage implements TokenStorage {
  _TokenStorage(this.token);
  String? token;
  @override
  Future<String?> readAccessToken() async => token;
  @override
  Future<void> writeAccessToken(String value) async {
    token = value;
  }

  @override
  Future<void> clearAccessToken() async {
    token = null;
  }
}
