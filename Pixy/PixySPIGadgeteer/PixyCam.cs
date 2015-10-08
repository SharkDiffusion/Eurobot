using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Threading;
using Gadgeteer;
using Gadgeteer.Modules;

namespace PixySPIGadgeteer
{
    //----------------------------------------------------------------------------------------
    // Written by Stuart Payne - Calgary,AB,Canada - 17 March 2015
    // This code is free to use without license, subject to the usual Microsoft licenses required for development etc.
    // Code is based upon the arduino code on the Pixy website. It is used to detect and track objects at a high frame rate without putting a big processing load on the Gadgeteer mainboard.
    // If you like the code, and use it or improve on it, please let me know on Twitter @stuincanada. Enjoy!
    // I will try and post a full article with pictures on http://stuincanada.wordpress.com/
   
    //spi documentation sample code: https://www.ghielectronics.com/docs/14/spi
    //gadgeteer socket wiring: http://www.gadgeteering.net/content/socket-types
    //pixi socket wiring: http://cmucam.org/projects/cmucam5/wiki/Pixy_Serial_Protocol
    //pixi sample code: http://cmucam.org/projects/cmucam5/wiki/Porting_Guide 
    //----------------------------------------------------------------------------------------


    //----------------------------------------------------------------------------------------
    //PLEASE READ BEFORE RUNNING CODE
    // Required hardware:
    //   1. CMUCAM5 from Charmed Labs
    //   2. Gadgeteer .NET mainboard - ideally a Raptor but any that support SPI should work
    //   3. USB power gadgeteer module - I use the USB Client DP module - https://www.ghielectronics.com/catalog/product/280
    //   4. Arduino cable that comes with the CMUCAM5 Pixy Camera (will need to cut the connector off the Arduino end)
    //   5. Gadgeteer .NET cable that you can cut one end off, or a Gadgeteer breakout module that you solder the Pixy cable to - https://www.ghielectronics.com/catalog/product/405

    // GETTING STARTED:
    //1. Plug in the pixycam to the usb port, use PixyMon windows app to configure the brightness and the signatures to detect objects etc.
    //2. In pixymon app, go to File-> configure, then to the Interface tab, and ensure 'data out port' is set to 0 (zero) - this ensures data is output using 'ICSP SPI' interface
    //3. Make your cable to connect the PixyCam to the Gadgeteer board as per instructions below
    //4. Plug the Gadgeteer board into USB, this will also power the PixyCam no problem in my tests
    //5. In program.cs file, ensure the code matches the socket number you plugged the pixycam into on the mainboard - change line: _Cam = new PixyCam(3); //I've plugged mine into socket 3
    //NOTE: This code has only been tested on a Raptor 400Mhz board. You may have difficulty keeping up with the 1Mhz SPI data rate on other boards, though it may work fine
    //NOTE: I have not tested this with color signature blocks but it should work
    //----------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------
    //Gadgeteer wiring for serial peripheral interface (SPI) (socket viewed from above)
    //      -------
    //      | 1  2| (pin 1 cable is red)
    //      | 3  4|
    // notch |5  6|
    //      | 7  8|
    //      | 9 10|
    //      -------
    //pin 1 3.3v, pin 2 5v, pin 10 GND
    //pin 6 is the chip-select (CS) line                   
    //pin 7 is the master-out/slave-in (MOSI) line
    //pin 8 is the master-in/slave-out (MISO) line
    //pin 9 is the clock (SCK) line
    //----------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------
    //pixycam wiring for SPI (socket viewed from above)
    //      -------
    //      | 1  2| (pin 1 cable is red)
    //      | 3  4|
    // notch |5  6|
    //      | 7  8| not connected
    //      | 9 10| not connected
    //      -------
    //pin 1: master-in/slave-out (MISO) line
    //pin 2: 5v
    //pin 3: clock (SCK) line
    //pin 4: master-out/slave-in (MOSI) line
    //pin 6: GND
    //----------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------
    // WARNING: Be careful, if you get this wiring wrong you may permanently damage your board or the Pixycam - take care!
    //----------------------------------------------------------------------------------------
    // Therefore connect gadgeteer to pixycam, you will need to cut the pixycam cable supplied, and splice it into a gadgeteer cable, or use a breakout module for gadgeteer.
    // The wiring required is:
    // gadgeteer pin 2 -> pixycam pin 2 (5v)
    // gadgeteer pin 7 -> pixycam pin 4 (MOSI)
    // gadgeteer pin 8 -> pixycam pin 1 (MISO)
    // gadgeteer pin 9 -> pixycam pin 3 (SCK)
    // gadgeteer pin 10-> pixycam pin 6 (GND) (or pixy pin 8 or pixy pin 10)
    //---------------------------------------------------------------------------------------

