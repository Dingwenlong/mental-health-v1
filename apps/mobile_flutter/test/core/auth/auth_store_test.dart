import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';

void main() {
  test('MFA_REQUIRED selects the code input by stable problem code', () async {
    final gateway = _FakeAuthGateway()
      ..loginFailure = const ApiFailure(
        code: 'MFA_REQUIRED',
        message: 'This text must not control the branch',
      );
    final storage = _MemoryTokenStorage();
    final store = AuthStore(gateway: gateway, storage: storage);

    final firstAttempt = await store.login(
      email: 'doctor@example.test',
      password: 'local-password',
    );

    expect(firstAttempt, isFalse);
    expect(store.needsMfaCode, isTrue);
    expect(store.errorCopyKey, 'auth.mfaRequired');

    gateway.loginFailure = null;
    final secondAttempt = await store.login(
      email: 'doctor@example.test',
      password: 'local-password',
      totpCode: '123456',
    );

    expect(secondAttempt, isTrue);
    expect(store.isAuthenticated, isTrue);
    expect(storage.token, 'access-token');
  });

  test('MFA setup uses the setup token before issuing an API token', () async {
    final gateway = _FakeAuthGateway()
      ..loginFailure = const ApiFailure(
        code: 'MFA_REQUIRED',
        message: 'Setup required',
        data: <String, dynamic>{'setupToken': 'setup-token'},
      );
    final store = AuthStore(gateway: gateway, storage: _MemoryTokenStorage());

    await store.login(email: 'admin@example.test', password: 'local-password');

    expect(store.needsMfaSetup, isTrue);
    expect(store.mfaManualKey, 'manual-key');
    gateway.loginFailure = null;

    final completed = await store.completeMfaSetup('123456');

    expect(completed, isTrue);
    expect(gateway.confirmedSetupToken, 'setup-token');
    expect(store.isAuthenticated, isTrue);
  });
}

class _FakeAuthGateway implements AuthGateway {
  ApiFailure? loginFailure;
  String? confirmedSetupToken;

  @override
  Future<String> login({
    required String email,
    required String password,
    String? totpCode,
  }) async {
    if (loginFailure case final failure?) {
      throw failure;
    }
    return 'access-token';
  }

  @override
  Future<String> beginMfaSetup(String setupToken) async => 'manual-key';

  @override
  Future<void> confirmMfaSetup(String setupToken, String totpCode) async {
    confirmedSetupToken = setupToken;
  }
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
