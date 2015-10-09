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
using Gadgeteer.Modules.GHIElectronics;
using Gadgeteer.Modules.OrionRobotics;

namespace Braitenberg
{
    public partial class Program
    {
        GT.Timer timer = new GT.Timer(100);
        GT.SocketInterfaces.AnalogInput capteurDroite;
        GT.SocketInterfaces.AnalogInput capteurCentre;
        GT.SocketInterfaces.AnalogInput capteurGauche;
        int vitesseMax = 64;

        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            capteurDroite = BrkCapteurs.CreateAnalogInput(GT.Socket.Pin.Three);
            capteurCentre = BrkCapteurs.CreateAnalogInput(GT.Socket.Pin.Five);
            capteurGauche = BrkCapteurs.CreateAnalogInput(GT.Socket.Pin.Four);
            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            Debug.Print("Program Started");

            double capteurDroiteValue = capteurDroite.ReadProportion();
            double capteurCentreValue = capteurCentre.ReadProportion();
            double capteurGaucheValue = capteurGauche.ReadProportion();

            roboClaw.Configure(38400, GT.SocketInterfaces.SerialParity.None, GT.SocketInterfaces.SerialStopBits.One, 8);
            if (!roboClaw.IsOpen())
            {
                roboClaw.Open();
            }          
        }

        void AvanceMoteurGauche(double _pValeurCapteur)
        {
            int vitesse = vitesseMax * (int)_pValeurCapteur;
            roboClaw.M1Speed(vitesse);
        }

        void AvanceMoteurDroit(double _pValeurCapteur)
        {
            int vitesse = vitesseMax * (int)_pValeurCapteur;
            roboClaw.M1Speed(vitesse);
        }

        void RalentirMoteur(double _pValeurCapteur) 
        {
            int vitesse = vitesseMax * (int)_pValeurCapteur;
            roboClaw.MixedSpeed(vitesse, vitesse);
        }
        
        void ShowMessage(int ligne, string message)
        {
            charDisplay.Clear();
            charDisplay.CursorHome();
            charDisplay.SetCursorPosition(ligne - 1, 0);
            charDisplay.Print(message);
        }
    }
}
