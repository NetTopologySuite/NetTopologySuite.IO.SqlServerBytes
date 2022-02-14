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

            var geometries = new Queue<(Geometry, int)>();
            geometries.Enqueue((geometry, -1));

            // TODO: For geography (ellipsoidal) data, set IsLargerThanAHemisphere
            var geography = new Geography
            {
                SRID = Math.Max(0, geometry.SRID),
                IsValid = geometry.IsValid
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

                    // TODO: Handle CircularString, CompoundCurve & CurvePolygon

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
                    int pointOffset = geography.Points.Count;
                    bool pointsAdded = false;

                    foreach (var coordinate in g.Coordinates)
                    {
                        geography.Points.Add(
                            IsGeography
                                ? new SqlPoint { Long = coordinate.X, Lat = coordinate.Y }
                                : new SqlPoint { X = coordinate.X, Y = coordinate.Y });
                        pointsAdded = true;

                        if (_emitZ)
                        {
                            geography.ZValues.Add(coordinate.Z);
                        }

                        if (_emitM)
                        {
                            geography.MValues.Add(coordinate.M);
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

            if (geography.ZValues.All(double.IsNaN))
            {
                geography.ZValues.Clear();
            }

            if (geography.MValues.All(double.IsNaN))
            {
                geography.MValues.Clear();
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
    }
}
