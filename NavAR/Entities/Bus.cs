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
        public String RouteName { get; set; }
        public String NextStopID { get; set; }
        public String IconFilePath { get; set; }

        public override int GetHashCode()
        {
            return this.MTDId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            Bus busObj = obj as Bus;
            if (busObj == null)
            {
                return false;
            }
            else
            {
                return this.MTDId == busObj.MTDId;
            }
        }
    }
}
