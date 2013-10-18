using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.IO;
using System.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Threading;
using System.Device.Location;

using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using PhoneServices = Microsoft.Phone.Maps.Services;

using NavAR.MTD;
using NavAR.MTDService;
using NavAR.Resources;
using NavAR.Entities;
using NavAR.Helpers;

using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;

namespace NavAR
{
    public partial class MainPage : PhoneApplicationPage
    {
        // User location variables
        private GeoCoordinate MyCoordinate = null;
        private double GPSAccuracy = 0.0;

        // Route variables
        private RouteQuery MyQuery = null;
        private List<GeoCoordinate> MyCoordinates = new List<GeoCoordinate>();
        private MapRoute MyMapRoute = null;
        //private ReverseGeocodeQuery MyReverseGeocodeQuery = null;

        // MTD/transit variables
        // TODO: Should have our own classes
        private HashSet<BusStop> LocalBusStops = new HashSet<BusStop>();
        private BusStop MyBusStop = null;
        private HashSet<departure> MTDDepartures= new HashSet<departure>();
        private HashSet<Bus> LocalBuses = new HashSet<Bus>();

        private double ScanRadiusInMetres = 1000d;

        // Map graphics
        private MapLayer MarkerMapLayer = new MapLayer();

        // Timers
        private DispatcherTimer DrawingTimer = new DispatcherTimer();
        private DispatcherTimer LocationTimer = new DispatcherTimer();
        private DispatcherTimer BusStopScanTimer = new DispatcherTimer();
        private DispatcherTimer BusScanTimer = new DispatcherTimer();
        private DispatcherTimer CompassTimer = new DispatcherTimer();
        private DispatcherTimer DemoTimer = new DispatcherTimer();

        // Compass
        private Compass Compass = Compass.GetDefault();

        // Test 
        private readonly bool DEMO = true;
        private List<GeoCoordinate> DemoVisitCoordinates = null;
        private List<GeoCoordinate> DemoWalkCoordinates = new List<GeoCoordinate>()
        {
            new GeoCoordinate(40.11384d, -88.22489d),   //  Siebel Center, Urbana
            new GeoCoordinate(40.10979d, -88.22726d),   //  Illini Union, Urbana
            new GeoCoordinate(40.10484d, -88.22874d),   //  Main Library, Urbana
            new GeoCoordinate(40.10149d, -88.23605d),   //  ARC, Champaign
            new GeoCoordinate(40.09076d, -88.21130d)    //  Orchard Downs, Champaign
        };

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Sample code to localize the ApplicationBar
            BuildLocalizedApplicationBar();

            // Fire off logic once the page loads
            Loaded += MainPage_Loaded;
        }

        /// <summary>
        /// Event fired when all main page elements are loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            TimeSpan initialTimeSpan = new TimeSpan(0, 0, 1);

            // Set off GPS timer
            if (DEMO)
            {
                StartDemoWalk();
            }
            else
            {
                LocationTimer.Tick += new EventHandler(LocateUser);
                LocationTimer.Interval = initialTimeSpan;
                LocationTimer.Start();
            }
            // Set off drawing timer
            DrawingTimer.Tick += new EventHandler(DrawMapMarkers);
            DrawingTimer.Interval = initialTimeSpan;
            DrawingTimer.Start();

            // Set off MTD scanning timer
            BusStopScanTimer.Tick += new EventHandler(LocateBusStops);
            BusStopScanTimer.Interval = initialTimeSpan;
            BusStopScanTimer.Start();

            // Set off MTD bus scanning timer
            BusScanTimer.Tick += new EventHandler(LocateBuses);
            BusScanTimer.Interval = initialTimeSpan;
            BusScanTimer.Start();

