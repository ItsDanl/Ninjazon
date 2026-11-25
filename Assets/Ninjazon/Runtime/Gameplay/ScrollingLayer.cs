using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ninjazon.Runtime.Gameplay
{
    public sealed class ScrollingLayer : MonoBehaviour
    {
        private const float SeamTolerance = 0.001f;
        [SerializeField] private SpriteRenderer[] tiles;
        [SerializeField] private float speedMultiplier = 1f;
        [SerializeField] private float edgeBleed = 1f;

        private readonly List<SpriteRenderer> orderedTiles = new();
        private bool initialized;

        public IReadOnlyList<float> OrderedCenters
        {
            get
            {
                EnsureInitialized();
                var centers = new float[orderedTiles.Count];
                for (var i = 0; i < orderedTiles.Count; i++) centers[i] = orderedTiles[i].bounds.center.x;
                return centers;
            }
        }

        public bool HasGap
        {
            get
            {
                EnsureInitialized();
                for (var i = 1; i < orderedTiles.Count; i++)
                {
                    if (orderedTiles[i].bounds.min.x - orderedTiles[i - 1].bounds.max.x > SeamTolerance) return true;
                }

                return false;
            }
        }

        public bool ValidateCoverage(float cameraHalfWidth, out string error)
        {
            return ValidateCoverage(0f, cameraHalfWidth, out error);
        }

        public bool ValidateCoverage(float cameraCenterX, float cameraHalfWidth, out string error)
        {
            if (!TryInitialize(out error)) return false;
            if (cameraHalfWidth < 0f)
            {
                error = "Camera half width must not be negative.";
                return false;
            }

            var requiredLeft = cameraCenterX - cameraHalfWidth - edgeBleed;
            var requiredRight = cameraCenterX + cameraHalfWidth + edgeBleed;
            if (orderedTiles[0].bounds.min.x - requiredLeft > SeamTolerance ||
                requiredRight - orderedTiles[^1].bounds.max.x > SeamTolerance)
            {
                error = "Tile coverage must extend past both camera edges and the configured edge bleed.";
                return false;
            }

            for (var i = 1; i < orderedTiles.Count; i++)
            {
                if (orderedTiles[i].bounds.min.x - orderedTiles[i - 1].bounds.max.x > SeamTolerance)
                {
                    error = "Tile coverage contains a gap between adjacent tiles.";
                    return false;
                }
            }

            var requiredWidth = requiredRight - requiredLeft;
            var availableWidth = orderedTiles[^1].bounds.max.x - orderedTiles[0].bounds.min.x;
            var widestTile = 0f;
            for (var i = 0; i < orderedTiles.Count; i++)
            {
                widestTile = Mathf.Max(widestTile, orderedTiles[i].bounds.size.x);
            }

            if (availableWidth < requiredWidth + widestTile)
            {
                error = "Tile coverage requires one additional tile width beyond both camera edges and edge bleeds to remain continuous while scrolling.";
                return false;
            }

            error = null;
            return true;
        }

        public void Advance(float worldSpeed, float deltaTime, float leftBoundary)
        {
            Advance(worldSpeed, deltaTime, leftBoundary, -leftBoundary);
        }

        public void Advance(float worldSpeed, float deltaTime, float leftBoundary, float rightBoundary)
        {
            EnsureInitialized();
            if (rightBoundary < leftBoundary) throw new ArgumentException("Right boundary must not be left of the left boundary.");
            var distance = worldSpeed * speedMultiplier * deltaTime;
            for (var i = 0; i < orderedTiles.Count; i++)
            {
                orderedTiles[i].transform.position += Vector3.left * distance;
            }

            while (orderedTiles[0].bounds.max.x < leftBoundary - edgeBleed)
            {
                MoveLeftmostAfterRightmost();
            }

            while (orderedTiles[^1].bounds.max.x < rightBoundary + edgeBleed)
            {
                MoveLeftmostAfterRightmost();
            }
        }

        private void MoveLeftmostAfterRightmost()
        {
            var leftmost = orderedTiles[0];
            var rightmost = orderedTiles[^1];
            var shift = rightmost.bounds.max.x - leftmost.bounds.min.x;
            leftmost.transform.position += Vector3.right * shift;
            orderedTiles.RemoveAt(0);
            orderedTiles.Add(leftmost);
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            if (!TryInitialize(out var error)) throw new InvalidOperationException(error);
        }

        private bool TryInitialize(out string error)
        {
            if (initialized)
            {
                error = null;
                return true;
            }

            if (tiles == null || tiles.Length == 0)
            {
                error = $"{name} requires serialized tile renderers.";
                return false;
            }

            if (edgeBleed < 0f)
            {
                error = $"{name} requires a non-negative edge bleed.";
                return false;
            }

            orderedTiles.Clear();
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                if (tile == null)
                {
                    orderedTiles.Clear();
                    error = $"{name} has a missing serialized tile renderer at index {i}.";
                    return false;
                }

                if (tile.gameObject.activeInHierarchy && tile.enabled) orderedTiles.Add(tile);
            }

            if (orderedTiles.Count < 2)
            {
                orderedTiles.Clear();
                error = $"{name} requires at least two active serialized tile renderers.";
                return false;
            }

            orderedTiles.Sort((left, right) => left.bounds.min.x.CompareTo(right.bounds.min.x));
            initialized = true;
            error = null;
            return true;
        }
    }
}
