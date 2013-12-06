#define DEBUG_AGENT
using System.Diagnostics;
using System.Windows;
using Microsoft.Phone.Scheduler;
using Microsoft.Phone.Shell;
using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;
using System.Linq;
using System;

using GART;
using System.Threading;
using GART.Data;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using IsolatedStorage;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.IO;

using ToolStackPNGWriterLib;
using LiveTileScheduledTaskAgent.MTDService;

namespace LiveTileScheduledTaskAgent
{
    public class ScheduledAgent : ScheduledTaskAgent
    {
        private Size TILE_SIZE = new Size(691, 336);
        private String TILE_TEXT_IMAGE_URI = "shared/shellcontent/tile_text.png";

        /// <remarks>
        /// ScheduledAgent constructor, initializes the UnhandledException handler
        /// </remarks>
        static ScheduledAgent()
        {
            // Subscribe to the managed exception handler
            Deployment.Current.Dispatcher.BeginInvoke(delegate
            {
                Application.Current.UnhandledException += UnhandledException;
            });
        }

        /// Code to execute on Unhandled Exceptions
        private static void UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                Debugger.Break();
            }
        }

        /// <summary>
        /// Agent that runs a scheduled task
        /// </summary>
        /// <param name="task">
        /// The invoked task
        /// </param>
        /// <remarks>
        /// This method is called when a periodic or resource intensive task is invoked
        /// Live Tile update logic goes here
        /// </remarks>
        protected override void OnInvoke(ScheduledTask task)
        {
            Deployment.Current.Dispatcher.BeginInvoke(StartCreateTileImage);

#if DEBUG_AGENT
            ScheduledActionService.LaunchForTest(task.Name, TimeSpan.FromSeconds(10));
            System.Diagnostics.Debug.WriteLine("Periodic task is started again: " + task.Name);
#endif     

            NotifyComplete();   // Do not remove this
        }

        public void CompensateForRender(int[] bitmapPixels)
        {
            if (bitmapPixels.Length == 0) return;
 
            for (int i = 0; i < bitmapPixels.Length; i++)
            {
                uint pixel = unchecked((uint)bitmapPixels[i]);
 
                double a = (pixel >> 24) & 255;
                if ((a == 255) || (a == 0)) continue;
 
                double r = ( pixel >> 16 ) & 255;
                double g = ( pixel >> 8 ) & 255;
                double b = ( pixel ) & 255;
 
                double factor = 255 / a;
                uint newR = (uint)Math.Round(r * factor);
                uint newG = (uint)Math.Round(g * factor);
                uint newB = (uint)Math.Round(b * factor);
 
                // compose
                bitmapPixels[i] = unchecked((int)(( pixel & 0xFF000000 ) | ( newR << 16 ) | ( newG << 8 ) | newB ));
            }
        }

        /// <summary>
        /// Load the default bmp image for the tile back and front
        /// </summary>
        private void StartCreateTileImage()
        {
            var bmp = new BitmapImage
            {
                CreateOptions = BitmapCreateOptions.None
            };

            bmp.ImageOpened += OnBackgroundBmpOpened;
            bmp.UriSource = new Uri("TileImages/back.png", UriKind.RelativeOrAbsolute);
        }

        /// <summary>
        /// Default bmp images are loaded, start processing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBackgroundBmpOpened(object sender, RoutedEventArgs e)
        {
            var img = sender as BitmapImage;
            var wbmp = CreateTileImage(img);
            SaveTileImage(wbmp);
            UpdateTileIcon();
        }

        /// <summary>
        /// Create custom images for the Live Tile
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        private WriteableBitmap CreateTileImage(BitmapImage img)
        {
            var image = new Image 
            { 
                Source = img,
                Width = TILE_SIZE.Width,
                Height = TILE_SIZE.Height
            };
 
            // Initialize text to display on tile
            var textBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 30,
                Text = ""
            };

            // Read stops from the main app
            List<IsolatedStorage.StoredBusStop> sharedStops = IsolatedStorage.Manager.ReadStops();
            
            // Find some bus stop
            if (sharedStops == null || sharedStops.Count <= 0)
            {
                textBlock.Text += "No local poinits of interest";
            }
            else
            {
                for (int i = 0; i < Math.Min(sharedStops.Count, 3); i++)
                {
                    textBlock.Text += sharedStops[i].Name + "\n\t" + sharedStops[i].Departures + "\n";
                }
            }
 
            // Measure the actual size of the TextBlock, this is with
            // the characters full hight/width (includning char spacing)
            var width = textBlock.ActualWidth;
            var height = textBlock.ActualHeight;
 
            // Place the text in the lower right corner
            Canvas.SetLeft(textBlock, 10);
            Canvas.SetTop(textBlock, 60);
 
            // Create a canvas and place the image and text on it
            var canvas = new Canvas
            {
                Height = TILE_SIZE.Width,
                Width = TILE_SIZE.Height
            };
 
            // This is where the images is placed
            canvas.Children.Add(image);
            canvas.Children.Add(textBlock);
 
            // Render all of it to the WritableBitmap
            var wbmp = new WriteableBitmap((int)TILE_SIZE.Width, (int)TILE_SIZE.Height);
            wbmp.Render(canvas, null);
            wbmp.Invalidate();
 
            return wbmp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wbmp"></param>
        private void SaveTileImage(WriteableBitmap wbmp)
        {
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (store.FileExists(TILE_TEXT_IMAGE_URI))
                    store.DeleteFile(TILE_TEXT_IMAGE_URI);

                using (var stream = store.OpenFile(TILE_TEXT_IMAGE_URI, FileMode.OpenOrCreate))
                {
                    PNGWriter.DetectWBByteOrder();
                    PNGWriter.WritePNG(wbmp, stream);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateTileIcon()
        {
            FlipTileData tileData = new FlipTileData
            {
                WideBackBackgroundImage = new Uri("isostore:/" + TILE_TEXT_IMAGE_URI, UriKind.RelativeOrAbsolute)
            };

            var tile = ShellTile.ActiveTiles.First();
            try
            {
                tile.Update(tileData);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }   
        }

    }
}