            // Set off Compass
            CompassTimer.Tick += new EventHandler(DisplayCurrentReading);
            CompassTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            CompassTimer.Start();
        }
        
        /// <summary>
        /// Demo Walk simulation
        /// </summary>
        public void StartDemoWalk()
        {
            MyCoordinate = DemoWalkCoordinates.First();
            MyMap.SetView(MyCoordinate, 13d);
            MyQuery = new RouteQuery();

            MyQuery.Waypoints = DemoWalkCoordinates;
            MyQuery.QueryCompleted += (object querySender, QueryCompletedEventArgs<PhoneServices.Route> queryEventArgs) =>
            {
                if (queryEventArgs.Error == null)
                {
                    // Update and draw the new map route
                    MyMapRoute = new MapRoute(queryEventArgs.Result);
                    DemoVisitCoordinates = MyMapRoute.Route.Geometry.ToList();
                    MyMap.AddRoute(MyMapRoute);
                    MyQuery.Dispose();
                }

                DemoTimer.Tick += new EventHandler(DemoWalk);
                DemoTimer.Interval = new TimeSpan(0, 0, 2);
                DemoTimer.Start();
            };
            MyQuery.QueryAsync();
        }

        private void DemoWalk(object sender, EventArgs e)
        {
            if (DemoVisitCoordinates.Count < 1)
            {
                DemoTimer.Stop();
                MessageBox.Show("Demo Walk ended");
                return;
            }

            MyCoordinate = DemoVisitCoordinates.ElementAt(0);
            DemoVisitCoordinates.RemoveAt(0);
            
            MyMap.SetView(MyCoordinate, MyMap.ZoomLevel);
        }

        /// <summary>
        /// Use GPS to locate the user and mark his position on map
        /// </summary>
        private async void LocateUser(object sender, EventArgs e)
        {
            LocationTimer.Stop();

            Geolocator geolocator = new Geolocator();
            geolocator.DesiredAccuracy = PositionAccuracy.High;

            try
            {
                // Send a request for user location asynchronously
                Geoposition currentPosition = await geolocator.GetGeopositionAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
                GPSAccuracy = currentPosition.Coordinate.Accuracy;
            
                // Update variables one the request response comes in
                // Fire events that were blocked
                Dispatcher.BeginInvoke(() =>
                {
                    GeoCoordinate newCoordinate = new GeoCoordinate(currentPosition.Coordinate.Latitude, currentPosition.Coordinate.Longitude);
                    if (MyCoordinate == null)
                    {
                        MyCoordinate = newCoordinate;
                        MyMap.SetView(MyCoordinate, 15d);
                    }
                    else if (MyCoordinate != newCoordinate)
                    {
                        // Moves Map view to your coordinate, not always desired
                        // TODO: Add a button for the user to move to current location when it's out of center
                        //MyMap.SetView(MyCoordinate, MyMap.ZoomLevel);
                    }

                    LocationTimer.Interval = new TimeSpan(0, 0, 5);
                    LocationTimer.Start();
                });
            }
            catch
            {
                MessageBox.Show("Current location cannot be obtained. Check that location service is turned on in phone settings.");
            }
        }

        /// <summary>
        /// Use MTD API to locate a specific number of bus stops closest to you
        /// </summary>
        public void LocateBusStops(object sender, EventArgs e)
        {
            if (MyCoordinate == null) return;

            BusStopScanTimer.Stop();

            // Initialize API client and send a request
            WsServiceClient client = new WsServiceClient();
            client.GetStopsByLatLonAsync(MTDAPI.API_KEY, (Decimal)MyCoordinate.Latitude, (Decimal)MyCoordinate.Longitude, 10, String.Empty);

            // Set the complete event handler
            client.GetStopsByLatLonCompleted += 
                (object requestSender, GetStopsByLatLonCompletedEventArgs requestEventArgs) =>
                {
                    rsp result = requestEventArgs.Result;
                    if (result.stops.Count > 0)
                    {
                        LocalBusStops.Clear();

                        for (int i = 0; i < result.stops.Count; i++)
                        {
                            Stop stop = result.stops[i];
                            StopPoint stopPoint = stop.stop_points[0];

                            BusStop busStop = new BusStop
                            {
                                Name = stop.stop_name,
                                Coordinate = new GeoCoordinate((Double)stopPoint.stop_lat, (Double)stopPoint.stop_lon),
                                MTDId = stop.stop_id
                            };

                            LocalBusStops.Add(busStop);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Couldn't find any bus stops nearby");
                    }

                    // Restart the timer
                    BusStopScanTimer.Interval = new TimeSpan(0, 0, 2);     // 10 seconds
                    BusStopScanTimer.Start();
                };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void LocateBuses(object sender, EventArgs e)
        {
            if (MyCoordinate == null) return;

            BusScanTimer.Interval = new TimeSpan(0, 0, 10);      

            // Initialize API client and send a request
            WsServiceClient client = new WsServiceClient();
            client.GetVehiclesAsync(MTDAPI.API_KEY);
            client.GetVehiclesCompleted += 
                (object requestSender, GetVehiclesCompletedEventArgs requestEventArgs) =>
                {
                    rsp result = requestEventArgs.Result;
                    if (result.vehicles.Count > 0)
                    {
                        LocalBuses.Clear(); 
                        LocalBuses = new HashSet<Bus>();

                        foreach (vehicle MTDbus in result.vehicles)
                        {
                            GeoCoordinate busCoordinate = new GeoCoordinate((Double)MTDbus.location.lat, (Double)MTDbus.location.lon);
                            Bus bus = new Bus
                            {
                                Coordinate = busCoordinate,
                                MTDId = MTDbus.vehicle_id
                            };
                            LocalBuses.Add(bus);
                        }
                    }
                };
        }

        public void LocateMyBusDepartures(object sender, EventArgs e)
        {
            if (MyBusStop != null)
            {
                // Initialize API client and send a request
                WsServiceClient client = new WsServiceClient();
                client.GetDeparturesByStopAsync(MTDAPI.API_KEY, MyBusStop.MTDId, String.Empty, 60, 5);
                client.GetDeparturesByStopCompleted +=
                    (object requestSender, GetDeparturesByStopCompletedEventArgs requestEventArgs) =>
                    {
                        rsp result = requestEventArgs.Result;
                        if (result.departures.Count > 0)
                        {
                            MTDDepartures.Clear();
                            MTDDepartures = new HashSet<departure>(result.departures.ToArray());
                        }
                    };
            }
        }

        /// <summary>
        /// Redraws all points of interest on the map
        /// </summary>
        private void DrawMapMarkers(object sender, EventArgs e)
        {
            MyMap.Layers.Clear();
            MarkerMapLayer = new MapLayer();
         
            // Draw marker for current position
            if (MyCoordinate != null)
            {
                DrawAccuracyRadius(MarkerMapLayer);
                DrawMapMarker(MyCoordinate, Colors.Red, MarkerMapLayer);
            }

            // Draw markers for nearby bus stops
            foreach (BusStop busStop in LocalBusStops)
            {
                DrawMapMarker(busStop.Coordinate, Colors.Blue, MarkerMapLayer);
            }

            // Drawmarkers for nearby buses
            foreach (Bus bus in LocalBuses)
            {
                double distanceTo = CoordinateMath.DistanceBetween(MyCoordinate, bus.Coordinate);
                if (distanceTo <= ScanRadiusInMetres)
                {
                    DrawMapMarker(bus.Coordinate, Colors.Green, MarkerMapLayer);
                }
            }

            MyMap.Layers.Add(MarkerMapLayer);

            // Restart the timer
            DrawingTimer.Interval = new TimeSpan(0, 0, 1);
            DrawingTimer.Start();
        }

        /// <summary>
        /// Draw a circle around user's location to show error
        /// </summary>
        /// <param name="mapLayer"></param>
        private void DrawAccuracyRadius(MapLayer mapLayer)
        {
            // The ground resolution (in meters per pixel) varies depending on the level of detail
            // and the latitude at which it’s measured. It can be calculated as follows:
            double metersPerPixels = (Math.Cos(MyCoordinate.Latitude * Math.PI / 180) * 2 * Math.PI * 6378137) / (256 * Math.Pow(2, MyMap.ZoomLevel));
            double radius = GPSAccuracy / metersPerPixels;

            Ellipse ellipse = new Ellipse();
            ellipse.Width = radius * 2;
            ellipse.Height = radius * 2;
            ellipse.Fill = new SolidColorBrush(Color.FromArgb(75, 200, 0, 0));

            MapOverlay overlay = new MapOverlay();
            overlay.Content = ellipse;
            overlay.GeoCoordinate = new GeoCoordinate(MyCoordinate.Latitude, MyCoordinate.Longitude);
            overlay.PositionOrigin = new Point(0.5, 0.5);
            mapLayer.Add(overlay);
        }

        /// <summary>
        /// Draws a colored marker for a coordinate on a map layer
        /// </summary>
        /// <param name="coordinate"></param>
        /// <param name="color"></param>
        /// <param name="mapLayer"></param>
        private void DrawMapMarker(GeoCoordinate coordinate, Color color, MapLayer mapLayer)
        {
            // Create a map marker
            Polygon polygon = new Polygon();
            polygon.Points.Add(new Point(0, 0));
            polygon.Points.Add(new Point(0, 75));
            polygon.Points.Add(new Point(25, 0));
            polygon.Fill = new SolidColorBrush(color);
            
            // Enable marker to be tapped for location information
            polygon.Tag = new GeoCoordinate(coordinate.Latitude, coordinate.Longitude);
            polygon.MouseLeftButtonUp += new MouseButtonEventHandler(Marker_Click);

            // Create a MapOverlay and add marker
            MapOverlay overlay = new MapOverlay();
            overlay.Content = polygon;
            overlay.GeoCoordinate = new GeoCoordinate(coordinate.Latitude, coordinate.Longitude);
            overlay.PositionOrigin = new Point(0.0, 1.0);
            mapLayer.Add(overlay);
        }
        
        /// <summary>
        /// Event fired whenever any of the markers are clicked
        /// Draws a path from user's location to the clicked marker
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Marker_Click(object sender, EventArgs e)
        {
            Polygon p = (Polygon)sender;
            GeoCoordinate geoCoordinate = (GeoCoordinate)p.Tag;

            MyQuery = new RouteQuery();

            // Emtpy coordinates from previous queries
            MyCoordinates.Clear();

            // Add own coordinate
            MyCoordinates.Add(MyCoordinate);

            // Add destination coordinate
            MyCoordinates.Add(geoCoordinate);
            MyQuery.Waypoints = MyCoordinates;
            MyQuery.QueryCompleted += (object querySender, QueryCompletedEventArgs<PhoneServices.Route> queryEventArgs) =>
                {
                    if (queryEventArgs.Error == null)
                    {
                        // Remove an existing route
                        if (MyMapRoute != null)
                        {
                            MyMap.RemoveRoute(MyMapRoute);
                        }

                        // Update and draw the new map route
                        MyMapRoute = new MapRoute(queryEventArgs.Result);
                        MyMap.AddRoute(MyMapRoute);
                        MyQuery.Dispose();
                    }
                };
            MyQuery.QueryAsync(); 
        }

        /// <summary>
        /// Get Current Compass reading
        /// </summary>
        private void DisplayCurrentReading(object sender, object args)
        {
            CompassReading reading = Compass.GetCurrentReading();
            if (reading != null)
            {
                MyTransform.Rotation = reading.HeadingTrueNorth ?? reading.HeadingMagneticNorth;
            }
        }

        /// <summary>
        /// Reverse Gecoding, used to convert geocoordinate (lat,lon) to a physical address (street, country, ...)
        /// 
        /// Currently unused. Even setup:
        ///     MyReverseGeocodeQuery = new ReverseGeocodeQuery();
        ///     MyReverseGeocodeQuery.GeoCoordinate = new GeoCoordinate(geoCoordinate.Latitude, geoCoordinate.Longitude);
        ///     MyReverseGeocodeQuery.QueryCompleted += ReverseGeocodeQuery_QueryCompleted;
        ///     MyReverseGeocodeQuery.QueryAsync();
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReverseGeocodeQuery_QueryCompleted(object sender, QueryCompletedEventArgs<IList<MapLocation>> e)
        {
            if (e.Error == null)
            {
                MyQuery = new RouteQuery();

                // Emtpy coordinates from previous queries
                MyCoordinates.Clear();

                // Add own coordinate
                MyCoordinates.Add(MyCoordinate);

                // Add destination coordinate
                MyCoordinates.Add(e.Result[0].GeoCoordinate);
                MyQuery.Waypoints = MyCoordinates;
                //MyQuery.QueryCompleted += MyQuery_QueryCompleted;
                //MyQuery.QueryAsync();
                //MyReverseGeocodeQuery.Dispose();
            }
        }

        /// <summary>
        /// Sample code for building a localized ApplicationBar
        /// </summary>
        private void BuildLocalizedApplicationBar()
        {
            // Set the page's ApplicationBar to a new instance of ApplicationBar.
            ApplicationBar = new ApplicationBar();

            // Create a new button and set the text value to the localized string from AppResources.
            ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
            appBarButton.Text = AppResources.AppBarButtonText;
            ApplicationBar.Buttons.Add(appBarButton);

            // Create a new menu item with the localized string from AppResources.
            ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
            ApplicationBar.MenuItems.Add(appBarMenuItem);
        }
    }
}