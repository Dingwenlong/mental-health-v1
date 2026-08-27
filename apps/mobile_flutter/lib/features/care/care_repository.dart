import '../../core/api/api_client.dart';
import '../../generated/ui_copy.g.dart';

typedef CareJson = Map<String, dynamic>;
String careCopy(String key) => UiCopy.get(key);
String careToday() => DateTime.now()
    .toUtc()
    .add(const Duration(hours: 8))
    .toIso8601String()
    .substring(0, 10);
List<CareJson> careObjects(dynamic value) => (value as List)
    .map((item) => Map<String, dynamic>.from(item as Map))
    .toList();
String careTime(dynamic value) => value == null
    ? careCopy('care.noValue')
    : DateTime.parse(value.toString())
          .toUtc()
          .add(const Duration(hours: 8))
          .toString()
          .substring(0, 16);

class CareRepository {
  const CareRepository(this.client);
  final ApiClient client;
  Future<CareJson> entries(int page) =>
      client.get('me/check-ins?page=$page&pageSize=15');
  Future<CareJson?> todayEntry() async {
    final data = await client.get(
      'me/check-ins?from=${careToday()}&to=${careToday()}&pageSize=1',
    );
    final entries = careObjects(data['items']);
    return entries.isEmpty ? null : entries.first;
  }

  Future<void> saveEntry(String date, CareJson body) =>
      client.put('me/check-ins/$date', data: body);
  Future<void> deleteEntry(String date) => client.delete('me/check-ins/$date');
  Future<List<CareJson>> trends(int days) async =>
      careObjects(await client.getList('me/trends?days=$days'));
  Future<List<CareJson>> exercises() async =>
      careObjects(await client.getList('exercises'));
  Future<void> completeExercise(String id, String exerciseId) async {
    await client.post(
      'me/exercise-completions',
      data: {'id': id, 'exerciseId': exerciseId},
    );
  }

  Future<CareJson> completions(int page) =>
      client.get('me/exercise-completions?page=$page&pageSize=15');
  Future<List<CareJson>> candidates() async =>
      careObjects(await client.getList('me/sharing-grants/candidates'));
  Future<CareJson> grants(int page) =>
      client.get('me/sharing-grants?page=$page&pageSize=15');
  Future<void> grant(String followUpId) async {
    await client.post(
      'me/sharing-grants',
      data: {'followUpId': followUpId, 'acknowledged': true},
    );
  }

  Future<void> revoke(String id) => client.delete('me/sharing-grants/$id');
  Future<CareJson> plans(int page) =>
      client.get('care-plans?page=$page&pageSize=15');
  Future<CareJson> plan(String id) => client.get('care-plans/$id');
  Future<void> feedback(
    String planId,
    String taskId,
    String status,
    String? feedback,
  ) async {
    await client.post(
      'care-plans/$planId/tasks/$taskId/feedback',
      data: {'status': status, 'feedback': feedback, 'acknowledged': true},
    );
  }

  Future<CareJson> consultations({
    required int page,
    bool reports = false,
    String? status,
    String? from,
    String? to,
  }) => client.get(
    Uri(
      path: reports ? 'results' : 'consultations',
      queryParameters: {
        'page': '$page',
        'pageSize': '15',
        'status': ?status,
        if (from != null && from.isNotEmpty) 'from': '${from}T00:00:00+08:00',
        if (to != null && to.isNotEmpty) 'to': '${to}T23:59:59.9999999+08:00',
      },
    ).toString(),
  );
}
