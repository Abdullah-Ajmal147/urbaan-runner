import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

import '../log.dart';

/// Small JSON document store on the device. Saves are local-first: the app is fully playable offline.
abstract class LocalStore {
  Future<Map<String, Object?>?> read(String key);
  Future<void> write(String key, Map<String, Object?> value);
  Future<void> delete(String key);
}

class PrefsLocalStore implements LocalStore {
  PrefsLocalStore(this._prefs);
  final SharedPreferences _prefs;

  static Future<PrefsLocalStore> open() async => PrefsLocalStore(await SharedPreferences.getInstance());

  @override
  Future<Map<String, Object?>?> read(String key) async {
    final s = _prefs.getString(key);
    if (s == null) return null;
    try {
      return (jsonDecode(s) as Map).cast<String, Object?>();
    } catch (e) {
      Log.write(Log.error, 'corrupt local doc $key: $e');
      return null;
    }
  }

  @override
  Future<void> write(String key, Map<String, Object?> value) => _prefs.setString(key, jsonEncode(value));

  @override
  Future<void> delete(String key) => _prefs.remove(key);
}

/// In-memory store for tests.
class MemoryLocalStore implements LocalStore {
  final docs = <String, String>{};

  @override
  Future<Map<String, Object?>?> read(String key) async => docs[key] == null ? null : (jsonDecode(docs[key]!) as Map).cast<String, Object?>();

  @override
  Future<void> write(String key, Map<String, Object?> value) async => docs[key] = jsonEncode(value);

  @override
  Future<void> delete(String key) async => docs.remove(key);
}
