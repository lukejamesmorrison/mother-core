# Changelog - Mother Core

## [Unreleased]

## [0.3.0] - UPCOMING

### BREAKING CHANGES TO IMPLEMENT
- Rename `[Commands]` section to [commands] in programmable block
- Refactor Ping logic to use request header data and not body data
- Allow IntergridMessageService to use multiple channels, each with own encyption
- Rename `nav/set-flight-plan` to `fp/set`


## [0.2.14] - 2025-07-XX]

### Added
- Add `ALMANAC` display target to print the list of Alamanc records to the screen as a companion for the `MAP` display.
- Add `VersionManager` as part of Configuration module which will automatically update the programmable blocks Custom Data to help players update versions. This will be closely maintained and controlled for bloat as new versions are released.  

### Updated
- The communications system has been refactored to support simultaneous communication on multiple channels at once. This requires changes to the programmable block's custom data.
    - All communications are now set in the `[channels]` section of the custom data.
    - Players can communication on the open public channel using the `*` channel name without a password ie. `*=`
    - Players can communication on a private channel using `<channel>=<passcode>` ie. `Channel1=secret123`

## [0.2.13] - 2025-06-28]
### Added
- Add `boot` command to trigger system boot process.

###
- Fixed critical bug preventing large grids from booting due to a "script too complex" error.  The boot process has been refactored this appears to have resolved the issues. This comes at the cost of a slightly longer boot depending on the number of registered modules.

### Updated
- Updated how increments are handled in `BaseModuleCommand`.

### Fixed

## [0.2.12] - 2025-04-29]
### Added
- Add Air Vent hooks:
    - `onPressurized` => triggered when state is *Pressurized*.
    - `onPressurizing` => triggered when state is *Pressurizing*.
    - `onDepressurized` => triggered when state is *Depressurized*.
    - `onDepressurizing` => triggered when state is *Depressurizing*.

- Add Piston hooks:
    - `onExtended` => triggered when state is *Extended*.
    - `onExtending` => triggered when state is *Extending*.
    - `onRetracted` => triggered when state is *Retracted*.
    - `onRetracting` => triggered when state is *Retracting*.

- Add `block/toggle` command to toggle a block on and off.

### Updated
- Update Door hooks:
    - `onOpen` => triggered when state is fully *Open*.
    - `onOpening` => triggered when state is *Opening*.
    - `onClose` => triggered when state is fully *Closed*.
    - `onClosing` => triggered when state is *Closing*.
- Updated `Instructions.readme` to include Discord channel.

### Fixed
- Fixed critical bug caused by Fieldwork update (1.206).  This bug was related to how the programmable block API resolves a `List<>` when being enforced by an interface.  The solution was to use the new `MemorySafeList<>` type provided by Keen. We're back baby!

## [0.2.11] - 2025-04-18

### Added
- Add `CockpitModule` to manage cockpit blocks. Players can now use the `onOccupied` and `onEmpty` hooks.
- Add `dampeners/on` and `dampeners/off` commands to enable control of thruster dampeners.
- Add `handbrake/on` and `handbrake/off` commands to enable control of the handbrake on a grid.

### Updated
- All system modules are now decoupled and registered as either an `ICoreModule` or `IExtensionModule`.  This allows for better management of modules and their dependencies during boot and throughout runtime activities.
- Refactor Extension Modules and commands to use new BaseExtensionModule methods.
- Update file structure to isolate Mother OS modules from Mother Core.
- Rename `WaypointCommandQueue` to `WaypointRoutineQueue` for better context.
- Event subscription from within Extension modules is now much easier and uses the event type vs. name for filtering.
- Hinge and Rotor angle tolerances have been improved to 0.1 degrees.
- The `screen/print` command now support line breaks with the `\n` character with the message string.
- Move `print`, `clear`, and `help` commands into core modules where they belong.
- Move `FlightPlanner` to extension modules and rename to `FlightPlanningModule`.
- Move `DisplayManager` to extension modules and rename to `DisplayModule`.
- Move `get` and `set` commands to LocalStorage module.

### Fixed
- Fix bug related to leading and trailing spaces in tag definitions with a block's custom data.
- Fixed bug preventing pistons from extending correctly. This was related to the Activity Monitor not watching the piston correctly following a refactor of the ActivityMonitor.
- Fix bug where targeting a non-existant tag would throw an exception.  This is now handled gracefully and players are notified when a tag cannot be found.
- Fixed bug that was preventing `Connector.onUnlock` hook from firing in some cases.
- Romote Control block is now correctly set from the Block Catalogue.
- Fixed bug preventing LocalStorage from saving properly between program cycles.

