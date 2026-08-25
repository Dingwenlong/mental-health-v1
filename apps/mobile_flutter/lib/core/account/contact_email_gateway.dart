import '../api/api_client.dart';

abstract interface class ContactEmailGateway {
  Future<String?> getContactEmail();
  Future<void> putContactEmail(String? email);
}

class ApiContactEmailGateway implements ContactEmailGateway {
  const ApiContactEmailGateway(this._client);

  final ApiClient _client;

  @override
  Future<String?> getContactEmail() async {
    final response = await _client.get('account/contact-email');
    return response['email'] as String?;
  }

  @override
  Future<void> putContactEmail(String? email) {
    return _client.put(
      'account/contact-email',
      data: <String, dynamic>{'email': email},
    );
  }
}
