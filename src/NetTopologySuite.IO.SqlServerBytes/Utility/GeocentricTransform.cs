using System;
using NetTopologySuite.Algorithm;

namespace NetTopologySuite.IO.Utility
{
    /// <summary>
    /// Wenzel, H.-G.(1985): Hochauflösende Kugelfunktionsmodelle für
    /// das Gravitationspotential der Erde. Wiss. Arb. Univ. Hannover
    /// Nr. 137, p. 130-131.
    /// <para/>
    /// Programmed by
    /// <code>
    /// GGA- Leibniz-Institue of Applied Geophysics
    /// Stilleweg 2
    /// D-30655 Hannover
    /// Federal Republic of Germany
    ///
    /// Internet: www.gga-hannover.de
    /// </code>
    /// Hannover, March 1999, April 2004.
    /// <para/>
    ///see also: comments in statements
    /// </summary>
    /// <remarks>
    /// Mathematically exact and because of symmetry of rotation-ellipsoid,
    /// each point(X, Y, Z) has at least two solutions(Latitude1, Longitude1, Height1) and
    /// (Latitude2, Longitude2, Height2). Is point = (0., 0., Z)(P = 0.), so you get even
    /// four solutions, 	every two symmetrical to the semi-minor axis.
    /// Here Height1 and Height2 have at least a difference in order of
    /// radius of curvature (e.g. (0, 0, b) => (90., 0., 0.) or(-90., 0., -2b);
    /// (a+100.)*(sqrt(2.)/2., sqrt(2.)/2., 0.) => (0., 45., 100.) or
    /// (0., 225., -(2a+100.))).
    /// The algorithm always computes(Latitude, Longitude) with smallest |Height|.
    /// For normal computations, that means |Height|<10000.m, algorithm normally
    /// converges after to 2-3 steps!!!
    /// But if |Height| has the amount of length of ellipsoid's axis
    /// (e.g. -6300000.m), 	algorithm needs about 15 steps.
    /// </remarks>
    public class GeocentricTransform
    {
        public static GeocentricTransform CreateFor(int srid)
        {
            switch (srid)
            {
                default:
                case 4326:
                    return new GeocentricTransform(6378137, 6378137);
                    //return new GeocentricTransform(6378137, 6356752);
            }
        }

        /* local defintions and variables */
        /* end-criterium of loop, accuracy of sin(Latitude) */
        private const double GENAU = 1E-12;
        private const double GENAU2 = GENAU * GENAU;

        private const int MAXITER = 30;

        // private const double COS_67P5 = 0.38268343236508977;  /* cosine of 67.5 degrees */
        // private const double AD_C = 1.0026000;            /* Toms region 1 constant */
        private const double PI = 3.14159265358979323e0;
        private const double PI_OVER_2 = (PI / 2.0e0);

        private readonly double _a;
        private readonly double _a2;
        private readonly double _b;
        private readonly double _b2;
        private readonly double _e2;

        /// <summary>
        /// Creates a new instance of GeocentricGeodetic
        /// </summary>
        public GeocentricTransform(double equatorialRadius, double polarRadius)
        {
            _a = equatorialRadius;
            _b = polarRadius;
            _a2 = _a * _a;
            _b2 = _b * _b;
            _e2 = (_a2 - _b2) / _a2;
            //_ep2 = (_a2 - _b2)/_b2;
        }

        /// <summary>
        /// Converts lon, lat, height to x, y, z where lon and lat are in radians and everything else is meters
        /// </summary>
        /// <param name="lon"></param>
        /// <param name="lat"></param>
        /// <param name="height"></param>
        public (double x, double y, double z) GeodeticToGeocentric(double lon, double lat, double height)
        {
            lon = AngleUtility.ToRadians(lon);
            lat = AngleUtility.ToRadians(lat);

            /*
             * The function Convert_Geodetic_To_Geocentric converts geodetic coordinates
             * (latitude, longitude, and height) to geocentric coordinates (X, Y, Z),
             * according to the current ellipsoid parameters.
             *
             *    Latitude  : Geodetic latitude in radians                     (input)
             *    Longitude : Geodetic longitude in radians                    (input)
             *    Height    : Geodetic height, in meters                       (input)
             *    X         : Calculated Geocentric X coordinate, in meters    (output)
             *    Y         : Calculated Geocentric Y coordinate, in meters    (output)
             *    Z         : Calculated Geocentric Z coordinate, in meters    (output)
             *
             */

            /*
            ** Don't blow up if Latitude is just a little out of the value
            ** range as it may just be a rounding issue.  Also removed longitude
            ** test, it should be wrapped by cos() and sin().  NFW for PROJ.4, Sep/2001.
            */
            if (lat < -PI_OVER_2 && lat > -1.001 * PI_OVER_2)
                lat = -PI_OVER_2;
            else if (lat > PI_OVER_2 && lat < 1.001 * PI_OVER_2)
                lat = PI_OVER_2;
            else if ((lat < -PI_OVER_2) || (lat > PI_OVER_2))
            {
                /* lat out of range */
                return (double.NaN, double.NaN, double.NaN);
            }

            if (lon > PI)
                lon -= (2 * PI);
            double sinLat = Math.Sin(lat);
            double cosLat = Math.Cos(lat);
            double sin2Lat = sinLat * sinLat; /*  Square of sin(Latitude)  */
            double rn = _a / (Math.Sqrt(1.0e0 - _e2 * sin2Lat)); /*  Earth radius at location  */
            double x = (rn + height) * cosLat * Math.Cos(lon);
            double y = (rn + height) * cosLat * Math.Sin(lon);
            double z = ((rn * (1 - _e2)) + height) * sinLat;

            return (x, y, z);
        }

