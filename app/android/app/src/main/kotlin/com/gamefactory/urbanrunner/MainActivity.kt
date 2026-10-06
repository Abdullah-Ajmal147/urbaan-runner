package com.gamefactory.urbanrunner

import com.learntoflutter.flutter_embed_unity_android.unity.UnityPlayerSingleton
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

/**
 * Hybrid Composition support for the embedded Unity view (docs/decisions.md D31).
 *
 * Unity only starts rendering once it is told it has window focus. The plugin asks for focus when the view is
 * created, which works with a virtual display but not with Hybrid Composition, where the view joins the window a
 * moment later. Flutter calls "kick" once the platform view exists, and the activity forwards focus changes.
 */
class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "urbanrunner/unity_view").setMethodCallHandler { call, result ->
            when (call.method) {
                "kick" -> {
                    val unity = UnityPlayerSingleton.getInstance()
                    if (unity != null) {
                        // Focus only. Never pause/resume here: while Unity is still starting, pause() blocks for
                        // 2 s and leaves the engine stuck ("Timeout while trying to pause the Unity Engine").
                        unity.getFrameLayout().requestFocus()
                        unity.windowFocusChanged(true)
                    }
                    result.success(unity != null)
                }
                else -> result.notImplemented()
            }
        }
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        UnityPlayerSingleton.getInstance()?.windowFocusChanged(hasFocus)
    }
}
