## About DS Map Studio:
DS Map Studio is a standalone map editor for all the souls games. It is intended to be the successor to DSTools. It is currently in alpha testing, so expect DS3 to crash if you open 3 maps at once. Its supports saving maps for the DS Trillogy (not BB or DeS), but the editor still lacks the stability and polish expected from a full release, so keep that in mind, and save often.

## About This Fork:
* To differentiate it from the 13 other forks of Katalash's work, while also tempering expectations, I have named this DS Map Studio Re(mastered). It make some improvements over the original, but likely, not all the ones you wanted, and some major issues are not addressed.
* I finished [TalkSickWaist](https://github.com/TalkSickWaist)'s reintegration of improvements [Philiquaz](https://github.com/Philiquaz) made to [DS Param Studio](https://github.com/Philiquaz/DSParamStudio).
* Orbit controls have been added.
* The Display Groups Editor now works.
* Camera location/rotation saved and restored on project load.

## Basic usage instructions
### Game instructions
* **Dark Souls Prepare to die Edition**: Game must be unpacked with UDSFM before usage with Map Studio (https://www.nexusmods.com/darksouls/mods/1304).
* **Dark Souls Remastered**: You can 'downgrade' to PtdE using [InfernoPlus's Remastest mod](https://www.patreon.com/posts/58341679). You can then copy the map files into your PtdE installation, work out of that directory, and then copy back to the remastered installation.
* **Dark Souls 2 SOTFS**: Use UXM (https://www.nexusmods.com/sekiro/mods/26) to unpack the game. Params must also be decrypted before use (you can open and save them with Yapped Honey Bear edition (https://github.com/vawser/Yapped-Honey-Bear) until I implement this natively). Vanilla Dark Souls 2 is not supported.
* **Dark Souls 3 and Sekiro**: Use [UXM](https://www.nexusmods.com/sekiro/mods/26) to extract the game files.
* **Demon's Souls**: I test against the US version, but any valid full game dump of Demon's Souls will probably work out of the box. Make sure to disable the RPCS3 file cache to test changes if using the emulator.
* **Bloodborne**: Any valid full game dump should work out of the box. Note that some dumps will have the base game (1.0) and the patch separate, so the patch should be merged on top of the base game before use with map studio. You're on your own for installing mods to console at the moment.

### Mod projects
Map studio operates on top of something I call mod projects. These are typically stored in a separate directory from the base game, and all modifies files will be saved there instead of overwriting the base game files (there's exceptions for DS1 and DeS because we don't have a mod engine solution for them). The intended workflow is to install [mod engine](https://www.nexusmods.com/darksouls3/mods/332) for your respective game and set the modoverridedirectory in modengine.ini to your mod project directory. This way you don't have to modify base game files (and work on multiple mod projects at a time) and you can easily distribute a mod by zipping up the project directory and uploading it.

## System Requirements:
* Windows 7/8/8.1/10 (64-bit only)
* [Microsoft .Net Core 3.1 **Desktop** Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1)
* [Visual C++ Redistributable x64 - INSTALL THIS IF THE PROGRAM CRASHES ON STARTUP](https://aka.ms/vs/16/release/vc_redist.x64.exe)
* **A Vulkan Compatible Graphics Device with support for descriptor indexing**, even if you're just modding DS1: PTDE
* Intel GPUs currently don't seem to be working properly. At the moment, you will probably need a somewhat recent (2014+) NVIDIA or AMD GPU
* A 4GB (8GB recommended) graphics card if modding DS3/BB/Sekiro maps due to huge map sizes

## Special Thanks
* [Katalash](https://github.com/katalash) - Made DS Map Studio and DSTools.
* TKGP - Made Soulsformats
* [Pav](https://github.com/JohrnaJohrna)
* [Meowmaritus](https://github.com/meowmaritus) - Made DSAnimStudio, which DSMapStudio is loosely based on
* [PredatorCZ](https://github.com/PredatorCZ) - Reverse engineered Spline-Compressed Animation entirely.
* [Horkrux](https://github.com/horkrux) - Reverse engineered the header and swizzling used on non-PC platform textures.
* [Vawser](https://github.com/vawser) - DS2/3 Documentation
* [Philiquaz](https://github.com/Philiquaz) - Continued development through DSParamStudio, which I swear I'll integrate back in once I know how to.

## Libraries Utilized
* Soulsformats
* [Newtonsoft Json.NET](https://www.newtonsoft.com/json)
* Veldrid for rendering
* ImGui.NET for UI
* A small portion of [HavokLib](https://github.com/PredatorCZ/HavokLib), specifically the spline-compressed animation decompressor, adapted for C#
* Recast for navigation mesh generation
* Fork Awesome font for icons