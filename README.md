# NetTopologySuite.IO.SqlServerBytes
A SQL Server IO module for NTS which works directly with the serialization format

| License | Actions | NuGet | MyGet (pre-release) |
| ------- | ------ | ----- | ------------------- |
| [![License](https://img.shields.io/github/license/NetTopologySuite/NetTopologySuite.IO.SqlServerBytes.svg)](https://github.com/NetTopologySuite/NetTopologySuite.IO.SqlServerBytes/blob/master/LICENSE) | [![.NET](https://github.com/NetTopologySuite/NetTopologySuite.IO.SqlServerBytes/actions/workflows/dotnet.yml/badge.svg)](https://github.com/NetTopologySuite/NetTopologySuite.IO.SqlServerBytes/actions/workflows/dotnet.yml) | [![NuGet](https://img.shields.io/nuget/v/NetTopologySuite.IO.SqlServerBytes.svg)](https://www.nuget.org/packages/NetTopologySuite.IO.SqlServerBytes/) | [![MyGet](https://img.shields.io/myget/nettopologysuite/vpre/NetTopologySuite.IO.SqlServerBytes.svg?style=flat)](https://myget.org/feed/nettopologysuite/package/nuget/NetTopologySuite.IO.SqlServerBytes) |

## Usage

### Reading
Read geography and geometry columns like this.

``` csharp
var geometryReader = new SqlServerBytesReader { IsGeography = true };
var bytes = dataReader.GetSqlBytes(columnOrdinal).Value;
var geometry = geometryReader.Read(bytes);
```

### Writing
Write parameters like this.

``` csharp
var geometry = new Point(-122.129797, 47.640049) { SRID = 4326 };
var geometryWriter = new SqlServerBytesWriter { IsGeography = true };
var bytes = geometryWriter.Write(geometry);
var parameter = command.Parameters
    .AddWithValue(parameterName, new SqlBytes(bytes));

// TODO: Set these if you're using Microsoft.Data.SqlClient
//parameter.SqlDbType = SqlDbType.Udt;
//parameter.UdtTypeName = "geography";
```

## Known limitations
### Validity
SqlServer and NetTopologySuite have a slightly different notion of a geometries validity. SqlServer stores this
information along with the geometry data and the `SqlServerBytesWriter` uses NetTopologySuite's `Geometry.IsValid` value.
You might get SqlServer geometries that return `STIsValid() = true` but `STIsValidReason() = false`.

### Fullglobe 
SqlServer geography types include `FULLGLOBE`, basically a polygon where the globe is the outer ring (shell)
and the interior rings (holes) define areas that are excluded. To achive this, SqlServer is rigid about
ring orientations for geographies.
Kind | req. Orientation
--- | ---
outer rings | **counter clockwise**
inner rings | **clockwise**
  
**This is _currently_ not representable using NetTopologySuite geometries** and the `SqlServerBytesWriter`
throws an `ArgumentException` if writing a geometry is requested where the exterior ring is oriented **clockwise**.

#### Measures
SqlServer geography types use the metric system for measures like length, distance and area.
For NetTopologySuite geometries everything is planar and thus all return values are in the unit of the input 
coordinates. In case the coordinates are geographic these values are mostly useless.    
Furthermore you can easily create buffers of geometries that exceed the extent of a hemisphere. SqlServer rejects these.
  