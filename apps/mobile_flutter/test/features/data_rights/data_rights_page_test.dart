import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/features/data_rights/data_rights_page.dart';

void main() {
  testWidgets('default export excludes raw media and saves the zip', (
    tester,
  ) async {
    final gateway = _Gateway();
    final saver = _Saver();
    await tester.pumpWidget(
      MaterialApp(
        home: DataRightsPage(gateway: gateway, saver: saver),
      ),
    );

    await tester.tap(find.text('导出我的数据'));
    await tester.pumpAndSettle();

    expect(gateway.exports, <(bool, bool)>[(false, false)]);
    expect(saver.savedFileNames, <String>['my-data.zip']);
    expect(find.text('已保存到下载目录：my-data.zip'), findsOneWidget);
  });

  testWidgets('raw media is exported only after a second confirmation', (
    tester,
  ) async {
    final gateway = _Gateway();
    await tester.pumpWidget(
      MaterialApp(
        home: DataRightsPage(gateway: gateway, saver: _Saver()),
      ),
    );

    await tester.tap(find.byKey(const Key('include-raw-media')));
    await tester.tap(find.text('导出我的数据'));
    await tester.pumpAndSettle();
    expect(find.text('再次确认包含原始录音和录像'), findsOneWidget);
    expect(gateway.exports, isEmpty);

    await tester.tap(find.text('确认包含'));
    await tester.pumpAndSettle();

    expect(gateway.exports, <(bool, bool)>[(true, true)]);
  });

  testWidgets('data deletion waits for confirmation', (tester) async {
    final gateway = _Gateway();
    await tester.pumpWidget(
      MaterialApp(
        home: DataRightsPage(gateway: gateway, saver: _Saver()),
      ),
    );

    await tester.tap(find.text('清除我的数据'));
    await tester.pumpAndSettle();
    expect(gateway.deleted, isFalse);

    await tester.tap(find.text('确认清除'));
    await tester.pumpAndSettle();

    expect(gateway.deleted, isTrue);
    expect(find.text('数据已清除'), findsOneWidget);
  });
}

class _Gateway implements DataRightsGateway {
  final List<(bool, bool)> exports = <(bool, bool)>[];
  bool deleted = false;

  @override
  Future<DataExportPackage> export({
    required bool includeRawMedia,
    required bool confirmRawMedia,
  }) async {
    exports.add((includeRawMedia, confirmRawMedia));
    return DataExportPackage(
      fileName: 'my-data.zip',
      bytes: Uint8List.fromList(<int>[1, 2, 3]),
    );
  }

  @override
  Future<void> deleteDemoData() async {
    deleted = true;
  }
}

class _Saver implements DataExportSaver {
  final List<String> savedFileNames = <String>[];

  @override
  Future<String> save(DataExportPackage dataPackage) async {
    savedFileNames.add(dataPackage.fileName);
    return dataPackage.fileName;
  }
}
