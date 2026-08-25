import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../api/api_client.dart';

abstract interface class TokenStorage {
  Future<String?> readAccessToken();
  Future<void> writeAccessToken(String token);
  Future<void> clearAccessToken();
}

class SecureTokenStorage implements TokenStorage {
  const SecureTokenStorage({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  static const _accessTokenKey = 'access_token';
  final FlutterSecureStorage _storage;

  @override
  Future<String?> readAccessToken() => _storage.read(key: _accessTokenKey);

  @override
  Future<void> writeAccessToken(String token) =>
      _storage.write(key: _accessTokenKey, value: token);

  @override
  Future<void> clearAccessToken() => _storage.delete(key: _accessTokenKey);
}

class CaptchaBootstrap {
  const CaptchaBootstrap({
    required this.preChallengeToken,
    required this.prefix,
    required this.encryptedSceneId,
    required this.expiresAt,
  });

  final String preChallengeToken;
  final String prefix;
  final String encryptedSceneId;
  final DateTime expiresAt;
}

class SmsChallenge {
  const SmsChallenge({
    required this.challengeToken,
    required this.expiresAt,
    required this.resendAt,
  });

  final String challengeToken;
  final DateTime expiresAt;
  final DateTime resendAt;
}

typedef CaptchaExecutor = Future<String?> Function(CaptchaBootstrap bootstrap);

abstract interface class AuthGateway {
  Future<CaptchaBootstrap> bootstrapPhone(String phoneNumber);

  Future<SmsChallenge> createSmsChallenge(
    String preChallengeToken,
    String captchaVerifyParam,
  );

  Future<String> verifySmsCode(String challengeToken, String code);
}

class ApiAuthGateway implements AuthGateway {
  const ApiAuthGateway(this._client);

  final ApiClient _client;

  @override
  Future<CaptchaBootstrap> bootstrapPhone(String phoneNumber) async {
    final response = await _client.post(
      'auth/captcha/bootstrap',
      data: <String, dynamic>{'phoneNumber': phoneNumber, 'client': 'android'},
    );
    return CaptchaBootstrap(
      preChallengeToken: response['preChallengeToken'] as String,
      prefix: response['prefix'] as String,
      encryptedSceneId: response['encryptedSceneId'] as String,
      expiresAt: DateTime.parse(response['expiresAt'] as String).toUtc(),
    );
  }

  @override
  Future<SmsChallenge> createSmsChallenge(
    String preChallengeToken,
    String captchaVerifyParam,
  ) async {
    final response = await _client.post(
      'auth/sms/challenges',
      data: <String, dynamic>{
        'preChallengeToken': preChallengeToken,
        'captchaVerifyParam': captchaVerifyParam,
      },
    );
    return SmsChallenge(
      challengeToken: response['challengeToken'] as String,
      expiresAt: DateTime.parse(response['expiresAt'] as String).toUtc(),
      resendAt: DateTime.parse(response['resendAt'] as String).toUtc(),
    );
  }

  @override
  Future<String> verifySmsCode(String challengeToken, String code) async {
    final response = await _client.post(
      'auth/sms/verify',
      data: <String, dynamic>{'challengeToken': challengeToken, 'code': code},
    );
    return response['accessToken'] as String;
  }

  Future<String> login({
    required String email,
    required String password,
    String? totpCode,
  }) async {
    final response = await _client.post(
      'auth/login',
      data: <String, dynamic>{
        'email': email,
        'password': password,
        'totpCode': totpCode,
      },
    );
    return response['accessToken'] as String;
  }
}

class AuthStore extends ChangeNotifier {
  AuthStore({required this.gateway, required this.storage});

  final AuthGateway gateway;
  final TokenStorage storage;

  bool isAuthenticated = false;
  bool isBusy = false;
  String? phoneNumber;
  int secondsUntilResend = 0;
  String? errorCopyKey;

  String? _challengeToken;
  Timer? _countdown;

  bool get hasChallenge => _challengeToken != null;

  Future<void> restore() async {
    isAuthenticated = (await storage.readAccessToken())?.isNotEmpty ?? false;
    notifyListeners();
  }

  Future<bool> sendCode(
    String phoneNumberInput,
    CaptchaExecutor captchaExecutor,
  ) async {
    if (isBusy || secondsUntilResend > 0) return false;
    final normalizedInput = phoneNumberInput.trim();
    if (!RegExp(r'^1\d{10}$').hasMatch(normalizedInput)) {
      errorCopyKey = 'auth.phoneRequired';
      notifyListeners();
      return false;
    }

    isBusy = true;
    errorCopyKey = null;
    notifyListeners();
    try {
      final bootstrap = await gateway.bootstrapPhone(normalizedInput);
      final captchaVerifyParam = await captchaExecutor(bootstrap);
      if (captchaVerifyParam == null || captchaVerifyParam.isEmpty) {
        errorCopyKey = 'auth.captchaFailed';
        return false;
      }
      final challenge = await gateway.createSmsChallenge(
        bootstrap.preChallengeToken,
        captchaVerifyParam,
      );
      phoneNumber = normalizedInput;
      _challengeToken = challenge.challengeToken;
      _startCountdown(_secondsUntil(challenge.resendAt));
      return true;
    } on ApiFailure catch (failure) {
      _setFailure(failure);
      return false;
    } catch (_) {
      errorCopyKey = 'error.retry';
      return false;
    } finally {
      isBusy = false;
      notifyListeners();
    }
  }

  Future<bool> verifySmsCode(String code) async {
    final challengeToken = _challengeToken;
    if (challengeToken == null || !RegExp(r'^\d{6}$').hasMatch(code)) {
      errorCopyKey = 'auth.invalidSmsCode';
      notifyListeners();
      return false;
    }

    isBusy = true;
    errorCopyKey = null;
    notifyListeners();
    try {
      final token = await gateway.verifySmsCode(challengeToken, code);
      await storage.writeAccessToken(token);
      isAuthenticated = true;
      _challengeToken = null;
      _countdown?.cancel();
      secondsUntilResend = 0;
      return true;
    } on ApiFailure catch (failure) {
      _setFailure(failure);
      return false;
    } finally {
      isBusy = false;
      notifyListeners();
    }
  }

  Future<void> logout() async {
    await storage.clearAccessToken();
    _countdown?.cancel();
    isAuthenticated = false;
    phoneNumber = null;
    _challengeToken = null;
    secondsUntilResend = 0;
    errorCopyKey = null;
    notifyListeners();
  }

  void _setFailure(ApiFailure failure) {
    errorCopyKey = switch (failure.code) {
      'INVALID_PHONE_NUMBER' => 'auth.phoneRequired',
      'CAPTCHA_FAILED' => 'auth.captchaFailed',
      'LOGIN_CHALLENGE_INVALID' => 'auth.challengeInvalid',
      'SMS_RATE_LIMITED' => 'auth.smsRateLimited',
      'INVALID_SMS_CODE' => 'auth.invalidSmsCode',
      'AUTH_PROVIDER_UNAVAILABLE' => 'auth.providerUnavailable',
      'FORBIDDEN_RESOURCE' => 'error.forbidden',
      _ => 'error.retry',
    };
    final retryAfterSeconds = failure.retryAfterSeconds;
    if (failure.code == 'SMS_RATE_LIMITED' &&
        retryAfterSeconds != null &&
        retryAfterSeconds > 0) {
      _startCountdown(retryAfterSeconds);
    }
  }

  int _secondsUntil(DateTime target) {
    final milliseconds = target
        .difference(DateTime.now().toUtc())
        .inMilliseconds;
    if (milliseconds <= 0) return 60;
    return (milliseconds / Duration.millisecondsPerSecond).ceil();
  }

  void _startCountdown(int seconds) {
    _countdown?.cancel();
    secondsUntilResend = seconds < 0 ? 0 : seconds;
    if (secondsUntilResend == 0) return;
    _countdown = Timer.periodic(const Duration(seconds: 1), (timer) {
      secondsUntilResend = secondsUntilResend > 0 ? secondsUntilResend - 1 : 0;
      if (secondsUntilResend == 0) timer.cancel();
      notifyListeners();
    });
  }

  @override
  void dispose() {
    _countdown?.cancel();
    super.dispose();
  }
}
