using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Properties;

using FigureAttribute = NetTopologySuite.IO.Serialization.FigureAttribute;
using Geography = NetTopologySuite.IO.Serialization.Geography;
using OpenGisType = NetTopologySuite.IO.Serialization.OpenGisType;
using SegmentType = NetTopologySuite.IO.Serialization.SegmentType;

namespace NetTopologySuite.IO
{
    /// <summary>
    ///     Reads geography or geometry data in the SQL Server serialization format (described in MS-SSCLRT) into
    ///     <see cref="Geometry"/> instances.
    /// </summary>
    public class SqlServerBytesReader
    {
        private readonly NtsGeometryServices _services;
        private readonly CoordinateSequenceFactory _sequenceFactory;
        private Ordinates _handleOrdinates;
        private double _maxLinearizationAngle = Math.PI / 32;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SqlServerBytesReader"/> class.
        /// </summary>
        /// <remarks>
        ///     The current <see cref="NtsGeometryServices.Instance"/> will be used.
        /// </remarks>
        public SqlServerBytesReader()
            : this(NtsGeometryServices.Instance)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SqlServerBytesReader"/> class using the provided <see cref="NtsGeometryServices"/> instance.
        /// </summary>
        /// <param name="services"> The geometry services used to create <see cref="GeometryFactory"/> instances.</param>
        public SqlServerBytesReader(NtsGeometryServices services)
        {
            _services = services ?? NtsGeometryServices.Instance;
            _sequenceFactory = _services.DefaultCoordinateSequenceFactory;
            _handleOrdinates = AllowedOrdinates;
        }

        /// <summary>
        ///     Gets or sets whether invalid linear rings should be fixed. Returns false since invalid rings are
        ///     disallowed. Setting does nothing.
        /// </summary>
        [Obsolete("This is unused within this library and will be removed in a later version.  It was only needed when this type implemented an interface that no longer exists.")]
        public virtual bool RepairRings
        {
            get => false;
            set { }
        }

        /// <summary>
        ///     Gets or sets whether the SpatialReference ID must be handled. Returns true since it's always handled.
        ///     Setting does nothing.
        /// </summary>
        [Obsolete("This is unused within this library and will be removed in a later version.  It was only needed when this type implemented an interface that no longer exists.")]
        public virtual bool HandleSRID
        {
            get => true;
            set { }
        }

        /// <summary>
        ///     Gets an <see cref="Ordinates"/> flag that indicate which ordinates can be handled.
        /// </summary>
        public virtual Ordinates AllowedOrdinates
            => Ordinates.XYZM & _sequenceFactory.Ordinates;

