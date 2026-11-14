# Space Mission Design Game

A minimal interactive space mission design and management game inspired by Apollo 11. Players (individually or in teams of up to 4) design a complete space mission: define objectives, assemble a spacecraft, select scientific instruments, choose a launch vehicle and orbit, then run a simulation and see how their engineering trade-offs affect mission performance.

## Gameplay Flow

1. **Mission Setup** — Pick a mission objective (e.g., Apollo 11 lunar landing).
2. **Spacecraft Design** — Add hull, propulsion, power, comms, and structural components while respecting mass, power, and budget limits.
3. **Instrument Selection** — Choose scientific instruments compatible with your destination; each adds mass, power draw, and data volume.
4. **Launch Selection** — Pick a launch vehicle and orbit; verify payload compatibility.
5. **Simulation** — Run the mission; the engine evaluates science return, cost efficiency, mass/power margins, data handling, and reliability.
6. **Results** — See your score, pass/fail status, feedback, and team rankings.

## Constraints Managed

- **Budget (USD)**
- **Mass (kg)**
- **Power (W)**
- **Data rate (kbps)**
- **Data storage (GB)**
- **Mission lifetime (days)**

## Multiplayer

Supports 1–4 players collaborating on a single mission design in turn-based (hotseat) fashion. Each player takes turns making design decisions; the final score is shared across the team.

## Unity Version

Open with **Unity 6000.6.7f1**. Batchmode crashes at licensing initialization on this machine — use Unity Hub GUI to open the project.

## Project Structure

```
Assets/
  Scenes/               (create MainMenu, MissionDesign, Simulation, Results scenes in Unity)
  Scripts/
    Data/               Mission data models (MissionObjective, SpacecraftComponent, Instrument, etc.)
    Game/               MissionManager, SpacecraftDesigner, SimulationEngine, ScoreManager, MultiplayerManager
    UI/                 MainMenuUI, MissionSetupUI, SpacecraftDesignUI, InstrumentSelectionUI, LaunchSelectionUI, SimulationUI, ResultsUI
  Resources/
    MissionData/        JSON mission definitions (Apollo11.json, etc.)
ProjectSettings/        Unity project settings
```

## License

MIT License for original code.
