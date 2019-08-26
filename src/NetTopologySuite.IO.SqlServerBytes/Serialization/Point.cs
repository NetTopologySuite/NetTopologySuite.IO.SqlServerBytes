using System.IO;

namespace NetTopologySuite.IO.Serialization
{
    internal class Point
    {
        private double _x;
        private double _y;

        public double X
        {
            get => _x;
            set => _x = value;
        }

        public double Y
        {
            get => _y;
            set => _y = value;
        }

        public double Lat
        {
            get => _x;
            set => _x = value;
        }

        public double Long
        {
            get => _y;
            set => _y = value;
        }

        public static Point ReadFrom(BinaryReader reader)
            => new Point
            {
                X = reader.ReadDouble(),
                Y = reader.ReadDouble()
            };

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(_x);
            writer.Write(_y);
        }
    }
}
