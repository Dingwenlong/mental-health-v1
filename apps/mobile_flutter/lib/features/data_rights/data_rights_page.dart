import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/api/api_client.dart';
import '../../design/app_design.dart';
import '../../generated/ui_copy.g.dart';

class DataExportPackage {
  const DataExportPackage({required this.fileName, required this.bytes});

  final String fileName;
  final Uint8List bytes;
}

abstract interface class DataRightsGateway {
  Future<DataExportPackage> export({
    required bool includeRawMedia,
    required bool confirmRawMedia,
  });

  Future<void> deleteDemoData();
}

class ApiDataRightsGateway implements DataRightsGateway {
  const ApiDataRightsGateway(this.client);

  final ApiClient client;

  @override
  Future<DataExportPackage> export({
    required bool includeRawMedia,
    required bool confirmRawMedia,
  }) async {
    final query = includeRawMedia
        ? '?includeRawMedia=true&confirmRawMedia=$confirmRawMedia'
        : '';
    final response = await client.getBytes('data-rights/export$query');
    return DataExportPackage(
      fileName: response.fileName,
      bytes: Uint8List.fromList(response.bytes),
    );
  }

  @override
  Future<void> deleteDemoData() => client.delete('data-rights/demo-data');
}

abstract interface class DataExportSaver {
  Future<String> save(DataExportPackage dataPackage);
}

class AndroidDownloadsDataExportSaver implements DataExportSaver {
  static const _channel = MethodChannel(
    'com.example.mentalhealth.mobile_flutter/data_export',
  );

  const AndroidDownloadsDataExportSaver();

  @override
  Future<String> save(DataExportPackage dataPackage) async {
    final savedName = await _channel.invokeMethod<String>('saveDataExport', {
      'fileName': dataPackage.fileName,
      'bytes': dataPackage.bytes,
    });
    return savedName ?? dataPackage.fileName;
  }
}

class DataRightsPage extends StatefulWidget {
  const DataRightsPage({required this.gateway, required this.saver, super.key});

  final DataRightsGateway gateway;
  final DataExportSaver saver;

  @override
  State<DataRightsPage> createState() => _DataRightsPageState();
}

class _DataRightsPageState extends State<DataRightsPage> {
  bool _includeRawMedia = false;
  bool _exporting = false;
  bool _deleting = false;
  String? _savedFileName;
  String? _statusCopyKey;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(UiCopy.get('dataRights.title'))),
    body: ListView(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 36),
      children: <Widget>[
        AppPanel(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const Icon(Icons.download_outlined, color: AppPalette.pine),
                  const SizedBox(width: 10),
                  Text(
                    UiCopy.get('dataRights.exportSection'),
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Text(UiCopy.get('dataRights.exportHelp')),
              const SizedBox(height: 12),
              CheckboxListTile(
                key: const Key('include-raw-media'),
                contentPadding: EdgeInsets.zero,
                value: _includeRawMedia,
                title: Text(UiCopy.get('dataRights.includeRawMedia')),
                onChanged: _exporting
                    ? null
                    : (value) =>
                          setState(() => _includeRawMedia = value ?? false),
              ),
              FilledButton.icon(
                onPressed: _exporting || _deleting ? null : _export,
                icon: const Icon(Icons.download_outlined),
                label: Text(
                  UiCopy.get(
                    _exporting ? 'dataRights.exporting' : 'dataRights.export',
                  ),
                ),
              ),
            ],
          ),
        ),
        if (_savedFileName case final fileName?) ...<Widget>[
          const SizedBox(height: 12),
          AppNotice(
            text: '${UiCopy.get('dataRights.savedPrefix')}$fileName',
            icon: Icons.check_circle_outline_rounded,
          ),
        ],
        if (_statusCopyKey case final copyKey?) ...<Widget>[
          const SizedBox(height: 12),
          AppNotice(
            text: UiCopy.get(copyKey),
            tone: copyKey == 'dataRights.deleted'
                ? AppTone.success
                : AppTone.danger,
          ),
        ],
        const SizedBox(height: 28),
        AppPanel(
          color: const Color(0xFFFFF4F3),
          borderColor: const Color(0xFFE8B8B4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const Icon(Icons.delete_outline, color: AppPalette.danger),
                  const SizedBox(width: 10),
                  Text(
                    UiCopy.get('dataRights.deleteSection'),
                    style: Theme.of(context).textTheme.titleLarge
                        ?.copyWith(color: AppPalette.danger),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Text(UiCopy.get('dataRights.deleteHelp')),
              const SizedBox(height: 14),
              OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppPalette.danger,
                  side: const BorderSide(color: AppPalette.danger),
                ),
                onPressed: _exporting || _deleting ? null : _delete,
                icon: const Icon(Icons.delete_outline),
                label: Text(
                  UiCopy.get(
                    _deleting ? 'dataRights.deleting' : 'dataRights.delete',
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    ),
  );

  Future<void> _export() async {
    if (_includeRawMedia) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          title: Text(UiCopy.get('dataRights.rawMediaConfirmTitle')),
          content: Text(UiCopy.get('dataRights.rawMediaConfirmBody')),
          actions: <Widget>[
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(false),
              child: Text(UiCopy.get('common.cancel')),
            ),
            FilledButton(
              onPressed: () => Navigator.of(dialogContext).pop(true),
              child: Text(UiCopy.get('dataRights.rawMediaConfirm')),
            ),
          ],
        ),
      );
      if (confirmed != true) return;
    }

    setState(() {
      _exporting = true;
      _statusCopyKey = null;
    });
    try {
      final dataPackage = await widget.gateway.export(
        includeRawMedia: _includeRawMedia,
        confirmRawMedia: _includeRawMedia,
      );
      final savedFileName = await widget.saver.save(dataPackage);
      if (!mounted) return;
      setState(() => _savedFileName = savedFileName);
    } catch (_) {
      if (!mounted) return;
      setState(() => _statusCopyKey = 'error.retry');
    } finally {
      if (mounted) setState(() => _exporting = false);
    }
  }

  Future<void> _delete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(UiCopy.get('dataRights.deleteConfirmTitle')),
        content: Text(UiCopy.get('dataRights.deleteConfirmBody')),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(UiCopy.get('common.cancel')),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppPalette.danger),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(UiCopy.get('dataRights.deleteConfirm')),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() {
      _deleting = true;
      _statusCopyKey = null;
    });
    try {
      await widget.gateway.deleteDemoData();
      if (!mounted) return;
      setState(() {
        _savedFileName = null;
        _statusCopyKey = 'dataRights.deleted';
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _statusCopyKey = 'error.retry');
    } finally {
      if (mounted) setState(() => _deleting = false);
    }
  }
}