        /// <summary>
        ///     Gets and sets <see cref="Ordinates"/> flag that indicate which ordinates shall be handled.
        /// </summary>
        /// <remarks>
        ///     No matter which <see cref="Ordinates"/> flag you supply, <see cref="Ordinates.XY"/> are always
        ///     processed, the rest is binary and 'ed with <see cref="AllowedOrdinates"/>.
        /// </remarks>
        public virtual Ordinates HandleOrdinates
        {
            get => _handleOrdinates;
            set
            {
                value = Ordinates.XY | (AllowedOrdinates & value);
                _handleOrdinates = value;
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether to read geography data. If not, geometry data will be read.
        /// </summary>
        public virtual bool IsGeography { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether geometries containing circular arc segments (CircularString,
        ///     CompoundCurve and CurvePolygon) should be approximated using linear geometries (LineString and Polygon)
        ///     while reading. If not (the default), reading such a geometry throws a <see cref="ParseException"/>.
        /// </summary>
        /// <remarks>
        ///     Arcs of geography data are approximated in longitude/latitude space. This differs slightly from SQL
        ///     Server's STCurveToLine method which linearizes geography arcs on the ellipsoid.
        /// </remarks>
        public virtual bool LinearizeCurves { get; set; }

        /// <summary>
        ///     Gets or sets the maximum angle, in radians, subtended by a single line segment when approximating a
        ///     circular arc. Smaller values produce more accurate approximations containing more points. The default
        ///     value (pi/32) matches the granularity used by SQL Server's STCurveToLine method. The original points of
        ///     an arc are always preserved.
        /// </summary>
        public virtual double MaxLinearizationAngle
        {
            get => _maxLinearizationAngle;
            set
            {
                if (double.IsNaN(value) || value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }

                _maxLinearizationAngle = value;
            }
        }

        /// <summary>
        ///     Reads a geometry representation from a <see cref="T:byte[]"/> to a Geometry.
        /// </summary>
        /// <param name="source"> The source to read the geometry from </param>
        /// <returns> A Geometry </returns>
        public virtual Geometry Read(byte[] source)
        {
            using (var stream = new MemoryStream(source))
            {
                return Read(stream);
            }
        }

        /// <summary>
        ///     Reads a geometry representation from a <see cref="Stream"/> to a Geometry.
        /// </summary>
        /// <param name="stream"> The stream to read from. </param>
        /// <returns> A geometry. </returns>
        public virtual Geometry Read(Stream stream)
        {
            Geography geography;
            using (var reader = new BinaryReader(stream))
            {
                geography = Geography.ReadFrom(reader);
            }

            return ToGeometry(geography);
        }

        private Geometry ToGeometry(Geography geography)
        {
            if (geography.SRID == -1)
            {
                return null;
            }

            bool handleZ = _handleOrdinates.HasFlag(Ordinates.Z) && geography.ZValues.Count > 0;
            bool handleM = _handleOrdinates.HasFlag(Ordinates.M) && geography.MValues.Count > 0;

            var factory = _services.CreateGeometryFactory(geography.SRID);
            var geometries = new Dictionary<int, Stack<Geometry>>();
            var figureSegments = LinearizeCurves ? GetFigureSegments(geography) : null;
            int lastFigureIndex = geography.Figures.Count - 1;
            int lastPointIndex = geography.Points.Count - 1;

            for (int shapeIndex = geography.Shapes.Count - 1; shapeIndex >= 0; shapeIndex--)
            {
                var shape = geography.Shapes[shapeIndex];
                var figures = new Stack<CoordinateSequence>();

                if (shape.FigureOffset != -1)
                {
                    for (int figureIndex = lastFigureIndex; figureIndex >= shape.FigureOffset; figureIndex--)
                    {
                        var figure = geography.Figures[figureIndex];
                        int pointCount = figure.PointOffset != -1
                            ? lastPointIndex + 1 - figure.PointOffset
                            : 0;
                        var coordinates = _sequenceFactory.Create(pointCount, _handleOrdinates);

                        if (pointCount != 0)
                        {
                            for (int pointIndex = figure.PointOffset; pointIndex <= lastPointIndex; pointIndex++)
                            {
                                var point = geography.Points[pointIndex];
                                int coordinateIndex = pointIndex - figure.PointOffset;

                                coordinates.SetX(coordinateIndex, IsGeography ? point.Long : point.X);
                                coordinates.SetY(coordinateIndex, IsGeography ? point.Lat : point.Y);

                                if (handleZ)
                                {
                                    coordinates.SetZ(coordinateIndex, geography.ZValues[pointIndex]);
                                }

                                if (handleM)
                                {
                                    coordinates.SetM(coordinateIndex, geography.MValues[pointIndex]);
                                }
                            }

                            lastPointIndex = figure.PointOffset - 1;
                        }

                        if (LinearizeCurves
                            && (figure.FigureAttribute == FigureAttribute.Arc
                                || figure.FigureAttribute == FigureAttribute.Curve))
                        {
                            coordinates = Linearize(
                                coordinates,
                                figure.FigureAttribute,
                                figureSegments?[figureIndex],
                                handleZ,
                                handleM);
                        }

                        figures.Push(coordinates);
                    }

                    lastFigureIndex = shape.FigureOffset - 1;
                }

                Geometry geometry;
                switch (shape.Type)
                {
                    case OpenGisType.Point:
                        geometry = factory.CreatePoint(figures.SingleOrDefault());
                        Debug.Assert(!geometries.ContainsKey(shapeIndex));
                        break;

                    case OpenGisType.LineString:
                        geometry = factory.CreateLineString(figures.SingleOrDefault());
                        Debug.Assert(!geometries.ContainsKey(shapeIndex));
                        break;

                    case OpenGisType.Polygon:
                        geometry = CreatePolygon();
                        Debug.Assert(!geometries.ContainsKey(shapeIndex));
                        break;

                    case OpenGisType.MultiPoint:
                        geometry = factory.CreateMultiPoint(
                            geometries.TryGetValue(shapeIndex, out var points)
                                ? points.Cast<Point>().ToArray()
                                : null);
                        geometries.Remove(shapeIndex);
                        break;

                    case OpenGisType.MultiLineString:
                        geometry = factory.CreateMultiLineString(
                            geometries.TryGetValue(shapeIndex, out var lineStrings)
                                ? lineStrings.Cast<LineString>().ToArray()
                                : null);
                        geometries.Remove(shapeIndex);
                        break;

                    case OpenGisType.MultiPolygon:
                        geometry = factory.CreateMultiPolygon(
                                geometries.TryGetValue(shapeIndex, out var polygons)
                                    ? polygons.Cast<Polygon>().ToArray()
                                    : null);
                        geometries.Remove(shapeIndex);
                        break;

                    case OpenGisType.GeometryCollection:
                        geometry = factory.CreateGeometryCollection(
                                geometries.TryGetValue(shapeIndex, out var children)
                                    ? children.ToArray()
                                    : null);
                        geometries.Remove(shapeIndex);
                        break;

                    case OpenGisType.CircularString:
                    case OpenGisType.CompoundCurve:
                        if (!LinearizeCurves)
                        {
                            throw new ParseException(string.Format(Resources.UnsupportedCurvedGeographyType, shape.Type));
                        }

                        geometry = factory.CreateLineString(figures.SingleOrDefault());
                        Debug.Assert(!geometries.ContainsKey(shapeIndex));
                        break;

                    case OpenGisType.CurvePolygon:
                        if (!LinearizeCurves)
                        {
                            throw new ParseException(string.Format(Resources.UnsupportedCurvedGeographyType, shape.Type));
                        }

                        geometry = CreatePolygon();
                        Debug.Assert(!geometries.ContainsKey(shapeIndex));
                        break;

                    default:
                        throw new ParseException(string.Format(Resources.UnexpectedGeographyType, shape.Type));
                }

                if (!geometries.ContainsKey(shape.ParentOffset))
                {
                    geometries.Add(shape.ParentOffset, new Stack<Geometry>());
                }

                geometries[shape.ParentOffset].Push(geometry);

                Polygon CreatePolygon()
                {
                    var rings = figures.Select(f => factory.CreateLinearRing(f)).ToList();
                    var shell = IsGeography
                        ? rings.FirstOrDefault(r => r.IsCCW)
                        : rings.FirstOrDefault();
                    return factory.CreatePolygon(
                        shell,
                        Enumerable.ToArray(
                            IsGeography
                            ? rings.Where(r => r != shell)
                            : rings.Skip(1)));
                }
            }

            Debug.Assert(geometries.Keys.Count == 1);

            return geometries[-1].Single();
        }

        /// <summary>
        ///     Determines which of the geography's segment types belong to each curve figure. Segment types are
        ///     serialized for all curve figures together, in figure order; each line segment type corresponds to one
        ///     additional point of its figure, each arc segment type to two.
        /// </summary>
        /// <returns> Segment types by figure index (null for non-curve figures), or null if there are no segments. </returns>
        private static List<SegmentType[]> GetFigureSegments(Geography geography)
        {
            if (geography.Segments.Count == 0)
            {
                return null;
            }

            var result = new List<SegmentType[]>(geography.Figures.Count);
            int segmentIndex = 0;
            for (int figureIndex = 0; figureIndex < geography.Figures.Count; figureIndex++)
            {
                var figure = geography.Figures[figureIndex];
                if (figure.FigureAttribute != FigureAttribute.Curve
                    || figure.PointOffset == -1)
                {
                    result.Add(null);
                    continue;
                }

                int nextPointOffset = geography.Points.Count;
                for (int i = figureIndex + 1; i < geography.Figures.Count; i++)
                {
                    if (geography.Figures[i].PointOffset != -1)
                    {
                        nextPointOffset = geography.Figures[i].PointOffset;
                        break;
                    }
                }

                var segments = new List<SegmentType>();
                int pointCount = nextPointOffset - figure.PointOffset;
                int pointsConsumed = 1;
                while (pointsConsumed < pointCount)
                {
                    if (segmentIndex == geography.Segments.Count)
                    {
                        throw new ParseException(Resources.MalformedCurveData);
                    }

                    var segmentType = geography.Segments[segmentIndex++].Type;
                    segments.Add(segmentType);
                    pointsConsumed += segmentType == SegmentType.Line || segmentType == SegmentType.FirstLine
                        ? 1
                        : 2;
                }

                if (pointsConsumed != pointCount)
                {
                    throw new ParseException(Resources.MalformedCurveData);
                }

                result.Add(segments.ToArray());
            }

            return result;
        }

        /// <summary>
        ///     Approximates a curve figure using line segments. The original points of the figure are always part of
        ///     the result; additional points are inserted along each arc as dictated by
        ///     <see cref="MaxLinearizationAngle"/>. Z and M values of inserted points are interpolated linearly.
        /// </summary>
        private CoordinateSequence Linearize(
            CoordinateSequence coordinates,
            FigureAttribute figureAttribute,
            SegmentType[] segmentTypes,
            bool handleZ,
            bool handleM)
        {
            if (coordinates.Count == 0)
            {
                return coordinates;
            }

            var points = new List<double[]> { GetPoint(coordinates, 0) };

            if (figureAttribute == FigureAttribute.Arc)
            {
                if (coordinates.Count < 3 || coordinates.Count % 2 != 1)
                {
                    throw new ParseException(Resources.MalformedCurveData);
                }

                for (int i = 0; i + 2 < coordinates.Count; i += 2)
                {
                    AddArc(points, coordinates, i, i + 1, i + 2);
                }
            }
            else
            {
                if (segmentTypes == null)
                {
                    throw new ParseException(Resources.MalformedCurveData);
                }

                int pointIndex = 0;
                foreach (var segmentType in segmentTypes)
                {
                    if (segmentType == SegmentType.Line || segmentType == SegmentType.FirstLine)
                    {
                        points.Add(GetPoint(coordinates, pointIndex + 1));
                        pointIndex += 1;
                    }
                    else
                    {
                        AddArc(points, coordinates, pointIndex, pointIndex + 1, pointIndex + 2);
                        pointIndex += 2;
                    }
                }
            }

            var result = _sequenceFactory.Create(points.Count, _handleOrdinates);
            for (int i = 0; i < points.Count; i++)
            {
                double[] point = points[i];
                result.SetX(i, point[0]);
                result.SetY(i, point[1]);

                if (handleZ)
                {
                    result.SetZ(i, point[2]);
                }

                if (handleM)
                {
                    result.SetM(i, point[3]);
                }
            }

            return result;
        }

        /// <summary>
        ///     Appends the approximation of a single circular arc (start, mid, end) to points, excluding the start
        ///     point. Degenerate (collinear) arcs are appended as straight line segments.
        /// </summary>
        private void AddArc(List<double[]> points, CoordinateSequence coordinates, int startIndex, int midIndex, int endIndex)
        {
            double[] start = GetPoint(coordinates, startIndex);
            double[] mid = GetPoint(coordinates, midIndex);
            double[] end = GetPoint(coordinates, endIndex);

            // The determinant is twice the signed area of the triangle (start, mid, end). If it's zero, the points
            // are collinear and no circle passes through all three
            double d = 2 * (start[0] * (mid[1] - end[1]) + mid[0] * (end[1] - start[1]) + end[0] * (start[1] - mid[1]));
            if (d == 0)
            {
                points.Add(mid);
                points.Add(end);
                return;
            }

            double startSquared = start[0] * start[0] + start[1] * start[1];
            double midSquared = mid[0] * mid[0] + mid[1] * mid[1];
            double endSquared = end[0] * end[0] + end[1] * end[1];
            double centerX = (startSquared * (mid[1] - end[1]) + midSquared * (end[1] - start[1]) + endSquared * (start[1] - mid[1])) / d;
            double centerY = (startSquared * (end[0] - mid[0]) + midSquared * (start[0] - end[0]) + endSquared * (mid[0] - start[0])) / d;
            double radius = Math.Sqrt(
                (start[0] - centerX) * (start[0] - centerX) + (start[1] - centerY) * (start[1] - centerY));

            if (double.IsNaN(centerX) || double.IsInfinity(centerX)
                || double.IsNaN(centerY) || double.IsInfinity(centerY)
                || double.IsNaN(radius) || double.IsInfinity(radius))
            {
                points.Add(mid);
                points.Add(end);
                return;
            }

            // Approximating each half of the arc separately keeps the original points in the result. SQL Server's
            // STCurveToLine method does the same
            bool clockwise = d < 0;
            AddArcHalf(points, start, mid, centerX, centerY, radius, clockwise);
            AddArcHalf(points, mid, end, centerX, centerY, radius, clockwise);
        }

        private void AddArcHalf(List<double[]> points, double[] from, double[] to, double centerX, double centerY, double radius, bool clockwise)
        {
            double fromAngle = Math.Atan2(from[1] - centerY, from[0] - centerX);
            double toAngle = Math.Atan2(to[1] - centerY, to[0] - centerX);

            double sweep = clockwise ? fromAngle - toAngle : toAngle - fromAngle;
            if (sweep < 0)
            {
                sweep += 2 * Math.PI;
            }

            int segmentCount = (int)Math.Ceiling(sweep / _maxLinearizationAngle);
            if (segmentCount < 1)
            {
                segmentCount = 1;
            }

            for (int i = 1; i < segmentCount; i++)
            {
                double angle = fromAngle + (clockwise ? -sweep : sweep) * i / segmentCount;
                double fraction = (double)i / segmentCount;

                points.Add(
                    new[]
                    {
                        centerX + radius * Math.Cos(angle),
                        centerY + radius * Math.Sin(angle),
                        from[2] + (to[2] - from[2]) * fraction,
                        from[3] + (to[3] - from[3]) * fraction
                    });
            }

            points.Add(to);
        }

        private static double[] GetPoint(CoordinateSequence coordinates, int index)
            => new[] { coordinates.GetX(index), coordinates.GetY(index), coordinates.GetZ(index), coordinates.GetM(index) };
    }
}
