# Third-party assets and code

Every external file in the app or the Unity project is listed here (HANDOVER §3.12). Art, audio and fonts are added from M2.

| Item | Source | License | Where it's used |
| --- | --- | --- | --- |
| flutter_embed_unity Unity scripts (`SendToFlutter`, `ProjectExporter*`) v1.4.0 | https://github.com/learntoflutter/flutter_embed_unity (`example_unity_6000_0_project/Assets/FlutterEmbed`, commit 344d7fd) | MIT | `unity/Assets/FlutterEmbed/` (vendored unchanged) |
| flutter_embed_unity / flutter_embed_unity_6000_0_android 2.0.0 | pub.dev | MIT | Flutter dependency |
| Newtonsoft Json for Unity (`com.unity.nuget.newtonsoft-json` 3.2.1) | Unity package registry | MIT | bridge JSON in `unity/Assets/Core/Bridge/` |
| uuid 4.6.0 | pub.dev | MIT | session IDs (Flutter) |
| TextMesh Pro Essential Resources (shaders, settings) | Unity `com.unity.ugui` 2.0.0 package | Unity Companion License | `unity/Assets/TextMesh Pro/` |
| Liberation Sans font (TMP default, in-run HUD until M2) | bundled with TMP Essential Resources | SIL Open Font License 1.1 (`unity/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`) | `unity/Assets/TextMesh Pro/Fonts/` |
| Kenney "Animated Characters: Protagonists" 1.1 (characterMedium model, run/jump/idle animations, base skins) | https://kenney.nl/assets/animated-characters-protagonists | CC0 1.0 (`unity/Assets/Games/UrbanRunner/Art/Characters/Kenney/License.txt`) | courier model; Mira/Dex/Juno skins are our own repaint (`SkinBaker`) |
| Phosphor Icons 2.1.1 (@phosphor-icons/core) | https://github.com/phosphor-icons/core | MIT | menu icons (design mockups, then the Flutter shell) |
| Bricolage Grotesque, Instrument Sans | Google Fonts | SIL Open Font License 1.1 | proposed menu typefaces (to be bundled, never fetched at runtime) |
