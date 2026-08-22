import 'dart:io';

import 'package:crypto/crypto.dart';

import '../../core/api/api_client.dart';

class MediaRecordingUploader {
  const MediaRecordingUploader(this._client);

  static const int chunkSize = 1024 * 1024;

  final ApiClient _client;

  Future<void> upload({
    required String sessionId,
    required File file,
    required String idempotencyKey,
  }) async {
    final length = await file.length();
    if (length == 0) {
      throw const ApiFailure(
        code: 'EMPTY_RECORDING',
        message: 'The recording is empty.',
      );
    }
    final expectedChunks = (length + chunkSize - 1) ~/ chunkSize;
    final created = await _client.post(
      'uploads',
      data: <String, dynamic>{
        'sessionId': sessionId,
        'contentType': 'video/mp4',
        'expectedChunks': expectedChunks,
        'idempotencyKey': idempotencyKey,
      },
    );
    final uploadId = created['id']?.toString();
    if (uploadId == null || uploadId.isEmpty) {
      throw const ApiFailure(
        code: 'INVALID_RESPONSE',
        message: 'Upload id is missing.',
      );
    }

    final input = await file.open();
    try {
      for (var index = 0; index < expectedChunks; index += 1) {
        final bytes = await input.read(chunkSize);
        await _retry(
          () => _client.putBytes('uploads/$uploadId/chunks/$index', bytes),
        );
      }
    } finally {
      await input.close();
    }

    final digest = await sha256.bind(file.openRead()).first;
    await _retry(
      () => _client.post(
        'uploads/$uploadId/complete',
        data: <String, dynamic>{
          'expectedSha256': digest.toString(),
          'idempotencyKey': '$idempotencyKey-complete',
        },
      ),
    );
  }

  Future<T> _retry<T>(Future<T> Function() operation) async {
    Object? lastError;
    for (var attempt = 0; attempt < 3; attempt += 1) {
      try {
        return await operation();
      } catch (error) {
        lastError = error;
        if (attempt < 2) {
          await Future<void>.delayed(
            Duration(milliseconds: 250 * (attempt + 1)),
          );
        }
      }
    }
    throw lastError!;
  }
}
