using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using System.Collections.Immutable;

namespace IngameScript
{
    /// <summary>
    /// The SpriteFactory class is a factory for creating sprites. 
    /// Sprites are most easily drawn on the Display class.
    /// </summary>
    public static class SpriteFactory
    {
        /// <summary>
        /// CreateShape a new Texture Sprite.
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="position"></param>
        /// <param name="size"></param>
        /// <param name="color"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public static MySprite CreateShape(string texture, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
            return new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = texture,
                Position = position,
                Size = size,
                Color = color,
                Alignment = TextAlignment.CENTER,
                RotationOrScale = rotation
            };
        }
    }
}
