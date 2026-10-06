import 'package:flutter/foundation.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_embed_unity/flutter_embed_unity.dart';
// The plugin's own listener registry: same message path as its EmbedUnity widget.
// ignore: implementation_imports
import 'package:flutter_embed_unity/src/unity_message_listener.dart';
// ignore: implementation_imports
import 'package:flutter_embed_unity/src/unity_message_listeners.dart';

/// The embedded Unity game, shown full screen (D31).
///
/// The plugin's `EmbedUnity` uses `AndroidView`. Unity renders into a SurfaceView, which that mode can only show
/// through a virtual display: every game frame is drawn off-screen, copied into a texture and drawn again by
/// Flutter, paced by Flutter's frames. That was the in-game lag on real phones.
///
/// Here Android uses **Hybrid Composition** (`initExpensiveAndroidView`): SurfaceFlinger shows Unity's surface
/// directly, with no copy. Its cost (Flutter's own UI is composed on the platform thread) doesn't matter here: during
/// a run Flutter draws nothing over the game. iOS keeps the plugin's widget.
class UnityGameView extends StatefulWidget {
  const UnityGameView({super.key, this.onMessageFromUnity});

  final void Function(String)? onMessageFromUnity;

  /// Must match the plugin's registered view factory (flutter_embed_unity_platform_interface).
  static const viewType = 'com.learntoflutter/flutter_embed_unity';

  @override
  State<UnityGameView> createState() => _UnityGameViewState();
}

class _UnityGameViewState extends State<UnityGameView> implements UnityMessageListener {
  static const _native = MethodChannel('urbanrunner/unity_view');

  /// Gives Unity window focus once the Hybrid Composition view exists (see MainActivity). Twice: once right
  /// away, once after the view has surely joined the window.
  Future<void> _kick() async {
    for (final delay in const [Duration(milliseconds: 50), Duration(milliseconds: 700)]) {
      await Future<void>.delayed(delay);
      if (!mounted) return;
      try {
        await _native.invokeMethod<bool>('kick');
      } on MissingPluginException {
        return; // tests / other hosts
      } on PlatformException {
        return;
      }
    }
  }

  @override
  void initState() {
    super.initState();
    if (defaultTargetPlatform == TargetPlatform.android) UnityMessageListeners.instance.addListener(this);
  }

  @override
  void dispose() {
    if (defaultTargetPlatform == TargetPlatform.android) UnityMessageListeners.instance.removeListener(this);
    super.dispose();
  }

  @override
  void onMessageFromUnity(String data) => widget.onMessageFromUnity?.call(data);

  @override
  Widget build(BuildContext context) {
    if (defaultTargetPlatform != TargetPlatform.android) return EmbedUnity(onMessageFromUnity: widget.onMessageFromUnity);
    return PlatformViewLink(
      viewType: UnityGameView.viewType,
      surfaceFactory: (context, controller) => AndroidViewSurface(
        controller: controller as AndroidViewController,
        // Every touch belongs to the game (swipes start anywhere on screen).
        gestureRecognizers: <Factory<OneSequenceGestureRecognizer>>{Factory<OneSequenceGestureRecognizer>(EagerGestureRecognizer.new)},
        hitTestBehavior: PlatformViewHitTestBehavior.opaque,
      ),
      onCreatePlatformView: (params) => PlatformViewsService.initExpensiveAndroidView(
        id: params.id,
        viewType: UnityGameView.viewType,
        layoutDirection: TextDirection.ltr,
        creationParamsCodec: const StandardMessageCodec(),
        onFocus: () => params.onFocusChanged(true),
      )
        ..addOnPlatformViewCreatedListener(params.onPlatformViewCreated)
        ..addOnPlatformViewCreatedListener((_) => _kick())
        ..create(),
    );
  }
}