    public class PixyCam : Module
    {
        private byte _lastByte = 0;

        private const byte PIXY_INITIAL_ARRAYSIZE = 10;
        private const byte PIXY_MAXIMUM_ARRAYSIZE = 135;
        private const UInt16 PIXY_FRAME_START_WORD = 0xaa55;       //2 of these = start of new frame or one of these followed by...
        private const UInt16 PIXY_FRAME_START_WORD_CC = 0xaa56;    //one of these (cc=colour code object)
        private const UInt16 PIXY_START_WORD_WRONG_ORDER = 0x55aa; //need to resync if this is read (one byte out when reading)
        private const byte PIXY_SEND_SYNC_BYTE = 0x5a;             //5A 1011010 must send some bits while reading some bits
        private const byte PIXY_SEND_SYNC_BYTE_DATA = 0x5b;        //5B 1011011 must send some bits while reading some bits
       
        //cirucular queue for sending / receiving
        private const int PIXY_OUTBUF_SIZE = 64;
        private byte[] g_outBuf;
        private int g_outLen = 0;
        private int g_outWriteIndex = 0;
        private int g_outReadIndex = 0;

        //debugging
        public System.Text.StringBuilder _Bytes = new System.Text.StringBuilder();

        private SPI PixyCamSPI;
        private bool _SkipStart;
        private bool _IsColourBlock = false;

        private int _BlockArraySize = 0;
        private PixyBlock[] _Blocks;

        public PixyBlock[] Blocks {
            get
            {
                return _Blocks;
            }
            set
            {
                _Blocks = value;
            }
        }

        public PixyCam(int socketNumber)
        {
            //Raptor sockets 1,3, 11 support spi
            Socket spiSocket = Socket.GetSocket(socketNumber, true, this, null);
            spiSocket.EnsureTypeIsSupported('S', this); //will error if you've plugged it into a socket that doesnt support spi - look for a socket labeled 'S' on the board
            SPI.SPI_module spiModule = spiSocket.SPIModule;

            SPI.Configuration PixyCamSPIConfig = new SPI.Configuration(
                Cpu.Pin.GPIO_NONE,  //chip select port - not required for ICSPI like Arduino no slave select
                false,              //chip select active state - If true, the chip select port will be set to high when accessing the chip; if false, the chip select port will be set to low when accessing the chip
                0,                  //chip select setup time - amount of time that will elapse between the time at which the device is selected and the time at which the clock and the clock data transmission will start
                0,                  //chip select hold time -  specifies the amount of time that the chip select port must remain in the active state before the device is unselected
                true,               //clock idle state - If true, the SPI clock signal will be set to high while the device is idle; if false, the SPI clock signal will be set to low while the device is idle
                true,               //clock edge - If true, data is sampled on the SPI clock rising edge; if false, the data is sampled on the SPI clock falling edge
                1000,               //clock kHz (1mhz) - 1000=1Mhz sampling rate
                spiModule           //SPI module defined above
                );

            //if you get an error here, it might be because youve plugged in more than 1 SPI device (e.g. a screen) that's conflicting. 
            //Just try a different socket for the cam or the other SPI device and this usually goes away
            PixyCamSPI = new SPI(PixyCamSPIConfig); 
            _Blocks = new PixyBlock[PIXY_INITIAL_ARRAYSIZE];
            g_outBuf = new byte[PIXY_OUTBUF_SIZE];
        }

