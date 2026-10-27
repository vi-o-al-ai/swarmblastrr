using System;
using System.Collections.Generic;
using UnityEngine;
using Swarmblastrr.Core;
using Swarmblastrr.Track;

namespace Swarmblastrr.UI
{
    /// <summary>
    /// Pure polish: on <see cref="GameEvents.GateHit"/> spawns a world-space <see cref="TextMesh"/>
    /// at the gate reading <see cref="GateMath.Label"/>, rises and fades it for 0.8 s, then destroys
    /// it. A single instance lives on a GameObject named "FloatingText"; it must never throw.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        private const string ObjectName = "FloatingText";
        private const float Lifetime = 0.8f;
        private const float RiseSpeed = 3f;
        private const float SpawnHeight = 2.2f;
        private const float CharacterSize = 0.2f;
        private const int FontSize = 64;

        private static FloatingText _instance;

        private readonly List<Entry> _entries = new List<Entry>();
        private bool _subscribed;

        /// <summary>
        /// Finds the existing instance (or the "FloatingText" GameObject) or creates one, and returns it.
        /// </summary>
        public static FloatingText Ensure()
        {
            if (_instance != null)
            {
                return _instance;
            }

            FloatingText existing = FindFirstObjectByType<FloatingText>();
            if (existing != null)
            {
                _instance = existing;
                existing.Subscribe();
                return existing;
            }

            GameObject go = GameObject.Find(ObjectName);
            if (go == null)
            {
                go = new GameObject(ObjectName);
            }

            FloatingText created = go.GetComponent<FloatingText>();
            if (created == null)
            {
                created = go.AddComponent<FloatingText>();
            }

            _instance = created;
            created.Subscribe();
            return created;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ClearEntries();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (_entries.Count == 0)
            {
                return;
            }

            try
            {
                Tick(Time.deltaTime);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                ClearEntries();
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameEvents.GateHit += OnGateHit;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameEvents.GateHit -= OnGateHit;
            _subscribed = false;
        }

        private void OnGateHit(GateOperation op, int before, int after, Vector3 worldPos)
        {
            try
            {
                GateOperation labelOp;
                int value;
                DescribeGate(op, before, after, out labelOp, out value);
                Spawn(GateMath.Label(labelOp, value), GateMath.ColorFor(labelOp, value), worldPos);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void Spawn(string label, Color color, Vector3 worldPos)
        {
            GameObject go = new GameObject("FloatingLabel");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos + new Vector3(0f, SpawnHeight, 0f);

            Camera cam = Camera.main;
            if (cam != null)
            {
                go.transform.rotation = cam.transform.rotation;
            }

            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = label;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = CharacterSize;
            mesh.fontSize = FontSize;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = color;

            Font font = UiFactory.DefaultFont();
            if (font != null)
            {
                mesh.font = font;
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    renderer = go.AddComponent<MeshRenderer>();
                }

                renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _entries.Add(new Entry(mesh, color));
        }

        private void Tick(float deltaTime)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                entry.Age += deltaTime;

                if (entry.Mesh == null)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                if (entry.Age >= Lifetime)
                {
                    Destroy(entry.Mesh.gameObject);
                    _entries.RemoveAt(i);
                    continue;
                }

                float t = entry.Age / Lifetime;
                Transform tr = entry.Mesh.transform;
                tr.position += Vector3.up * (RiseSpeed * deltaTime);
                tr.localScale = Vector3.one * (1f + 0.25f * t);

                Color faded = entry.BaseColor;
                faded.a = entry.BaseColor.a * (1f - t * t);
                entry.Mesh.color = faded;
            }
        }

        private void ClearEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Mesh != null)
                {
                    Destroy(_entries[i].Mesh.gameObject);
                }
            }

            _entries.Clear();
        }

        /// <summary>
        /// Recovers the gate value from the (before, after) counts so the label matches the gate.
        /// Falls back to the raw delta when clamping makes the operation ambiguous.
        /// </summary>
        private static void DescribeGate(GateOperation op, int before, int after, out GateOperation labelOp, out int value)
        {
            switch (op)
            {
                case GateOperation.Add:
                    labelOp = GateOperation.Add;
                    value = Mathf.Max(0, after - before);
                    return;
                case GateOperation.Subtract:
                    labelOp = GateOperation.Subtract;
                    value = Mathf.Max(0, before - after);
                    return;
                case GateOperation.Multiply:
                    if (before > 0 && after >= before)
                    {
                        labelOp = GateOperation.Multiply;
                        value = Mathf.Max(1, Mathf.RoundToInt(after / (float)before));
                        return;
                    }
                    break;
                case GateOperation.Divide:
                    if (after > 0 && before >= after)
                    {
                        labelOp = GateOperation.Divide;
                        value = Mathf.Max(1, Mathf.RoundToInt(before / (float)after));
                        return;
                    }
                    break;
            }

            int delta = after - before;
            labelOp = delta >= 0 ? GateOperation.Add : GateOperation.Subtract;
            value = Mathf.Abs(delta);
        }

        private sealed class Entry
        {
            public readonly TextMesh Mesh;
            public readonly Color BaseColor;
            public float Age;

            public Entry(TextMesh mesh, Color baseColor)
            {
                Mesh = mesh;
                BaseColor = baseColor;
                Age = 0f;
            }
        }
    }
}
