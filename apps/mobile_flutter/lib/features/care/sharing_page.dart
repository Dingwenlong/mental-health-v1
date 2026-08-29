import 'package:flutter/material.dart';

import '../../design/app_design.dart';
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
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
      children: [
        AppNotice(
          text: careCopy('care.sharingNotice'),
          icon: Icons.shield_outlined,
        ),
        const SizedBox(height: 20),
        AppSectionHeader(title: careCopy('care.currentShare')),
        if (data.$1.isEmpty && (data.$2['total'] as num) == 0)
          AppStatePanel(message: careCopy('care.noDoctors')),
        ...data.$1.map((doctor) {
          final id = doctor['followUpId'] as String;
          return Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: AppPanel(
              padding: const EdgeInsets.all(18),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const Icon(
                        Icons.medical_services_outlined,
                        color: AppPalette.pine,
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          doctor['doctorName'] as String,
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                      ),
                      AppStatusBadge(
                        label: careCopy('care.notShared'),
                        tone: AppTone.neutral,
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(
                    '${careCopy('care.nextFollowUp')} · ${careTime(doctor['dueAt'])}',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
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
                  FilledButton.icon(
                    onPressed: !_busy && _checked.contains(id)
                        ? () =>
                              _change(() => widget.repository.grant(id), reload)
                        : null,
                    icon: const Icon(Icons.lock_open_rounded),
                    label: Text(careCopy('care.grant')),
                  ),
                ],
              ),
            ),
          );
        }),
        ...careObjects(data.$2['items']).map(
          (grant) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: AppPanel(
              padding: EdgeInsets.zero,
              child: ListTile(
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: 18,
                  vertical: 8,
                ),
                leading: Icon(
                  grant['active'] == true
                      ? Icons.verified_user_outlined
                      : Icons.lock_outline_rounded,
                  color: grant['active'] == true
                      ? AppPalette.pine
                      : AppPalette.muted,
                ),
                title: Text(grant['doctorName'] as String),
                subtitle: Padding(
                  padding: const EdgeInsets.only(top: 5),
                  child: Align(
                    alignment: Alignment.centerLeft,
                    child: AppStatusBadge(
                      label: careCopy(
                        grant['active'] == true
                            ? 'care.shared'
                            : 'care.expiredGrant',
                      ),
                      tone: grant['active'] == true
                          ? AppTone.success
                          : AppTone.neutral,
                    ),
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
