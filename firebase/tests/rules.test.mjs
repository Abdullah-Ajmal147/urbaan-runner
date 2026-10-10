// Firestore security rules tests (HANDOVER §7.1, §9 M4 exit). Run inside the emulator:
//   firebase emulators:exec --project demo-urban-runner --only firestore,auth "npm test"
import { after, before, beforeEach, describe, test } from 'node:test';
import { readFileSync } from 'node:fs';
import { assertFails, assertSucceeds, initializeTestEnvironment } from '@firebase/rules-unit-testing';
import { deleteDoc, doc, getDoc, setDoc, Timestamp, updateDoc } from 'firebase/firestore';

let env;
const created = Timestamp.fromMillis(Date.UTC(2026, 9, 5));
const user = (over = {}) => ({
  createdAt: created, lastActiveAt: created, schemaVersion: 1, sparks: 100, xp: 0, level: 1,
  selectedCharacter: 'mira', selectedTrail: 'none', ownedItems: ['mira'],
  upgrades: { magnet: 0, shield: 0, double: 0 }, settings: { music: true }, ...over,
});
const player = (over = {}) => ({ bestDistance: 500, totalRuns: 3, dailyBest: { date: '2026-10-05', distance: 300 }, missions: [], ...over });
const db = (uid) => (uid ? env.authenticatedContext(uid) : env.unauthenticatedContext()).firestore();
const seed = (path, data) => env.withSecurityRulesDisabled((c) => setDoc(doc(c.firestore(), path), data));

before(async () => {
  env = await initializeTestEnvironment({
    projectId: 'demo-urban-runner',
    firestore: { rules: readFileSync(new URL('../firestore.rules', import.meta.url), 'utf8') },
  });
});
after(() => env.cleanup());
beforeEach(() => env.clearFirestore());

describe('sign-in and ownership', () => {
  test('signed-out clients can do nothing', async () => {
    await assertFails(getDoc(doc(db(), 'users/alice')));
    await assertFails(setDoc(doc(db(), 'users/alice'), user()));
  });
  test('a user can create and read their own profile', async () => {
    await assertSucceeds(setDoc(doc(db('alice'), 'users/alice'), user()));
    await assertSucceeds(getDoc(doc(db('alice'), 'users/alice')));
    await assertSucceeds(setDoc(doc(db('alice'), 'games/urban_runner/players/alice'), player()));
  });
  test("another user's documents are denied", async () => {
    await seed('users/alice', user());
    await seed('games/urban_runner/players/alice', player());
    await assertFails(getDoc(doc(db('mallory'), 'users/alice')));
    await assertFails(updateDoc(doc(db('mallory'), 'users/alice'), { sparks: 1 }));
    await assertFails(getDoc(doc(db('mallory'), 'games/urban_runner/players/alice')));
    await assertFails(setDoc(doc(db('mallory'), 'games/urban_runner/players/alice'), player({ bestDistance: 1e6 })));
    await assertFails(deleteDoc(doc(db('mallory'), 'users/alice')));
  });
});

describe('validation', () => {
  test('wrong types and out-of-range values are rejected', async () => {
    const d = db('alice');
    await assertFails(setDoc(doc(d, 'users/alice'), user({ sparks: -1 })));
    await assertFails(setDoc(doc(d, 'users/alice'), user({ sparks: '100' })));
    await assertFails(setDoc(doc(d, 'users/alice'), user({ level: 0 })));
    await assertFails(setDoc(doc(d, 'users/alice'), user({ upgrades: { magnet: 6 } })));
    await assertFails(setDoc(doc(d, 'users/alice'), user({ upgrades: { laser: 1 } })));
    await assertFails(setDoc(doc(d, 'users/alice'), user({ isAdmin: true })), 'unknown fields');
    await assertFails(setDoc(doc(d, 'users/alice'), user({ createdAt: 'yesterday' })));
    await assertFails(setDoc(doc(d, 'games/urban_runner/players/alice'), player({ totalRuns: 1.5 })));
    await assertFails(setDoc(doc(d, 'games/urban_runner/players/alice'), player({ dailyBest: { date: '5/10/2026', distance: 1 } })));
  });
  test('streak and Lantern Hunt fields are accepted and validated', async () => {
    const d = db('alice');
    const ok = { streak: 3, streakBest: 7, lastStreakClaim: '2026-10-10', huntDate: '2026-10-10', huntFound: 5, huntStreak: 2, huntLastComplete: '' };
    await assertSucceeds(setDoc(doc(d, 'games/urban_runner/players/alice'), player(ok)));
    await assertFails(setDoc(doc(d, 'games/urban_runner/players/alice'), player({ ...ok, huntFound: 6 })));
    await assertFails(setDoc(doc(d, 'games/urban_runner/players/alice'), player({ ...ok, streak: -1 })));
    await assertFails(setDoc(doc(d, 'games/urban_runner/players/alice'), player({ ...ok, lastStreakClaim: 'yesterday' })));
    await assertFails(setDoc(doc(d, 'games/urban_runner/players/alice'), player({ ...ok, huntStreak: '2' })));
  });
  test('createdAt can never change', async () => {
    await seed('users/alice', user());
    await assertFails(updateDoc(doc(db('alice'), 'users/alice'), { createdAt: Timestamp.now() }));
    await assertSucceeds(updateDoc(doc(db('alice'), 'users/alice'), { selectedCharacter: 'dex' }));
  });
  test('bestDistance and totalRuns never decrease', async () => {
    await seed('games/urban_runner/players/alice', player());
    const p = doc(db('alice'), 'games/urban_runner/players/alice');
    await assertFails(updateDoc(p, { bestDistance: 499 }));
    await assertFails(updateDoc(p, { totalRuns: 2 }));
    await assertSucceeds(updateDoc(p, { bestDistance: 501.5, totalRuns: 4 }));
  });
  test('sparks rise by at most 20,000 per write (spending is fine)', async () => {
    await seed('users/alice', user({ sparks: 1000 }));
    const u = doc(db('alice'), 'users/alice');
    await assertFails(updateDoc(u, { sparks: 21001 }));
    await assertSucceeds(updateDoc(u, { sparks: 21000 }));
    await assertSucceeds(updateDoc(u, { sparks: 0 }));
    await assertFails(setDoc(doc(db('bob'), 'users/bob'), user({ sparks: 20001 })), 'a fresh profile is capped too');
  });
});

describe('server-only and default deny', () => {
  test('clients cannot read or write purchases', async () => {
    await seed('purchases/p1', { uid: 'alice', productId: 'starter_pack' });
    await assertFails(getDoc(doc(db('alice'), 'purchases/p1')));
    await assertFails(setDoc(doc(db('alice'), 'purchases/p2'), { uid: 'alice', productId: 'remove_ads' }));
  });
  test('any other path is denied', async () => {
    await assertFails(setDoc(doc(db('alice'), 'leaderboard/alice'), { score: 1 }));
    await assertFails(setDoc(doc(db('alice'), 'games/other_game/players/alice'), player()));
  });
});
