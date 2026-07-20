using System;
using System.Globalization;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using Xunit;

namespace NetTopologySuite.IO
{
    /// <summary>
    ///     Tests for reading geometries containing circular arc segments with
    ///     <see cref="SqlServerBytesReader.LinearizeCurves"/> enabled.
    /// </summary>
    /// <remarks>
    ///     All serialized values in these tests were produced by SQL Server 2025 using
    ///     CONVERT(varchar(max), CAST(geometry::Parse(N'...') AS varbinary(max)), 2). Expected WKT values used for
    ///     point-wise comparisons are the output of SQL Server's STCurveToLine method for the same values.
    /// </remarks>
    public class SqlServerBytesReaderCurveTest
    {
        // CIRCULARSTRING (0 0, 1 1, 2 0)
        private const string CircularStringBytes =
            "0000000002040300000000000000000000000000000000000000000000000000F03F000000000000F03F0000000000000040000000000000000001000000020000000001000000FFFFFFFF0000000008";

        [Fact]
        public void Read_linearizes_CircularString()
        {
            var geometry = Read(CircularStringBytes);

            AssertPointwiseEqual(
                "LINESTRING (0 0, 0.0048152733278030713 0.09801714032956059, 0.019214719596769569 0.19509032201612825, 0.043059664267791176 0.29028467725446239, 0.076120467488713262 0.38268343236508973, 0.11807873565164506 0.47139673682599753, 0.16853038769745476 0.55557023301960207, 0.226989546637263 0.63439328416364549, 0.29289321881345254 0.70710678118654746, 0.36560671583635451 0.773010453362737, 0.44442976698039793 0.83146961230254524, 0.52860326317400252 0.88192126434835494, 0.61731656763491027 0.92387953251128674, 0.70971532274553761 0.95694033573220882, 0.80490967798387181 0.98078528040323043, 0.90198285967043945 0.99518472667219693, 1 1, 1.0980171403295607 0.99518472667219693, 1.1950903220161282 0.98078528040323043, 1.2902846772544625 0.95694033573220882, 1.3826834323650896 0.92387953251128674, 1.4713967368259975 0.88192126434835494, 1.5555702330196022 0.83146961230254524, 1.6343932841636455 0.773010453362737, 1.7071067811865475 0.70710678118654746, 1.7730104533627369 0.63439328416364549, 1.8314696123025453 0.55557023301960207, 1.8819212643483549 0.47139673682599753, 1.9238795325112867 0.38268343236508973, 1.9569403357322088 0.29028467725446239, 1.9807852804032304 0.19509032201612825, 1.9951847266721969 0.09801714032956059, 2 0)",
                geometry);
        }

        [Fact]
        public void Read_linearizes_CircularString_with_multiple_arcs()
        {
            // CIRCULARSTRING (0 0, 1 1, 2 0, 3 -1, 4 0)
            var geometry = Read(
                "0000000002040500000000000000000000000000000000000000000000000000F03F000000000000F03F000000000000004000000000000000000000000000000840000000000000F0BF0000000000001040000000000000000001000000020000000001000000FFFFFFFF0000000008");

            var lineString = Assert.IsType<LineString>(geometry);
            Assert.Equal(65, lineString.NumPoints);

            // the original points are preserved
            AssertCoordinateEqual(0, 0, lineString.Coordinates[0]);
            AssertCoordinateEqual(1, 1, lineString.Coordinates[16]);
            AssertCoordinateEqual(2, 0, lineString.Coordinates[32]);
            AssertCoordinateEqual(3, -1, lineString.Coordinates[48]);
            AssertCoordinateEqual(4, 0, lineString.Coordinates[64]);

            AssertOnCircle(1, 0, 1, lineString.Coordinates.Take(33));
            AssertOnCircle(3, 0, 1, lineString.Coordinates.Skip(32));
        }

        [Fact]
        public void Read_linearizes_collinear_arc_using_line_segments()
        {
            // CIRCULARSTRING (0 0, 1 1, 2 2)
            var geometry = Read(
                "0000000002040300000000000000000000000000000000000000000000000000F03F000000000000F03F0000000000000040000000000000004001000000020000000001000000FFFFFFFF0000000008");

            Assert.Equal("LINESTRING (0 0, 1 1, 2 2)", geometry.AsText());
        }

