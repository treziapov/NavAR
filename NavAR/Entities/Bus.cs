using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GART.Data;

namespace NavAR.Entities
{
    /// <summary>
    /// 
    /// </summary>
    class Bus : ARItem
    {
        public GeoCoordinate Coordinate { get; set; }
        public String MTDId { get; set; }
    }
}
