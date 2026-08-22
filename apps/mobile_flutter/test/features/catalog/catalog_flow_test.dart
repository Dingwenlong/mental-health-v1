import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/catalog/catalog_page.dart';
import 'package:mobile_flutter/features/catalog/catalog_repository.dart';

void main() {
  testWidgets(
    'user selects paid AI chat, grants each consent, and creates a demo order',
    (WidgetTester tester) async {
      final repository = _FakeCatalogRepository();

      await tester.pumpWidget(
        MaterialApp(home: CatalogPage(repository: repository)),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('AI 虚拟人 · 文字'));
      await tester.pumpAndSettle();

      expect(find.text('模拟收费，不会真实扣款'), findsOneWidget);
      expect(
        tester.widget<Switch>(find.byKey(const Key('consent-service'))).value,
        isFalse,
      );
      expect(
        tester.widget<Switch>(find.byKey(const Key('consent-recording'))).value,
        isFalse,
      );
      expect(
        tester.widget<Switch>(find.byKey(const Key('consent-analysis'))).value,
        isFalse,
      );
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('create-order')))
            .onPressed,
        isNull,
      );

      await tester.tap(find.byKey(const Key('consent-service')));
      await tester.tap(find.byKey(const Key('consent-recording')));
      await tester.tap(find.byKey(const Key('consent-analysis')));
      await tester.pump();
      await tester.tap(find.byKey(const Key('create-order')));
      await tester.pumpAndSettle();

      expect(repository.createdPlanIds, <String>['plan-ai-chat-paid']);
      expect(repository.recordedConsents, <ConsentChoice>{
        ConsentChoice.service,
        ConsentChoice.recording,
        ConsentChoice.analysis,
      });
      expect(find.text('等待确认模拟收费'), findsOneWidget);
    },
  );
}

class _FakeCatalogRepository implements CatalogRepository {
  final List<String> createdPlanIds = <String>[];
  Set<ConsentChoice> recordedConsents = <ConsentChoice>{};

  @override
  Future<List<ServicePlanSummary>> listPlans() async => <ServicePlanSummary>[
    const ServicePlanSummary(
      id: 'plan-ai-chat-paid',
      kind: ServiceKind.ai,
      channel: ServiceChannel.chat,
      paymentMode: PaymentMode.demoPaid,
      priceInMinorUnits: 9900,
      currency: 'CNY',
      durationMinutes: 30,
    ),
  ];

  @override
  Future<DemoOrderSummary> createOrder({
    required String planId,
    required Set<ConsentChoice> consents,
  }) async {
    createdPlanIds.add(planId);
    recordedConsents = Set<ConsentChoice>.from(consents);
    return const DemoOrderSummary(
      id: 'order-1',
      status: DemoOrderStatus.awaitingDemoPayment,
      amountInMinorUnits: 9900,
      currency: 'CNY',
    );
  }
}