### Removed
- Removed Controller classes. This has turn out to be an unecessary abstraction at this state. Modules can handle their own request handling for now.
- Remove logic for rendering lines to a text display. This logic will be re-evaluated when Mother GUI begins development.
- Remove `CommunicationsModule`. These capabilities are handled via other modules.
- Remove `NavigationModule`. These capabilities are handled via other modules.

## [0.2.10] - 2025-04-07

### Added
- Add `Terminal` core module to separate Terminal and Display responsibilities due to their difference in use and implementation.
- Add `SpriteFactory` to manage sprite creation and rendering.
- Add **Tags**.  Blocks can now be targeting using tags that are defined with a block's Custom Data. This makes it easy to target groups of blocks without worrying about group merging and hiding with the grid terminal.
- `BaseExtensionModule` now provides accessor for BlockCatalogue.GetBlocksByName().

### Updated
- Extension modules and commands have been refactored for size.
- Refactor DisplayManager to abstract sprite management.  Some display manipluation has also been migrated into the `Display` class.
- Refactored core modules for file size.

## [0.2.9] - 2025-03-30

### Added
- Add `screen/print` command to allow players to print custom messages to an LCD screen.
- Add `ScreenModule` to manage interacts with LCD screens.
- Add `MergeBlockModule` to manage merge blocks and processes.  Merge block development is ongoing and will require some hacking to make work correctly. Stay tuned.
- Add groundwork for *Docking Procedure* logic to work with flight plans correctly.
- `FlightControlSystem` can now control thruster dampeners.


### Updated
- The `dock` command will now only use connectors that are power on and in an `Unconnected` state.
- Docking request/responses now adhere to a response code system defined in `Response`.
- The `IntergridMessageService` now creates responses with a mandatory response code.
- Docking sequences have been improved for use in space with failsafe checks to ensure smooth docking. Planetary testing still ongoing and failsafes should prevent crashing. Use planetary docking with caution for now.
- `ColorHelper` now holds logic for converting and retreving colors for use in commands and modules.
- `BaseExtensionModule` now contains more common method accessors for modules to use ie. `Subsribe()`, `Emit()`.
- Update boot logic of `Mother` to ensure extension modules can access all core modules and services during boot.
- Mother now uses the the max radius around the grid as a safe zone for flying procedures. Grids send safe radius as part of their standard message Header.

### Fixed
- Resolve error when trying to run `fcs/stop` command without an active flight plan.
- Grids no longer remain in a 10 degree rotated position during final landing phase.  Rotation is nessecary to jolt the grid into motion.  This feels unecessary and I am still exploring a fix to eliminate this behaviour.

## [0.2.8] - 2025-03-20

### Added
- All `IMyTerminalBlock` blocks can now be controlled via `BlockModule`.  Players can use the `block/on` and `block/off` commands to toggle blocks on and off.  This includes blocks like Refineries, Turrets, and Button Panels.
- Add `IBlockStateHandler` to help with block state management.
- Add `DoorOpenedEvent` and `DoorClosedEvent`.
- Add `DockingModule` and `dock` command.

### Update
- BlockCatalogue block loading has been improved for performance.
- BaseExtensionModule has been expanded to abstract away more common capabilities for developers.
- Hooks are now triggered on `Doors` when activatd by the player.  Due to the new aporoach to block state management, hooks can easily be fired from player interactions, or by Mother via commands. 


## [0.2.7 - Hotfix 1] - 2025-03-15

### Update
- Modify command parsing for improved performance.
- Updated ConnectorModule performance when scanning for connector state changes.


## [0.2.7] - 2025-03-14

### Added 
- Added `gear/auto` command to control the autolock state of landing gear and magnetic plates.
- Add `gyro/face` command to orient to grid towards a **GPS waypoint**. A future update will enable facing towards Almanac records. Not useable in-game yet but will be part of autodocking update coming soon.
- Add `piston/speed` command to set the speed of a piston.
- Add `SensorModule` to allow players to make use of sensor blocks.  Players can use *hooks* to trigger events when a sensor detects an entity.
    - Hooks: `onDetect`, `onClear`

### Updated
- `rotor/lock` and `rotor/unlock` commands now set upper/lower limits to enable rotation without specific angle defninitions via the `rotor/speed` command.
- `piston/speed`, `rotor/speed` and `hinge/speed` commands now accept increment/decrement options to adjust speed while in motion. ie. `rotor/speed Rotor1 2 --add` will increment the current rotor speed by 2 RPM.

