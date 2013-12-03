#define DEBUG_AGENT
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
using Media = System.Windows.Media;
using WinPoint = System.Windows.Point;
using WinColor = System.Windows.Media.Color;

using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using Microsoft.Phone.Scheduler;

using Microsoft.Xna.Framework;
using PhoneServices = Microsoft.Phone.Maps.Services;

using NavAR.MTD;
using NavAR.MTDService;
using NavAR.Resources;
using NavAR.Entities;
using NavAR.Helpers;

using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;
using Windows.Phone.Devices.Notification;

using GART;
using GART.BaseControls;
using GART.Data;
using GART.Controls;

namespace NavAR
{
    public partial class MainPage : PhoneApplicationPage
    {
        private PeriodicTask PeriodicTask;
        private String PeriodicTaskName = "Update Live Tile";
        public bool EnableAgents = true;

        // Flags/Modes
        private readonly bool DEMO = false;
        
        // User Settings
        public bool Heading = true;

        // User location variables
        private GeoCoordinate MyCoordinate = null;
        private double GPSAccuracy = 0.0;

        // Route variables
        private RouteQuery MyQuery = null;
        private List<GeoCoordinate> MyCoordinates = new List<GeoCoordinate>();
        private MapRoute MyMapRoute = null;
        //private ReverseGeocodeQuery MyReverseGeocodeQuery = null;

        // MTD/transit variables
        private HashSet<Bus> LocalBuses = new HashSet<Bus>();
        private HashSet<BusStop> LocalBusStops = new HashSet<BusStop>();
        private BusStop MyBusStop = null;
        private HashSet<departure> MTDDepartures= new HashSet<departure>();

        private double ScanRadiusInMetres = 500d;

        // Map graphics
        private MapLayer MarkerMapLayer = new MapLayer();

        // Timers
        private DispatcherTimer DrawingTimer = new DispatcherTimer();
        private DispatcherTimer LocationTimer = new DispatcherTimer();
        private DispatcherTimer BusStopScanTimer = new DispatcherTimer();
        private DispatcherTimer BusScanTimer = new DispatcherTimer();
        private DispatcherTimer CompassTimer = new DispatcherTimer();
        private DispatcherTimer DemoTimer = new DispatcherTimer();
        private DispatcherTimer RealTimeBusStopScanner = new DispatcherTimer();

        // Compass
        private Compass Compass = Compass.GetDefault();

        //Vibration Variables
        private VibrationDevice notifyUserWithVibration = VibrationDevice.GetDefault();
        private String prevBusStop = "NONE";
        //private HashSet<BusStop> BusStopsInRealTime = new HashSet<BusStop>();

        // Test/Demo
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
            //BuildLocalizedApplicationBar();

            // Fire off logic once the page loads
            Loaded += MainPage_Loaded;
        }

        /// <summary>
        /// Event fired when all main page elements are loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
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

            // Set off RealTimeScanner
            RealTimeBusStopScanner.Tick += new EventHandler(LocateBusStopsInRealTime);
            RealTimeBusStopScanner.Interval = initialTimeSpan;
            RealTimeBusStopScanner.Start();

