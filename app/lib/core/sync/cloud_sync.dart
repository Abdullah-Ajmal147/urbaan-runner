import 'dart:async';

import '../log.dart';

/// A document database with anonymous sign-in: Firestore in the app, an in-memory fake in tests.
/// Values may contain [DateTime]s; the backend stores them as its native timestamp type.
abstract class CloudBackend {
  /// Signs in anonymously if needed and returns the uid, or throws when offline.
  Future<String> signIn();

  /// Reads one document; null if it doesn't exist.
  Future<Map<String, Object?>?> read(String path);

  /// Writes several documents together (all or nothing).
  Future<void> writeAll(Map<String, Map<String, Object?>> docs);
}

/// How one game's local state maps to cloud documents and how two copies merge (HANDOVER §7.1).
abstract class SyncAdapter<T> {
  /// Every document path this game stores for [uid].
  List<String> paths(String uid);

  /// The cloud copy, or null when there is none yet.
  T? decode(String uid, Map<String, Map<String, Object?>?> docs);

  /// Documents to upload. [remote] is what the server holds now, so the adapter can respect
  /// server-side rules (e.g. how much a value may rise in one write).
  Map<String, Map<String, Object?>> encode(String uid, T value, Map<String, Map<String, Object?>?> remote);

  T merge(T local, T remote);
}

enum SyncStatus { off, idle, syncing, synced, offline, failed }

/// Local-first sync: the device copy is always the one the game uses; the cloud copy is merged in when
/// reachable and the merge result is written back. Failures never block play, they just retry later.
class CloudSync<T> {
  CloudSync({
    required this.backend,
    required this.adapter,
    required this.readLocal,
    required this.applyMerged,
    this.debounce = const Duration(seconds: 2),
    this.retryAfter = const Duration(seconds: 30),
  });

  final CloudBackend backend;
  final SyncAdapter<T> adapter;
  final T Function() readLocal;
  final Future<void> Function(T merged) applyMerged;
  final Duration debounce;
  final Duration retryAfter;

  SyncStatus _status = SyncStatus.idle;
  SyncStatus get status => _status;
  String? uid;
  Timer? _timer;
  Future<bool>? _running;
  bool _again = false;
  bool _disposed = false;
  bool _applying = false;

  /// Something changed locally: sync soon (several saves in a row cause one sync).
  /// Saves caused by applying a merge don't count, or every sync would trigger the next.
  void schedule() {
    if (_disposed || _applying) return;
    _timer?.cancel();
    _timer = Timer(debounce, syncNow);
  }

  /// Sign in, read, merge, write. Returns true when the cloud now matches the device.
  Future<bool> syncNow() {
    if (_disposed) return Future.value(false);
    if (_running != null) {
      if (!_applying) _again = true; // a save arrived mid-sync: run once more afterwards
      return _running!;
    }
    _timer?.cancel();
    return _running = _sync().whenComplete(() {
      _running = null;
      if (_again && !_disposed) {
        _again = false;
        schedule();
      }
    });
  }

  Future<bool> _sync() async {
    _status = SyncStatus.syncing;
    try {
      final id = uid ??= await backend.signIn();
      final paths = adapter.paths(id);
      final remoteDocs = {for (final p in paths) p: await backend.read(p)};
      final remote = adapter.decode(id, remoteDocs);
      var value = readLocal();
      if (remote != null) {
        value = adapter.merge(value, remote);
        _applying = true;
        try {
          await applyMerged(value);
        } finally {
          _applying = false;
        }
      }
      final out = adapter.encode(id, value, remoteDocs);
      await backend.writeAll(out);
      final complete = _matches(out, value, id);
      _status = SyncStatus.synced;
      Log.write(Log.game, 'sync ok uid=$id${complete ? '' : ' (partial, more to upload)'}');
      if (!complete) schedule();
      return complete;
    } on SyncOfflineException {
      _status = SyncStatus.offline;
    } catch (e) {
      _status = SyncStatus.failed;
      Log.write(Log.error, 'sync failed: $e');
    }
    if (!_disposed) {
      _timer?.cancel();
      _timer = Timer(retryAfter, syncNow);
    }
    return false;
  }

  /// Whether a second encode against what we just wrote would change nothing (no capped values left).
  bool _matches(Map<String, Map<String, Object?>> written, T value, String id) {
    final again = adapter.encode(id, value, written);
    for (final e in again.entries) {
      final w = written[e.key];
      if (w == null || !_deepEquals(w, e.value)) return false;
    }
    return true;
  }

  static bool _deepEquals(Object? a, Object? b) {
    if (a is Map && b is Map) {
      if (a.length != b.length) return false;
      for (final k in a.keys) {
        if (!b.containsKey(k) || !_deepEquals(a[k], b[k])) return false;
      }
      return true;
    }
    if (a is List && b is List) {
      if (a.length != b.length) return false;
      for (var i = 0; i < a.length; i++) {
        if (!_deepEquals(a[i], b[i])) return false;
      }
      return true;
    }
    return a == b;
  }

  void dispose() {
    _disposed = true;
    _timer?.cancel();
  }
}

/// Thrown by a backend when the network or the service can't be reached.
class SyncOfflineException implements Exception {
  const SyncOfflineException([this.message = 'offline']);
  final String message;
  @override
  String toString() => 'SyncOfflineException: $message';
}

/// In-memory backend for tests. Can go offline and enforce simple write checks like the rules do.
class MemoryCloudBackend implements CloudBackend {
  final docs = <String, Map<String, Object?>>{};
  bool online = true;
  int signIns = 0, writes = 0;
  String uidToIssue = 'test-uid';

  /// Rejects a write the way Firestore rules would (returns an error message, or null to accept).
  String? Function(String path, Map<String, Object?>? before, Map<String, Object?> after)? rules;

  @override
  Future<String> signIn() async {
    if (!online) throw const SyncOfflineException();
    signIns++;
    return uidToIssue;
  }

  @override
  Future<Map<String, Object?>?> read(String path) async {
    if (!online) throw const SyncOfflineException();
    final d = docs[path];
    return d == null ? null : Map.of(d);
  }

  @override
  Future<void> writeAll(Map<String, Map<String, Object?>> out) async {
    if (!online) throw const SyncOfflineException();
    for (final e in out.entries) {
      final err = rules?.call(e.key, docs[e.key], e.value);
      if (err != null) throw StateError('PERMISSION_DENIED: $err');
    }
    writes++;
    docs.addAll(out.map((k, v) => MapEntry(k, Map.of(v))));
  }
}
