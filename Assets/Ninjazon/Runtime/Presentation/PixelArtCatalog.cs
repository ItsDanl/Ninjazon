using System;
using UnityEngine;

namespace Ninjazon.Runtime.Presentation
{
    [Serializable]
    public sealed class PixelAnimationClip
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField, Min(1f)] private float framesPerSecond = 10f;
        [SerializeField] private bool loop = true;
        public Sprite[] Frames => frames;
        public float FramesPerSecond => framesPerSecond;
        public bool Loop => loop;

        public PixelAnimationClip() { }

        public PixelAnimationClip(Sprite[] frames, float framesPerSecond, bool loop)
        {
            this.frames = frames ?? Array.Empty<Sprite>();
            this.framesPerSecond = Mathf.Max(1f, framesPerSecond);
            this.loop = loop;
        }
    }

    [CreateAssetMenu(menuName = "Ninjazon/Pixel Art Catalog", fileName = "PixelArtCatalog")]
    public sealed class PixelArtCatalog : ScriptableObject
    {
        [Header("Runner")]
        [SerializeField] private PixelAnimationClip runnerIdle = new();
        [SerializeField] private PixelAnimationClip runnerRun = new();
        [SerializeField] private PixelAnimationClip runnerJump = new();
        [SerializeField] private PixelAnimationClip runnerSlide = new();
        [SerializeField] private PixelAnimationClip runnerHit = new();
        [SerializeField] private PixelAnimationClip runnerCapture = new();
        [Header("Environment")]
        [SerializeField] private Sprite skyGradient;
        [SerializeField] private Sprite skylineFar;
        [SerializeField] private Sprite skylineMid;
        [SerializeField] private Sprite skylineNear;
        [SerializeField] private Sprite rooftopTile;
        [SerializeField] private Sprite rooftopDetails;
        [Header("Hazards")]
        [SerializeField] private Sprite barricade;
        [SerializeField] private Sprite laserGantry;
        [SerializeField] private Sprite blockedRoute;
        [SerializeField] private Sprite scannerDrone;
        [Header("Pickups")]
        [SerializeField] private PixelAnimationClip dataChip = new();
        [SerializeField] private PixelAnimationClip credit = new();
        [SerializeField] private PixelAnimationClip jammer = new();
        [Header("Effects")]
        [SerializeField] private PixelAnimationClip landing = new();
        [SerializeField] private PixelAnimationClip pickupBurst = new();
        [SerializeField] private PixelAnimationClip impact = new();
        [SerializeField] private PixelAnimationClip capture = new();

        public PixelAnimationClip RunnerIdle => runnerIdle;
        public PixelAnimationClip RunnerRun => runnerRun;
        public PixelAnimationClip RunnerJump => runnerJump;
        public PixelAnimationClip RunnerSlide => runnerSlide;
        public PixelAnimationClip RunnerHit => runnerHit;
        public PixelAnimationClip RunnerCapture => runnerCapture;
        public Sprite SkyGradient => skyGradient;
        public Sprite SkylineFar => skylineFar;
        public Sprite SkylineMid => skylineMid;
        public Sprite SkylineNear => skylineNear;
        public Sprite RooftopTile => rooftopTile;
        public Sprite RooftopDetails => rooftopDetails;
        public Sprite Barricade => barricade;
        public Sprite LaserGantry => laserGantry;
        public Sprite BlockedRoute => blockedRoute;
        public Sprite ScannerDrone => scannerDrone;
        public PixelAnimationClip DataChip => dataChip;
        public PixelAnimationClip Credit => credit;
        public PixelAnimationClip Jammer => jammer;
        public PixelAnimationClip Landing => landing;
        public PixelAnimationClip PickupBurst => pickupBurst;
        public PixelAnimationClip Impact => impact;
        public PixelAnimationClip Capture => capture;
    }
}