        [Fact]
        public void Read_keeps_original_points_when_arc_is_small()
        {
            // CIRCULARSTRING (100 0, 100.00003 0.005, 100 0.01)
            var geometry = Read(
                "00000000020403000000000000000000594000000000000000003541D47D000059407B14AE47E17A743F00000000000059407B14AE47E17A843F01000000020000000001000000FFFFFFFF0000000008");

            Assert.Equal("LINESTRING (100 0, 100.00003 0.005, 100 0.01)", geometry.AsText());
        }

        [Fact]
        public void Read_works_when_CircularString_empty()
        {
            var geometry = Read("000000000204000000000000000001000000FFFFFFFFFFFFFFFF08");

            Assert.Equal("LINESTRING EMPTY", geometry.AsText());
        }

        [Fact]
        public void Read_linearizes_CompoundCurve()
        {
            // COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0), (3 0, 4 0))
            var geometry = Read(
                "0000000002040500000000000000000000000000000000000000000000000000F03F00000000000000000000000000000040000000000000F03F000000000000084000000000000000000000000000001040000000000000000001000000030000000001000000FFFFFFFF000000000903000000020302");

            AssertPointwiseEqual(
                "LINESTRING (0 0, 1 0, 1.0048152733278031 0.09801714032956059, 1.0192147195967696 0.19509032201612825, 1.0430596642677912 0.29028467725446239, 1.0761204674887133 0.38268343236508973, 1.1180787356516451 0.47139673682599753, 1.1685303876974547 0.55557023301960207, 1.2269895466372631 0.63439328416364549, 1.2928932188134525 0.70710678118654746, 1.3656067158363545 0.773010453362737, 1.4444297669803978 0.83146961230254524, 1.5286032631740025 0.88192126434835494, 1.6173165676349104 0.92387953251128674, 1.7097153227455375 0.95694033573220882, 1.8049096779838718 0.98078528040323043, 1.9019828596704393 0.99518472667219693, 2 1, 2.0980171403295604 0.99518472667219693, 2.1950903220161284 0.98078528040323043, 2.2902846772544625 0.95694033573220882, 2.3826834323650896 0.92387953251128674, 2.4713967368259975 0.88192126434835494, 2.5555702330196022 0.83146961230254524, 2.6343932841636457 0.773010453362737, 2.7071067811865475 0.70710678118654746, 2.7730104533627369 0.63439328416364549, 2.8314696123025453 0.55557023301960207, 2.8819212643483549 0.47139673682599753, 2.923879532511287 0.38268343236508973, 2.9569403357322086 0.29028467725446239, 2.9807852804032304 0.19509032201612825, 2.9951847266721972 0.09801714032956059, 3 0, 4 0)",
                geometry);
        }

        [Fact]
        public void Read_linearizes_CompoundCurve_with_multiple_line_points()
        {
            // COMPOUNDCURVE ((0 0, 1 0, 2 0), CIRCULARSTRING (2 0, 3 1, 4 0, 5 -1, 6 0))
            var geometry = Read(
                "0000000002040700000000000000000000000000000000000000000000000000F03F0000000000000000000000000000004000000000000000000000000000000840000000000000F03F000000000000104000000000000000000000000000001440000000000000F0BF0000000000001840000000000000000001000000030000000001000000FFFFFFFF00000000090400000002000301");

            var lineString = Assert.IsType<LineString>(geometry);
            Assert.Equal(67, lineString.NumPoints);
            AssertCoordinateEqual(0, 0, lineString.Coordinates[0]);
            AssertCoordinateEqual(1, 0, lineString.Coordinates[1]);
            AssertCoordinateEqual(2, 0, lineString.Coordinates[2]);
            AssertCoordinateEqual(3, 1, lineString.Coordinates[18]);
            AssertCoordinateEqual(4, 0, lineString.Coordinates[34]);
            AssertCoordinateEqual(5, -1, lineString.Coordinates[50]);
            AssertCoordinateEqual(6, 0, lineString.Coordinates[66]);
        }

