using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SolidWorks_ASsembly_Instructor
{
    // Legacy callers and export models retain their interface. CAD creation uses
    // PreciseConstraintEngine directly, without these boundary conversions.
    public sealed class ConstraintEngine
    {
        private readonly PreciseConstraintEngine engine = new PreciseConstraintEngine();
        public CoordinateSystemDescription Centroid(IReadOnlyList<CoordinateSystemDescription> references, Vector3 localOffset) =>
            engine.Centroid(references?.Select(PrecisePose.FromLegacy).ToList(), localOffset).ToLegacy();
        public CoordinateSystemDescription Orthogonal(CoordinateSystemDescription a, CoordinateSystemDescription b,
            CoordinateSystemDescription c, float distance, bool percent, float offset, string orthogonal, string normal) =>
            engine.Orthogonal(PrecisePose.FromLegacy(a), PrecisePose.FromLegacy(b), PrecisePose.FromLegacy(c),
                distance, percent, offset, orthogonal, normal).ToLegacy();
        public CoordinateSystemDescription Orthogonal(CoordinateSystemDescription a, CoordinateSystemDescription b,
            CoordinateSystemDescription c, float distance, bool percent, float offset, string orthogonal, string normal,
            CoordinateSystemDescription origin) =>
            engine.Orthogonal(PrecisePose.FromLegacy(a), PrecisePose.FromLegacy(b), PrecisePose.FromLegacy(c),
                distance, percent, offset, orthogonal, normal, PrecisePose.FromLegacy(origin)).ToLegacy();
        public CoordinateSystemDescription Transform(CoordinateSystemDescription reference, CoordinateSystemDescription local) =>
            engine.Transform(PrecisePose.FromLegacy(reference), PrecisePose.FromLegacy(local)).ToLegacy();
        public InPlaneValidationResult ValidateInPlane(CoordinateSystemDescription candidate,
            IReadOnlyList<CoordinateSystemDescription> references, float offset, float tolerance)
        {
            var result = engine.ValidateInPlane(PrecisePose.FromLegacy(candidate),
                references?.Select(PrecisePose.FromLegacy).ToList(), offset, tolerance);
            return new InPlaneValidationResult((float)result.SignedDistance, result.IsInPlane);
        }
    }
    public sealed class InPlaneValidationResult
    {
        public float SignedDistance { get; }
        public bool IsInPlane { get; }
        public InPlaneValidationResult(float signedDistance, bool isInPlane)
        { SignedDistance = signedDistance; IsInPlane = isInPlane; }
    }
}
