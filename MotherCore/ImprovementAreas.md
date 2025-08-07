# ISSUES

# Areas for further Improvement/ Optimization

## Add special exception handling when a user has a duplicate key in CustomData.
Right now system shows duplicate key exception @ ie. `Commands/fptest0`, but it does not tell us this is technically in CustomData.  This is a UX problem that we should address.

## Simplify Storage to use _Ini parser
https://spaceengineers.wiki.gg/wiki/Scripting/Handling_Configuration_and_Storage#:~:text=%22airlock%22))%3B-,SAVING%20TO%20STORAGE,-So%20far%20I%E2%80%99ve

We can likely just use storage as a wrapper around this class. It should allow us to get/set many different types with avaialable mothods allowing us to potential reduce reliance on the `Serializer` class.

## Store block specific commands within a blocks custom data
During boot, mother should load all custom data props for functional blocks, and expose them to the command terminal.  To make sure commands are easy to locate, introduce a `find` command to identify the block containing a command by name.

## 2. Debug Mode

Module and config is currently unused. How can I tak advantage of this module to debug Mother once in prod, vs. using DisplayManager or inline prints? Perhaps a custom "Exception" class for logging/printing"

## Add more commands and options:

2. Gyroscope Module
   1. Add pitch, roll, yaw

3. Thruster Module
   1. Add thrust max (accept max as arg)
   2. Add thrust min (accept min as arg)


## Logical operation

Tough one. How can users run logical operations against values provided, or a value in LocalStorage.  Where and in what format would these operations be stored?


## Mother cleanup opportunities:

- Consider improving request/response serialization	- we can pass an object type via the IGC, which means we do not need to stringify the entire request payload. Instead we can simplify to a generic object and then rebuild on receipt.
 
- Consider using MyIni module for serialization vs. Custom implementation. 

- Add Extension Modules	
    - Antenna block		
        - most importantly, can we automatically have two laser antennas find each other by using the almanac? This would remove the requirement for the player too manually set, and would allow dynamic targeting if a grid would like to use the single laser antenna for multiple targets.	

    - AI blocks? 		
        - What can be accessed here that would be useful? Likely actions and props related to offensive and defensive posturing. Follow player/ship for formation flight?
