import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';

void main() {
  test(
    'send code completes bootstrap and captcha before creating challenge',
    () async {
      final gateway = _FakeAuthGateway();
      final store = AuthStore(gateway: gateway, storage: _MemoryTokenStorage());

      final sent = await store.sendCode('13800138001', (bootstrap) async {
        gateway.events.add('captcha:${bootstrap.prefix}');
        return 'captcha-test-param';
      });

      expect(sent, isTrue);
      expect(gateway.events, <String>[
        'bootstrap:13800138001',
        'captcha:xfkdn8',
        'challenge:pre-token:captcha-test-param',
      ]);
      expect(store.hasChallenge, isTrue);
      expect(store.phoneNumber, '13800138001');
      expect(store.secondsUntilResend, 60);
      expect(store.errorCopyKey, isNull);
    },
  );

  test(
    'captcha close stops before challenge and maps to direct copy',
    () async {
      final gateway = _FakeAuthGateway();
      final store = AuthStore(gateway: gateway, storage: _MemoryTokenStorage());

      final sent = await store.sendCode('13800138001', (_) async => null);

      expect(sent, isFalse);
      expect(gateway.events, <String>['bootstrap:13800138001']);
      expect(store.hasChallenge, isFalse);
      expect(store.errorCopyKey, 'auth.captchaFailed');
    },
  );

  test(
    'verify passes only the transient code and stores the API token',
    () async {
      final gateway = _FakeAuthGateway();
      final storage = _MemoryTokenStorage();
      final store = AuthStore(gateway: gateway, storage: storage);
      await store.sendCode('13800138001', (_) async => 'captcha-test-param');

      final loggedIn = await store.verifySmsCode('246810');

      expect(loggedIn, isTrue);
      expect(gateway.verifiedCode, '246810');
      expect(store.isAuthenticated, isTrue);
      expect(storage.token, 'access-token');
    },
  );

  for (final testCase in <(String, String)>[
    ('INVALID_PHONE_NUMBER', 'auth.phoneRequired'),
    ('CAPTCHA_FAILED', 'auth.captchaFailed'),
    ('LOGIN_CHALLENGE_INVALID', 'auth.challengeInvalid'),
    ('SMS_RATE_LIMITED', 'auth.smsRateLimited'),
    ('INVALID_SMS_CODE', 'auth.invalidSmsCode'),
    ('AUTH_PROVIDER_UNAVAILABLE', 'auth.providerUnavailable'),
    ('UNEXPECTED', 'error.retry'),
  ]) {
    test('${testCase.$1} maps by stable problem code', () async {
      final gateway = _FakeAuthGateway()
        ..bootstrapFailure = ApiFailure(
          code: testCase.$1,
          message: 'Server text must not select UI copy',
          retryAfterSeconds: 75,
        );
      final store = AuthStore(gateway: gateway, storage: _MemoryTokenStorage());

      expect(
        await store.sendCode('13800138001', (_) async => 'unused'),
        isFalse,
      );
      expect(store.errorCopyKey, testCase.$2);
      expect(
        store.secondsUntilResend,
        testCase.$1 == 'SMS_RATE_LIMITED' ? 75 : 0,
      );
    });
  }
}

class _FakeAuthGateway implements AuthGateway {
  final events = <String>[];
  ApiFailure? bootstrapFailure;
  String? verifiedCode;

  @override
  Future<CaptchaBootstrap> bootstrapPhone(String phoneNumber) async {
    events.add('bootstrap:$phoneNumber');
    if (bootstrapFailure case final failure?) throw failure;
    return CaptchaBootstrap(
      preChallengeToken: 'pre-token',
      prefix: 'xfkdn8',
      encryptedSceneId: 'encrypted-scene-id',
      expiresAt: DateTime.now().toUtc().add(const Duration(minutes: 5)),
    );
  }

  @override
  Future<SmsChallenge> createSmsChallenge(
    String preChallengeToken,
    String captchaVerifyParam,
  ) async {
    events.add('challenge:$preChallengeToken:$captchaVerifyParam');
    return SmsChallenge(
      challengeToken: 'challenge-token',
      expiresAt: DateTime.now().toUtc().add(const Duration(minutes: 5)),
      resendAt: DateTime.now().toUtc().add(const Duration(seconds: 60)),
    );
  }

  @override
  Future<String> verifySmsCode(String challengeToken, String code) async {
    verifiedCode = code;
    return 'access-token';
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