## [0.2.6] - 2025-03-06

### Added 
- Add `block/action` command to enable players to run block actions current available via the a timer block / toolbar.  See `BlockModule`.
- Add `IMyMedicalRoom` to Block Catalogue.
- Add `pb/run` command to run other programmable blocks with an argument.
- Map displays can now be scaled and centered on a position in 2D and 3D (experimental).

### Updated
- Refactor Request & Response creation to factory method in `IntergridMessageService`.
- Connector hooks are now also triggered when using the keyboard shortcut `P` or manipulating the connector via the toolbar.
- Local programmable blocks are no longer rendered on the map display.

### Fixed
- Fixed ambiguity in command execution from wait command.  Commands should now execute in parallel vs. sequentially (in some cases).
- Mother now correctly loads LCD displays on subgrids.

## [0.2.5] - 2025-02-16

### Added
- Add `ICoreModule` and `BaseCoreModule` to aid with decompling and simplifying core module behaviours.
- Modules can now register blocks with BlockCatalogue to improve decoupling. This delegates block definition to the Modules, but allows management to remain within the BlockCatalogue.
- The BlockCatalogue now also accesses block-level CustomData to enable block-level configuration. I am working on zooming the map display among other items.
- Players can now acces block `hooks` via a block's CustomData.  This allows players to circumvent the event controller when automating localized systems, like an airlock.
- Add `piston/stop` command for stopping a piston while in motion. Note that pistons do not lock like a Rotor or Hinge.
- Players can use `this` in place of the block name when running a hook from a block's custom data. ie. A door can close itself with `door/close this;`.
- Added the following block hooks that can be defined with a block's own customData:
    - BlockModule - all blocks.
        - `onOn` => triggered when Mother turns a block on with `block/on <block>`
        - `onOff` => triggered when Mother turns a block off with `block/off <block>`
    - DoorModule - doors.
        - `onOpen` => triggered when Mother opens a door with `door/open <door>`
        - `onClose` => triggered when Mother closes a door with `door/close <door>`
    - ConnectorModule - connectors.
        - `onLock` => triggered when Mother connects a connector with `connector/lock <connector>`
        - `onUnlock` => triggered when Mother disconnects a connector with `connector/unlock <connector>`
- Block hooks can be defined within Mother's customdata to trigger events on blocks.  This allows for localized automation of blocks.


### Updated
- Block Catalogue now manages `IMyTerminalBlock` vs `IMyFunctionalBlock` types for greater flexibility.
- Rename `IModule` to `IExtensionModule`
- `RemoveKeywords` build script now removes `private` and `readonly` keywords during build.
- Add core modules now conform to `ICoreModule` interface.
- Module accessors updated.
- Move Request + Response related events to `Core/Modules/IntergridMessageService`.
- Display scaling is now improved and will be accessible via block level configuration in a future update.

### Fixed
- `DoorModule` commands are fixed in documentation.  Doors may be opened with the `door/open` and `door/close` commands.
- Mother can now run on programmable blocks connected via a subgrid without issue.
- Mother now correctly updates groups and blocks when a connector connects/disconnects.

### Removed
- Deprecate `QueryController` in favour of newest Request/Response pattern.


## [0.2.4] - 2025-02-12

### Added
- Add script to remove redundant keywords for target environment (programmable block script interface)
- Add `SoundModule` to manage sound blocks with the following commands:
    - `sound/set`
    - `sound/play`
    - `sound/stop`

- Drills, Welders, and Grinders can now be controlled using the following commands:
    - `block/on`
    - `block/off`

- Add `AirVentModule` to manage air vents with the following commands:
    - `vent/pressurize`
    - `vent/depressurize`
    - `vent/toggle`

### Updated
- Script instruction block now contains link to Discord server - https://discord.gg/aPa2UGHy.
- Configuration is now updated with missing defaults on boot and Recompile. This will reduce errors related to invalid configuration in CustomData.  Mother will not replace existing values.
- Cockpit map displays now have improved scaling.  I will add the ability to set custom scales in a future update.

### Fixed
- The `piston/distance` command now correctly accepts a `speed` option.
- Default configuration in documentation now uses correct sytnax.
- `print` command now works correctly.


## [0.2.3] - 2025-02-02

### Added
- Add `wait` command to introduce delays in commands and routines.

