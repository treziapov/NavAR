using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace NavAR.MTD
{
    /// <summary>
    /// Needs refactoring, could do an abstract method (for later)
    /// </summary>
    public class MTDAPI
    {
        // Static fields
        public static readonly string DEFAULT_URL = "http://developer.cumtd.com/api/{0}/{1}/{2}";
        public static readonly string API_KEY = "87c0a1cbb62b43b0ae753e6ac29542ea";
        public static readonly string DEFAULT_VERSION = "v2.2";
        public static readonly string DEFAULT_RESPONSE_FORMAT = "xml";
        public static readonly int DEFAULT_TIMEOUT = 2 * 60 * 1000;       // 2 minutes

        /// <summary>
        /// 
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static String SetDefaultMTDUrlParameters(MTDMethod method)
        {
            return String.Format(DEFAULT_URL, DEFAULT_VERSION, DEFAULT_RESPONSE_FORMAT, method.ToString());
        }
    }
}
