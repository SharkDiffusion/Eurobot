using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.OrionRobotics;
using Gadgeteer.Modules;
using Gadgeteer.SocketInterfaces;
using Gadgeteer.Modules.GHIElectronics;

namespace RoboClawExample
{

    /* 
     * This program utilizes RoboClaw from Orion Robotics gadgeter driver ported.
     * The gadgeteer driver is ported from the original .Net Framework class library made by Orion Robotics
     * to .Net Micro Framework Gadgeteer module by Ahmed Mohsen on 15 February 2014
     * 
     * The driver uses packet serial mode 7 and supports any U or K socket
     * Physical connection can be made using G-Plug, Breakout, Extender, Terminal block
     * On any U or K socket connect Pin 4 to RoboClaw S1 and Pin 5 to S2.
     * If you are using different power sources for the gadgeteer board and RoboClaw,
     * then ground Pin 10 must be connected to any logic ground pin on RoboClaw.
     * 
     * The driver supports all RoboClaw boards with firmware version 4.
     */
    
    public partial class Program
    {
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            Debug.Print("Program Started");

            roboClaw.Configure(38400, SerialParity.None, SerialStopBits.One, 8);

            if (!roboClaw.IsOpen())
                roboClaw.Open();

            Thread.Sleep(5000);
            Demo();
            Thread.Sleep(5000);
        }

        private void Demo()
        {
            roboClaw.ST_M1Forward(64); //Cmd 0
            roboClaw.ST_M2Backward(64); //Cmd 5
            Thread.Sleep(2000);
            roboClaw.ST_M1Backward(64); //Cmd 1
            roboClaw.ST_M2Forward(64); //Cmd 6
            Thread.Sleep(2000);
            roboClaw.ST_M1Drive(96); //Cmd 6
            roboClaw.ST_M2Drive(32); //Cmd 7
            Thread.Sleep(2000);
            roboClaw.ST_M1Drive(32); //Cmd 6
            roboClaw.ST_M2Drive(96); //Cmd 7
            Thread.Sleep(2000);
            //stop motors
            roboClaw.ST_M1Drive(0);
            roboClaw.ST_M2Drive(0);
            Thread.Sleep(10000);
            roboClaw.ST_MixedForward(64); //Cmd 8
            Thread.Sleep(2000);
            roboClaw.ST_MixedBackward(64); //Cmd 9
            Thread.Sleep(2000);
            roboClaw.ST_MixedRight(64); //Cmd 10
            Thread.Sleep(2000);
            roboClaw.ST_MixedLeft(64); //Cmd 11
            Thread.Sleep(2000);
            roboClaw.ST_MixedDrive(32); //Cmd 12
            Thread.Sleep(2000);
            roboClaw.ST_MixedDrive(96); //Cmd 12
            Thread.Sleep(2000);
            roboClaw.ST_MixedTurn(32); //Cmd 13
            Thread.Sleep(2000);
            roboClaw.ST_MixedTurn(96); //Cmd 13
            Thread.Sleep(2000);
            //stop motors
            roboClaw.ST_MixedForward(0);
            Thread.Sleep(10000);
        }
    }
}
