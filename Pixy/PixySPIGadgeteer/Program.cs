using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;

namespace PixySPIGadgeteer
{
    public partial class Program
    {
        PixyCam _Cam;

        void ProgramStarted()
        {
            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            Debug.Print("Program Started");

            _Cam = new PixyCam(3); //change this to use whatever socket you've plugged the pixycam into

            Thread t = new Thread(new ThreadStart(GetBlocks));
            t.Start();

        }

        //get the block locations from the camera
        private void GetBlocks()
        {

            while (true)
            {
                _Cam._Bytes = new System.Text.StringBuilder();
                DateTime start = DateTime.Now;
                int numblocks = _Cam.GetBlocks(10);
                int ms = DateTime.Now.Subtract(start).Milliseconds;

                Debug.Print("============================================");
                Debug.Print("Geting blocks took:" + ms.ToString() +" ms");
                Debug.Print("Bytes received over SPI: " + _Cam._Bytes.ToString());

                if (numblocks > 0)
                {
                    //print the blocks
                    for (int i = 0; i < numblocks; i++)
                    {
                        if (_Cam.Blocks[i] != null) Debug.Print(_Cam.Blocks[i].ToString());
                    }
                }
                Thread.Sleep(100); //comment this out if you need maximum frame rate (upto 50fps) - its included for debugging only so 
                Mainboard.SetDebugLED(false); //turn the debug light off (we've finished detecting blocks)
            }
        }

       
    }
}
