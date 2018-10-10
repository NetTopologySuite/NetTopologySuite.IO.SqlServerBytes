# NetTopologySuite.IO.SqlServerBytes
A SQL Server IO module for NTS which works directly with the serialization format

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
