using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace IsolatedStorage
{
    /// <summary>
    /// 
    /// </summary>
    public class StoredBusStop
    {
        public Double Latitude { get; set; }
        public Double Longitude { get; set; }
        public String Name { get; set; }
    }

    /// <summary>
    /// Storage Manager
    /// </summary>
    public static class Manager
    {
        // Named mutex
        private static Mutex Mutex = new Mutex(false, "NavARSharedData");
        private static String BusStopsFileName = "BusStops.txt";

        /// <summary>
        /// Read Isolated Storage
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static List<StoredBusStop> ReadStops()
        {
            List<StoredBusStop> items = null;

            var test = IsolatedStorageSettings.ApplicationSettings; 

            Mutex.WaitOne();
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (var stream = new IsolatedStorageFileStream(BusStopsFileName, FileMode.OpenOrCreate, FileAccess.Read, store))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            if (!reader.EndOfStream)
                            {
                                var serializer = new XmlSerializer(typeof(List<StoredBusStop>));
                                items = serializer.Deserialize(reader) as List<StoredBusStop>;
                            }
                        }
                    }
                }
            }
            finally
            {
                Mutex.ReleaseMutex();
            }

            Debug.WriteLine("Read Isolated Storage");

            return items;
        }
        
        /// <summary>
        /// Write to Isolated Storage
        /// </summary>
        /// <param name="data"></param>
        public static void WriteStops(IEnumerable<StoredBusStop> stops)
        {
            Debug.WriteLine("Writing to Isolated Storage");

            Mutex.WaitOne();
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (var stream = new IsolatedStorageFileStream(BusStopsFileName, FileMode.Create, FileAccess.Write, store))
                    {
                        var serializer = new XmlSerializer(typeof(List<StoredBusStop>));
                        serializer.Serialize(stream, stops);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Something unexpeted happened");
            }
            finally
            {
                Mutex.ReleaseMutex();
            }
        }

    }
}
