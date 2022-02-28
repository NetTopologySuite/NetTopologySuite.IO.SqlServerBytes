using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Properties;

using Figure = NetTopologySuite.IO.Serialization.Figure;
using FigureAttribute = NetTopologySuite.IO.Serialization.FigureAttribute;
using Geography = NetTopologySuite.IO.Serialization.Geography;
using OpenGisType = NetTopologySuite.IO.Serialization.OpenGisType;
using SqlPoint = NetTopologySuite.IO.Serialization.Point;
using SqlShape = NetTopologySuite.IO.Serialization.Shape;

namespace NetTopologySuite.IO
{
    /// <summary>
    ///     Writes <see cref="Geometry"/> instances into geography or geometry data in the SQL Server serialization
    ///     format (described in MS-SSCLRT).
    /// </summary>
    public class SqlServerBytesWriter
    {
        private bool _emitZ = true;
        private bool _emitM = true;
        private Func<Geometry, bool> _customGgeometryFactory = default;

        /// <summary>
        ///     Gets or sets the desired <see cref="IO.ByteOrder"/>. Returns <see cref="IO.ByteOrder.LittleEndian"/> since
        ///     it's required. Setting does nothing.
        /// </summary>
        [Obsolete("This is unused within this library and will be removed in a later version.  It was only needed when this type implemented an interface that no longer exists.")]
        public virtual ByteOrder ByteOrder
        {
            get => ByteOrder.LittleEndian;
            set { }
        }

        /// <summary>
        ///     Gets or sets whether the SpatialReference ID must be handled. Returns true since it's required. Setting
        ///     does nothing.
        /// </summary>
        [Obsolete("This is unused within this library and will be removed in a later version.  It was only needed when this type implemented an interface that no longer exists.")]
        public virtual bool HandleSRID
        {
            get => true;
            set { }
        }

        /// <summary>
        ///     Gets and <see cref="Ordinates"/> flag that indicate which ordinates can be handled.
        /// </summary>
        [Obsolete("This is unused within this library and will be removed in a later version.  It was only needed when this type implemented an interface that no longer exists.")]
        public virtual Ordinates AllowedOrdinates
            => Ordinates.XYZM;

