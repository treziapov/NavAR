using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace NavAR.CUMTD_API
{
    class CUMTDAPI
    {
        private const string API_KEY = "87c0a1cbb62b43b0ae753e6ac29542ea";
        private const string API_URL = "http://developer.cumtd.com/api/{version}/{format}/{method}?key={api_key}";
        private const string API_VERSION = "v2.2";
        private const string RESPONSE_FORMAT = "xml";

        /// <summary>
        /// 
        /// </summary>
        public void GetCUStops()
        {
            string filledUrl = String.Format(API_URL, API_VERSION, RESPONSE_FORMAT, "xml", "bla", API_KEY);
            WebRequest request = WebRequest.Create(API_URL);
            
            return;
        }
    }
}
