using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace Skeletom.BattleStation.Graphics.Animations
{
    public class AnimatedTextureDisplay : BaseAnimatedElement
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
                    image.filterMode = FilterMode.Point;
                    image.wrapMode = TextureWrapMode.Clamp;
                    this.delay = delay;
                }
            }
            public List<Frame> frames;
            public AnimatedTexture(IEnumerable<Frame> frames)
            {
                this.frames = new List<Frame>(frames);
            }
        }

        [SerializeField]
        private RawImage _image;

        private AnimatedTexture _tex;

        public void DisplayTexture(AnimatedTexture texture)
        {
            _tex = texture;
        }

        protected override IEnumerator Animate()
        {
            if (_tex != null && _tex.frames.Count > 0)
            {
                foreach (AnimatedTexture.Frame frame in _tex.frames)
                {
                    _image.texture = frame.image;
                    yield return new WaitForSeconds(frame.delay);
                }
            }
        }

        protected override void InitializeImplementation()
        {

        }

        protected override void ResetImplementation()
        {
            if (_tex != null && _tex.frames.Count > 0)
            {
                _image.texture = _tex.frames[0].image;
            }
        }
    }
}
