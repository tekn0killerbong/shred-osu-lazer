using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Shred.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Shred.UI
{
    /// <summary>Shared geometry for the editor and gameplay. Every head is centred
    /// before applying shear, just like the fret receptor.</summary>
    public partial class ShredNoteVisual : Container
    {
        private readonly Box body;
        private readonly Box hook;
        private readonly ShredGlyph head;
        private readonly Circle joint;
        private readonly Drawable halo;
        private readonly ShredGlow haloOutline;
        private readonly ShredHitObject note;
        private readonly ShredSkinSettings skin;
        private double? failureStarted;

        public ShredNoteVisual(ShredHitObject note, ShredSkinSettings skin, ShredSkinAssets assets = null)
        {
            this.note = note;
            this.skin = skin;
            RelativeSizeAxes = Axes.Both;
            Anchor = Origin = Anchor.Centre;
            Color4 colour = note.IsWham ? ShredWhamGeometry.Colour
                : note.Fret == 0 ? Color4.White : ShredPlayfield.FretColour(note.Fret);
            float shear = skin.Shear(note.Row);
            // Put the angled grip behind the bar and tuck its beginning inside
            // the bar so the two pieces read as a continuous silhouette.
            Add(hook = new Box { Anchor = Anchor.Centre, Origin = Anchor.CentreLeft, Colour = colour, Rotation = 20 });
            Add(body = new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Colour = colour, Alpha = 0.4f, Shear = new Vector2(shear, 0) });
            Add(joint = new Circle { Anchor = Anchor.Centre, Origin = Anchor.Centre, Colour = colour });
            var frames = assets?.GetFrames("shred-hopo-halo");
            halo = frames != null && frames.Length > 0
                ? new ShredAnimatedSprite(frames, skin.AnimationFPS) { Colour = skin.HaloColour, Blending = BlendingParameters.Additive }
                : new ShredGlow(skin.HaloColour, skin.HaloRadius, shear);
            halo.Anchor = halo.Origin = Anchor.Centre;
            halo.RelativeSizeAxes = Axes.Both;
            Add(halo);
            Add(haloOutline = new ShredGlow(skin.HaloColour, skin.HaloRadius, shear, hollow: true)
            { Anchor = Anchor.Centre, Origin = Anchor.Centre, RelativeSizeAxes = Axes.Both });
            Add(head = new ShredGlyph(Color4.White, shear: shear, solid: true) { Anchor = Anchor.Centre, Origin = Anchor.Centre, RelativeSizeAxes = Axes.Both });
        }

        public static Color4 SustainColour(Color4 colour)
            => new Color4(colour.R * 0.4f, colour.G * 0.4f, colour.B * 0.4f, colour.A);

        public static Vector2 SustainEndAtUpstreamEdge(Vector2 centre, Vector2 direction, float height)
        {
            // Follow the lane's shear to the incoming edge of the receptor.
            // This is a visual offset only; EndTime remains the release time.
            if (Math.Abs(direction.Y) < 0.0001f) return centre;
            return centre - direction * (height / (2 * Math.Abs(direction.Y)));
        }

        public void Layout(Vector2 tailOffset, float unit, bool holding = false, bool failed = false, bool selected = false, bool editor = false)
        {
            Color4 colour = note.IsWham ? ShredWhamGeometry.Colour
                : note.Fret == 0 ? Color4.White : ShredPlayfield.FretColour(note.Fret);
            if (selected)
                colour = new Color4(colour.R + (1 - colour.R) * 0.45f,
                    colour.G + (1 - colour.G) * 0.45f, colour.B + (1 - colour.B) * 0.45f, colour.A);
            head.Colour = colour;
            body.Colour = SustainColour(colour);
            if (note.IsWham) body.Colour = colour;
            hook.Colour = joint.Colour = colour;
            Vector2 shear = new Vector2(skin.Shear(note.Row), 0);
            head.Shear = body.Shear = halo.Shear = haloOutline.Shear = shear;
            if (note.IsWham) body.Shear = new Vector2(skin.Shear(0), 0);
            halo.Alpha = 0;
            if (!holding && !note.IsWham && note.Articulation != Gameplay.ShredPracticeArticulation.Pick)
                halo.Alpha = skin.HaloOpacity;
            // Restore the warmer HP-specific skin colour, with only a small
            // outline boost over the original halo instead of two full glows.
            haloOutline.Alpha = halo.Alpha * 0.15f;
            head.Alpha = editor && holding ? 0 : 1;
            if (note.IsWham) head.Alpha = 0;
            body.Alpha = note.IsLong ? 1 : 0;
            if (note.IsWham) body.Alpha = ShredWhamGeometry.Opacity;
            joint.Alpha = note.IsWham ? 1 : 0;
            hook.Alpha = 0;
            if (note.IsWham) hook.Alpha = ShredWhamGeometry.Opacity;
            if (note.IsLong && !note.IsWham)
                tailOffset = SustainEndAtUpstreamEdge(tailOffset, skin.Direction(note.Row), DrawHeight);
            body.Position = tailOffset / 2;
            body.Size = note.IsWham
                ? new Vector2(Math.Abs(tailOffset.X), DrawHeight)
                : new Vector2(DrawWidth, Math.Abs(tailOffset.Y));

            hook.Position = tailOffset - new Vector2(ShredWhamGeometry.HookInset * unit, 0);
            hook.Size = new Vector2(24 * unit, DrawHeight);
            joint.Position = hook.Position;
            joint.Size = new Vector2(DrawHeight);
            if (!failed) failureStarted = null;
            else failureStarted ??= Time.Current;
            float brightness = failed ? 1 - (1 - 95f / 255) * (float)Math.Clamp((Time.Current - failureStarted.Value) / 160, 0, 1) : 1;
            Colour = new Color4(brightness, brightness, brightness, 1);
            // White OPEN notes cannot be lightened further. Fade only their
            // visual, leaving the hit object's lifetime and input unchanged.
            Alpha = 1;
            if (selected && !note.IsWham && note.Fret == 0) Alpha = 0.6f;
        }
    }
}
