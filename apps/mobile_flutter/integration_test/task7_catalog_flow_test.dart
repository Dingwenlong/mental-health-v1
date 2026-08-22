import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';
import 'package:mobile_flutter/features/catalog/catalog_repository.dart';
import 'package:mobile_flutter/features/catalog/service_plan_card.dart';
import 'package:mobile_flutter/generated/ui_copy.g.dart';
import 'package:mobile_flutter/main.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Android completes the local catalog and demo order flow', (
    WidgetTester tester,
  ) async {
    const apiBaseUrl = String.fromEnvironment('API_BASE_URL');
    const email = String.fromEnvironment('DEMO_USER_EMAIL');
    const password = String.fromEnvironment('DEMO_USER_PASSWORD');
    expect(apiBaseUrl, isNotEmpty);
    expect(email, isNotEmpty);
    expect(password, isNotEmpty);

    final storage = _MemoryTokenStorage();
    final client = ApiClient(
      baseUrl: apiBaseUrl,
      readAccessToken: storage.readAccessToken,
    );
    final auth = AuthStore(gateway: ApiAuthGateway(client), storage: storage);
    final catalog = ApiCatalogRepository(client);

    await tester.pumpWidget(
      MentalHealthApp(authStore: auth, catalogRepository: catalog),
    );
    await tester.enterText(find.byKey(const Key('login-email')), email);
    await tester.enterText(find.byKey(const Key('login-password')), password);
    await tester.tap(find.byKey(const Key('login-submit')));
    await _pumpUntil(tester, find.byType(ServicePlanCard));

    final paidAiPlan = find.byWidgetPredicate(
      (widget) =>
          widget is ServicePlanCard &&
          widget.plan.kind == ServiceKind.ai &&
          widget.plan.channel == ServiceChannel.chat &&
          widget.plan.paymentMode == PaymentMode.demoPaid,
    );
    expect(paidAiPlan, findsOneWidget);
    await tester.ensureVisible(paidAiPlan);
    await tester.tap(paidAiPlan);
    await tester.pumpAndSettle();
    expect(find.text(UiCopy.get('order.demoPaid')), findsOneWidget);

    for (final key in <Key>[
      const Key('consent-service'),
      const Key('consent-recording'),
      const Key('consent-analysis'),
    ]) {
      final consent = find.byKey(key);
      await tester.ensureVisible(consent);
      await tester.tap(consent);
      await tester.pump();
    }
    final createOrder = find.byKey(const Key('create-order'));
    await tester.ensureVisible(createOrder);
    await tester.tap(createOrder);
    await _pumpUntil(
      tester,
      find.text(UiCopy.get('order.awaitingDemoPayment')),
    );

    await tester.tap(find.byKey(const Key('confirm-order')));
    await _pumpUntil(tester, find.text(UiCopy.get('order.confirmed')));
  });
}

Future<void> _pumpUntil(
  WidgetTester tester,
  Finder finder, {
  int attempts = 80,
}) async {
  for (var attempt = 0; attempt < attempts; attempt += 1) {
    await tester.pump(const Duration(milliseconds: 250));
    if (finder.evaluate().isNotEmpty) {
      return;
    }
  }
  expect(finder, findsWidgets);
}

class _MemoryTokenStorage implements TokenStorage {
  String? token;

  @override
  Future<void> clearAccessToken() async => token = null;

  @override
  Future<String?> readAccessToken() async => token;

  @override
  Future<void> writeAccessToken(String value) async => token = value;
}
