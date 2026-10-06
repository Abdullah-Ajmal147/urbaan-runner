import 'dart:async';
import 'dart:ui';

import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:firebase_remote_config/firebase_remote_config.dart';
import 'package:flutter/foundation.dart';

import '../analytics/analytics.dart';
import '../log.dart';
import '../remote_config/remote_config.dart';
import '../sync/cloud_sync.dart';

/// How Firebase is connected in this build (HANDOVER §7.1, D26).
enum FirebaseMode { off, emulator, real }

abstract final class FirebaseSetup {
  static const demoProject = 'demo-urban-runner';

  /// Never throws: any problem means [FirebaseMode.off] and the game stays fully playable offline.
  static Future<FirebaseMode> init() async {
    const emulatorHost = String.fromEnvironment('UR_FIREBASE_EMULATOR');
    try {
      if (emulatorHost.isNotEmpty && !kReleaseMode) {
        await Firebase.initializeApp(
          options: const FirebaseOptions(
            apiKey: 'demo-api-key',
            appId: '1:000000000000:android:0000000000000000',
            messagingSenderId: '000000000000',
            projectId: demoProject,
          ),
        );
        await FirebaseAuth.instance.useAuthEmulator(emulatorHost, 9099);
        FirebaseFirestore.instance.useFirestoreEmulator(emulatorHost, 8080);
        Log.write(Log.game, 'firebase emulator $emulatorHost');
        return FirebaseMode.emulator;
      }
      // Reads the values the google-services Gradle plugin generates; throws when the file wasn't there.
      await Firebase.initializeApp();
      await FirebaseCrashlytics.instance.setCrashlyticsCollectionEnabled(!kDebugMode);
      FlutterError.onError = FirebaseCrashlytics.instance.recordFlutterFatalError;
      PlatformDispatcher.instance.onError = (error, stack) {
        FirebaseCrashlytics.instance.recordError(error, stack, fatal: true);
        return true;
      };
      Log.write(Log.game, 'firebase project ${Firebase.app().options.projectId}');
      return FirebaseMode.real;
    } catch (e) {
      Log.write(Log.game, 'firebase off (${e.runtimeType})');
      return FirebaseMode.off;
    }
  }
}

/// Firestore + anonymous Auth behind [CloudBackend].
class FirestoreBackend implements CloudBackend {
  FirestoreBackend({FirebaseAuth? auth, FirebaseFirestore? db, this.timeout = const Duration(seconds: 15)})
      : _auth = auth ?? FirebaseAuth.instance,
        _db = db ?? FirebaseFirestore.instance;

  final FirebaseAuth _auth;
  final FirebaseFirestore _db;
  final Duration timeout;

  @override
  Future<String> signIn() => _guard(() async => (_auth.currentUser ?? (await _auth.signInAnonymously()).user!).uid);

  @override
  Future<Map<String, Object?>?> read(String path) => _guard(() async {
        // From the server: merging against a stale cache could undo another device's progress.
        final snap = await _db.doc(path).get(const GetOptions(source: Source.server));
        final data = snap.data();
        return data == null ? null : _fromFirestore(data) as Map<String, Object?>;
      });

  @override
  Future<void> writeAll(Map<String, Map<String, Object?>> docs) => _guard(() async {
        final batch = _db.batch();
        docs.forEach((path, data) => batch.set(_db.doc(path), _toFirestore(data) as Map<String, Object?>));
        await batch.commit();
      });

  Future<T> _guard<T>(Future<T> Function() op) async {
    try {
      return await op().timeout(timeout);
    } on TimeoutException {
      throw const SyncOfflineException('timeout');
    } on FirebaseAuthException catch (e) {
      if (e.code == 'network-request-failed') throw SyncOfflineException(e.code);
      rethrow;
    } on FirebaseException catch (e) {
      if (e.code == 'unavailable' || e.code == 'deadline-exceeded') throw SyncOfflineException(e.code);
      rethrow;
    }
  }

  static Object? _toFirestore(Object? v) => switch (v) {
        DateTime d => Timestamp.fromDate(d),
        Map m => m.map((k, x) => MapEntry(k as String, _toFirestore(x))),
        List l => l.map(_toFirestore).toList(),
        _ => v,
      };

  static Object? _fromFirestore(Object? v) => switch (v) {
        Timestamp t => t.toDate().toUtc(),
        Map m => m.map((k, x) => MapEntry(k as String, _fromFirestore(x))),
        List l => l.map(_fromFirestore).toList(),
        _ => v,
      };
}

/// Firebase Analytics, plus the same debug log line so logcat checks work on every build.
class FirebaseAnalyticsSink implements Analytics {
  FirebaseAnalyticsSink([FirebaseAnalytics? fa]) : _fa = fa ?? FirebaseAnalytics.instance;
  final FirebaseAnalytics _fa;
  final _debug = DebugAnalytics();

  @override
  bool get enabled => _debug.enabled;

  @override
  set enabled(bool value) {
    _debug.enabled = value;
    unawaited(_fa.setAnalyticsCollectionEnabled(value));
  }

  @override
  void log(String name, [Map<String, Object> params = const {}]) {
    _debug.log(name, params); // validates the name and respects "Share usage data"
    if (!enabled) return;
    unawaited(_fa.logEvent(name: name, parameters: params).catchError((Object e) => Log.write(Log.error, 'analytics: $e')));
  }
}

/// Firebase Remote Config: values fetched in one session are activated at the next start (§7.3).
class FirebaseRemoteConfigSource implements RemoteConfigSource {
  FirebaseRemoteConfigSource([FirebaseRemoteConfig? rc]) : _rc = rc ?? FirebaseRemoteConfig.instance;
  final FirebaseRemoteConfig _rc;

  @override
  Future<Map<String, Object?>> activatedValues() async {
    await _rc.activate();
    return {
      for (final e in _rc.getAll().entries)
        if (e.value.source == ValueSource.valueRemote) e.key: e.value.asString(),
    };
  }

  @override
  Future<void> fetchForNextStart() async {
    await _rc.setConfigSettings(RemoteConfigSettings(fetchTimeout: const Duration(seconds: 10), minimumFetchInterval: const Duration(hours: 1)));
    await _rc.fetch(); // fetched, not activated: it applies from the next app start
  }
}
