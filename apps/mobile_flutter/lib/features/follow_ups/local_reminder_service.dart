import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_timezone/flutter_timezone.dart';
import 'package:timezone/data/latest_all.dart' as tz_data;
import 'package:timezone/timezone.dart' as tz;

import '../../generated/ui_copy.g.dart';
import 'follow_up_model.dart';

abstract interface class LocalReminderService {
  Future<bool> schedule(UserFollowUp task);
}

class DeviceLocalReminderService implements LocalReminderService {
  DeviceLocalReminderService({FlutterLocalNotificationsPlugin? plugin})
    : _plugin = plugin ?? FlutterLocalNotificationsPlugin();

  final FlutterLocalNotificationsPlugin _plugin;
  bool _initialized = false;

  @override
  Future<bool> schedule(UserFollowUp task) async {
    final dueAt = task.dueAt;
    if (dueAt == null || !dueAt.isAfter(DateTime.now())) return true;
    await _initialize();
    if (!await _requestPermission()) return false;

    final reminderAt = dueAt.subtract(const Duration(minutes: 30));
    final scheduledAt = reminderAt.isAfter(DateTime.now()) ? reminderAt : dueAt;
    await _plugin.zonedSchedule(
      id: _stableNotificationId(task.id),
      title: UiCopy.get('notification.followUpTitle'),
      body: UiCopy.get('notification.followUpBody'),
      scheduledDate: tz.TZDateTime.from(scheduledAt, tz.local),
      notificationDetails: NotificationDetails(
        android: AndroidNotificationDetails(
          'follow_up',
          UiCopy.get('notification.followUpTitle'),
          channelDescription: UiCopy.get(
            'notification.followUpChannelDescription',
          ),
        ),
      ),
      androidScheduleMode: AndroidScheduleMode.exactAllowWhileIdle,
      payload: task.id,
    );
    return true;
  }

  Future<void> _initialize() async {
    if (_initialized) return;
    await _configureLocalTimeZone();
    await _plugin.initialize(
      settings: const InitializationSettings(
        android: AndroidInitializationSettings('@mipmap/ic_launcher'),
      ),
    );
    _initialized = true;
  }

  Future<bool> _requestPermission() async {
    if (kIsWeb || !Platform.isAndroid) return true;
    final android = _plugin
        .resolvePlatformSpecificImplementation<
          AndroidFlutterLocalNotificationsPlugin
        >();
    final notifications = await android?.requestNotificationsPermission();
    if (notifications == false) return false;
    final exact = await android?.requestExactAlarmsPermission();
    return exact != false;
  }

  static Future<void> _configureLocalTimeZone() async {
    tz_data.initializeTimeZones();
    if (kIsWeb || Platform.isWindows || Platform.isLinux) return;
    final local = await FlutterTimezone.getLocalTimezone();
    tz.setLocalLocation(tz.getLocation(local.identifier));
  }

  static int _stableNotificationId(String value) {
    var hash = 0;
    for (final unit in value.codeUnits) {
      hash = (hash * 31 + unit) & 0x7fffffff;
    }
    return hash;
  }
}