        //must send some data (any filler if no data in queue) to get data back OF SAME SIZE AS SENT
        private byte GetByte(byte dataInTheOutQueue)
        {
            byte[] buffer = new byte[1];
            PixyCamSPI.WriteRead(new byte[] { dataInTheOutQueue }, buffer);
            _lastByte = buffer[0];
            //debugging if you want to see what bytes were read from the cam
            _Bytes.Append(_lastByte);
            _Bytes.Append(",");

            return buffer[0];
        }

        //All values in the object block are 16-bit words, receive least-signifcant byte first (little endian).
        //So, for example, to get the sync word 0xaa55, you will get 0x55 (first byte) then 0xaa (second byte).
        //you also have to write some filler sync bits or real output data when reading so things dont get out of sync
        private UInt16 GetWord()
        {
            UInt16 w = 0;
            byte c;
            byte cout = 0;
            if (g_outLen != 0) //something ready in queue to be sent when we are receiving
            {
                w = GetByte(PIXY_SEND_SYNC_BYTE_DATA); //get first byte of word - dont send anthing but filler
                cout = g_outBuf[g_outReadIndex++]; //put queued data in cout (sent on 2nd get = see GetByte(cout) later)
                g_outLen--;
                if (g_outReadIndex == PIXY_OUTBUF_SIZE) //if at end of queue go back to start
                    g_outReadIndex = 0;
            }
            else //nothing to send just send some filler while getting first byte
                w = GetByte(PIXY_SEND_SYNC_BYTE); //get first byte of word - dont send anthing but filler sync bits

            c = GetByte(cout); //SEND THE QUEUED DATA OUT (and get 2nd byte of word)

            w = (UInt16)(((UInt16)w) << 8); //move 2nd byte to 1st byte (msb)
            w |= c; //include the 2nd byte in the word

            //Debug.Print("read word:" + w);
            return w;
        }

        //put the data into the out queue, this will automatically be sent when
        //GetByte is called when reading some data (data is being read all the time)
        //(data has to be sent when data is received so as to use as sync bits)
        private bool Send(byte[] data)
        {
            int i = 0;
            int len = data.Length;

            // check to see if we have enough space in our circular queue
            if (g_outLen + len > PIXY_OUTBUF_SIZE)
                return false;

            g_outLen += len;
            for (i = 0; i < len; i++)
            {
                g_outBuf[g_outWriteIndex++] = data[i];
                if (g_outWriteIndex == PIXY_OUTBUF_SIZE)
                    g_outWriteIndex = 0;
            }
            return (len>0);
        }

        //find the start of the frame - there could be 0,1 or many blocks per frame
        private bool GetStartOfFrame()
        {
            UInt16 w;
            UInt16 lastw = 0xffff;
           
            while (true)
            {
                w = GetWord();
                if (w == 0 && lastw == 0)
                {
                    Thread.Sleep(10); //wait a bit before trying to find the start of the frame
                    return false;
                }
                else if (w == PIXY_FRAME_START_WORD && lastw == PIXY_FRAME_START_WORD)
                {
                    _IsColourBlock = false;
                    return true;
                }
                else if (w == PIXY_FRAME_START_WORD_CC && lastw == PIXY_FRAME_START_WORD)
                {
                    _IsColourBlock = true;
                    return true;
                }
                else if (w == PIXY_START_WORD_WRONG_ORDER)
                {
                    GetByte(0); // resync
                }
                lastw = w;
            }
        }