        /// <summary>
        ///     Gets and sets <see cref="Ordinates"/> flag that indicate which ordinates shall be handled.
        /// </summary>
        /// <remarks>
        ///     No matter which <see cref="Ordinates"/> flag you supply, <see cref="Ordinates.XY"/> are always
        ///     processed, the rest is binary and 'ed with <see cref="Ordinates.XYZM"/>.
        /// </remarks>
        public virtual Ordinates HandleOrdinates
        {
            get
            {
                var value = Ordinates.XY;
                if (_emitZ)
                {
                    value |= Ordinates.Z;
                }
                if (_emitM)
                {
                    value |= Ordinates.M;
                }

                return value;
            }
            set
            {
                _emitZ = value.HasFlag(Ordinates.Z);
                _emitM = value.HasFlag(Ordinates.M);
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether to write geography data. If not, geometry data will be written.
        /// </summary>
        public virtual bool IsGeography { get; set; }

        /// <summary>
        /// Gets or sets a validator for a geometry. The result of this validator will be used to set the Valid flag of the Geography in SQL Server
        /// </summary>
        public virtual Func<Geometry, bool> GeometryValidator
        {
            get => _customGgeometryFactory ?? new Func<Geometry, bool>(geomerty => geomerty.IsValid);
            set => _customGgeometryFactory = value;
        }

        /// <summary>
        ///     Writes a binary representation of a given geometry.
        /// </summary>
        /// <param name="geometry"> The geometry </param>
        /// <returns> The binary representation of geometry </returns>
        public virtual byte[] Write(Geometry geometry)
        {
            using (var stream = new MemoryStream())
            {
                Write(geometry, stream);

                return stream.ToArray();
            }
        }

        /// <summary>
        ///     Writes a binary representation of a given geometry.
        /// </summary>
        /// <param name="geometry"> The geometry </param>
        /// <param name="stream"> The stream to write to. </param>
        public virtual void Write(Geometry geometry, Stream stream)
        {
            var geography = ToGeography(geometry);

            using (var writer = new BinaryWriter(stream))
            {
                geography.WriteTo(writer);
            }
        }

        private Geography ToGeography(Geometry geometry)
        {
            if (geometry == null)
            {
                return new Geography { SRID = -1 };
            }

            // Check if geometry has z- or m-ordinate values
            var checkZM = new CheckZMFilter();
            geometry.Apply(checkZM);
            bool emitZ = _emitZ & checkZM.HasZ;
            bool emitM = _emitM & checkZM.HasM;

            var geometries = new Queue<(Geometry, int)>();
            geometries.Enqueue((geometry, -1));

            // TODO: For geography (ellipsoidal) data, set IsLargerThanAHemisphere
            var geography = new Geography
            {
                SRID = Math.Max(0, geometry.SRID),
                IsValid = GeometryValidator(geometry)
            };

            while (geometries.Count > 0)
            {
                var (currentGeometry, parentOffset) = geometries.Dequeue();

                int figureOffset = geography.Figures.Count;
                bool figureAdded = false;
                switch (currentGeometry)
                {
                    case Point point:
                    case LineString lineString:
                        figureAdded = addFigure(currentGeometry, FigureAttribute.PointOrLine);
                        break;

                    case Polygon polygon:
                        if (IsGeography
                            && !polygon.IsEmpty
                            && !polygon.Shell.IsCCW)
                        {
                            throw new ArgumentException(Resources.InvalidGeographyShellOrientation);
                        }

                        figureAdded = addFigure(polygon.Shell, FigureAttribute.PointOrLine);

                        foreach (var hole in polygon.Holes)
                        {
                            if (IsGeography
                                && hole.IsCCW)
                            {
                                throw new ArgumentException(Resources.InvalidGeographyHoleOrientation);
                            }

                            figureAdded |= addFigure(hole, FigureAttribute.PointOrLine);
                        }
                        break;

                    case GeometryCollection geometryCollection:
                        foreach (var item in geometryCollection.Geometries)
                        {
                            geometries.Enqueue((item, geography.Shapes.Count));
                            figureAdded = true;
                        }
                        break;

                    default:
                        throw new InvalidOperationException(
                            string.Format(Resources.UnexpectedGeometryType, geometry.GetType()));
                }

                geography.Shapes.Add(
                    new SqlShape
                    {
                        ParentOffset = parentOffset,
                        FigureOffset = figureAdded ? figureOffset : -1,
                        Type = ToOpenGisType(currentGeometry.OgcGeometryType)
                    });

                bool addFigure(Geometry g, FigureAttribute figureAttribute)
                {
                    CoordinateSequence sequence;
                    if (g is Point p) sequence = p.CoordinateSequence;
                    else if (g is LineString l) sequence = l.CoordinateSequence;
                    else throw new ArgumentException("Unexpected geometry type", nameof(g));

                    int pointOffset = geography.Points.Count;
                    bool pointsAdded = false;

                    for (int i = 0; i < sequence.Count; i++)
                    {
                        geography.Points.Add(
                            IsGeography
                                ? new SqlPoint { Long = sequence.GetX(i), Lat = sequence.GetY(i) }
                                : new SqlPoint { X = sequence.GetX(i), Y = sequence.GetY(i) });
                        pointsAdded = true;

                        if (emitZ)
                        {
                            geography.ZValues.Add(sequence.GetZ(i));
                        }

                        if (emitM)
                        {
                            geography.MValues.Add(sequence.GetM(i));
                        }
                    }

                    if (!pointsAdded)
                    {
                        return false;
                    }

                    geography.Figures.Add(
                        new Figure
                        {
                            FigureAttribute = figureAttribute,
                            PointOffset = pointOffset
                        });

                    return true;
                }
            }

            return geography;
        }

        private OpenGisType ToOpenGisType(OgcGeometryType type)
        {
            if (type < OgcGeometryType.Point
                || type > OgcGeometryType.CurvePolygon)
            {
                throw new InvalidOperationException(string.Format(Resources.UnexpectedOgcGeometryType, type));
            }

            return (OpenGisType)type;
        }

        /// <summary>
        /// Filter class to evaluate if a geometry has z- and m-ordinate values.
        /// </summary>
        /// <remarks>Used <c>IGeometryComponentFilter</c> because <c>IEntireCoordinateSequence</c> is not available in NTS v2.0</remarks>
        private class CheckZMFilter : IGeometryComponentFilter
        {
            /// <summary>
            /// Geometry has z-ordinate values
            /// </summary>
            public bool HasZ { get; private set; }

            /// <summary>
            /// Geometry has m-ordinate values
            /// </summary>
            public bool HasM { get; private set; }

            void IGeometryComponentFilter.Filter(Geometry geom)
            {
                CoordinateSequence seq = null;
                switch (geom)
                {
                    case Point p:
                        seq = p.CoordinateSequence;
                        break;
                    case LineString ls:
                        seq = ls.CoordinateSequence;
                        break;
                }

                // If we don't have a sequence we don't have anything to evaluate
                if (seq == null) return;

                // Update properties
                if (seq.HasZ) HasZ = true;
                if (seq.HasM) HasM = true;
            }
        }
    }
}
