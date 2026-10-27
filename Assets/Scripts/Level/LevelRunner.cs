using System.Collections.Generic;
using UnityEngine;
using Swarmblastrr.Crowd;
using Swarmblastrr.Track;

namespace Swarmblastrr.Level
{
    /// <summary>
    /// Drives the interaction between the player's crowd and the track. There is no physics:
    /// every frame while <see cref="Active"/>, each live, unconsumed element within
    /// <c>playerZ - 15 .. playerZ + 60</c> is asked <see cref="TrackElement.Overlaps"/> and, if so,
    /// gets <see cref="TrackElement.OnCrowdOverlap"/> with this frame's delta time.
    /// </summary>
    public class LevelRunner : MonoBehaviour
    {
        private const float BehindRange = 15f;
        private const float AheadRange = 60f;

        private IReadOnlyList<TrackElement> _elements;
        private CrowdController _player;

        /// <summary>True between <see cref="Begin"/> and <see cref="Stop"/>.</summary>
        public bool Active { get; private set; }

        /// <summary>
        /// Starts polling <paramref name="elements"/> against <paramref name="player"/>. The list is
        /// read live, so the builder's own list can be passed. Null arguments leave the runner inactive.
        /// </summary>
        public void Begin(IReadOnlyList<TrackElement> elements, CrowdController player)
        {
            _elements = elements;
            _player = player;
            Active = elements != null && player != null;

            if (!Active)
            {
                Debug.LogWarning("[Swarmblastrr] LevelRunner.Begin called without elements or player; runner stays inactive.");
            }
        }

        /// <summary>Stops polling and drops the element and player references.</summary>
        public void Stop()
        {
            Active = false;
            _elements = null;
            _player = null;
        }

        private void Update()
        {
            if (!Active || _player == null)
            {
                return;
            }

            if (_elements == null)
            {
                Stop();
                return;
            }

            float playerZ = _player.Position.z;
            float minZ = playerZ - BehindRange;
            float maxZ = playerZ + AheadRange;
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < _elements.Count; i++)
            {
                TrackElement element = _elements[i];
                if (element == null || element.Consumed)
                {
                    continue;
                }

                float z = element.Z;
                if (z < minZ || z > maxZ)
                {
                    continue;
                }

                if (element.Overlaps(_player))
                {
                    element.OnCrowdOverlap(_player, deltaTime);
                }

                // The callback may have ended the run (win, loss, or an explicit Stop).
                if (!Active || _player == null || _elements == null)
                {
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
