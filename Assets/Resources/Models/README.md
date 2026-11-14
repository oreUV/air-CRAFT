# Rocket Models

Two sets of models live in this folder:

## 1. Saturn V parts (currently used by the game)

`Mission3DView.cs` assembles the actual Saturn V rocket from parts baked from
`C:\Users\vaish\Downloads\Saturn V Rocket\` (STL files). They live in
`Assets/Resources/Models/SaturnV/`:

| Part file                  | Role in the stack |
|----------------------------|-------------------|
| `S-IC bottom`              | First-stage base (largest diameter) |
| `S-IC top`                 | First-stage upper section |
| `S-IC joining cube`        | Small connector between S-IC sections |
| `stage 1-2 coupler`        | Interstage connector |
| `S-II`                     | Second stage |
| `S-IV b`                   | Third stage |
| `service module`           | Instrument unit / service module |
| `lem shroud`               | Lunar module shroud |
| `command moduel`           | Command module (capsule) — note the source filename's spelling |
| `escape tower`             | Launch escape tower on top |
| `fin (print 4)`            | Four fins around the first stage |

Parts are baked by `Tools/blender_bake_saturnv.py`, which parses each STL
(Blender 5.1 has no built-in STL importer, so triangles are read manually),
stands the part up on its principal axis, flips the wide end down, moves the
pivot to bottom-centre, and exports as Y-up FBX.

## 2. Tripo models (kept aside, not used)

`cockpit.fbx`, `fueltank1.fbx`, `thruster1.fbx` are the original Tripo-generated
meshes from `C:\Users\vaish\Downloads\models\`. They remain in this folder but
are **no longer loaded by the game** — the Saturn V stack replaced them. They are
kept as a fallback reference and in case you want to switch back.

## Runtime assembly

`Mission3DView.StackSaturnVPart()` loads each Saturn V prefab, applies a global
scale so the full vehicle is ~28 units tall, and stacks them bottom to top with
bottom-centre alignment. Four `fin (print 4)` parts are placed around the first
stage. If the SaturnV folder is missing or empty, the game falls back to
primitive cylinders and spheres.

## Re-baking the Saturn V parts

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b -P "Tools\blender_bake_saturnv.py"
```

Blender exits with code 1 after a successful run; that is normal.

## Replacing a part

1. Drop a new STL into `C:\Users\vaish\Downloads\Saturn V Rocket\` using the same filename.
2. Re-run the bake command above.
3. In Unity, press Play — no scene or prefab wiring is required.
