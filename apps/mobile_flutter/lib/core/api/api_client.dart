import 'package:dio/dio.dart';

class ApiBinaryResponse {
  const ApiBinaryResponse({required this.bytes, required this.fileName});

  final List<int> bytes;
  final String fileName;
}

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
    final networkMessage = <String>[
      if (error.message case final message? when message.isNotEmpty) message,
      if (error.error case final cause?) cause.toString(),
    ].join(' · ');
    return ApiFailure(
      code: problem['code']?.toString() ?? 'NETWORK_ERROR',
      message:
          problem['detail']?.toString() ??
          problem['title']?.toString() ??
          (networkMessage.isEmpty ? null : networkMessage) ??
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

  Future<Map<String, dynamic>> get(String path) async {
    final response = await _request<dynamic>('GET', path);
    if (response is! Map) {
      throw const ApiFailure(
        code: 'INVALID_RESPONSE',
        message: 'Expected a JSON object',
      );
    }
    return Map<String, dynamic>.from(response);
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

  Future<ApiBinaryResponse> getBytes(String path) async {
    try {
      final token = await readAccessToken();
      final response = await _dio.get<List<int>>(
        path,
        options: Options(
          responseType: ResponseType.bytes,
          headers: token == null || token.isEmpty
              ? null
              : <String, String>{'Authorization': 'Bearer $token'},
        ),
      );
      final bytes = response.data;
      if (bytes == null) {
        throw const ApiFailure(
          code: 'INVALID_RESPONSE',
          message: 'Expected binary response',
        );
      }
      return ApiBinaryResponse(
        bytes: bytes,
        fileName: _fileNameFrom(response.headers) ?? 'my-demo-data.zip',
      );
    } on DioException catch (error) {
      throw ApiFailure.fromDio(error);
    }
  }

  Future<Map<String, dynamic>> putBytes(String path, List<int> bytes) async {
    try {
      final token = await readAccessToken();
      final response = await _dio.put<dynamic>(
        path,
        data: bytes,
        options: Options(
          contentType: 'application/octet-stream',
          headers: token == null || token.isEmpty
              ? null
              : <String, String>{'Authorization': 'Bearer $token'},
        ),
      );
      if (response.data is! Map) {
        throw const ApiFailure(
          code: 'INVALID_RESPONSE',
          message: 'Expected a JSON object',
        );
      }
      return Map<String, dynamic>.from(response.data as Map);
    } on DioException catch (error) {
      throw ApiFailure.fromDio(error);
    }
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

  static String? _fileNameFrom(Headers headers) {
    final disposition = headers.value('content-disposition');
    if (disposition == null) return null;
    final encoded = RegExp(
      r"filename\*=UTF-8''([^;]+)",
      caseSensitive: false,
    ).firstMatch(disposition)?.group(1);
    if (encoded != null && encoded.isNotEmpty) {
      return Uri.decodeComponent(encoded);
    }
    return RegExp(
      r'filename="?([^";]+)"?',
      caseSensitive: false,
    ).firstMatch(disposition)?.group(1);
  }
}
