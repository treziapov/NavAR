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

using Windows.Devices.Geolocation;

using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Maps.Services;
using Microsoft.Phone.Maps.Controls;

using NavAR.MTD;
using NavAR.MTDService;

namespace NavAR
{
    public partial class MainPage : PhoneApplicationPage
    {
        private static ReverseGeocodeQuery MyReverseGeocodeQuery = null;
        private static GeoCoordinate MyCoordinate = null;
        private static HashSet<MTDService.Stop> MyBusStops = new HashSet<Stop>();
        private double _accuracy = 0.0;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();

            Loaded += MainPage_Loaded;
        }

        /// <summary>
        /// Event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            LocateUser();
        }

        /// <summary>
        /// Helper method to update MyLocationMap with current GPS coordinates
        /// </summary>
        private async void LocateUser()
        {
            Geolocator geolocator = new Geolocator();
            geolocator.DesiredAccuracy = PositionAccuracy.High;

            try
            {
                Geoposition currentPosition = await geolocator.GetGeopositionAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
                _accuracy = currentPosition.Coordinate.Accuracy;
            
                Dispatcher.BeginInvoke(() =>
                {
                    MyCoordinate = new GeoCoordinate(currentPosition.Coordinate.Latitude, currentPosition.Coordinate.Longitude);
                    MyMap.SetView(MyCoordinate, 15d);
                    LocateBusStops();
                    DrawMapMarkers();
                });
            }
            catch
            {
                // Couldn't get current location - location might be disabled in settings
                MessageBox.Show("Current location cannot be obtained. Check that location service is turned on in phone settings.");
            }
        }

        public void LocateBusStops()
        {
            MTDService.WsServiceClient client = new MTDService.WsServiceClient();
            client.GetStopsByLatLonAsync(MTDAPI.API_KEY, (Decimal)MyCoordinate.Latitude, (Decimal)MyCoordinate.Longitude, 10, "");
            client.GetStopsByLatLonCompleted += GetStopsByLatLonRequest_Completed;
        }

        void GetStopsByLatLonRequest_Completed(object sender, MTDService.GetStopsByLatLonCompletedEventArgs e)
        {
            MTDService.rsp result = e.Result;
            if (result.stops.Count > 0)
            {
                MyBusStops = new HashSet<Stop>(result.stops);
                DrawMapMarkers();
            }
            else
            {
                MessageBox.Show("Couldn't find any bus stops nearby");
            }
        }

        private void DrawMapMarkers()
        {
            MyMap.Layers.Clear();
            MapLayer mapLayer = new MapLayer();
         
            // Draw marker for current position
            if (MyCoordinate != null)
            {
                DrawAccuracyRadius(mapLayer);
                DrawMapMarker(MyCoordinate, Colors.Red, mapLayer);
            }

            // Draw markers for nearby bus stops
            foreach (MTDService.Stop busStop in MyBusStops)
            {
                MTDService.StopPoint stopPoint = busStop.stop_points.First();
                GeoCoordinate busStopCoord = new GeoCoordinate((Double)stopPoint.stop_lat, (Double)stopPoint.stop_lon);
                DrawMapMarker(busStopCoord, Colors.Blue, mapLayer);
            }

            MyMap.Layers.Add(mapLayer);
        }


        private void DrawAccuracyRadius(MapLayer mapLayer)
        {
            // The ground resolution (in meters per pixel) varies depending on the level of detail
            // and the latitude at which it’s measured. It can be calculated as follows:
            double metersPerPixels = (Math.Cos(MyCoordinate.Latitude * Math.PI / 180) * 2 * Math.PI * 6378137) / (256 * Math.Pow(2, MyMap.ZoomLevel));
            double radius = _accuracy / metersPerPixels;

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
        
        private void Marker_Click(object sender, EventArgs e)
        {
            Polygon p = (Polygon)sender;
            GeoCoordinate geoCoordinate = (GeoCoordinate)p.Tag;
            MyReverseGeocodeQuery = new ReverseGeocodeQuery();
            MyReverseGeocodeQuery.GeoCoordinate = new GeoCoordinate(geoCoordinate.Latitude, geoCoordinate.Longitude);
            MyReverseGeocodeQuery.QueryCompleted += ReverseGeocodeQuery_QueryCompleted;
            MyReverseGeocodeQuery.QueryAsync();
        }
    
        private void ReverseGeocodeQuery_QueryCompleted(object sender, QueryCompletedEventArgs<IList<MapLocation>> e)
        {
            if (e.Error == null)
            {
                if (e.Result.Count > 0)
                {
                    MapAddress address = e.Result[0].Information.Address;
                    String msgBoxText = "";
                
                    if (address.Country.Length > 0) msgBoxText += "\n" + address.Country;
                    MessageBox.Show(msgBoxText, "Map Explorer", MessageBoxButton.OK);
                } 
            }
        }

        // Sample code for building a localized ApplicationBar
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