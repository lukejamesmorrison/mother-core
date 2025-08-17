using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// This file is used to improve script minification my masking programmable block API types 
/// with custom types. This means you only need to define the API type once here, and then 
/// all other references can be minified. This is a HUGE character count reducer.
/// </summary>
namespace IngameScript
{
    /// <summary>
    /// The IMotherMotorStator interface extends the IMyMotorStator interface.
    /// </summary>
    public interface IMotherMotorStator : IMyMotorStator { }
}
