# Known issues

| # | Issue | Impact | Workaround / owner decision |
| --- | --- | --- | --- |
| K1 | **Unity 6000.3.25f1 batchmode sometimes hangs during shutdown** on this Linux machine, after it has finished its work and logged its result (`Exiting with code N` / `UR_BATCH_EXIT N`). It started once an emulator was running. Killing adb, the ILPP runner or the shader compiler did not release it, and ptrace is restricted, so no stack trace is available. | Raw `Unity -batchmode -runTests` commands from HANDOVER §8.3 can block forever. | `tools/env.sh` and `tools/env.ps1` (`ur_unity` / `Invoke-Unity`) read Unity's own verdict from the log and stop the process 90 s later if it is still alive. A run that never reports a verdict still fails, via the 45 min timeout. Always use the tools scripts. |
| K2 | **Firestore rules can't fully protect the economy** (HANDOVER §7.1). A modified client can still write plausible values within the rule limits. | Cheating is possible on a rooted device. | Needs server-side validation and purchase verification (Cloud Functions, Blaze plan: owner task 12), planned for a later phase. |
