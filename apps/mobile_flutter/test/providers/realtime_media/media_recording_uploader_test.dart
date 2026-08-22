import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/api/api_client.dart';
import 'package:mobile_flutter/providers/realtime_media/media_recording_uploader.dart';

void main() {
  test(
    'recording is split, retried, and completed with the full hash',
    () async {
      final directory = await Directory.systemTemp.createTemp(
        'media-uploader-test-',
      );
      try {
        final bytes = List<int>.generate(
          MediaRecordingUploader.chunkSize + 17,
          (index) => index % 251,
        );
        final file = File('${directory.path}${Platform.pathSeparator}clip.mp4');
        await file.writeAsBytes(bytes, flush: true);
        final client = _FakeApiClient();

        await MediaRecordingUploader(client).upload(
          sessionId: 'session-1',
          file: file,
          idempotencyKey: 'recording-1',
        );

        expect(client.expectedChunks, 2);
        expect(client.chunkAttempts[0], 3);
        expect(client.chunks[0], hasLength(MediaRecordingUploader.chunkSize));
        expect(client.chunks[1], hasLength(17));
        expect(client.completedHash, sha256.convert(bytes).toString());
        expect(client.completionKey, 'recording-1-complete');
      } finally {
        await directory.delete(recursive: true);
      }
    },
  );

  test('empty recording is rejected before an upload is created', () async {
    final directory = await Directory.systemTemp.createTemp(
      'empty-media-uploader-test-',
    );
    try {
      final file = File('${directory.path}${Platform.pathSeparator}empty.mp4');
      await file.writeAsBytes(const <int>[]);
      final client = _FakeApiClient();

      await expectLater(
        MediaRecordingUploader(client).upload(
          sessionId: 'session-1',
          file: file,
          idempotencyKey: 'recording-empty',
        ),
        throwsA(
          isA<ApiFailure>().having(
            (failure) => failure.code,
            'code',
            'EMPTY_RECORDING',
          ),
        ),
      );
      expect(client.expectedChunks, isNull);
    } finally {
      await directory.delete(recursive: true);
    }
  });
}

class _FakeApiClient extends ApiClient {
  _FakeApiClient()
    : super(baseUrl: 'http://example.test/', readAccessToken: () async => null);

  final Map<int, int> chunkAttempts = <int, int>{};
  final Map<int, List<int>> chunks = <int, List<int>>{};
  int? expectedChunks;
  String? completedHash;
  String? completionKey;

  @override
  Future<Map<String, dynamic>> post(
    String path, {
    Map<String, dynamic>? data,
    String? bearerToken,
  }) async {
    if (path == 'uploads') {
      expectedChunks = data?['expectedChunks'] as int?;
      return <String, dynamic>{'id': 'upload-1'};
    }
    if (path == 'uploads/upload-1/complete') {
      completedHash = data?['expectedSha256'] as String?;
      completionKey = data?['idempotencyKey'] as String?;
      return <String, dynamic>{'status': 'Completed'};
    }
    throw StateError('Unexpected POST path: $path');
  }

  @override
  Future<Map<String, dynamic>> putBytes(String path, List<int> bytes) async {
    final index = int.parse(path.split('/').last);
    final attempt = (chunkAttempts[index] ?? 0) + 1;
    chunkAttempts[index] = attempt;
    if (index == 0 && attempt < 3) {
      throw const ApiFailure(code: 'NETWORK_ERROR', message: 'retry');
    }
    chunks[index] = List<int>.from(bytes);
    return <String, dynamic>{'index': index};
  }
}
