# Third-party assets and code

Every external file in the app or the Unity project is listed here (HANDOVER §3.12). Art, audio and fonts are added from M2.

| Item | Source | License | Where it's used |
| --- | --- | --- | --- |
| flutter_embed_unity Unity scripts (`SendToFlutter`, `ProjectExporter*`) v1.4.0 | https://github.com/learntoflutter/flutter_embed_unity (`example_unity_6000_0_project/Assets/FlutterEmbed`, commit 344d7fd) | MIT | `unity/Assets/FlutterEmbed/` (vendored unchanged) |
| flutter_embed_unity / flutter_embed_unity_6000_0_android 2.0.0 | pub.dev | MIT | Flutter dependency |
| Newtonsoft Json for Unity (`com.unity.nuget.newtonsoft-json` 3.2.1) | Unity package registry | MIT | bridge JSON in `unity/Assets/Core/Bridge/` |
| uuid 4.6.0 | pub.dev | MIT | session IDs (Flutter) |
