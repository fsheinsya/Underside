# Turn event CI trial

## What is checked

The existing game rules are preserved: event draws begin at the end of turn 20,
the announcement appears once at the start of turn 21, and the battle ends after
turn 40 without processing turn 41. Each of the four events has its own random
draw; an event is not guaranteed on every eligible turn.

`BattleTurnEventChecks.RunBoundaries` simulates 32 fixed random seeds in each of
the story and ranking modes (64 battles). High-HP, non-damaging fighters with
debuff immunity keep the fixtures alive through turn 40. The checks cover:

- No event on turns 1 through 19.
- Events can occur on turns 20, 21, and 40, after both actors have acted.
- An eligible turn can also have no event.
- Exactly one announcement at the start of turn 21.
- Exactly turns 1 through 40, with no turn 41.
- Story ends in a raid with no winner or payout; ranking reaches adjudication.

The previous random state is restored, and no scenes or saved player data are
modified. These are editor simulation checks; they do not verify animation or a
built executable. The existing full `BattleTurnEventChecks.Run` also includes
these boundary checks.

## Run on this PC

Close the Unity Editor for UnderSide, then run in PowerShell:

```powershell
cd C:\User\Unity\UnderSide
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ci\Test-TurnEvents.ps1
```

The runner looks for the project's Unity version in the normal Unity Hub folder
and in the current user's `Desktop/unity` folder. For a different installation:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ci\Test-TurnEvents.ps1 -UnityPath 'D:\Unity\6000.4.2f1\Editor\Unity.exe'
```

Success prints `TURN_BOUNDARY_CHECKS_PASSED`. The default log is
`Logs/turn-event-boundaries.log`. An unsuccessful Unity exit or a missing success
marker fails the command, including compilation and license errors.

The checks can also be run inside Unity, outside Play mode, using
`Tools > UnderSide > Check Turn 20 and 40 Boundaries`.

## Run from Jenkins

Open the existing Pipeline job's configuration. Replace its Pipeline script
with the contents of `ci/Jenkinsfile.turn-events`, save, and choose Build Now.
Close UnderSide's Unity Editor first, and use the same Windows user that can
run the licensed Unity Editor. Do not run another job against the same project
folder at the same time.

This trial intentionally tests the current local working folder, including
uncommitted changes. It does not poll Git, check out a pushed commit, or build
the game. Once this trial works, the script can be run from a separate Git
checkout to test pushed commits. Jenkins saves the log in each run's artifacts;
the success marker appears in Console Output.
