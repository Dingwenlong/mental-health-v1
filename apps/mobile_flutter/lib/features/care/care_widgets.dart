import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import 'care_repository.dart';

class CarePageFrame<T> extends StatefulWidget {
  const CarePageFrame({
    required this.title,
    required this.load,
    required this.builder,
    this.actions = const [],
    super.key,
  });
  final String title;
  final Future<T> Function() load;
  final Widget Function(T data, VoidCallback reload) builder;
  final List<Widget> actions;
  @override
  State<CarePageFrame<T>> createState() => _CarePageFrameState<T>();
}

class _CarePageFrameState<T> extends State<CarePageFrame<T>>
    with WidgetsBindingObserver {
  late Future<T> _future;
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _future = widget.load();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _reload();
  }

  void _reload() {
    if (mounted) {
      setState(() {
        _future = widget.load();
      });
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: Text(widget.title),
      actions: [
        ...widget.actions,
        IconButton(
          onPressed: _reload,
          tooltip: careCopy('care.refresh'),
          icon: const Icon(Icons.refresh),
        ),
      ],
    ),
    body: FutureBuilder<T>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(careCopy('care.retry')),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: _reload,
                    child: Text(careCopy('care.refresh')),
                  ),
                ],
              ),
            ),
          );
        }
        return widget.builder(snapshot.data as T, _reload);
      },
    ),
  );
}

class CarePagination extends StatelessWidget {
  const CarePagination({
    required this.page,
    required this.total,
    required this.onChanged,
    super.key,
  });
  final int page, total;
  final ValueChanged<int> onChanged;
  @override
  Widget build(BuildContext context) => Row(
    mainAxisAlignment: MainAxisAlignment.spaceBetween,
    children: [
      TextButton(
        onPressed: page > 1 ? () => onChanged(page - 1) : null,
        child: Text(careCopy('care.previous')),
      ),
      Text('$page / ${(total / 15).ceil().clamp(1, 10000)}'),
      TextButton(
        onPressed: page * 15 < total ? () => onChanged(page + 1) : null,
        child: Text(careCopy('care.next')),
      ),
    ],
  );
}

void careError(BuildContext context, Object error) {
  final key = error is ApiFailure && error.status == 409
      ? 'care.conflict'
      : 'care.retry';
  ScaffoldMessenger.of(context)
      .showSnackBar(SnackBar(content: Text(careCopy(key))));
}

Widget careNotice(String key) => Padding(
  padding: const EdgeInsets.symmetric(vertical: 12),
  child: Text(careCopy(key)),
);
