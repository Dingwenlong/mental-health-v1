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

abstract interface class AuthGateway {
  Future<String> login({
    required String email,
    required String password,
    String? totpCode,
  });

  Future<String> beginMfaSetup(String setupToken);

  Future<void> confirmMfaSetup(String setupToken, String totpCode);
}

class ApiAuthGateway implements AuthGateway {
  const ApiAuthGateway(this._client);

  final ApiClient _client;

  @override
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

  @override
  Future<String> beginMfaSetup(String setupToken) async {
    final response = await _client.post(
      'auth/mfa/setup',
      data: const <String, dynamic>{'totpCode': null},
      bearerToken: setupToken,
    );
    return response['manualKey'] as String;
  }

  @override
  Future<void> confirmMfaSetup(String setupToken, String totpCode) async {
    await _client.post(
      'auth/mfa/setup',
      data: <String, dynamic>{'totpCode': totpCode},
      bearerToken: setupToken,
    );
  }
}

class AuthStore extends ChangeNotifier {
  AuthStore({required this.gateway, required this.storage});

  final AuthGateway gateway;
  final TokenStorage storage;

  bool isAuthenticated = false;
  bool isBusy = false;
  bool needsMfaCode = false;
  bool needsMfaSetup = false;
  String? mfaManualKey;
  String? errorCopyKey;
  String? serverMessage;
  String? _setupToken;
  String _email = '';
  String _password = '';

  Future<void> restore() async {
    isAuthenticated = (await storage.readAccessToken())?.isNotEmpty ?? false;
    notifyListeners();
  }

  Future<bool> login({
    required String email,
    required String password,
    String? totpCode,
  }) async {
    _email = email.trim();
    _password = password;
    return _runLogin(totpCode: totpCode);
  }

  Future<bool> completeMfaSetup(String totpCode) async {
    final setupToken = _setupToken;
    if (setupToken == null) {
      return false;
    }

    isBusy = true;
    errorCopyKey = null;
    notifyListeners();
    try {
      await gateway.confirmMfaSetup(setupToken, totpCode);
      needsMfaSetup = false;
      return await _runLogin(totpCode: totpCode, manageBusy: false);
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
    isAuthenticated = false;
    needsMfaCode = false;
    needsMfaSetup = false;
    _password = '';
    notifyListeners();
  }

  Future<bool> _runLogin({String? totpCode, bool manageBusy = true}) async {
    if (manageBusy) {
      isBusy = true;
      errorCopyKey = null;
      serverMessage = null;
      notifyListeners();
    }
    try {
      final token = await gateway.login(
        email: _email,
        password: _password,
        totpCode: totpCode,
      );
      await storage.writeAccessToken(token);
      isAuthenticated = true;
      needsMfaCode = false;
      needsMfaSetup = false;
      _password = '';
      return true;
    } on ApiFailure catch (failure) {
      if (failure.code == 'MFA_REQUIRED') {
        final setupToken = failure.data['setupToken']?.toString();
        if (setupToken != null && setupToken.isNotEmpty) {
          _setupToken = setupToken;
          try {
            mfaManualKey = await gateway.beginMfaSetup(setupToken);
            needsMfaSetup = true;
            needsMfaCode = false;
          } on ApiFailure catch (setupFailure) {
            _setFailure(setupFailure);
            return false;
          }
        } else {
          needsMfaCode = true;
          needsMfaSetup = false;
        }
      }
      _setFailure(failure);
      return false;
    } finally {
      if (manageBusy) {
        isBusy = false;
        notifyListeners();
      }
    }
  }

  void _setFailure(ApiFailure failure) {
    errorCopyKey = switch (failure.code) {
      'INVALID_CREDENTIALS' => 'auth.invalidCredentials',
      'INVALID_MFA_CODE' => 'auth.invalidMfaCode',
      'MFA_REQUIRED' => 'auth.mfaRequired',
      'FORBIDDEN_RESOURCE' => 'error.forbidden',
      _ => 'error.retry',
    };
    serverMessage = failure.message;
  }
}