            // Start updating the live tile
            StartPeriodicAgent();
        }

        /// <summary>
        /// 
        /// </summary>
        private void StartPeriodicAgent()
        {
            // Check if old task is running, remove if it is
            EnableAgents = true;
            IEnumerable<PeriodicTask> actions = ScheduledActionService.GetActions<PeriodicTask>();
            PeriodicTask = ScheduledActionService.Find(PeriodicTaskName) as PeriodicTask;
            if (PeriodicTask != null)
            {
                try
                {
                    ScheduledActionService.Remove(PeriodicTaskName);
                }
                catch (Exception)
                {
                }
            }

            // Start a new task
            PeriodicTask = new PeriodicTask(PeriodicTaskName);
            PeriodicTask.Description = "This is a NavAR application's live tile update agent.";
            PeriodicTask.ExpirationTime = DateTime.Now.AddMinutes(5);
            try
            {
                ScheduledActionService.Add(PeriodicTask);

#if DEBUG_AGENT
                ScheduledActionService.LaunchForTest(PeriodicTaskName, TimeSpan.FromSeconds(10));
                System.Diagnostics.Debug.WriteLine("Periodic task is started: " + PeriodicTaskName);
#endif
            }
            catch (InvalidOperationException exception)
            {
                // User action is required to avoid the exception next time
                if (exception.Message.Contains("BNS Error: The action is disabled"))
                {
                    MessageBox.Show("Background agents for this application have been disabled by the user");
                }
            }
            // Otherwise, no user action is required
            catch (SchedulerServiceException)
            {

            }
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
                DemoTimer.Interval = new TimeSpan(0, 0, 3);
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
                                GeoLocation = new GeoCoordinate((Double)stopPoint.stop_lat, (Double)stopPoint.stop_lon),
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

        protected override void OnNavigatedFrom(System.Windows.Navigation.NavigationEventArgs e)
        {
            // Stop AR services
            ARDisplay.StopServices();

            base.OnNavigatedFrom(e);
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            // Start AR services
            ARDisplay.StartServices();

            base.OnNavigatedTo(e);
        }

        private void CameraButton_Click(object sender, EventArgs e)
        {
            UIHelper.ToggleVisibility(MyMap);
            UIHelper.ToggleVisibility(VideoPreview);
            UIHelper.ToggleVisibility(WorldView);
        }

        private void CenterButton_Click(object sender, EventArgs e)
        {
            if (MyCoordinate != null)
            {
                MyMap.SetView(MyCoordinate, MyMap.ZoomLevel);
            }
        }

        private void HeadingButton_Click(object sender, EventArgs e)
        {
            if (Heading)
            {
                MyMap.Heading = 0;
            }
            Heading = !Heading;
        }   

        /// <summary>
        /// Redraws all points of interest on the map
        /// </summary>
        private void DrawMapMarkers(object sender, EventArgs e)
        {
            MyMap.Layers.Clear();
            MarkerMapLayer = new MapLayer();

            HashSet<ARItem> currentItems = new HashSet<ARItem>(ARDisplay.ARItems);
            // Draw markers for nearby bus stops
            foreach (BusStop busStop in LocalBusStops)
            {
                DrawMapMarker(busStop.GeoLocation, Media.Colors.Blue, MarkerMapLayer);

                ARItem found = ARDisplay.ARItems.SingleOrDefault(item => (item.GetType() == typeof(BusStop)) && (item as BusStop).Name == busStop.Name);
                if (found != null)
                {
                    found.GeoLocation = busStop.GeoLocation;
                    currentItems.Remove(found);
                }
                else
                {
                    ARDisplay.ARItems.Add(busStop);
                }
                //ARDisplay.ARItems.Add(new ARItem() { GeoLocation = busStop.GeoLocation, Content = busStop.Name});
            }

            // Drawmarkers for nearby buses
            foreach (Bus bus in LocalBuses)
            {
                double distanceTo = CoordinateMath.DistanceBetween(MyCoordinate, bus.Coordinate);
                if (distanceTo <= ScanRadiusInMetres)
                {
                    DrawMapMarker(bus.Coordinate, Media.Colors.Green, MarkerMapLayer);
                    ARItem busARItem = new ARItem()
                    {
                        Content = bus.MTDId,
                        GeoLocation = bus.Coordinate
                    };

                    ARItem found = ARDisplay.ARItems.SingleOrDefault(item => (item.GetType() == typeof(Bus)) && (item as Bus).MTDId == bus.MTDId);
                    if (found != null)
                    {
                        found.GeoLocation = bus.GeoLocation;
                        currentItems.Remove(found);
                    }
                    else
                    {
                        ARDisplay.ARItems.Add(busARItem);
                    }
                }
            }

            // Remove items that went off radar
            foreach (ARItem item in currentItems)
            {
                ARDisplay.ARItems.Remove(item);
            }

            // Draw marker for current position
            if (MyCoordinate != null)
            {
                DrawAccuracyRadius(MarkerMapLayer);
                DrawMapMarker(MyCoordinate, Media.Colors.Red, MarkerMapLayer);
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
            ellipse.Fill = new SolidColorBrush(Media.Color.FromArgb(75, 200, 0, 0));

            MapOverlay overlay = new MapOverlay();
            overlay.Content = ellipse;
            overlay.GeoCoordinate = new GeoCoordinate(MyCoordinate.Latitude, MyCoordinate.Longitude);
            overlay.PositionOrigin = new WinPoint(0.5, 0.5);
            mapLayer.Add(overlay);
        }

        /// <summary>
        /// Draws a colored marker for a coordinate on a map layer
        /// </summary>
        /// <param name="coordinate"></param>
        /// <param name="color"></param>
        /// <param name="mapLayer"></param>
        private void DrawMapMarker(GeoCoordinate coordinate, WinColor color, MapLayer mapLayer)
        {
            // Create a map marker
            Polygon polygon = new Polygon();
            polygon.Points.Add(new WinPoint(0, 0));
            polygon.Points.Add(new WinPoint(0, 75));
            polygon.Points.Add(new WinPoint(25, 0));
            polygon.Fill = new SolidColorBrush(color);
            
            // Enable marker to be tapped for location information
            polygon.Tag = new GeoCoordinate(coordinate.Latitude, coordinate.Longitude);
            polygon.MouseLeftButtonUp += new MouseButtonEventHandler(Marker_Click);

            // Create a MapOverlay and add marker
            MapOverlay overlay = new MapOverlay();
            overlay.Content = polygon;
            overlay.GeoCoordinate = new GeoCoordinate(coordinate.Latitude, coordinate.Longitude);
            overlay.PositionOrigin = new WinPoint(0.0, 1.0);
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
                double angle = reading.HeadingTrueNorth ?? reading.HeadingMagneticNorth;
                MyTransform.Rotation = angle;

                if (Heading)
                {
                    MyMap.Heading = angle;
                }
            }
        }

        /// <summary>
        /// Reverse Gecoding, used to convert geocoordinate (lat,lon) to a physical address (street, country, ...)
        /// 
        /// Currently unused. Event setup:
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
        /// Use MTD API to locate a specific number of bus stops closest to you in real time
        /// </summary>
        public void LocateBusStopsInRealTime(object sender, EventArgs e)
        {
            if (MyCoordinate == null) return;

            RealTimeBusStopScanner.Stop();

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

                        for (int i = 0; i < result.stops.Count; i++)
                        {
                            Stop stop = result.stops[i];
                            StopPoint stopPoint = stop.stop_points[0];
                            System.Diagnostics.Debug.WriteLine(stop.distance);

                            //if distance between my location and stop is less than 300 feet, vibrate
                            if (stop.distance < 300 && prevBusStop != stop.stop_id)
                            {
                                notifyUserWithVibration.Vibrate(TimeSpan.FromSeconds(3));
                                prevBusStop = stop.stop_id;
                                MessageBox.Show("You are close to" + stop.stop_name);
                                break;
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Couldn't find any bus stops nearby");
                    }

                    // Restart the timer
                    RealTimeBusStopScanner.Interval = new TimeSpan(0, 0, 10);     // 10 seconds
                    RealTimeBusStopScanner.Start();
                };
        }

        /// <summary>
        /// Sample code for building a localized ApplicationBar
        /// </summary>
        //private void BuildLocalizedApplicationBar()
        //{
        //    // Set the page's ApplicationBar to a new instance of ApplicationBar.
        //    ApplicationBar = new ApplicationBar();

        //    // Create a new button and set the text value to the localized string from AppResources.
        //    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
        //    appBarButton.Text = AppResources.AppBarButtonText;
        //    ApplicationBar.Buttons.Add(appBarButton);

        //    // Create a new menu item with the localized string from AppResources.
        //    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
        //    ApplicationBar.MenuItems.Add(appBarMenuItem);
        //}
    }
}










































