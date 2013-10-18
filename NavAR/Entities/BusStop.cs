using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Device.Location;

namespace NavAR.Entities
{
    /// <summary>
    /// 
    /// </summary>
    class BusStop
    {
        public String MTDId { get; set; }
        public String Name { get; set; }
        public GeoCoordinate Coordinate { get; set; }

        public override int GetHashCode()
        {
            return Coordinate.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Coordinate.Equals(obj as GeoCoordinate);
        }

    }
}
