using UnityEngine;
using Swarmblastrr.Core;
using Swarmblastrr.Crowd;

namespace Swarmblastrr.Track
{
    /// <summary>
    /// One half of a gate pair. When the player's crowd centre passes through it the gate applies
    /// <see cref="Operation"/> / <see cref="Value"/> to the count once, then hides itself. The
    /// visual is a translucent wall tinted by <see cref="GateMath.ColorFor"/>, an opaque frame and
    /// a world-space label showing <see cref="GateMath.Label"/>.
    /// </summary>
    public class Gate : TrackElement
    {
        private const float WallHeight = 2.5f;
        private const float WallThickness = 0.15f;
        private const float WallAlpha = 0.55f;
        private const float FrameThickness = 0.2f;
        private const float LabelCharacterSize = 0.18f;
        private const float LabelHeight = 3.4f;
        private const float DefaultForwardSpeed = 8f;

        /// <summary>Arithmetic applied to the player's count.</summary>
        public GateOperation Operation = GateOperation.Add;

        /// <summary>Operand of <see cref="Operation"/>.</summary>
        public int Value = 1;

        private GameObject _visualRoot;
        private float _forwardSpeed = DefaultForwardSpeed;

        /// <summary>Sets the operation, operand and lateral half-width. Call before <see cref="BuildVisuals"/>.</summary>
        public void Configure(GateOperation op, int value, float halfWidth)
        {
            Operation = op;
            Value = value;
            HalfWidth = Mathf.Max(0.01f, halfWidth);
        }

        /// <summary>
        /// Gates test the player's centre X against their own half-width with no radius slack so
        /// only one gate of a pair triggers. The seam between a pair is at the lane centre; a gate
        /// sitting left of the centre does not own that shared edge, so a crowd at exactly X = 0
        /// triggers the right-hand gate only. The test is bounded at both ends in Z: the crowd's
        /// front must have reached the gate plane, and the gate stops testing once the crowd's back
        /// is well past the wall.
        /// </summary>
        public override bool Overlaps(CrowdController player)
        {
            if (Consumed || player == null)
            {
                return false;
            }

            if (player.FrontZ < Z)
            {
                return false;
            }

            // Trailing-edge cut. The margin is the furthest the crowd can travel in one clamped
            // frame, so a long frame cannot carry a small crowd from in front of the plane to past
            // this cut without the gate ever seeing it in between.
            float maxStep = Mathf.Max(0f, _forwardSpeed) * Time.maximumDeltaTime;
            if (player.Position.z - player.Radius > Z + WallThickness + maxStep)
            {
                return false;
            }

            float dx = player.Position.x - transform.position.x;
            if (dx < -HalfWidth || dx > HalfWidth)
            {
                return false;
            }

            if (transform.position.x < 0f && dx >= HalfWidth)
            {
                return false;
            }

            return true;
        }

        /// <summary>Applies the gate to the player once and hides the visuals.</summary>
        public override void OnCrowdOverlap(CrowdController player, float deltaTime)
        {
            if (Consumed || player == null)
            {
                return;
            }

            Consumed = true;
            ConsumeSibling();
            player.ApplyGate(Operation, Value, transform.position);
            HideVisuals();
        }

        /// <summary>
        /// Marks the gate as passed without applying it or touching its visuals. Used for the half
        /// of a pair the crowd did not take, so a lateral swipe while the crowd is still crossing
        /// the gate plane cannot apply both halves.
        /// </summary>
        internal void MarkPassed()
        {
            Consumed = true;
        }

        /// <summary>Translucent wall (HalfWidth*2 x 2.5 x 0.15), opaque frame and a label above.</summary>
        public override void BuildVisuals(GameConfig config)
        {
            if (config != null)
            {
                _forwardSpeed = config.ForwardSpeed;
            }

            DestroyVisuals();

            _visualRoot = new GameObject("Visuals");
            _visualRoot.transform.SetParent(transform, false);

            Color tint = GateMath.ColorFor(Operation, Value);
            Color wallColor = tint;
            wallColor.a = WallAlpha;
            Color frameColor = tint * 0.75f;
            frameColor.a = 1f;

            float width = HalfWidth * 2f;
            Transform root = _visualRoot.transform;
            Mesh cube = TrackVisuals.CubeMesh();
            Material frameMaterial = TrackVisuals.SolidMaterial(frameColor);

            TrackVisuals.CreatePrimitive(
                "Wall", root, cube, TrackVisuals.SolidMaterial(wallColor),
                new Vector3(0f, WallHeight * 0.5f, 0f),
                new Vector3(width, WallHeight, WallThickness));

            // Opaque sill under the wall hides the seam where the translucent wall meets the floor.
            TrackVisuals.CreatePrimitive(
                "Sill", root, cube, frameMaterial,
                new Vector3(0f, FrameThickness * 0.5f, 0f),
                new Vector3(width, FrameThickness, FrameThickness + WallThickness));

            TrackVisuals.CreatePrimitive(
                "TopBar", root, cube, frameMaterial,
                new Vector3(0f, WallHeight + FrameThickness * 0.5f, 0f),
                new Vector3(width + FrameThickness, FrameThickness, FrameThickness + WallThickness));

            float postX = HalfWidth - FrameThickness * 0.5f;
            float postHeight = WallHeight + FrameThickness;
            Vector3 postScale = new Vector3(FrameThickness, postHeight, FrameThickness + WallThickness);
            TrackVisuals.CreatePrimitive("PostLeft", root, cube, frameMaterial, new Vector3(-postX, postHeight * 0.5f, 0f), postScale);
            TrackVisuals.CreatePrimitive("PostRight", root, cube, frameMaterial, new Vector3(postX, postHeight * 0.5f, 0f), postScale);

            TrackVisuals.CreateLabel(
                root, GateMath.Label(Operation, Value), Color.white, LabelCharacterSize,
                new Vector3(0f, LabelHeight, 0f));
        }

        /// <summary>
        /// The level builder parents both halves of a pair under the same root at the same Z. Once
        /// this half has fired, every other unconsumed gate at this Z under the same parent is
        /// marked passed so the pair is mutually exclusive.
        /// </summary>
        private void ConsumeSibling()
        {
            Transform parent = transform.parent;
            if (parent == null)
            {
                return;
            }

            float z = Z;
            foreach (Transform child in parent)
            {
                Gate other = child.GetComponent<Gate>();
                if (other != null && other != this && !other.Consumed && Mathf.Approximately(other.Z, z))
                {
                    other.MarkPassed();
                }
            }
        }

        private void HideVisuals()
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(false);
            }
        }

        private void DestroyVisuals()
        {
            if (_visualRoot != null)
            {
                Destroy(_visualRoot);
                _visualRoot = null;
            }
        }
    }
}
