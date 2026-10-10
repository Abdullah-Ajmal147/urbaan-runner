# Release shrinking (R8) keep rules.

# WorkManager (pulled in by google_mobile_ads) opens its Room database by reflection: Room looks up the
# generated <Name>_Impl class by name. Without these rules R8 removes it and the app crashes on launch with
# "Failed to create an instance of androidx.work.impl.WorkDatabase" (D35).
-keep class * extends androidx.room.RoomDatabase { <init>(); }
-keep class androidx.work.impl.WorkDatabase_Impl { *; }
-keep class androidx.work.** { *; }
-dontwarn androidx.work.**