        /// <summary>
        /// Converts x, y, z to lon, lat, height
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public (double lon, double lat, double height) GeocentricToGeodetic(double x, double y, double z)
        {
            double lon;
            double lat;
            double height;

            double cosPhi; /* cos of searched geodetic latitude */
            double sinPhi; /* sin of searched geodetic latitude */
            double sinDiffPhi; /* end-criterium: addition-theorem of sin(Latitude(iter)-Latitude(iter-1)) */
            //bool At_Pole = false;     /* indicates location is in polar region */

            double p = Math.Sqrt(x * x + y * y);
            double rr = Math.Sqrt(x * x + y * y + z * z); /* distance between center and location */

            /*	special cases for latitude and longitude */
            if (p / _a < GENAU)
            {
                /*  special case, if P=0. (X=0., Y=0.) */
                //At_Pole = true;
                lon = 0.0;

                /*  if (X, Y, Z)=(0., 0., 0.) then Height becomes semi-minor axis
                 *  of ellipsoid (=center of mass), Latitude becomes PI/2 */
                if (rr / _a < GENAU)
                {
                    lat = PI_OVER_2;
                    height = -_b;
                    return (lon, AngleUtility.ToDegrees(lat), height);
                }
            }
            else
            {
                /*  ellipsoidal (geodetic) longitude
                 *  interval: -PI < Longitude <= +PI */
                lon = Math.Atan2(y, x);
            }

            /* --------------------------------------------------------------
             * Following iterative algorithm was developped by
             * "Institut für Erdmessung", University of Hannover, July 1988.
             * Internet: www.ife.uni-hannover.de
             * Iterative computation of CPHI, SPHI and Height.
             * Iteration of CPHI and SPHI to 10**-12 radian resp.
             * 2*10**-7 arcsec.
             * --------------------------------------------------------------
             */
            double ct = z / rr; // sin of geocentric latitude // looks like these two should be flipped (TD).
            double st = p / rr; // cos of geocentric latitude
            double rx = 1.0 / Math.Sqrt(1.0 - _e2 * (2.0 - _e2) * st * st);
            double cosPhi0 = st * (1.0 - _e2) * rx; /* cos of start or old geodetic latitude in iterations */
            double sinPhi0 = ct * rx; /* sin of start or old geodetic latitude in iterations */
            int iter = 0; /* # of continous iteration, max. 30 is always enough (s.a.) */

            /* loop to find sin(Latitude) resp. Latitude
             * until |sin(Latitude(iter)-Latitude(iter-1))| < genau */
            do
            {
                iter++;
                double earthRadius = _a / Math.Sqrt(1.0 - _e2 * sinPhi0 * sinPhi0);

                /*  ellipsoidal (geodetic) height */
                height = p * cosPhi0 + z * sinPhi0 - earthRadius * (1.0 - _e2 * sinPhi0 * sinPhi0);

                double rk = _e2 * earthRadius / (earthRadius + height);
                rx = 1.0 / Math.Sqrt(1.0 - rk * (2.0 - rk) * st * st);
                cosPhi = st * (1.0 - rk) * rx;
                sinPhi = ct * rx;
                sinDiffPhi = sinPhi * cosPhi0 - cosPhi * sinPhi0;
                cosPhi0 = cosPhi;
                sinPhi0 = sinPhi;
            } while (sinDiffPhi * sinDiffPhi > GENAU2 && iter < MAXITER);

            /*	ellipsoidal (geodetic) latitude */
            lat = Math.Atan(sinPhi / Math.Abs(cosPhi));

            return (AngleUtility.ToDegrees(lon), AngleUtility.ToDegrees(lat), height);
        }
    }
}

