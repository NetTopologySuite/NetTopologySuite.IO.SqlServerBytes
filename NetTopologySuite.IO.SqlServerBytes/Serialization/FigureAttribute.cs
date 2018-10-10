namespace NetTopologySuite.IO.Serialization
{
    internal enum FigureAttribute : byte
    {
        Unknown = 0, // NB: Called "Point" in MS-SSCLRT (v20170816), but never used
        PointOrLine = 1, // NB: Called "Line" in MS-SSCLRT (v20170816), but it's also used for points
        Arc = 2,
        Curve = 3
    }
}
