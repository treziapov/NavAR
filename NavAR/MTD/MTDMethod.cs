using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavAR.MTD
{
    public enum MTDMethod
    {
        GetStopsByLonLat
    }

     ///// <summary>
     //   /// Abort the request if the timer fires. 
     //   /// </summary>
     //   /// <param name="state"></param>
     //   /// <param name="timedOut"></param>
     //   private static void TimeoutCallback(object state, bool timedOut)
     //   {
     //       if (timedOut)
     //       {
     //           HttpWebRequest request = state as HttpWebRequest;
     //           if (request != null)
     //           {
     //               request.Abort();
     //               MessageBox.Show("Timeout occured");
     //           }
     //       }
     //   }

     //   /// <summary>
     //   /// 
     //   /// </summary>
     //   public static void SendGetStopsByLonLatRequest()
     //   {
     //       string url = String.Format("{0}?key={1}&lat={2}&lon={3}", 
     //           MTDAPI.SetDefaultMTDUrlParameters(MTDMethod.GetStopsByLonLat), MTDAPI.API_KEY, MyCoordinate.Latitude, MyCoordinate.Longitude);

     //       try
     //       {
     //           HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
     //           IAsyncResult result = (IAsyncResult)request.BeginGetRequestStream(new AsyncCallback(FinishGetStopsByLonLatRequest), request);

     //           // this line implements the timeout, if there is a timeout, the callback fires and the request becomes aborted
     //           ThreadPool.RegisterWaitForSingleObject(
     //               result.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), request, MTDAPI.DEFAULT_TIMEOUT, true);
     //       }
     //       catch (Exception exception)
     //       {
     //           // TODO: return some kind of error
     //           MessageBox.Show(
     //               String.Format("Error occured while sending MTD request: {0}", exception.Message));

     //       }
     //   }

     //   /// <summary>
     //   /// 
     //   /// </summary>
     //   /// <param name="result"></param>
     //   public static void FinishGetStopsByLonLatRequest(IAsyncResult result)
     //   {
     //       try
     //       {
     //           HttpWebResponse response = (result.AsyncState as HttpWebRequest).EndGetResponse(result) as HttpWebResponse;
     //           Stream stream = response.GetResponseStream();

     //           XmlReaderSettings settings = new XmlReaderSettings();
     //           using (XmlReader reader = XmlReader.Create(stream, settings))
     //           {
     //               string xml = "";
     //               while (reader.Read())
     //               {
     //                   switch (reader.NodeType)
     //                   {
     //                       case XmlNodeType.Element:
     //                           xml += String.Format("Start Element {0}", reader.Name);
     //                           break;
     //                       case XmlNodeType.Text:
     //                           xml += String.Format("Text Node: {0}", reader.Value);
     //                           break;
     //                       case XmlNodeType.EndElement:
     //                           xml += String.Format("End Element {0}", reader.Name);
     //                           break;
     //                       default:
     //                           xml += String.Format("Other node {0} with value {1}", reader.NodeType, reader.Value);
     //                           break;
     //                   }
     //               }
     //               MessageBox.Show(xml);
     //           }
     //       }
     //       catch (Exception exception)
     //       {
     //           MessageBox.Show(
     //               String.Format("Error occured while processing MTD request response: ", exception.Message));
     //       }
     //   }
}
