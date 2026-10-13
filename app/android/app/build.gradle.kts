import java.util.Properties

plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// Firebase is optional (D26/D34): without the git-ignored google-services.json the app builds and runs local-only.
if (file("google-services.json").exists()) {
    apply(plugin = "com.google.gms.google-services")
    apply(plugin = "com.google.firebase.crashlytics")
}

// Upload key (D36): lives outside the repo, never committed. Without it, release falls back to debug signing.
val keyPropsFile = file(System.getenv("UR_KEY_PROPERTIES") ?: "${System.getProperty("user.home")}/cred/urban-runner/key.properties")
val keyProps = Properties().apply { if (keyPropsFile.exists()) keyPropsFile.inputStream().use { load(it) } }

android {
    namespace = "com.gamefactory.urbanrunner"
    compileSdk = 36
    // Must match Unity 6000.3 (NDK r27c)
    ndkVersion = "27.2.12479018"

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        // Final package ID, confirmed by the owner 2026-10-13 (D36)
        applicationId = "com.gamefactory.urbanrunner"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = 25 // Unity 6000.3 minimum
        targetSdk = 36 // Google Play requirement since 31 Aug 2026
        // Uses the version code from pubspec.yaml. When using split APKs, 1000 * ABI_VERSION
        // is added automatically by Flutter. (https://developer.android.com/studio/build/configure-apk-splits#configure-APK-versions)
        // You can force using the value of versionCode by specifying the `-P force-version-code-ignoring-abi=true`
        // flag during build.
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        // Google's official AdMob test app ID; never a real ID in debug builds (CLAUDE.md)
        manifestPlaceholders["admobAppId"] = "ca-app-pub-3940256099942544~3347511713"
        // Unity's export always packs x86_64; keep only the ABIs Flutter was asked for, so phone/store
        // builds are ARM-only and only the emulator build carries x86_64 (CLAUDE.md, D36).
        (project.findProperty("target-platform") as String?)?.let { platforms ->
            val abis = platforms.split(",").mapNotNull {
                mapOf("android-arm" to "armeabi-v7a", "android-arm64" to "arm64-v8a", "android-x64" to "x86_64")[it.trim()]
            }
            if (abis.isNotEmpty()) ndk { abiFilters.clear(); abiFilters.addAll(abis) }
        }
    }

    signingConfigs {
        if (keyPropsFile.exists()) {
            create("upload") {
                storeFile = file(keyProps.getProperty("storeFile"))
                storePassword = keyProps.getProperty("storePassword")
                keyAlias = keyProps.getProperty("keyAlias")
                keyPassword = keyProps.getProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            signingConfig = signingConfigs.findByName("upload") ?: signingConfigs.getByName("debug")
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            // The real AdMob app ID comes only from the release command line (owner task 5)
            (project.findProperty("urAdmobAppId") as String?)?.let { manifestPlaceholders["admobAppId"] = it }
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}

dependencies {
    implementation(project(":unityLibrary"))
    // MainActivity talks to the UnityPlayer (focus for Hybrid Composition, D31). The classes ship inside
    // unityLibrary already, so they're only needed at compile time here.
    compileOnly(files("../unityLibrary/libs/unity-classes.jar"))
}
