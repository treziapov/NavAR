using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavAR.Helpers
{
    class CoordinateMath
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="coord1"></param>
        /// <param name="coord2"></param>
        /// <returns></returns>
        public static double DistanceBetween(GeoCoordinate coord1, GeoCoordinate coord2)
        {
            double lat1 = coord1.Latitude;
            double lat2 = coord2.Latitude;
            double lng1 = coord1.Longitude;
            double lng2 = coord2.Longitude;

            double earthRadius = 6371000;
            double dLat = DegreeToRadian(lat2 - lat1);
            double dLng = DegreeToRadian(lng2 - lng1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(DegreeToRadian(lat1)) * Math.Cos(DegreeToRadian(lat2)) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c;
        }

        private static double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }
    }
}