        [Fact]
        public void Read_works_when_CompoundCurve_empty()
        {
            var geometry = Read("000000000204000000000000000001000000FFFFFFFFFFFFFFFF09");

            Assert.Equal("LINESTRING EMPTY", geometry.AsText());
        }

        [Fact]
        public void Read_linearizes_CurvePolygon()
        {
            // CURVEPOLYGON (CIRCULARSTRING (0 0, 2 2, 4 0, 2 -2, 0 0))
            var geometry = Read(
                "00000000020405000000000000000000000000000000000000000000000000000040000000000000004000000000000010400000000000000000000000000000004000000000000000C00000000000000000000000000000000001000000020000000001000000FFFFFFFF000000000A");

            var polygon = Assert.IsType<Polygon>(geometry);
            Assert.True(polygon.IsValid);
            Assert.Equal(65, polygon.ExteriorRing.NumPoints);
            Assert.Empty(polygon.InteriorRings);
            AssertOnCircle(2, 0, 2, polygon.ExteriorRing.Coordinates);

            // the area of the inscribed polygon is (n/2)r²sin(2π/n)
            Assert.Equal(0.5 * 64 * 4 * Math.Sin(2 * Math.PI / 64), polygon.Area, 9);
        }

        [Fact]
        public void Read_linearizes_circle_from_issue_18()
        {
            // CURVEPOLYGON (CIRCULARSTRING (40288.3829913593 59811.5850000016, 41716.865 61240.0670086423,
            // 43145.3470086407 59811.5850000016, 41716.865 58383.1029913609, 40288.3829913593 59811.5850000016))
            var geometry = Read(
                "000000000204050000005B1877410CACE34061EC51B87234ED40E17A14AE9B5EE440E74EEF2402E7ED4068DDB11A2B11E54061EC51B87234ED40E17A14AE9B5EE440DB89B44BE381EC405B1877410CACE34061EC51B87234ED4001000000020000000001000000FFFFFFFF000000000A");

            var polygon = Assert.IsType<Polygon>(geometry);
            Assert.True(polygon.IsValid);

            // rounding of the serialized coordinates can push a half arc just past a multiple of the maximum
            // angle, adding a point to it
            Assert.InRange(polygon.ExteriorRing.NumPoints, 65, 69);

            const double centerX = 41716.865;
            const double centerY = 59811.5850000016;
            const double radius = 41716.865 - 40288.3829913593;
            AssertOnCircle(centerX, centerY, radius, polygon.ExteriorRing.Coordinates, 1e-6);

            // the area of an inscribed polygon with n >= 64 sides is within 0.2% of the area of the circle
            Assert.InRange(polygon.Area, Math.PI * radius * radius * 0.998, Math.PI * radius * radius);
        }

        [Fact]
        public void Read_linearizes_CurvePolygon_with_compound_ring()
        {
            // CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (0 0, 1 1, 2 0), (2 0, 0 0)))
            var geometry = Read(
                "0000000002040400000000000000000000000000000000000000000000000000F03F000000000000F03F000000000000004000000000000000000000000000000000000000000000000001000000030000000001000000FFFFFFFF000000000A020000000302");

            var polygon = Assert.IsType<Polygon>(geometry);
            Assert.True(polygon.IsValid);
            Assert.Equal(34, polygon.ExteriorRing.NumPoints);
            AssertCoordinateEqual(1, 1, polygon.ExteriorRing.Coordinates[16]);
            AssertCoordinateEqual(2, 0, polygon.ExteriorRing.Coordinates[32]);
        }

