import 'package:dio/dio.dart';

typedef AccessTokenReader = Future<String?> Function();

class ApiFailure implements Exception {
  const ApiFailure({
    required this.code,
    required this.message,
    this.status,
    this.data = const <String, dynamic>{},
  });

  final String code;
  final String message;
  final int? status;
  final Map<String, dynamic> data;

  factory ApiFailure.fromDio(DioException error) {
    final raw = error.response?.data;
    final problem = raw is Map
        ? Map<String, dynamic>.from(raw)
        : const <String, dynamic>{};
    return ApiFailure(
      code: problem['code']?.toString() ?? 'NETWORK_ERROR',
      message:
          problem['detail']?.toString() ??
          problem['title']?.toString() ??
          error.message ??
          'Request failed',
      status: error.response?.statusCode,
      data: problem,
    );
  }

  @override
  String toString() => 'ApiFailure($code, $status)';
}

class ApiClient {
  ApiClient({required String baseUrl, required this.readAccessToken, Dio? dio})
    : _dio =
          dio ??
          Dio(
            BaseOptions(
              baseUrl: baseUrl,
              connectTimeout: const Duration(seconds: 10),
              receiveTimeout: const Duration(seconds: 20),
              sendTimeout: const Duration(seconds: 20),
              headers: const <String, String>{
                'Accept': 'application/json',
                'Content-Type': 'application/json',
              },
            ),
          );

  final Dio _dio;
  final AccessTokenReader readAccessToken;

  Future<List<dynamic>> getList(String path) async {
    final response = await _request<dynamic>('GET', path);
    if (response is! List) {
      throw const ApiFailure(
        code: 'INVALID_RESPONSE',
        message: 'Expected a JSON array',
      );
    }
    return response;
  }

  Future<Map<String, dynamic>> post(
    String path, {
    Map<String, dynamic>? data,
    String? bearerToken,
  }) async {
    final response = await _request<dynamic>(
      'POST',
      path,
      data: data,
      bearerToken: bearerToken,
    );
    if (response is! Map) {
      throw const ApiFailure(
        code: 'INVALID_RESPONSE',
        message: 'Expected a JSON object',
      );
    }
    return Map<String, dynamic>.from(response);
  }

  Future<void> delete(String path) async {
    await _request<dynamic>('DELETE', path);
  }

  Future<T> _request<T>(
    String method,
    String path, {
    Object? data,
    String? bearerToken,
  }) async {
    try {
      final token = bearerToken ?? await readAccessToken();
      final response = await _dio.request<T>(
        path,
        data: data,
        options: Options(
          method: method,
          headers: token == null || token.isEmpty
              ? null
              : <String, String>{'Authorization': 'Bearer $token'},
        ),
      );
      return response.data as T;
    } on DioException catch (error) {
      throw ApiFailure.fromDio(error);
    }
  }
}
