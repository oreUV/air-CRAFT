# Rocket Models

These FBX files are the spacecraft parts used by the 3D view (`Mission3DView.cs`).
They are loaded at runtime with:

```csharp
Resources.Load<GameObject>("Models/cockpit");
Resources.Load<GameObject>("Models/fueltank1");
Resources.Load<GameObject>("Models/thruster1");
```

If a file is missing the game falls back to a primitive-built rocket, so the scene
always renders.

## Source of truth

The original user-supplied meshes live outside the repo at
`C:\Users\vaish\Downloads\models\`. They are Tripo-generated meshes exported from
Blender, each a single mesh with the shared material
`tripo_9a69999c_f4f5_4143_bba6_bcee0b57bc2c`.

The copies in this folder are **baked** by `Tools/blender_bake_models.py`, which
makes them predictable for the game:

| Model        | Role in the stack          | Notes |
|--------------|----------------------------|-------|
| `cockpit`    | Command module (capsule)   | Conical crew capsule |
| `fueltank1`  | Propellant tanks / boosters| Tall cylindrical stage |
| `thruster1`  | Engine cluster             | Aligned by principal axis, nozzle down |

## What the bake does

Run it with Blender 5.1+:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b -P "Tools\blender_bake_models.py"
```

For every model the script:

1. Imports the source FBX and joins all mesh objects.
2. Applies all transforms.
3. For `thruster1`, rotates the mesh about its principal axis so the bell stands upright.
4. Flips the model if needed so the **widest end points down** (heat shield / tank base / nozzle).
5. Moves the pivot to the **bottom-centre** of the bounding box.
6. Exports with Unity axes (`axis_forward='-Z'`, `axis_up='Y'`) into this folder.

Blender exits with code 1 after a successful run; that is normal and the
`BAKE_SUMMARY` block printed at the end shows the exported bounds.

## Runtime scaling

The bake only standardises orientation and pivot. `Mission3DView.PlaceModel`
still measures each prefab's renderer bounds at runtime and rescales it to the
height/width the assembly asks for, then re-anchors bottom-centre. That means a
re-exported or replacement model with a different import scale will still stack
correctly without code changes.

## Replacing a model

1. Drop the new FBX into `C:\Users\vaish\Downloads\models\` using the same filename.
2. Re-run the bake command above.
3. In Unity, press Play — no scene or prefab wiring is required.
