using System;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO.Properties;
using Xunit;

namespace NetTopologySuite.IO
{
    public class SqlServerBytesWriterTest
    {
        [Theory]
        [InlineData(
            "POINT EMPTY",
            "000000000104000000000000000001000000FFFFFFFFFFFFFFFF01")]
        [InlineData(
            "POINT (1 2)",
            "00000000010C000000000000F03F0000000000000040")]
        [InlineData(
            "POINT (1 2 3)",
            "00000000010D000000000000F03F00000000000000400000000000000840")]
        [InlineData(
            "LINESTRING EMPTY",
            "000000000104000000000000000001000000FFFFFFFFFFFFFFFF02")]
        [InlineData(
            "LINESTRING (0 0, 0 1)",
            "000000000114000000000000000000000000000000000000000000000000000000000000F03F")]
        [InlineData(
            "LINESTRING (0 0 1, 0 1 2)",
            "000000000115000000000000000000000000000000000000000000000000000000000000F03F000000000000F03F0000000000000040")]
        [InlineData(
            "LINESTRING (0 0, 0 1 2)",
            "000000000115000000000000000000000000000000000000000000000000000000000000F03F000000000000F8FF0000000000000040")]
        [InlineData(
            "LINESTRING (0 0, 0 1, 0 2)",
            "00000000010403000000000000000000000000000000000000000000000000000000000000000000F03F0000000000000000000000000000004001000000010000000001000000FFFFFFFF0000000002")]
        [InlineData(
            "LINESTRING (0 0 1, 0 1 2, 0 2 3)",
            "00000000010503000000000000000000000000000000000000000000000000000000000000000000F03F00000000000000000000000000000040000000000000F03F0000000000000040000000000000084001000000010000000001000000FFFFFFFF0000000002")]
        [InlineData(
            "POLYGON EMPTY",
            "000000000104000000000000000001000000FFFFFFFFFFFFFFFF03")]
        // 0xE61000000104000000000000000001000000FFFFFFFFFFFFFFFF03
        [InlineData(
            "POLYGON ((0 0, 0 1, 1 1, 0 0))",
            "00000000010404000000000000000000000000000000000000000000000000000000000000000000F03F000000000000F03F000000000000F03F0000000000000000000000000000000001000000020000000001000000FFFFFFFF0000000003")]
        [InlineData(
            "POLYGON ((0 0, 0 3, 3 3, 3 0, 0 0), (1 1, 1 2, 2 2, 2 1, 1 1))",
            "0000000001040A0000000000000000000000000000000000000000000000000000000000000000000840000000000000084000000000000008400000000000000840000000000000000000000000000000000000000000000000000000000000F03F000000000000F03F000000000000F03F0000000000000040000000000000004000000000000000400000000000000040000000000000F03F000000000000F03F000000000000F03F020000000200000000000500000001000000FFFFFFFF0000000003")]
        [InlineData(
            "POLYGON ((0 0, 0 3, 3 3, 3 0, 0 0), (0 0, 0 2, 2 2, 2 0, 0 0))",
            "0000000001000A00000000000000000000000000000000000000000000000000000000000000000008400000000000000840000000000000084000000000000008400000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000040000000000000004000000000000000400000000000000040000000000000000000000000000000000000000000000000020000000200000000000500000001000000FFFFFFFF0000000003")]
        [InlineData(
            "GEOMETRYCOLLECTION EMPTY",
            "000000000104000000000000000001000000FFFFFFFFFFFFFFFF07")]
        [InlineData(
            "GEOMETRYCOLLECTION (POINT (0 0))",
            "000000000104010000000000000000000000000000000000000001000000010000000002000000FFFFFFFF0000000007000000000000000001")]
        [InlineData(
            "GEOMETRYCOLLECTION (POINT (0 0), POINT (0 1))",
            "00000000010402000000000000000000000000000000000000000000000000000000000000000000F03F020000000100000000010100000003000000FFFFFFFF0000000007000000000000000001000000000100000001")]
        [InlineData(
            "GEOMETRYCOLLECTION (POINT (0 0), POINT EMPTY, POINT (0 1))",
            "00000000010402000000000000000000000000000000000000000000000000000000000000000000F03F020000000100000000010100000004000000FFFFFFFF000000000700000000000000000100000000FFFFFFFF01000000000100000001")]
        [InlineData(
            "GEOMETRYCOLLECTION (GEOMETRYCOLLECTION (POINT (0 1)))",
            "000000000104010000000000000000000000000000000000F03F01000000010000000003000000FFFFFFFF0000000007000000000000000007010000000000000001")]
        [InlineData(
            "GEOMETRYCOLLECTION (POINT (0 0), GEOMETRYCOLLECTION (POINT (0 1)))",
            "00000000010402000000000000000000000000000000000000000000000000000000000000000000F03F020000000100000000010100000004000000FFFFFFFF0000000007000000000000000001000000000100000007020000000100000001")]
        [InlineData(
            "MULTIPOINT ((0 0))",
            "000000000104010000000000000000000000000000000000000001000000010000000002000000FFFFFFFF0000000004000000000000000001")]
        [InlineData(
            "MULTILINESTRING ((0 0, 0 1))",
            "00000000010402000000000000000000000000000000000000000000000000000000000000000000F03F01000000010000000002000000FFFFFFFF0000000005000000000000000002")]
        [InlineData(
            "MULTIPOLYGON (((0 0, 0 1, 1 1, 0 0)))",
            "00000000010404000000000000000000000000000000000000000000000000000000000000000000F03F000000000000F03F000000000000F03F0000000000000000000000000000000001000000020000000002000000FFFFFFFF0000000006000000000000000003")]
        [InlineData(
            "CIRCULARSTRING (0 0, 1 1, 2 0)",
            "0000000002040300000000000000000000000000000000000000000000000000F03F000000000000F03F0000000000000040000000000000000001000000020000000001000000FFFFFFFF0000000008")]
        [InlineData(
            "COMPOUNDCURVE ((0 0, 1 0), CIRCULARSTRING (1 0, 2 1, 3 0))",
            "0000000002040400000000000000000000000000000000000000000000000000F03F00000000000000000000000000000040000000000000F03F0000000000000840000000000000000001000000030000000001000000FFFFFFFF0000000009020000000203")]
        [InlineData(
            "CURVEPOLYGON (CIRCULARSTRING (2 1, 1 2, 0 1, 1 0, 2 1))",
            "000000000204050000000000000000000040000000000000F03F000000000000F03F00000000000000400000000000000000000000000000F03F000000000000F03F00000000000000000000000000000040000000000000F03F01000000020000000001000000FFFFFFFF000000000A")]
        public void Write_works(string wkt, string expected)
        {
            var geometry = new WKTReaderEx().Read(wkt);

            Assert.Equal(expected, Write(geometry));
        }

