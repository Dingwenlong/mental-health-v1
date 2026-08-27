import 'package:flutter/material.dart';

import 'care_repository.dart';
import 'care_widgets.dart';

class SharingPage extends StatefulWidget {
  const SharingPage({required this.repository, super.key});
  final CareRepository repository;
  @override
  State<SharingPage> createState() => _SharingPageState();
}

class _SharingPageState extends State<SharingPage> {
  int _page = 1;
  bool _busy = false;
  final Set<String> _checked = {};
  Future<void> _change(
    Future<void> Function() action,
    VoidCallback reload,
  ) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await action();
      if (mounted) {
        _checked.clear();
        reload();
      }
    } catch (error) {
      if (mounted) careError(context, error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(
    BuildContext context,
  ) => CarePageFrame<(List<CareJson>, CareJson)>(
    key: ValueKey(_page),
    title: careCopy('care.sharing'),
    load: () async => (
      await widget.repository.candidates(),
      await widget.repository.grants(_page),
    ),
    builder: (data, reload) => ListView(
      padding: const EdgeInsets.all(20),
      children: [
        careNotice('care.sharingNotice'),
        if (data.$1.isEmpty) careNotice('care.noDoctors'),
        ...data.$1.map((doctor) {
          final id = doctor['followUpId'] as String;
          return Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    doctor['doctorName'] as String,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Text(careTime(doctor['dueAt'])),
                  CheckboxListTile(
                    contentPadding: EdgeInsets.zero,
                    value: _checked.contains(id),
                    title: Text(careCopy('care.sharingConfirm')),
                    onChanged: _busy
                        ? null
                        : (value) => setState(() {
                            if (value == true) {
                              _checked.add(id);
                            } else {
                              _checked.remove(id);
                            }
                          }),
                  ),
                  FilledButton(
                    onPressed: !_busy && _checked.contains(id)
                        ? () =>
                              _change(() => widget.repository.grant(id), reload)
                        : null,
                    child: Text(careCopy('care.grant')),
                  ),
                ],
              ),
            ),
          );
        }),
        ...careObjects(data.$2['items']).map(
          (grant) => Card(
            child: ListTile(
              title: Text(grant['doctorName'] as String),
              subtitle: Text(
                careCopy(
                  grant['active'] == true ? 'care.shared' : 'care.expiredGrant',
                ),
              ),
              trailing: grant['active'] == true
                  ? TextButton(
                      onPressed: _busy
                          ? null
                          : () => _change(
                              () => widget.repository.revoke(
                                grant['id'] as String,
                              ),
                              reload,
                            ),
                      child: Text(careCopy('care.revoke')),
                    )
                  : null,
            ),
          ),
        ),
        CarePagination(
          page: _page,
          total: (data.$2['total'] as num).toInt(),
          onChanged: (page) => setState(() => _page = page),
        ),
      ],
    ),
  );
}
