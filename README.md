# DepthLayer Studio for LightBurn

Native Windows prototype for turning a photo into depth-style LightBurn mask layers.

## What It Does

- Opens a JPG, PNG, BMP, GIF, or TIFF photo.
- Slices the photo into cumulative tonal masks.
- Shows a preview of every generated PNG mask.
- Lets you tune each layer's cutoff, depth, passes, power, and speed.
- Exports a LightBurn project file with separate mask layers, plus numbered PNG backups and notes.
- Can open the exported LightBurn project through LightBurn's local UDP command.

The app does not start a laser job. LightBurn remains the final place to inspect, align, set layer behavior, and run the job.

## Built App

After rebuilding, the executable is here:

```text
bin\DepthLayer Studio.exe
```

You can also use:

```text
run-built-app.cmd
```

## Rebuild

Run this from PowerShell:

```powershell
.\build-windows.ps1
```

The script builds:

```text
bin\DepthLayer Studio.exe
```

## Typical Workflow

1. Open a photo.
2. Choose the material preset.
3. Click `Slice photo`.
4. Tune layer cutoffs and laser settings.
5. Click `Export project`.
6. Click `Send to LightBurn`, or open the exported `.lbrn2` file in LightBurn.
7. Review the separate LightBurn layers and run a scrap-material test.

For best relief results, use high-contrast source photos with clear shadows and strong edges.