        [Fact]
        public void Read_linearizes_CurvePolygon_with_linear_shell_and_curved_hole()
        {
            // CURVEPOLYGON ((0 0, 4 0, 4 4, 0 4, 0 0), CIRCULARSTRING (1 2, 2 3, 3 2, 2 1, 1 2))
            var geometry = Read(
                "0000000002040A0000000000000000000000000000000000000000000000000010400000000000000000000000000000104000000000000010400000000000000000000000000000104000000000000000000000000000000000000000000000F03F000000000000004000000000000000400000000000000840000000000000084000000000000000400000000000000040000000000000F03F000000000000F03F0000000000000040020000000100000000020500000001000000FFFFFFFF000000000A");

            var polygon = Assert.IsType<Polygon>(geometry);
            Assert.True(polygon.IsValid);

            // the linear shell is read as-is
            Assert.Equal("LINEARRING (0 0, 4 0, 4 4, 0 4, 0 0)", polygon.ExteriorRing.AsText());

            var hole = Assert.Single(polygon.InteriorRings);
            Assert.Equal(65, hole.NumPoints);
            AssertOnCircle(2, 2, 1, hole.Coordinates);
        }

        [Fact]
        public void Read_works_when_CurvePolygon_empty()
        {
            var geometry = Read("000000000204000000000000000001000000FFFFFFFFFFFFFFFF0A");

            Assert.Equal("POLYGON EMPTY", geometry.AsText());
        }

        [Fact]
        public void Read_linearizes_curves_inside_GeometryCollection()
        {
            // GEOMETRYCOLLECTION (CIRCULARSTRING (0 0, 1 1, 2 0), POINT (5 5))
            var geometry = Read(
                "0000000002040400000000000000000000000000000000000000000000000000F03F000000000000F03F0000000000000040000000000000000000000000000014400000000000001440020000000200000000010300000003000000FFFFFFFF0000000007000000000000000008000000000100000001");

            var collection = Assert.IsType<GeometryCollection>(geometry);
            Assert.Equal(2, collection.NumGeometries);

            var lineString = Assert.IsType<LineString>(collection.GetGeometryN(0));
            Assert.Equal(33, lineString.NumPoints);
            AssertOnCircle(1, 0, 1, lineString.Coordinates);

            Assert.Equal("POINT (5 5)", collection.GetGeometryN(1).AsText());
        }

        [Fact]
        public void Read_linearizes_curves_inside_nested_GeometryCollection()
        {
            // GEOMETRYCOLLECTION (GEOMETRYCOLLECTION (CURVEPOLYGON (CIRCULARSTRING (0 0, 1 1, 2 0, 1 -1, 0 0))))
            var geometry = Read(
                "0000000002040500000000000000000000000000000000000000000000000000F03F000000000000F03F00000000000000400000000000000000000000000000F03F000000000000F0BF0000000000000000000000000000000001000000020000000003000000FFFFFFFF000000000700000000000000000701000000000000000A");

            var collection = Assert.IsType<GeometryCollection>(geometry);
            var nested = Assert.IsType<GeometryCollection>(collection.GetGeometryN(0));
            var polygon = Assert.IsType<Polygon>(nested.GetGeometryN(0));
            Assert.True(polygon.IsValid);
            Assert.Equal(65, polygon.ExteriorRing.NumPoints);
            AssertOnCircle(1, 0, 1, polygon.ExteriorRing.Coordinates);
        }

        [Fact]
        public void Read_linearizes_CircularString_when_IsGeography()
        {
            // CIRCULARSTRING (0 0, 1 1, 2 0)
            var geometry = Read(
                "E610000002040300000000000000000000000000000000000000000000000000F03F000000000000F03F0000000000000000000000000000004001000000020000000001000000FFFFFFFF0000000008",
                isGeography: true);

            var lineString = Assert.IsType<LineString>(geometry);
            Assert.Equal(4326, lineString.SRID);
            Assert.Equal(33, lineString.NumPoints);
            AssertCoordinateEqual(0, 0, lineString.Coordinates[0]);
            AssertCoordinateEqual(1, 1, lineString.Coordinates[16]);
            AssertCoordinateEqual(2, 0, lineString.Coordinates[32]);
            AssertOnCircle(1, 0, 1, lineString.Coordinates);
        }

