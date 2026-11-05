<!--div style="display: none">
# MotherCore
</div-->

 ```
███╗   ███╗ ██████╗ ████████╗██╗  ██╗███████╗██████╗      ██████╗ ██████╗ ██████╗ ███████╗
████╗ ████║██╔═══██╗╚══██╔══╝██║  ██║██╔════╝██╔══██╗    ██╔════╝██╔═══██╗██╔══██╗██╔════╝
██╔████╔██║██║   ██║   ██║   ███████║█████╗  ██████╔╝    ██║     ██║   ██║██████╔╝█████╗  
██║╚██╔╝██║██║   ██║   ██║   ██╔══██║██╔══╝  ██╔══██╗    ██║     ██║   ██║██╔══██╗██╔══╝  
██║ ╚═╝ ██║╚██████╔╝   ██║   ██║  ██║███████╗██║  ██║    ╚██████╗╚██████╔╝██║  ██║███████╗
╚═╝     ╚═╝ ╚═════╝    ╚═╝   ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝     ╚═════╝ ╚═════╝ ╚═╝  ╚═╝╚══════╝
 ```

 [View Full Docs](https://lukejamesmorrison.github.io/mother-docs)

Mother Core is a framework that acts as a foundation to Space Engineers programmable block scripts.  Building custom scripts on top of Mother Core is designed to be simple and intutive for both new and expert script developers.  It simplifies intergrid communication, navigation, block automation, and event monitoring. Should players chose, they can use Mother Core to run most automations on their grid without the need for an Event Controller or Timer Block.

Any grid running an instance of Mother Core will cooperate, greatly expanding what players can do with multiple grids. [Mother OS](https://steamcommunity.com/sharedfiles/filedetails/?id=3411507973) was built as the first of several Mother-powered scripts in development to address several dimensions of the Space Engineers gameplay experience.

You will need a basic knowledge of programming to get the most out of Mother Core. However, I was highly motivated to build Mother to reduce the barrier of entry for players to begin writing their own scripts.  Mother exists so that programmable blocks become easier to work with.

The empire must grow.

> [!CAUTION] 
> Mother Core should never be modified directly in your project folder.
## Dependencies

Like most Space Engineers scripts, Mother Core requires [MDK2](https://github.com/malforge/mdk2) to enable a rich development experience. A big thanks to **Malware** for their incredible contributions to the Space Engineers community.

##  About Agentluke
I have always been passionate about aviation and space. I studied Aerospace Engineering, and flew in fighter jets in the Air Force for over a decade. I have been writing software since university, and I have always been fascinated by the intersection of software and hardware.

I discovered Space Engineers several years ago after my brother had been playing it for some time. I was immediately hooked. Sandbox games and builders are my jam, and Space Engineers is an excellent space sandbox. I find the automation capabilties quite clunky and require a lot of menu navigation, but curiousity got the best of me as I explored what the programmable block could do!  

I built Mother to make my ships operate more like intelligent spaceships, rather than just a collection of blocks flying through space. This project has been a labor of love for about 18 months. I hope you enjoy using Mother as much as I enjoyed building it.

**Luke** - 
Space Engineer 🚀🇨🇦

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://buymeacoffee.com/Agentluke) 
[![YouTube](https://img.shields.io/badge/YouTube-%23FF0000.svg?style=for-the-badge&logo=YouTube&logoColor=white)](https://www.youtube.com/@agentluke)
[![Steam](https://img.shields.io/badge/steam-%23000000.svg?style=for-the-badge&logo=steam&logoColor=white)](https://steamcommunity.com/id/agentluke/myworkshopfiles/)

## Build Tools
Releases include a `build/` folder, installed to `Core/build` in your script project. It shrinks the packaged script after MDK builds it, so your script fits in the programmable block:

- `MotherCore.targets` runs after `MdkPack`. It runs the optimizer, then removes `private` and `readonly` keywords from the script.
- `ScriptOptimizer/` is the optimizer. It runs with `dotnet run`, so the .NET SDK must be installed. If it cannot run, the script is left as MDK packed it and the build still succeeds.
- `RemoveKeywords.ps1` removes the keywords.

Projects created with `mother new` import the targets already. To add them to an existing project, put this in your `.csproj` and run `mother install` (or copy the `build/` folder from the latest release into `Core/`):

```xml
<Import Project="$(MSBuildProjectDirectory)\Core\build\MotherCore.targets" Condition="Exists('$(MSBuildProjectDirectory)\Core\build\MotherCore.targets')" />
```

Set `<MotherCoreOptimize>false</MotherCoreOptimize>` or `<MotherCoreRemoveKeywords>false</MotherCoreRemoveKeywords>` in the project to skip a step. If your project has its own post-pack target that edits the script (such as `MinifyScriptFile`), keep it after this import so it receives the optimized script.

## Deploying a New Version
Mother Core is released through GitHub. Pushing a version tag runs the [Build and Release workflow](.github/workflows/copy-to-dist.yaml), which publishes a GitHub Release. [Mother CLI](../MotherCLI/README.md) installs Core from the latest release.

1. Finish and merge your changes into the release branch, and confirm the MotherCore tests pass.
2. Update `src/CHANGELOG.md` with a new `## [x.y.z] - date` entry. It ships inside the release.
3. Update the version badge in `src/README.md` to `x.y.z`.
4. Commit the changes.
5. Tag the commit with a `v` prefix and push the tag. The workflow only runs for tags matching `v*`.

   ```powershell
   git tag v1.2.0
   git push origin v1.2.0
   ```

6. Wait for the **Build and Release** action to finish. It copies `src/Mother.cs`, `Commands/`, `Modules/`, `Utilities/`, `CHANGELOG.md`, `LICENSE.md`, `README.md` and the build tools (`build/MotherCore.targets`, `build/RemoveKeywords.ps1`, `build/ScriptOptimizer/`) into `dist/`, zips them as `mother-core-vX.Y.Z.zip`, and attaches the zip to a new GitHub Release.
7. Verify the release contains the zip with a `build/` folder, and that a fresh Core install through Mother CLI pulls the new version.

Use semantic versioning (`major.minor.patch`). Only tags are released, so pushing to a branch does not publish anything.

## Contributions
Contributions are welcome. Please submit a pull request and share why you decided to start building your own programmable block script :)