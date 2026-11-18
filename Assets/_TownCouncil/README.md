# Town Council 2045: pre-game flow

## First run (in Unity)

1. **Town Council > 1. Create Sample Data**: makes the Newhaven story as editable assets in `Data/`. It includes 4 roles, 4 rounds with 2 policies each, 5 values, 8 buildings, the welcome pages and the Game Config. Running it again never overwrites assets that already exist.
2. **Town Council > 2. Build Game Scene**: copies the template's `BasicScene`, which already has the XR rig and network managers, into `Scenes/TownCouncil.unity`. It then builds the welcome space, the foyer, one room per role and the council chamber. The scene becomes the first scene in Build Settings.
3. Press **Play**. Without a headset, use the template's XR Device Simulator. To test with several players, use Multiplayer Play Mode (Window > Multiplayer Play Mode).

Other tools:

- **Town Council > Validate Scenario** checks for missing outcomes, empty text and buildings with no model.
- **Town Council > Debug Window** (Play mode) shows every seat, skips the lobby countdown, jumps between areas and previews AI votes.

## The player's journey

Welcome pages → Join a council (create, join by code, quick join) → **Foyer**: see who's joined, pick a role card → fade → **the role's own room**: name, goal, secret → fade → **Chamber**: your seat at the round table, city model in the middle, CIVIC above it.

## Lobby and AI seats (Game Config)

- `lobbyWaitSeconds` (60): after the first person joins, empty seats become AI when the countdown ends. `restartCountdownOnJoin` restarts the countdown each time someone joins.
- `minHumansToStart` (1): the fewest people needed before the AI fills the rest.
- `aiMode`: **Fixed** votes as written on each policy per role. **Dynamic** scores choices from the role's value priorities, the outcome's meter changes and the humans' votes.
- AI picks its cards only after every person has picked, so people always get a real choice.
- If a player drops out after the council locks, an AI takes over their role.
- Whoever runs the session sees **Start now with AI** in the foyer.

## The game (once every seat is filled and seated)

CIVIC wakes up. Then for each round:

1. **Round intro**: the year and title, and CIVIC's introduction.
2. **Proposal**: CIVIC pitches the policy, and the buildings it affects glow. Touch a building to hear a resident.
3. **Decision window**: drop your token on Agree, Disagree or Amend. You can move it until time runs out. AI seats vote at random moments. When every seat has voted, the timer drops to 5 s.
4. **Result**: the board shows the count. A tie or no votes means CIVIC proceeds (the Agree outcome).
5. **Future view**: the lights dim, the year rolls forward, the round's decisions change buildings on the model, and headlines and diary entries appear.

Steps 2–4 repeat for each policy when a round's **Presentation** is *One After Another*. With *All At Once* they happen once, with one ballot row per policy and one shared timer.

**Secrets:** your role card lies by your ballot. Place it on the glowing reveal spot to show your secret to everyone. An AI reveals its role's secret when a policy lists that role in **AI Reveal Roles**.

**Epilogue:** the town's value fingerprint, every secret above its seat, and each player's personal ending, chosen from the role's endings by the flags the outcomes set. Round 4's pitch uses `{agreementPercent}` and its intro uses `{deadlocks}`, both from the real game record.

Pacing, CIVIC's reaction lines and "show votes live" are in **Game Config**. The **Debug Window** can skip phases, vote and reveal as this headset without VR, and shows meters and flags.

## Speech (optional)

CIVIC, residents and important text (the welcome pages) can be spoken aloud with each word highlighted. Speech is fully optional:

1. **Package:** install `com.saurabh.futurecity.offlinetts` (Package Manager ▸ + ▸ Add package from tarball). Then run FutureCity ▸ Plugins ▸ OfflineTTS ▸ **Download Supertonic 3 Models** and **Create or Locate Settings Asset**. Without the package, the `TownCouncil.Speech` assembly doesn't compile and the game shows captions only.
2. **Scene:** **Town Council > 2. Build Game Scene** adds speech automatically when the package is installed. For an existing scene, use **Town Council > Speech > Add Speech To Open Scene**. **Remove Speech From Open Scene** takes it out again.
3. **Switches (Game Config):** `speechEnabled` (master), `speakCivic`, `speakResidents`, `speakNarration`, `highlightSpokenWords`.
4. **At runtime:** the **Voice: On/Off** button on the welcome and foyer panels mutes this headset (remembered between sessions). The Debug Window also has a mute toggle. From code, set `SpeechHub.Muted`.

Recorded or baked audio on a line (`DialogueLine.audio` or `PerspectiveClip.audio`) always plays instead of live speech. Voices per speaker (CIVIC, narrator, residents) are set on the **Speech (TTS)** object. To read more text aloud, add **Speakable Text** to any TextMeshPro text.

## More policies per round

Select a Round asset and press **Add policy**, or drag more policies into its list. The new policy gets its three outcomes automatically. Nothing in code assumes two.

## Code layout

| Assembly | What's in it |
| --- | --- |
| `TownCouncil.Core` | Data assets, vote rules, AI brains, `CouncilRecord` (meters, city, flags, CIVIC stats from the history). Plain C#, no networking |
| `TownCouncil.Networking` | `CouncilSession`: seats, role claims, countdown, AI fill, secret reveals. `CouncilGame`: phases, votes, AI votes, results history |
| `TownCouncil.Experience` | Director, fades, areas, panels, cards, seats, city model, CIVIC orb |
| `TownCouncil.Speech` (+ `.Editor`) | Optional: connects `SpeechHub` to FutureCity Offline TTS. Compiles only when that package is installed |
| `TownCouncil.Editor` | Sample data, scene builder, validator, debug window, inspectors |

Everything the scene builder makes is placeholder geometry. Replace it freely: put a role's room prefab in `RoleDefinition.roomEnvironmentPrefab` and rebuild, or edit the scene directly.
