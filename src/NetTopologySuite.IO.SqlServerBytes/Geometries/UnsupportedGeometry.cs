using System;
using System.Collections.Generic;
using NetTopologySuite.IO.Properties;
using NetTopologySuite.IO.Serialization;

namespace NetTopologySuite.Geometries
{
    internal class UnsupportedGeometry : Geometry
    {
        private readonly OpenGisType _shapeType;

        public UnsupportedGeometry(OpenGisType shapeType, Geography geography)
            : base(GeometryFactory.Default)
        {
            _shapeType = shapeType;
            Geography = geography;
        }

        public Geography Geography { get; }

        private NotSupportedException Unsupported()
            => new NotSupportedException(string.Format(Resources.UnexpectedGeographyType, _shapeType));

        #region Unsupported overrides

        public override double Area => throw Unsupported();
        public override Geometry Boundary => throw Unsupported();
        public override Dimension BoundaryDimension => throw Unsupported();
        public override Point Centroid => throw Unsupported();
        public override Coordinate Coordinate => throw Unsupported();
        public override Coordinate[] Coordinates => throw Unsupported();
        public override Dimension Dimension => throw Unsupported();
        public override string GeometryType => throw Unsupported();
        public override Point InteriorPoint => throw Unsupported();
        public override bool IsEmpty => throw Unsupported();
        public override bool IsRectangle => throw Unsupported();
        public override bool IsSimple => throw Unsupported();
        public override bool IsValid => throw Unsupported();
        public override double Length => throw Unsupported();
        public override int NumGeometries => throw Unsupported();
        public override int NumPoints => throw Unsupported();
        public override OgcGeometryType OgcGeometryType => throw Unsupported();

        protected override SortIndexValue SortIndex => throw Unsupported();

        public override void Apply(ICoordinateFilter filter) => throw Unsupported();
        public override void Apply(ICoordinateSequenceFilter filter) => throw Unsupported();
        public override void Apply(IGeometryComponentFilter filter) => throw Unsupported();
        public override void Apply(IGeometryFilter filter) => throw Unsupported();
        public override bool Contains(Geometry g) => throw Unsupported();
        public override Geometry ConvexHull() => throw Unsupported();
        public override bool Covers(Geometry g) => throw Unsupported();
        public override bool Crosses(Geometry g) => throw Unsupported();
        public override double Distance(Geometry g) => throw Unsupported();
        public override bool Equals(object o) => throw Unsupported();
        public override bool EqualsExact(Geometry other, double tolerance) => throw Unsupported();
        public override bool EqualsTopologically(Geometry g) => throw Unsupported();
        public override Geometry GetGeometryN(int n) => throw Unsupported();
        public override int GetHashCode() => throw Unsupported();
        public override double[] GetOrdinates(Ordinate ordinate) => throw Unsupported();
        public override bool Intersects(Geometry g) => throw Unsupported();
        public override bool IsWithinDistance(Geometry geom, double distance) => throw Unsupported();
        public override void Normalize() => throw Unsupported();
        public override bool Overlaps(Geometry g) => throw Unsupported();
        public override IntersectionMatrix Relate(Geometry g) => throw Unsupported();
        public override bool Relate(Geometry g, string intersectionPattern) => throw Unsupported();
        public override Geometry Reverse() => throw Unsupported();
        public override string ToString() => throw Unsupported();
        public override bool Touches(Geometry g) => throw Unsupported();

        protected override int CompareToSameClass(object o) => throw Unsupported();
        protected override int CompareToSameClass(object o, IComparer<CoordinateSequence> comp) => throw Unsupported();
        protected override Envelope ComputeEnvelopeInternal() => throw Unsupported();
        protected override Geometry CopyInternal() => throw Unsupported();
        protected override bool IsEquivalentClass(Geometry other) => throw Unsupported();

        #endregion
    }
}