        [Fact]
        public void Read_linearizes_CurvePolygon_when_IsGeography()
        {
            // CURVEPOLYGON (CIRCULARSTRING (0 0, 2 -2, 4 0, 2 2, 0 0))
            var geometry = Read(
                "E61000000204050000000000000000000000000000000000000000000000000000C0000000000000004000000000000000000000000000001040000000000000004000000000000000400000000000000000000000000000000001000000020000000001000000FFFFFFFF000000000A",
                isGeography: true);

            var polygon = Assert.IsType<Polygon>(geometry);
            Assert.Equal(4326, polygon.SRID);
            Assert.True(polygon.IsValid);
            Assert.Equal(65, polygon.ExteriorRing.NumPoints);
            AssertOnCircle(2, 0, 2, polygon.ExteriorRing.Coordinates);
        }

        [Fact]
        public void Read_uses_MaxLinearizationAngle()
        {
            var geometry = Read(CircularStringBytes, maxLinearizationAngle: Math.PI / 2);

            // each half of the arc subtends π/2, so no additional points are inserted
            Assert.Equal("LINESTRING (0 0, 1 1, 2 0)", geometry.AsText());

            geometry = Read(CircularStringBytes, maxLinearizationAngle: Math.PI / 4);

            var lineString = Assert.IsType<LineString>(geometry);
            Assert.Equal(5, lineString.NumPoints);
            AssertCoordinateEqual(1 - Math.Sqrt(2) / 2, Math.Sqrt(2) / 2, lineString.Coordinates[1]);
            AssertCoordinateEqual(1, 1, lineString.Coordinates[2]);
            AssertCoordinateEqual(1 + Math.Sqrt(2) / 2, Math.Sqrt(2) / 2, lineString.Coordinates[3]);
        }

        [Fact]
        public void MaxLinearizationAngle_validates_value()
        {
            var reader = new SqlServerBytesReader();

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.MaxLinearizationAngle = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.MaxLinearizationAngle = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.MaxLinearizationAngle = double.NaN);
        }

        [Fact]
        public void Read_preserves_Zs_when_linearizing()
        {
            // CIRCULARSTRING (0 0 5, 1 1 5, 2 0 5)
            var geometry = Read(
                "0000000002050300000000000000000000000000000000000000000000000000F03F000000000000F03F0000000000000040000000000000000000000000000014400000000000001440000000000000144001000000020000000001000000FFFFFFFF0000000008",
                CreateZMGeometryServices());

            var lineString = Assert.IsType<LineString>(geometry);
            Assert.Equal(33, lineString.NumPoints);
            for (int i = 0; i < lineString.NumPoints; i++)
            {
                Assert.Equal(5, lineString.CoordinateSequence.GetZ(i), 12);
            }
        }

        [Fact]
        public void Read_interpolates_Ms_when_linearizing()
        {
            // CIRCULARSTRING (0 0 NULL 1, 1 1 NULL 2, 2 0 NULL 3)
            var geometry = Read(
                "0000000002060300000000000000000000000000000000000000000000000000F03F000000000000F03F00000000000000400000000000000000000000000000F03F0000000000000040000000000000084001000000020000000001000000FFFFFFFF0000000008",
                CreateZMGeometryServices());

            var lineString = Assert.IsType<LineString>(geometry);
            Assert.Equal(33, lineString.NumPoints);
            Assert.Equal(1, lineString.CoordinateSequence.GetM(0), 12);
            Assert.Equal(1.5, lineString.CoordinateSequence.GetM(8), 12);
            Assert.Equal(2, lineString.CoordinateSequence.GetM(16), 12);
            Assert.Equal(3, lineString.CoordinateSequence.GetM(32), 12);
        }

        [Fact]
        public void Read_throws_when_arc_figure_has_even_point_count()
        {
            const string bytes = "00000000" // SRID
                + "02" // version
                + "04" // properties (IsValid)
                + "02000000" // point count
                + "00000000000000000000000000000000" // 0 0
                + "000000000000F03F000000000000F03F" // 1 1
                + "01000000" // figure count
                + "02" + "00000000" // arc figure at point 0
                + "01000000" // shape count
                + "FFFFFFFF" + "00000000" + "08"; // CircularString

            var ex = Assert.Throws<ParseException>(() => Read(bytes));

            Assert.Equal(Properties.Resources.MalformedCurveData, ex.Message);
        }

