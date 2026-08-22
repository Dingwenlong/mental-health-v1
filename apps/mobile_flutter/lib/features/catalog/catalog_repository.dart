import '../../core/api/api_client.dart';

enum ServiceKind { human, ai }

enum ServiceChannel { chat, video }

enum PaymentMode { free, demoPaid }

enum ConsentChoice { service, recording, analysis }

enum DemoOrderStatus { awaitingDemoPayment, confirmed }

class ServicePlanSummary {
  const ServicePlanSummary({
    required this.id,
    required this.kind,
    required this.channel,
    required this.paymentMode,
    required this.priceInMinorUnits,
    required this.currency,
    required this.durationMinutes,
  });

  final String id;
  final ServiceKind kind;
  final ServiceChannel channel;
  final PaymentMode paymentMode;
  final int priceInMinorUnits;
  final String currency;
  final int durationMinutes;

  factory ServicePlanSummary.fromJson(Map<String, dynamic> json) =>
      ServicePlanSummary(
        id: json['id'] as String,
        kind: _parseServiceKind(json['kind'] as String),
        channel: _parseServiceChannel(json['channel'] as String),
        paymentMode: _parsePaymentMode(json['paymentMode'] as String),
        priceInMinorUnits: json['priceInMinorUnits'] as int,
        currency: json['currency'] as String,
        durationMinutes: json['durationMinutes'] as int,
      );
}

class DemoOrderSummary {
  const DemoOrderSummary({
    required this.id,
    required this.status,
    required this.amountInMinorUnits,
    required this.currency,
  });

  final String id;
  final DemoOrderStatus status;
  final int amountInMinorUnits;
  final String currency;

  factory DemoOrderSummary.fromJson(Map<String, dynamic> json) =>
      DemoOrderSummary(
        id: json['id'] as String,
        status: _parseOrderStatus(json['status'] as String),
        amountInMinorUnits: json['amountInMinorUnits'] as int,
        currency: json['currency'] as String,
      );
}

abstract interface class CatalogRepository {
  Future<List<ServicePlanSummary>> listPlans();

  Future<DemoOrderSummary> createOrder({
    required String planId,
    required Set<ConsentChoice> consents,
  });
}

abstract interface class DemoOrderRepository {
  Future<DemoOrderSummary> confirmOrder(String orderId);
}

class ApiCatalogRepository implements CatalogRepository, DemoOrderRepository {
  const ApiCatalogRepository(this._client);

  static const _consentTextVersion = 'ui-copy-v1';
  final ApiClient _client;

  @override
  Future<List<ServicePlanSummary>> listPlans() async {
    final response = await _client.getList('catalog/plans');
    return response
        .map(
          (item) => ServicePlanSummary.fromJson(
            Map<String, dynamic>.from(item as Map),
          ),
        )
        .toList(growable: false);
  }

  @override
  Future<DemoOrderSummary> createOrder({
    required String planId,
    required Set<ConsentChoice> consents,
  }) async {
    for (final consent in ConsentChoice.values.where(consents.contains)) {
      await _client.post(
        'consents',
        data: <String, dynamic>{
          'kind': _consentKind(consent),
          'textVersion': _consentTextVersion,
        },
      );
    }

    final response = await _client.post(
      'orders',
      data: <String, dynamic>{
        'planId': planId,
        'idempotencyKey':
            'mobile-$planId-${DateTime.now().microsecondsSinceEpoch}',
      },
    );
    return DemoOrderSummary.fromJson(response);
  }

  @override
  Future<DemoOrderSummary> confirmOrder(String orderId) async {
    final response = await _client.post('orders/$orderId/confirm');
    return DemoOrderSummary.fromJson(response);
  }
}

ServiceKind _parseServiceKind(String value) =>
    value.toLowerCase() == 'ai' ? ServiceKind.ai : ServiceKind.human;

ServiceChannel _parseServiceChannel(String value) =>
    value.toLowerCase() == 'video' ? ServiceChannel.video : ServiceChannel.chat;

PaymentMode _parsePaymentMode(String value) =>
    value.toLowerCase() == 'demopaid' ? PaymentMode.demoPaid : PaymentMode.free;

DemoOrderStatus _parseOrderStatus(String value) =>
    value.toLowerCase() == 'confirm'
    ? DemoOrderStatus.confirmed
    : value.toLowerCase() == 'confirmed'
    ? DemoOrderStatus.confirmed
    : DemoOrderStatus.awaitingDemoPayment;

String _consentKind(ConsentChoice consent) => switch (consent) {
  ConsentChoice.service => 'Service',
  ConsentChoice.recording => 'Recording',
  ConsentChoice.analysis => 'AiAnalysis',
};
