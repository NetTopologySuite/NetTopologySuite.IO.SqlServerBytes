using System;
using System.Collections.Generic;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Mathematics;

namespace NetTopologySuite.IO.Utility
{
    /// <summary>
    /// A geography envelope is made up of a <see cref="Center"/> and an <see cref="Angle"/>
    /// </summary>
    internal struct GeographyEnvelope
    {
        /// <summary>
        /// Computes the envelope of a geography
        /// </summary>
        /// <param name="geometry">The geometry</param>
        /// <returns>The envelope of a geography</returns>
        public static GeographyEnvelope Compute(Geometry geometry)
        {
            if (geometry.IsEmpty)
                return new GeographyEnvelope();

            var flt = new GeographyEnvelopeFilter(GeocentricTransform.CreateFor(geometry.SRID));
            geometry.Apply(flt);
            flt.Compute(out var coordinate, out double angle);

            return new GeographyEnvelope { Angle = angle, Center = coordinate};
        }

        /// <summary>
        /// Gets the opening angle between <see cref="Center"/> and a vector to the
        /// envelope circle.
        /// </summary>
        public double Angle { get; private set; }

        /// <summary>
        /// Gets a value indicating the center of the envelope
        /// </summary>
        public Coordinate Center { get; private set; }

        private class GeographyEnvelopeFilter : IEntireCoordinateSequenceFilter
        {
            private readonly GeocentricTransform _transform;
            private readonly List<Vector3D> _vectors = new List<Vector3D>();

            /// <summary>
            /// Creates an instance of this class
            /// </summary>
            /// <param name="transform"></param>
            public GeographyEnvelopeFilter(GeocentricTransform transform)
            {
                _transform = transform;
            }

            void IEntireCoordinateSequenceFilter.Filter(CoordinateSequence seq)
            {
                int count = seq.Count;
                if (CoordinateSequences.IsRing(seq))
                    count--;

                for (int i = 0; i < count; i++)
                {
                    var xyz = _transform.GeodeticToGeocentric(seq.GetX(i), seq.GetY(i), 0d);
                    _vectors.Add(new Vector3D(xyz.x, xyz.y, xyz.z));
                }
            }

            bool IEntireCoordinateSequenceFilter.Done => false;

            bool IEntireCoordinateSequenceFilter.GeometryChanged => false;

            public void Compute(out CoordinateZ center, out double angle)
            {
                // Build average center vector
                var vCenterX = new DD(_vectors[0].X);
                var vCenterY = new DD(_vectors[0].Y);
                var vCenterZ = new DD(_vectors[0].Z);
                var divisor = new DD(_vectors.Count);
                for (int i = 1; i < _vectors.Count; i++)
                {
                    vCenterX += new DD(_vectors[i].X);
                    vCenterY += new DD(_vectors[i].Y);
                    vCenterZ += new DD(_vectors[i].Z);
                }
                var vCenter = new Vector3D(
                    (vCenterX / divisor).ToDoubleValue(),
                    (vCenterY / divisor).ToDoubleValue(),
                    (vCenterZ / divisor).ToDoubleValue());

                // look for max angle between center and vectors
                angle = -double.MaxValue;
                double vCenterLength = vCenter.Length();
                for (int i = 0; i < _vectors.Count; i++)
                {
                    double dot = vCenter.Dot(_vectors[i]);
                    double testAngle;
                    if (dot == 0d)
                    {
                        testAngle = 90d;
                    }
                    else
                    {
                        double cosPhi = dot / (vCenterLength * _vectors[i].Length());
                        testAngle = AngleUtility.ToDegrees(Math.Acos(cosPhi));
                        // Is angle obtuse
                        if (dot < 0)
                            testAngle += 180d;
                    }

                    // Update largest angle
                    if (testAngle > angle)
                        angle = testAngle;
                }

                // Transform to geodetic
                var centerLL = _transform.GeocentricToGeodetic(vCenter.X, vCenter.Y, vCenter.Z);
                center = new CoordinateZ(centerLL.lon, centerLL.lat, centerLL.height);
            }

        }
    }
}
