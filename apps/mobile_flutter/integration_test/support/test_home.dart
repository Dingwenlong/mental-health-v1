import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';
import 'package:mobile_flutter/features/ai_consultation/ai_session_launcher.dart';
import 'package:mobile_flutter/features/care/care_repository.dart';
import 'package:mobile_flutter/features/catalog/catalog_repository.dart';
import 'package:mobile_flutter/features/consultation/chat_connection.dart';
import 'package:mobile_flutter/features/data_rights/data_rights_page.dart';
import 'package:mobile_flutter/features/follow_ups/follow_up_list_page.dart';
import 'package:mobile_flutter/features/follow_ups/local_reminder_service.dart';
import 'package:mobile_flutter/features/results/analysis_progress_page.dart';
import 'package:mobile_flutter/generated/ui_copy.g.dart';
import 'package:mobile_flutter/main.dart';

Widget buildTestApp({
  required AuthStore authStore,
  required ApiClient client,
  required CatalogRepository catalogRepository,
  ChatSessionLauncher? chatLauncher,
  AiSessionLauncher? aiLauncher,
}) => MentalHealthApp(
  authStore: authStore,
  catalogRepository: catalogRepository,
  careRepository: CareRepository(client),
  chatLauncher: chatLauncher,
  aiLauncher: aiLauncher,
  resultGateway: ApiResultGateway(client),
  followUpRepository: ApiFollowUpRepository(client),
  reminders: DeviceLocalReminderService(),
  dataRightsGateway: ApiDataRightsGateway(client),
  dataExportSaver: const AndroidDownloadsDataExportSaver(),
);

Future<void> openTestCatalog(WidgetTester tester) async {
  for (
    var attempt = 0;
    attempt < 120 && find.byType(NavigationBar).evaluate().isEmpty;
    attempt++
  ) {
    await tester.pump(const Duration(milliseconds: 250));
  }
  final consultation = find.text(UiCopy.get('home.consultation'));
  await tester.tap(
    find.descendant(of: find.byType(NavigationBar), matching: consultation),
  );
  await tester.pumpAndSettle();
  await tester.tap(
    find.descendant(of: find.byType(ListTile), matching: consultation),
  );
  await tester.pumpAndSettle();
}