        [Fact]
        public void Read_throws_when_segments_missing()
        {
            const string bytes = "00000000" // SRID
                + "02" // version
                + "04" // properties (IsValid)
                + "03000000" // point count
                + "00000000000000000000000000000000" // 0 0
                + "000000000000F03F000000000000F03F" // 1 1
                + "00000000000000400000000000000000" // 2 0
                + "01000000" // figure count
                + "03" + "00000000" // curve figure at point 0
                + "01000000" // shape count
                + "FFFFFFFF" + "00000000" + "09" // CompoundCurve
                + "01000000" // segment count
                + "00"; // Line (consumes two of the three points)

            var ex = Assert.Throws<ParseException>(() => Read(bytes));

            Assert.Equal(Properties.Resources.MalformedCurveData, ex.Message);
        }

        private static NtsGeometryServices CreateZMGeometryServices()
            => new NtsGeometryServices(
                new PackedCoordinateSequenceFactory(
                    PackedCoordinateSequenceFactory.PackedType.Double),
                new PrecisionModel(PrecisionModels.Floating),
                srid: -1);

        private static void AssertPointwiseEqual(string expectedWkt, Geometry actual, double tolerance = 1e-12)
        {
            var expected = new WKTReader().Read(expectedWkt);
            Assert.Equal(expected.OgcGeometryType, actual.OgcGeometryType);
            var expectedCoordinates = expected.Coordinates;
            var actualCoordinates = actual.Coordinates;
            Assert.Equal(expectedCoordinates.Length, actualCoordinates.Length);
            for (int i = 0; i < expectedCoordinates.Length; i++)
            {
                AssertCoordinateEqual(expectedCoordinates[i].X, expectedCoordinates[i].Y, actualCoordinates[i], tolerance);
            }
        }

        private static void AssertCoordinateEqual(double expectedX, double expectedY, Coordinate actual, double tolerance = 1e-12)
        {
            Assert.True(
                Math.Abs(expectedX - actual.X) <= tolerance && Math.Abs(expectedY - actual.Y) <= tolerance,
                $"Expected ({expectedX.ToString(CultureInfo.InvariantCulture)} {expectedY.ToString(CultureInfo.InvariantCulture)})"
                    + $" but was ({actual.X.ToString(CultureInfo.InvariantCulture)} {actual.Y.ToString(CultureInfo.InvariantCulture)}).");
        }

        private static void AssertOnCircle(
            double centerX,
            double centerY,
            double radius,
            System.Collections.Generic.IEnumerable<Coordinate> coordinates,
            double tolerance = 1e-9)
        {
            foreach (var coordinate in coordinates)
            {
                double distance = Math.Sqrt(
                    (coordinate.X - centerX) * (coordinate.X - centerX)
                        + (coordinate.Y - centerY) * (coordinate.Y - centerY));
                Assert.True(
                    Math.Abs(distance - radius) <= tolerance,
                    $"({coordinate.X.ToString(CultureInfo.InvariantCulture)} {coordinate.Y.ToString(CultureInfo.InvariantCulture)})"
                        + $" isn't on the circle around ({centerX.ToString(CultureInfo.InvariantCulture)} {centerY.ToString(CultureInfo.InvariantCulture)})"
                        + $" with radius {radius.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        private Geometry Read(
            string bytes,
            NtsGeometryServices geometryServices = null,
            bool isGeography = false,
            double? maxLinearizationAngle = null)
        {
            byte[] byteArray = new byte[bytes.Length / 2];
            for (int i = 0; i < bytes.Length; i += 2)
            {
                byteArray[i / 2] = byte.Parse(bytes.Substring(i, 2), NumberStyles.HexNumber);
            }

            var reader = new SqlServerBytesReader(geometryServices ?? NtsGeometryServices.Instance)
            {
                IsGeography = isGeography,
                LinearizeCurves = true
            };

            if (maxLinearizationAngle.HasValue)
            {
                reader.MaxLinearizationAngle = maxLinearizationAngle.Value;
            }

            return reader.Read(byteArray);
        }
    }
}
