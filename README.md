# Terrarium

Welcome to the .NET Terrarium 2.0 project! Terrarium was created by members of the .NET Framework team in the .NET Framework 1.0 timeframe and was used initially as an internal test application. At conferences and via online chats, Terrarium provided a great way for developers to learn about the new .NET programming model and languages as they developed creatures and introduced them into a peer-to-peer ecosystem. 

![](whidbey_image001.jpg)

The Windows SDK team evolved the game in the .NET Framework 2.0 timeframe, but we haven't worked on it for over two years. As a result, the source code for Terrarium 2.0 doesn’t use the very latest .NET technologies. By making the source code available, we hope to provide a fun and interesting opportunity to learn about and use the latest advances in the .NET Framework. 

In Terrarium, you can create herbivores, carnivores, or plants and then introduce them into a peer-to-peer, networked ecosystem where they complete for survival. Terrarium demonstrates some of the features of the .NET Framework, including Windows Forms integration with DirectX®; XML Web services; support for peer-to-peer networking; support for multiple programming languages; the capability to update smart client, or Windows-based, applications via a remote Web server; and the evidence-based and code access security infrastructure. 

## Visual Studio 2026 upgrade notes

This fork has been upgraded to build and run with Visual Studio 2026 on Windows 11. The original README is kept as [README.md.original](README.md.original).

### Running the game

You need:

* Visual Studio 2026 with the **.NET desktop development** workload, which includes the .NET Framework 4.8 targeting pack.
* Internet access on the first build, so NuGet can download the graphics libraries.
* A GPU with Direct3D 10 or later. Without one, the game falls back to the Windows software renderer (WARP).

No DirectX SDK or runtime installer is needed.

To run it, open `Client\client.slnx` and press F5 with the default **Debug | Any CPU** configuration. **Terrarium** is the startup project, and the game builds to `Client\Terrarium\bin\Debug\Terrarium.exe`.

### What changed

The game client (`Client\client.slnx`):

* All 13 projects now target .NET Framework 4.8 instead of 3.5, and the VS 2008 `client.sln` is replaced by `client.slnx`.
* The renderer used DirectX 7 through `DXVBLib.dll`, which wraps `dx7vb.dll`, a library Windows no longer includes. It has been rewritten on Direct2D, DirectWrite and Direct3D 11, through [Vortice](https://github.com/amerkoleci/Vortice.Windows) 2.4.2, the last release that supports .NET Framework. The game view draws to a DXGI flip-model swap chain, and the magenta color key in the sprite sheets becomes real transparency when they load. `DXVBLib.dll` has been removed.
* DirectDraw's exclusive full-screen mode is gone, so the game always runs in a window.
* ClickOnce manifest signing is turned off, because it needed the old password-protected `Terrarium_TemporaryKey.pfx`.
* The code access security (CAS) policy is removed. .NET Framework 4 and later reject `AppDomain.SetAppDomainPolicy`, and the call stopped the game from starting.

The main solution, `Terrraium2010.slnx`, which holds the WPF client and the ASP.NET MVC server:

* All projects target .NET Framework 4.8, and the solution files are migrated from `.sln` to `.slnx`.
* The server moved from ASP.NET MVC 2 to MVC 5.2.9, and its tests from the VS 2010 test framework to MSTest 3.
* These two projects are only starting templates. The WPF client opens an empty window and the server is the stock MVC sample site, so the playable game is the one in `Client`.

### Known limitations

* **Creatures run with full trust.** The removed CAS policy was what limited creature assemblies to execute-only permissions. Creature code now runs with the same access to your computer as the game itself, so only load creatures you trust. Restoring a real sandbox would mean loading creatures in a restricted AppDomain.
* **There is no ecosystem server.** `www.terrariumserver.com` no longer exists, so ecosystem and peer-to-peer modes can't connect, and the game shows a message saying so. Local Terrarium mode works.
* **Not converted yet:** the creature samples (`Samples\Samples.sln`), the SDK solutions (`SDK\Solutions`), the original web server (`Server\server.sln`) and the setup projects (`.vdproj`). They still use their VS 2005 formats, and VS 2026 doesn't open setup projects or the old C++ `AsmCheck` project at all.

## Game Overview

The game can run in two modes:
* Terrarium Mode – 1) The user may run alone, without peers. In this case, the ecosystem presented on the screen represents the whole of the ecosystem. This is good for creature testing purposes. 2) The user may also elect to join with a select group of peers, expanding the ecosystem across all of the participating peer computers. This is simple to do. Each participating user opts into a special, private network by entering an agreed upon character string in the “channel” textbox on the Terrarium console. Upon entering that string, the user’s computer is matched with only those computers which also entered that same string.
* Ecosystem Mode – This is the standard mode, in which the user’s computer runs just a small slice of an ecosystem which spans all of the participating peer computers, worldwide.

In both modes, you can develop your own creatures or you can watch as other developers’ creatures battle it out for survival by running Terrarium as a standalone application or as a screensaver.

When creating a creature, you have control over everything from genetic traits (eyesight, speed, defensive power, attacking power, etc.) to behavior (the algorithms for locating prey, moving, attacking, etc.) to reproduction (how often a creature will give birth and what “genetic information,” if any, will be passed on to its offspring). Upon completing the development process, the code is compiled into an assembly (dynamically linked library, or DLL) that can be loaded into the local ecosystem slice, viewable through the Terrarium console. When a creature is initially introduced in Ecosystem Mode, ten instances of it are scattered throughout the local ecosystem. No more instances of that creature may be introduced by that user or any other on the network until the creature has died off completely. By contrast, if running in Terrarium Mode, an infinite number of instances of a given creature may be entered into the environment.

Once the creature is loaded into Terrarium, it acts on the instructions supplied by its code. Each creature is granted between 2 and 5 milliseconds (depending on the speed of the machine) to act before it is destroyed. This prevents any one creature from hogging the processor and halting the game.

Within each peer in the network, a blue “teleporter” ball rolls randomly about. If the user is running with active peers logged in (either in Ecosystem Mode or using a private channel in Terrarium Mode), whenever this blue ball rolls over a creature, that creature is transported to a randomly selected peer machine.