        [Theory]
        [InlineData("POINT (1 2)", "E6100000010C0000000000000040000000000000F03F")]
        [InlineData("POLYGON EMPTY", "E61000000104000000000000000001000000FFFFFFFFFFFFFFFF03")]
        public void Write_works_when_IsGeography(string wkt, string expectedHex)
        {
            var geometry = new WKTReaderEx().Read(wkt);
            geometry.SRID = 4326;

            Assert.Equal(expectedHex, Write(geometry, isGeography: true));
        }

        [Fact]
        public void Write_throws_when_IsGeography_and_shell_oriented_clockwise()
        {
            var geometry = new WKTReaderEx().Read("POLYGON ((0 0, 0 1, 1 1, 0 0))");
            geometry.SRID = 4326;

            var ex = Assert.Throws<ArgumentException>(
                () => Write(geometry, isGeography: true));

            Assert.Equal(Resources.InvalidGeographyShellOrientation, ex.Message);
        }

        [Fact]
        public void Write_throws_when_IsGeography_and_hole_oriented_counter_clockwise()
        {
            var geometry = new WKTReaderEx().Read("POLYGON ((0 0, 3 0, 3 3, 0 3, 0 0), (1 1, 2 1, 2 2, 1 2, 1 1))");
            geometry.SRID = 4326;

            var ex = Assert.Throws<ArgumentException>(
                () => Write(geometry, isGeography: true));

            Assert.Equal(Resources.InvalidGeographyHoleOrientation, ex.Message);
        }

        [Fact]
        public void Write_works_with_SRID()
        {
            var point = new Point(0, 0) { SRID = 4326 };

            Assert.Equal(
                "E6100000010C00000000000000000000000000000000",
                Write(point));
        }

        [Fact]
        public void Write_works_when_Point_with_M()
        {
            var factory = NtsGeometryServices.Instance.CreateGeometryFactory(
                new PackedCoordinateSequenceFactory(PackedCoordinateSequenceFactory.PackedType.Double));
            var point = factory.CreatePoint(new CoordinateZM(1, 2, 3, 4));

            Assert.Equal(
                "00000000010F000000000000F03F000000000000004000000000000008400000000000001040",
                Write(point));
        }

        [Fact]
        public void Write_works_when_LineString_with_Ms()
        {
            var points = new PackedDoubleCoordinateSequence(
                coords: new double[]
                {
                    0, 0, 0, 1,
                    0, 1, 0, 2
                },
                dimension: 4,
                measures: 1);
            var lineString = new LineString(points, Geometry.DefaultFactory);

            Assert.Equal(
                "000000000117000000000000000000000000000000000000000000000000000000000000F03F00000000000000000000000000000000000000000000F03F0000000000000040",
                Write(lineString));
        }

        [Fact]
        public void Write_works_when_LineString_with_M_one_NaN()
        {
            var points = new PackedDoubleCoordinateSequence(
                coords: new double[]
                {
                    0, 0, 0, Coordinate.NullOrdinate,
                    0, 1, 0, 1
                },
                dimension: 4,
                measures: 1);
            var lineString = new LineString(points, Geometry.DefaultFactory);

            Assert.Equal(
                "000000000117000000000000000000000000000000000000000000000000000000000000F03F00000000000000000000000000000000000000000000F8FF000000000000F03F",
                Write(lineString));
        }

        [Fact]
        public void Write_works_when_null()
        {
            Assert.Equal("FFFFFFFF", Write(null));
        }

        [Fact]
        public void HandleOrdinates_works()
        {
            var factory = NtsGeometryServices.Instance.CreateGeometryFactory(
                new PackedCoordinateSequenceFactory(PackedCoordinateSequenceFactory.PackedType.Double));
            var point = factory.CreatePoint(new CoordinateZM(1, 2, 3, 4));

            Assert.Equal(
                "00000000010C000000000000F03F0000000000000040",
                Write(point, Ordinates.XY));
        }

        private string Write(
            Geometry geometry,
            Ordinates handleOrdinates = Ordinates.XYZM,
            bool isGeography = false)
        {
            var writer = new SqlServerBytesWriter
            {
                HandleOrdinates = handleOrdinates,
                IsGeography = isGeography
            };

            return string.Concat(writer.Write(geometry).Select(b => b.ToString("X2")));
        }
    }
}
