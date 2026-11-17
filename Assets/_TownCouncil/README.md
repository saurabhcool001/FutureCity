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

## More policies per round

Select a Round asset and press **Add policy**, or drag more policies into its list. The new policy gets its three outcomes automatically. Nothing in code assumes two.

## Code layout

| Assembly | What's in it |
| --- | --- |
| `TownCouncil.Core` | Data assets, vote rules, AI brains. Plain C#, no networking |
| `TownCouncil.Networking` | `CouncilSession`: seats, role claims, countdown, AI fill (authority only) |
| `TownCouncil.Experience` | Director, fades, areas, panels, cards, seats, city model, CIVIC orb |
| `TownCouncil.Editor` | Sample data, scene builder, validator, debug window, inspectors |

Everything the scene builder makes is placeholder geometry. Replace it freely: put a role's room prefab in `RoleDefinition.roomEnvironmentPrefab` and rebuild, or edit the scene directly.
