/*
Copyright (c) 2013, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace MatterHackers.SerialPortConnecton
{
    public class SerialPortCommunication
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr securityAttrs, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        public event EventHandler EnableChanged;
        public event EventHandler ConnectionStateChanged;
        public event EventHandler ConnectionFailed;
        public event EventHandler ConnectionSucceeded;
        public event EventHandler ActivePrinterChanged;

        public delegate void RecievedLineEventHandler(string lineRead);
        public event RecievedLineEventHandler ReadLine;

        public delegate void WroteLineEventHandler(string lineWriten);
        public event WroteLineEventHandler WroteLine;

        public event RecievedLineEventHandler CommunicationUnconditional;

        public FoundStringStartsWithCallbacks ReadLineStartCallBacks = new FoundStringStartsWithCallbacks();
        public FoundStringContainsCallbacks ReadLineContainsCallBacks = new FoundStringContainsCallbacks();

        public FoundStringStartsWithCallbacks WriteLineStartCallBacks = new FoundStringStartsWithCallbacks();
        public FoundStringContainsCallbacks WriteLineContainsCallBacks = new FoundStringContainsCallbacks();

        bool readyForNextCommand = false;

        public enum CommunicationStates { Disconnected, AttemptingToConnect, FailedToConnect, Connected, Disconnecting, ConnectionLost };
        CommunicationStates communicationState = CommunicationStates.Disconnected;

        public CommunicationStates CommunicationState
        {
            get
            {
                return communicationState;
            }

            set
            {
                if (communicationState != value)
                {
                    // if it was printing
                    communicationState = value;
                    OnConnectionStateChanged();
                }
            }
        }

        bool stopTryingToConnect = false;

        string serialPortName;
        SerialPort serialPort;
        Thread readFromPrinterThread;
        Thread connectThread;

        public bool Disconnecting
        {
            get
            {
                return CommunicationState == CommunicationStates.Disconnecting;
            }
        }

        public bool PortIsConnected
        {
            get
            {
                switch (CommunicationState)
                {
                    case CommunicationStates.Disconnected:
                    case CommunicationStates.AttemptingToConnect:
                    case CommunicationStates.ConnectionLost:
                    case CommunicationStates.FailedToConnect:
                        return false;

                    case CommunicationStates.Disconnecting:
                    case CommunicationStates.Connected:
                        return true;

                    default:
                        throw new NotImplementedException("Make sure very satus returns the correct connected state.");
                }
            }
        }

        public SerialPortCommunication()
        {
        }

        public string PrinterConnectionStatusVerbose
        {
            get
            {
                switch (CommunicationState)
                {
                    case CommunicationStates.Disconnected:
                        return "Not Connected";
                    case CommunicationStates.Disconnecting:
                        return "Disconnecting";
                    case CommunicationStates.AttemptingToConnect:
                        return "Connecting...";
                    case CommunicationStates.ConnectionLost:
                        return "Connection Lost";
                    case CommunicationStates.FailedToConnect:
                        return "Connection Failed";
                    case CommunicationStates.Connected:
                        return "Connected";
                    default:
                        throw new NotImplementedException("Make sure very satus returns the correct connected state.");
                }
            }
        }

        void ConnectionCallbackTimer(object state)
        {
            Timer t = (Timer)state;
            if (!ContinueConnectionThread())
            {
                t.Dispose();
            }
            else
            {
                t.Change(100, 0);
            }
        }

        bool ContinueConnectionThread()
        {
            if (CommunicationState == CommunicationStates.AttemptingToConnect)
            {
                if (this.stopTryingToConnect)
                {
                    connectThread.Join(); //Halt connection thread
                    Disable();
                    OnConnectionFailed();
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                connectThread.Join(); //Halt connection thread
                OnConnectionSucceeded();
                return false;
            }
        }

        public void HaltConnectionThread()
        {
            this.stopTryingToConnect = true;
        }

        int baudRate;
        public int BaudRate
        {
            get
            {
                return baudRate;
            }
        }


        public void ConnectToSerialPort(string serialPort, int baudRate)
        {
            this.baudRate = baudRate;
            this.serialPortName = serialPort;

            //Attempt connecting to a specific printer
            CommunicationState = CommunicationStates.AttemptingToConnect;
            this.stopTryingToConnect = false;

            if (SerialPortIsAvailable(serialPort))
            {
                //Create a timed callback to determine whether connection succeeded
                Timer connectionTimer = new Timer(new TimerCallback(ConnectionCallbackTimer));
                connectionTimer.Change(100, 0);

                //Create and start connection thread
                connectThread = new Thread(Connect_Thread);
                connectThread.Name = "Connect To Printer";
                connectThread.IsBackground = true;
                connectThread.Start();
            }
            else
            {
                Debug.WriteLine(string.Format("Connection failed: {0}", serialPort));
                OnConnectionFailed();
            }
        }

        void Connect_Thread()
        {
            // Allow the user to set the appropriate properties.
            var portNames = SerialPort.GetPortNames();
            //Debug.WriteLine(string.Format("Open ports: {0}", portNames.Length));
            if (portNames.Length > 0)
            {
                //Debug.WriteLine(string.Format("Connecting to: {0} {1}", this.serialPortName, this.BaudRate));
                AttemptToConnect(this.serialPortName, this.BaudRate);
                if (CommunicationState == CommunicationStates.FailedToConnect)
                {
                    OnConnectionFailed();
                }
            }
            else
            {
                OnConnectionFailed();
            }
        }

        public void OnConnectionSucceeded()
        {
            CommunicationState = CommunicationStates.Connected;

            if (ConnectionSucceeded != null)
            {
                ConnectionSucceeded(this, null);
            }

            OnEnabledChanged();
        }

        public void OnConnectionStateChanged()
        {
            if (ConnectionStateChanged != null)
            {
                ConnectionStateChanged(this, null);
            }
        }

        public void OnConnectionFailed()
        {
            if (ConnectionFailed != null)
            {
                ConnectionFailed(this, null);
            }

            CommunicationState = CommunicationStates.FailedToConnect;
            OnEnabledChanged();
        }

        //Function is not mac-friendly
        bool SerialPortAlreadyOpen(string portName)
        {
            int dwFlagsAndAttributes = 0x40000000;

            //Borrowed from Microsoft's Serial Port Open Method :)
            SafeFileHandle hFile = CreateFile(@"\\.\" + portName, -1073741824, 0, IntPtr.Zero, 3, dwFlagsAndAttributes, IntPtr.Zero);
            if (hFile.IsInvalid)
            {
                return true;
            }

            hFile.Close();

            return false;
        }

        public bool SerialPortIsAvailable(string portName)
        //Check is serial port is in the list of available serial ports
        {
            try
            {
                string[] portNames = SerialPort.GetPortNames();
                return portNames.Any(x => string.Compare(x, portName, true) == 0);
            }
            catch
            {
                return false;
            }

        }

        void AttemptToConnect(string serialPortName, int baudRate)
        {
            if (PortIsConnected)
            {
                throw new Exception("You can only connect when not currently connected.");
            }

            CommunicationState = CommunicationStates.AttemptingToConnect;

            if (SerialPortIsAvailable(serialPortName) && !SerialPortAlreadyOpen(serialPortName))
            {
                serialPort = new SerialPort(serialPortName);
                serialPort.BaudRate = baudRate;
                //serialPort.Parity = Parity.None;
                //serialPort.DataBits = 8;
                //serialPort.StopBits = StopBits.One;
                //serialPort.Handshake = Handshake.None;
                serialPort.DtrEnable = true;

                // Set the read/write timeouts
                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;

                if (CommunicationState == CommunicationStates.AttemptingToConnect)
                {
                    try
                    {
                        serialPort.Open();

                        readFromPrinterThread = new Thread(ReadFromPrinter);
                        readFromPrinterThread.Name = "Read From Printer";
                        readFromPrinterThread.IsBackground = true;
                        readFromPrinterThread.Start();

                        // let's check if the printer will talk to us
                        WriteLineToPrinter("M114");
                        WriteLineToPrinter("M105");
                    }
                    catch (Exception)
                    {
                        OnConnectionFailed();
                    }
                }
            }
        }

        void OnEnabledChanged()
        {
            if (EnableChanged != null)
            {
                EnableChanged(this, null);
            }
        }

        public void WriteLineToPrinter(string lineToWrite)
        {
            FoundStringEventArgs foundResponse = new FoundStringEventArgs(lineToWrite);
            WriteLineStartCallBacks.CheckForKeys(foundResponse);
            WriteLineContainsCallBacks.CheckForKeys(foundResponse);
            if (foundResponse.SendToDelegateFunctions)
            {
                if (WroteLine != null)
                {
                    WroteLine(lineToWrite);
                }
            }

            WriteToPrinter(lineToWrite + "\r\n");
        }

        public void WriteToPrinter(string stuffToWrite)
        {
            if (PortIsConnected)
            {
                if (serialPort.IsOpen)
                {
                    //If the printer is printing, add line to command queue
                    if (CommunicationUnconditional != null)
                    {
                        CommunicationUnconditional("->" + stuffToWrite);
                    }
                    serialPort.Write(stuffToWrite);
                }
                else
                {
                    OnConnectionFailed();
                }
            }
        }

        public void Disable()
        {
            if (PortIsConnected)
            {
                CommunicationState = CommunicationStates.Disconnecting;
                readFromPrinterThread.Join();
                serialPort.Close();
                serialPort.Dispose();
                serialPort = null;
                CommunicationState = CommunicationStates.Disconnected;
            }
            OnEnabledChanged();
        }

        string lineBeingRead = "";
        string lastLineRead = "";
        public void ReadFromPrinter()
        {
            readyForNextCommand = true;
            while (CommunicationState == CommunicationStates.AttemptingToConnect
                || (PortIsConnected && serialPort.IsOpen && !Disconnecting))
            {
                try
                {
                    char nextChar = (char)serialPort.ReadChar();
                    if (nextChar == '\r' || nextChar == '\n')
                    {
                        lastLineRead = lineBeingRead;
                        lineBeingRead = "";
                        //Debug.WriteLine("Printer Response:'" + lastLineRead + "'"); // Building Inteligence Tool System

                        if (CommunicationUnconditional != null)
                        {
                            CommunicationUnconditional("<-" + lastLineRead);
                        }

                        FoundStringEventArgs foundResponse = new FoundStringEventArgs(lastLineRead);
                        ReadLineStartCallBacks.CheckForKeys(foundResponse);
                        ReadLineContainsCallBacks.CheckForKeys(foundResponse);

                        if (foundResponse.SendToDelegateFunctions)
                        {
                            if (ReadLine != null)
                            {
                                ReadLine(lastLineRead);
                            }
                        }

                        if (CommunicationState == CommunicationStates.AttemptingToConnect)
                        {
                            CommunicationState = CommunicationStates.Connected;
                        }
                    }
                    else
                    {
                        lineBeingRead += nextChar;
                    }
                }
                catch (TimeoutException)
                {
                }
                catch (IOException)
                {
                    OnConnectionFailed();
                }
                catch (InvalidOperationException)
                {
                    // this happens when the serial port closes after we check and before we read it.
                }
            }
            readyForNextCommand = true;
        }
    }
}