### Updated
- Update map display icons for better differentiation.
    - GPS Waypoints are diamonds
    - Grids are circles
    - Mother is a triangle
- Update documentation with more notes and images.
- Removed redundant BlockCatalogue documentation.
- Add `W` indicator to show when Mother is waiting to execute a command in time. See `wait` command.
    

## [0.2.2] - 2025-02-01

### Added
- Set line limit of terminal output to 20. Funny enough, this is the biggest source of performance loss.
- Add Mother logo to displays. The Empire must grow.
- Add `LandingGearModule` to support landing gear operations
    - `gear/lock`
    - `gear/unlock`
    - `gear/toggle`

- Add `TankModule` to support oxygen and hydrogen tanks.
    - `tank/stockpile`
    - `tank/share`
    - `tank/toggle`

### Updated
- System actions (ie. refresh almanac)  are now queued using the `Clock` to run, based on their criticality.
- Refactor `TerminalRoutine` to handle targeting and unpacking logic. `TerminalCommand` now only apply to local execution. 
- Refactor `CommandBus` to handle local and remote commands based on a `TerminalRoutine` vs. `TerminalCommand`.
- Update documentation links and examples.
- Update `IntergridCommunicationService` to use unpacked strings for transmitting remote commands
- Update sprite drawing in `DisplayManager`.

### Fixed
- Rotors now rotate in correct direction based on angle and with reset in the opposite direction to 0 degrees.


## [0.2.1] - 2025-01-30

### Added
- Add `Clock` core module.  Mother now runs on a 1 second loop by default.
- Add batteries to BlockCatalogue.
- Add `Batterymodule` to enable the following commands:
    - `battery/charge`
    - `battery/discharge`
    - `battery/auto`
    - `battery/toggle`

### Updated
- CommandBus is now more efficient at nesting local commands and routines into flight plan routines.
- CommandCheatsheet links and examples have been updated.
- hidden `hinge/reset` and `rotor/reset` commands from documentation.  They are unreliable at this time. Please use `hinge/rotate` and `rotor/rotate` to reset hinges and rotors for now.
- Commands and routines are automatically trimmed and sanitized when created.

### Fixed
- The Almanac is now correctly storing waypoints following the setting of a flight plan and loaded on Recompile.
- Fixed issue with `rotor/rotate` speed defaulting to 2 RPM when a negative value is used as the `speed` value. Speed is now unsigned, and only angle will determine the direction of rotation.

## [0.2.0] - 2025-01-28

### Added
- Add CommandQueue to support the queueing of commands in time and at waypoints in a flight plan.
- Runtime can now be set in CustomData via `general.update_frequency`. It defaults to Update100 (every 1.6 seconds). 10 may be used to toggle Update10 (6 times per second).
- Local commands can now be triggered by using an underscore `_` as the first character in the command string. ie. `_PowerOff`. This ensure that a grid can have local definitions of custom commands that can be targeted remotely.
- Add generalized `block/on` and `block/off` commands to support power toggling of functional blocks.

### Updated
- Flight plans now have separate `Loaded` and `Active` states for better control of autopilot.
- CommandBus can now queue commands for execution at a later time.
- Flight plan string can how include commands and routines to execute at a given waypoint:
    - `nav/set-flight-plan "GPS:Waypoint1:123:456:789:#FFFFFF:{light/color Light1 red; CustomCommand;...}"`
- Add `off` option got `light/blink` command and updated documentation.

### Fixed
- Rotors and hinges now correctly lock when stopping.
- Custom commands can now correctly be chained together in a routine.


## [0.1.4] - 2025-01-25

### Added
- Current flight plan is now displayed on `MMAP` displays.

### Updated		
- Grids not transmit updated positions while in motion.
- NavigationModule refactored into FlightPlanner core module.
- Flight plan progress is now commited to log. Use `MDEBUG` to view debug/log screen.

### Fixed
- Hide request payload in terminal.


## [0.1.3] - 2025-01-24

### Updated
- All messages between programmable blocks now contain position information.
- Almanac displays now update as a result of pings and messages from other grids.

### Fixed
- Security module now correctly retreives config. (Bool from String error)
- Fixed bug causing the Almanac to fail on a purge.


## [0.1.2] - 2025-01-23

### Updated
- The `timer/start` command now supports the `delay` option to allow custom delays.

### Fixed

- Hinges now work correctly when called as part of a group.
- Fixed error when passing options to `hinge/rotate` and `rotor/rotate` commands.