        /// <summary>
        /// Gets the co-ordinates of the blocks found (if any)
        /// </summary>
        /// <param name="maxBlocks"></param>
        /// <returns>The number of blocks found - upto maxBlocks</returns>
        public int GetBlocks(int maxBlocks)
        {

            UInt16 w = 0;
            int blockCount = 0;
            UInt16 checksum = 0;
            UInt16 sum = 0;
            PixyBlock block;

            if (!_SkipStart)
            {
                if (GetStartOfFrame() == false)
                {
                    return 0;
                }
                else
                {
                    Mainboard.SetDebugLED(true); //turn the debug light on the board to signify we've found the start of the frame
                }
            }
            else
            {
                _SkipStart = false;
            }
            
            while (blockCount < maxBlocks && blockCount < PIXY_MAXIMUM_ARRAYSIZE)
            {
                sum = 0;
                checksum = GetWord();
                if (checksum == PIXY_FRAME_START_WORD) // we've reached the beginning of the next frame
                {
                    _SkipStart = true;
                    _IsColourBlock = false;
                    break;
                }
                else if (checksum == PIXY_FRAME_START_WORD_CC)
                {
                    _SkipStart = true;
                    _IsColourBlock = true;
                    break;
                }
                else if (checksum==0)
                {
                    break;
                }

                if (blockCount > _BlockArraySize)
                {
                    Resize();
                }

                block = new PixyBlock();

                block.Signature = GetWord();
                sum += block.Signature;

                block.X = GetWord(); 
                sum += block.X;

                block.Y = GetWord();
                sum += block.Y;

                block.Width = GetWord();
                sum += block.Width;

                block.Height = GetWord();
                sum += block.Height;

                if (!_IsColourBlock)
                    block.ColorBlockAngle = 0;
                else
                {
                    block.ColorBlockAngle = GetWord();
                    sum += block.ColorBlockAngle;
                }

                block.Area = (int) (block.Height * block.Width);
                _Blocks[blockCount] = block;

                if (checksum == sum)
                {
                    blockCount++;
                }
                else
                {
                    Debug.Print("checksum error");
                }

                w = GetWord();
                if (w == PIXY_FRAME_START_WORD || w == PIXY_FRAME_START_WORD_CC)
                {
                    block.IsColorBlock = _IsColourBlock;
                }
                else
                    break;
            }
            return blockCount;
        }

        private void Resize()
        {
            _BlockArraySize += PIXY_INITIAL_ARRAYSIZE;
            PixyBlock[] newBlocks = new PixyBlock[_BlockArraySize];
            _Blocks.CopyTo(newBlocks, 0);
            _Blocks = newBlocks;
        }



        //0 to 255 - not tested
        public bool SetBrightness(byte brightness)
        {
            byte PIXY_CAM_BRIGHTNESS_SYNC = 0xfe;
            byte[] buffer = new byte[3] { 0x00, PIXY_CAM_BRIGHTNESS_SYNC, brightness };
            return Send(buffer);
        }


        //0 to 255 - not tested
        public bool SetLED(bool redLED, bool greenLED, bool blueLED)
        {
            byte red = 0;
            byte green = 0;
            byte blue = 0;
            if (redLED) red = 255;
            if (greenLED) green = 255;
            if (blueLED) blue = 255;
            byte PIXY_LED_SYNC = 0xFD;
            byte[] buffer = new byte[5] { 0x00, PIXY_LED_SYNC, red, green, blue };
            return Send(buffer);
        }
    }

    public class PixyBlock
    {
        /*
        Bytes    16-bit words   Description
        ----------------------------------------------------------------
        0, 1     0              sync (0xaa55)
        2, 3     1              checksum (sum of all 16-bit words 2-6)
        4, 5     2              signature number
        6, 7     3              x center of object
        8, 9     4              y center of object
        10, 11   5              width of object
        12, 13   6              height of object
         */
        public UInt16 Signature { get; set; }
        public UInt16 X { get; set; }  //0 to 319
        public UInt16 Y { get; set; }  //0 to 199
        public UInt16 Width { get; set; }
        public UInt16 Height { get; set; }
        public UInt16 ColorBlockAngle { get; set; } //not tested
        public int Area { get; set; } //added for my own reference - just multiplies the width x height - area not received from the cam
        public bool IsColorBlock { get; set; } //not tested

        public override string ToString()
        {
            return "S: " + Signature + " X: " + X + " Y: " + Y + " W: " + Width + " H: " + Height + " Color: " + IsColorBlock;
        }
    }
}
