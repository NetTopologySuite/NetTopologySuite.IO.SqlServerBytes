# NetTopologySuite.IO.SqlServerBytes
A SQL Server IO module for NTS which works directly with the serialization format

| License | Travis | NuGet |
| ------- | ------ | ----- |
| [![License](https://img.shields.io/github/license/NetTopologySuite/NetTopologySuite.IO.SqlServerBytes.svg)](https://github.com/NetTopologySuite/NetTopologySuite.IO.SqlServerBytes/blob/master/LICENSE) | [![Travis](https://travis-ci.org/NetTopologySuite/NetTopologySuite.IO.SqlServerBytes.svg?branch=master)](https://travis-ci.org/NetTopologySuite/NetTopologySuite.IO.SqlServerBytes) | [![NuGet](https://img.shields.io/nuget/v/NetTopologySuite.IO.SqlServerBytes.svg)](https://www.nuget.org/packages/NetTopologySuite.IO.SqlServerBytes/) |

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
command.Parameters.AddWithValue(parameterName, new SqlBytes(bytes));
```
