using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Device.Location;

using GART.Data;

namespace NavAR.Entities
{
    /// <summary>
    /// 
    /// </summary>
    class BusStop : ARItem
    {
        private string _name;
        private string _mtdid;
        private string _iconfilepath;

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    NotifyPropertyChanged(() => Name);

                    // Update the Content property as well for controls that
                    // only show this member
                    Content = value;
                }
            }
        }

        public string MTDId
        {
            get
            {
                return _mtdid;
            }
            set
            {
                if (_mtdid != value)
                {
                    _mtdid = value;
                    NotifyPropertyChanged(() => MTDId);

                    // Update the Content property as well for controls that
                    // only show this member
                    Content = value;
                }
            }
        }

        public string IconFilePath
        {
            get
            {
                return _iconfilepath;
            }
            set
            {
                if (_iconfilepath != value)
                {
                    _iconfilepath = value;
                    NotifyPropertyChanged(() => IconFilePath);

                    // Update the Content property as well for controls that
                    // only show this member
                    Content = value;
                }
            }
        }

        public override int GetHashCode()
        {
            return this.MTDId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            BusStop busStopObj = obj as BusStop;
            if (busStopObj == null)
            {
                return false;
            }
            else
            {
                return this.MTDId == busStopObj.MTDId;
            }
        }

    }
}
