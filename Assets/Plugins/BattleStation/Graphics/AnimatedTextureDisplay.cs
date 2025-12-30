using System;
using System.Collections.Generic;
using UnityEngine;

namespace Skeletom.BattleStation.Graphics.Animations
{
    public class AnimatedTextureDisplay : MonoBehaviour
    {
        [Serializable]
        public class AnimatedTexture
        {
            [Serializable]
            public struct Frame
            {
                public Texture image;
                public float delay;

                public Frame(Texture image, float delay)
                {
                    this.image = image;
                    this.delay = delay;
                }
            }
            public List<Frame> frames;
            public AnimatedTexture(IEnumerable<Frame> frames)
            {
                this.frames = new List<Frame>(frames);
            }
        }

        public void DisplayTexture(AnimatedTexture texture) {
            // TODO
        }
    }
}
