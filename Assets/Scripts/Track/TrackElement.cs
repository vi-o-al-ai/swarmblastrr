using UnityEngine;
using Swarmblastrr.Core;
using Swarmblastrr.Crowd;

namespace Swarmblastrr.Track
{
    /// <summary>
    /// Base class for everything placed along the lane. There is no physics: the level runner
    /// polls <see cref="Overlaps"/> every frame and calls <see cref="OnCrowdOverlap"/> while it is
    /// true and the element has not been <see cref="Consumed"/>.
    /// </summary>
    public abstract class TrackElement : MonoBehaviour
    {
        /// <summary>True once the element has finished interacting with the player for good.</summary>
        public bool Consumed { get; protected set; }

        /// <summary>World-space Z of this element (the lane runs along +Z).</summary>
        public float Z => transform.position.z;

        /// <summary>Lateral extent; the element spans transform.position.x ± HalfWidth.</summary>
        public float HalfWidth = 3f;

        /// <summary>
        /// True when the player's crowd should interact with this element this frame. Default: the
        /// crowd's front has reached <see cref="Z"/> and its centre is within
        /// <see cref="HalfWidth"/> plus half the crowd radius of this element's X.
        /// </summary>
        public virtual bool Overlaps(CrowdController player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.FrontZ < Z)
            {
                return false;
            }

            float dx = Mathf.Abs(player.Position.x - transform.position.x);
            return dx <= HalfWidth + player.Radius * 0.5f;
        }

        /// <summary>Called by the level runner every frame while <see cref="Overlaps"/> is true and not <see cref="Consumed"/>.</summary>
        public abstract void OnCrowdOverlap(CrowdController player, float deltaTime);

        /// <summary>Builds the visual children from primitive meshes. Called by the level builder after fields are set.</summary>
        public abstract void BuildVisuals(GameConfig config);
    }
}
