# Terrarium

Welcome to the .NET Terrarium 2.0 project! Terrarium was created by members of the .NET Framework team in the .NET Framework 1.0 timeframe and was used initially as an internal test application. At conferences and via online chats, Terrarium provided a great way for developers to learn about the new .NET programming model and languages as they developed creatures and introduced them into a peer-to-peer ecosystem. 

![](whidbey_image001.jpg)

The Windows SDK team evolved the game in the .NET Framework 2.0 timeframe, but we haven't worked on it for over two years. As a result, the source code for Terrarium 2.0 doesn’t use the very latest .NET technologies. By making the source code available, we hope to provide a fun and interesting opportunity to learn about and use the latest advances in the .NET Framework. 

In Terrarium, you can create herbivores, carnivores, or plants and then introduce them into a peer-to-peer, networked ecosystem where they complete for survival. Terrarium demonstrates some of the features of the .NET Framework, including Windows Forms integration with DirectX®; XML Web services; support for peer-to-peer networking; support for multiple programming languages; the capability to update smart client, or Windows-based, applications via a remote Web server; and the evidence-based and code access security infrastructure. 

## .NET 10 port

This fork has been moved from .NET Framework to .NET 10 and runs on Windows 11. The original README is kept as [README.md.original](README.md.original).

### Running the game

You need:

* Visual Studio 2026 with the **.NET desktop development** workload, or the .NET 10 SDK.
* Internet access on the first build, so NuGet can download the graphics and test libraries.
* A GPU with Direct3D 10 or later. Without one, the game falls back to the Windows software renderer (WARP).

No DirectX SDK or runtime installer is needed.

To run it, open `Terrarium.slnx` and press F5. **Terrarium** is the startup project. From the command line:

```
dotnet build Terrarium.slnx
dotnet run --project Client\Terrarium\terrarium.csproj
```

The game opens a local terrarium in `%AppData%\Terrarium\3.0.0` and saves it there on exit.

### Creatures

Building the solution also builds the creatures in the **Creatures** folder into `creatures\`:

* `Samples\Herbivore`, `Samples\Carnivore` and `Samples\Plant`: simple creatures of each kind.
* `SDK\Solutions\CS\Exercise1` to `Exercise3`: the C# solutions to the SDK exercises.

To add one to the game, click **Introduce Animal** and pick its `.dll` in `creatures\`. To write your own, start from a skeleton in `SDK\Skeletons` and a copy of one of the sample projects. Creature projects target `net10.0` and reference `Client\OrganismBase`. The shared settings are in the root `Directory.Build.props`.

The SDK docs and manuals in `SDK` are unchanged, so they still describe Visual Studio 2005. The VB exercise solutions were removed.

### What changed

* **Projects:** the game is now 7 SDK-style projects: `net10.0-windows` for the game, and `net10.0` for `OrganismBase`. They're in one solution, `Terrarium.slnx`, with the creatures and a test project. The unused projects were deleted: the WPF client, the ASP.NET MVC and original servers, the tools, the setup projects, the C++ `AsmCheck` and the experimental `*2`/`*WPF` client projects. The server's documentation pages were moved to `docs\`.
* **Renderer:** DirectX 7 (`DXVBLib.dll`, which Windows no longer includes) was replaced by Direct2D, DirectWrite and Direct3D 11 through [Vortice](https://github.com/amerkoleci/Vortice.Windows) 3.8.3. The game view draws to a DXGI flip-model swap chain, and the magenta color key in the sprite sheets becomes real transparency. The game always runs in a window.
* **Creature safety:** .NET 10 has no code access security, sandbox AppDomains or `Thread.Abort`. Instead, creatures are loaded in their own `AssemblyLoadContext`, and each creature assembly is checked before it loads. The checker reads its IL and metadata and allows only a short list of APIs: basic types, `Math`, `Random`, collections, LINQ, `System.Drawing` value types, `MemoryStream` and readers and writers, and `OrganismBase`. It rejects file, thread, reflection, process and network calls, locks, writable static fields, P/Invoke and pointers. A creature whose turn runs longer than one second is removed with its whole species, its species is blocked, and the game continues on a new thread.
* **Networking:** the peer-to-peer ecosystem and the web-service clients were removed, because `www.terrariumserver.com` no longer exists. Local Terrarium mode is the only mode.
* **Saves:** `BinaryFormatter` was removed from .NET, so terrariums are saved as JSON. Creatures still save their own state through `SerializeAnimal` and `DeserializeAnimal`.
* **Windows Forms:** the menus now use `ContextMenuStrip` and the statistics and species grids use `DataGridView`, because .NET removed the older controls.
* **Tests:** `Client\Terrarium.Tests` (MSTest) runs the game engine without its window. It covers every built creature, the creature checker, saving and loading, and the turn time limit. Run it with `dotnet test Terrarium.slnx`.

### Known limitations

* **The creature checker is not a real sandbox.** It blocks the APIs a creature would need to reach your computer, but creatures still run in the game's own process. Only load creatures you trust.
* **A creature stuck in an endless loop keeps one CPU thread busy** until you close the game, because .NET can't stop a running thread. The creature is removed from the terrarium straight away.
* **Old saves don't open.** When you open a `.ter` file from an earlier version, it's copied to `.ter.bak`, and the game starts a new terrarium and says so.
* **Ecosystem mode, teleporting to other computers and the species server are gone.**
* **The user data folder moved** to `%AppData%\Terrarium\3.0.0`, so settings and creatures from an earlier version aren't picked up.

## Game Overview

The game can run in two modes:
* Terrarium Mode – 1) The user may run alone, without peers. In this case, the ecosystem presented on the screen represents the whole of the ecosystem. This is good for creature testing purposes. 2) The user may also elect to join with a select group of peers, expanding the ecosystem across all of the participating peer computers. This is simple to do. Each participating user opts into a special, private network by entering an agreed upon character string in the “channel” textbox on the Terrarium console. Upon entering that string, the user’s computer is matched with only those computers which also entered that same string.
* Ecosystem Mode – This is the standard mode, in which the user’s computer runs just a small slice of an ecosystem which spans all of the participating peer computers, worldwide.

In both modes, you can develop your own creatures or you can watch as other developers’ creatures battle it out for survival by running Terrarium as a standalone application or as a screensaver.

When creating a creature, you have control over everything from genetic traits (eyesight, speed, defensive power, attacking power, etc.) to behavior (the algorithms for locating prey, moving, attacking, etc.) to reproduction (how often a creature will give birth and what “genetic information,” if any, will be passed on to its offspring). Upon completing the development process, the code is compiled into an assembly (dynamically linked library, or DLL) that can be loaded into the local ecosystem slice, viewable through the Terrarium console. When a creature is initially introduced in Ecosystem Mode, ten instances of it are scattered throughout the local ecosystem. No more instances of that creature may be introduced by that user or any other on the network until the creature has died off completely. By contrast, if running in Terrarium Mode, an infinite number of instances of a given creature may be entered into the environment.

Once the creature is loaded into Terrarium, it acts on the instructions supplied by its code. Each creature is granted between 2 and 5 milliseconds (depending on the speed of the machine) to act before it is destroyed. This prevents any one creature from hogging the processor and halting the game.

Within each peer in the network, a blue “teleporter” ball rolls randomly about. If the user is running with active peers logged in (either in Ecosystem Mode or using a private channel in Terrarium Mode), whenever this blue ball rolls over a creature, that creature is transported to a randomly selected peer machine.
