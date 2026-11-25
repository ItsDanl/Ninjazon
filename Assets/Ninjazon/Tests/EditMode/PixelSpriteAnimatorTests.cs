using Ninjazon.Runtime.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Ninjazon.Tests.EditMode
{
    public sealed class PixelSpriteAnimatorTests
    {
        [Test]
        public void NonLoopingClip_StopsOnItsLastFrame()
        {
            var fixture = CreateFixture(false);
            fixture.animator.Play(fixture.clip);
            fixture.animator.Tick(1f);

            Assert.That(fixture.animator.CurrentFrameIndex, Is.EqualTo(2));
            Assert.That(fixture.renderer.sprite, Is.SameAs(fixture.frames[2]));
            fixture.Dispose();
        }

        [Test]
        public void LoopingClip_WrapsAndSameClipDoesNotRestart()
        {
            var fixture = CreateFixture(true);
            fixture.animator.Play(fixture.clip);
            fixture.animator.Tick(0.21f);
            Assert.That(fixture.animator.CurrentFrameIndex, Is.EqualTo(2));

            fixture.animator.Play(fixture.clip);
            fixture.animator.Tick(0.11f);
            Assert.That(fixture.animator.CurrentFrameIndex, Is.EqualTo(0));
            fixture.Dispose();
        }

        [Test]
        public void EmptyClip_SafelyClearsTheRenderer()
        {
            var fixture = CreateFixture(true);
            fixture.animator.Play(new PixelAnimationClip(System.Array.Empty<Sprite>(), 10f, true));
            fixture.animator.Tick(1f);

            Assert.That(fixture.animator.CurrentFrameIndex, Is.EqualTo(-1));
            Assert.That(fixture.renderer.sprite, Is.Null);
            fixture.Dispose();
        }

        private static Fixture CreateFixture(bool loop)
        {
            var gameObject = new GameObject("PixelAnimatorTest", typeof(SpriteRenderer), typeof(PixelSpriteAnimator));
            var texture = new Texture2D(3, 1, TextureFormat.RGBA32, false);
            var frames = new Sprite[3];
            for (var i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i, 0, 1, 1), Vector2.one * 0.5f, 1f);
            return new Fixture(gameObject, texture, frames, new PixelAnimationClip(frames, 10f, loop));
        }

        private sealed class Fixture
        {
            public readonly GameObject gameObject;
            public readonly Texture2D texture;
            public readonly Sprite[] frames;
            public readonly SpriteRenderer renderer;
            public readonly PixelSpriteAnimator animator;
            public readonly PixelAnimationClip clip;

            public Fixture(GameObject gameObject, Texture2D texture, Sprite[] frames, PixelAnimationClip clip)
            {
                this.gameObject = gameObject;
                this.texture = texture;
                this.frames = frames;
                this.clip = clip;
                renderer = gameObject.GetComponent<SpriteRenderer>();
                animator = gameObject.GetComponent<PixelSpriteAnimator>();
            }

            public void Dispose()
            {
                foreach (var frame in frames) Object.DestroyImmediate(frame);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
