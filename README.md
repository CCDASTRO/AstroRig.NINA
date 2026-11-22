# AstroRig for N.I.N.A.

This plugin targets the N.I.N.A. 3.3.0.1069 nightly plugin API and builds with the .NET 10 SDK. It shares the standalone AstroRig inventory format, rig calculations, and Excel export. The plugin can create and maintain a rig from a new inventory without installing standalone AstroRig.

## View filter slots in the rig table

On **Imaging → AstroRig**, select a rig. Each filter wheel shows its occupied slot count and the filter in each occupied slot beneath the equipment name. Equipment names wrap, and you can resize columns or scroll horizontally in a narrow dock.

The [standalone AstroRig app](https://github.com/CCDASTRO/AstroRig) uses the same inventory format. It shows the slot list when you hover over the wheel name on the **Rigs** tab.

Hover over any equipment name in the rig table to see its saved specs.

## Build and install

1. Run `dotnet build AstroRig.NINA/AstroRig.NINA.csproj -c Release`.
2. With N.I.N.A. closed, put `AstroRig.NINA.dll` in `%LOCALAPPDATA%\NINA\Plugins\3.0.0\AstroRig`. Restart N.I.N.A. and check **Plugins → Installed**.
3. On the **Imaging** tab, open **AstroRig** from the panel selector. Open an inventory or use the one the panel loaded from standalone AstroRig's last saved file.
4. For a fresh setup, click **New inventory**, then **Add equipment** and **Add rig**. Select the rig and use **Assign equipment** to build it. Select a filter wheel row to edit slot assignments, then click **Save slots**.

Reload after changes from the standalone app. Close or reload the standalone inventory before saving there after making plugin edits, since the standalone app saves its full in-memory inventory.

## License and development disclosure

AstroRig for N.I.N.A. is released under the Mozilla Public License 2.0. CCDASTRO is the project owner. OpenAI Codex materially assisted with the plugin implementation and release preparation.