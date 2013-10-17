using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Device.Location;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Threading;
using System.IO;
using System.Xml;
using System.Windows.Threading;

using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using PhoneServices = Microsoft.Phone.Maps.Services;

using NavAR.MTD;
using NavAR.MTDService;
using NavAR.Resources;

using Windows.Devices.Geolocation;

namespace NavAR
{
    public partial class MainPage : PhoneApplicationPage
    {
        // User location variables
        private GeoCoordinate MyCoordinate = null;
        private double GpsAccuracy = 0.0;

        // Local transit variables
        private HashSet<MTDService.Stop> MyBusStops = new HashSet<Stop>();

        // Route variables
        private RouteQuery MyQuery = null;
        private List<GeoCoordinate> MyCoordinates = new List<GeoCoordinate>();
        private MapRoute MyMapRoute = null;
        //private ReverseGeocodeQuery MyReverseGeocodeQuery = null;

        // Map graphics
        private MapLayer MarkerMapLayer = new MapLayer();

        // Timers
        private DispatcherTimer DrawingTimer = new DispatcherTimer();
        private DispatcherTimer LocationTimer = new DispatcherTimer();
        private DispatcherTimer MTDScanTimer = new DispatcherTimer();

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
            TimeSpan none = new TimeSpan(0, 0, 0);

            // Set off GPS timer
            LocationTimer.Tick += new EventHandler(LocateUser);
            LocationTimer.Interval = none;
            LocationTimer.Start();

            // Set off drawing timer
            DrawingTimer.Tick += new EventHandler(DrawMapMarkers);
            DrawingTimer.Interval = none;
            DrawingTimer.Start();

            // Set off MTD scanning timer
            MTDScanTimer.Tick += new EventHandler(LocateBusStops);
            MTDScanTimer.Interval = none;
            MTDScanTimer.Start();
        }

        /// <summary>
        /// Use GPS to locate the user and mark his position on map
        /// </summary>
        private async void LocateUser(object sender, EventArgs e)
        {
            Geolocator geolocator = new Geolocator();
            geolocator.DesiredAccuracy = PositionAccuracy.High;

            try
            {
                // Send a request for user location asynchronously
                Geoposition currentPosition = await geolocator.GetGeopositionAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
                GpsAccuracy = currentPosition.Coordinate.Accuracy;
            
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
                });
            }
            catch
            {
                MessageBox.Show("Current location cannot be obtained. Check that location service is turned on in phone settings.");
            }

            LocationTimer.Interval = new TimeSpan(0, 0, 5);
            LocationTimer.Start();
        }

        /// <summary>
        /// Use MTD API to locate a specific number of bus stops closest to you
        /// </summary>
        public void LocateBusStops(object sender, EventArgs e)
        {
            if (MyCoordinate != null)
            {
                // Initialize API client and send a request
                MTDService.WsServiceClient client = new MTDService.WsServiceClient();
                client.GetStopsByLatLonAsync(MTDAPI.API_KEY, (Decimal)MyCoordinate.Latitude, (Decimal)MyCoordinate.Longitude, 10, String.Empty);

                // Set the complete event handler
                client.GetStopsByLatLonCompleted += GetStopsByLatLonRequest_Completed;
            }

            MTDScanTimer.Interval = new TimeSpan(0, 0, 10);
            MTDScanTimer.Start();
        }

        /// <summary>
        /// Process and store nearby bus stop information
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GetStopsByLatLonRequest_Completed(object sender, MTDService.GetStopsByLatLonCompletedEventArgs e)
        {
            MTDService.rsp result = e.Result;
            if (result.stops.Count > 0)
            {
                MyBusStops = new HashSet<Stop>(result.stops);
            }
            else
            {
                MessageBox.Show("Couldn't find any bus stops nearby");
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
            foreach (MTDService.Stop busStop in MyBusStops)
            {
                MTDService.StopPoint stopPoint = busStop.stop_points.First();
                GeoCoordinate busStopCoord = new GeoCoordinate((Double)stopPoint.stop_lat, (Double)stopPoint.stop_lon);
                DrawMapMarker(busStopCoord, Colors.Blue, MarkerMapLayer);
            }

            MyMap.Layers.Add(MarkerMapLayer);

            // Restart the timer
            DrawingTimer.Interval = new TimeSpan(0, 0, 5);
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
            double radius = GpsAccuracy / metersPerPixels;

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
            MyQuery.QueryCompleted += MyQuery_QueryCompleted;
            MyQuery.QueryAsync(); 
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
                MyQuery.QueryCompleted += MyQuery_QueryCompleted;
                MyQuery.QueryAsync();
                //MyReverseGeocodeQuery.Dispose();
            }
        }

        /// <summary>
        /// Processes the route result and displays it on map, clearing the old route
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MyQuery_QueryCompleted(object sender, QueryCompletedEventArgs<PhoneServices.Route> e)
        {
            if (e.Error == null)
            {
                // Remove an existing route
                if (MyMapRoute != null)
                {
                    MyMap.RemoveRoute(MyMapRoute);
                }

                // Update and draw the new map route
                MyMapRoute = new MapRoute(e.Result);
                MyMap.AddRoute(MyMapRoute);
                MyQuery.Dispose();
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