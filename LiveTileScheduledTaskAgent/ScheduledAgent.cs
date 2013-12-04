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

namespace LiveTileScheduledTaskAgent
{
    public class ScheduledAgent : ScheduledTaskAgent
    {
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
            Random random = new Random();

            // Find the NavAR live tile
            ShellTile tile = ShellTile.ActiveTiles.First();
            if (tile != null)
            {
                // Read from the shared isolated storage
                List<IsolatedStorage.StoredBusStop> sharedStops = IsolatedStorage.Manager.ReadStops();
                
                // Create new data for the live tile
                FlipTileData tileData = new FlipTileData();
                tileData.Title = "Updated live tile title";
                tileData.Count = random.Next(99);
                tileData.WideBackContent = "Bus stops: ";

                // Find some bus stop
                if (sharedStops!= null && sharedStops.Count > 0)
                {
                    for (int i = 0; i < Math.Min(sharedStops.Count, 5); i++)
                    {
                        tileData.WideBackContent += sharedStops[i].Name + ",";
                    }
                    tileData.WideBackContent.Trim(',');
                }

                // Update the live tile
                tile.Update(tileData);
            }

#if DEBUG_AGENT
            ScheduledActionService.LaunchForTest(task.Name, TimeSpan.FromSeconds(10));
            System.Diagnostics.Debug.WriteLine("Periodic task is started again: " + task.Name);
#endif

            NotifyComplete();   // Do not remove this
        }
    }
}