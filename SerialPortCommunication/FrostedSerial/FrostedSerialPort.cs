/*
Copyright (c) 2014, Kevin Pope
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace MatterHackers.SerialPortCommunication.FrostedSerial
{
    enum SerialSignal
    {
        None = 0,
        Cd = 1, // Carrier detect 
        Cts = 2, // Clear to send
        Dsr = 4, // Data set ready
        Dtr = 8, // Data terminal ready
        Rts = 16 // Request to send
    }

    public enum Handshake
    {
        None,
        XOnXOff,
        RequestToSend,
        RequestToSendXOnXOff
    }

    public enum StopBits
    {
        None,
        One,
        Two,
        OnePointFive
    }

    public enum Parity
    {
        None,
        Odd,
        Even,
        Mark,
        Space
    }

    public enum SerialError
    {
        RXOver = 1,
        Overrun = 2,
        RXParity = 4,
        Frame = 8,
        TXFull = 256
    }

    public enum SerialData
    {
        Chars = 1,
        Eof
    }

    public enum SerialPinChange
    {
        CtsChanged = 8,
        DsrChanged = 16,
        CDChanged = 32,
        Break = 64,
        Ring = 256
    } 

    public class SerialDataReceivedEventArgs : EventArgs
    {
        internal SerialDataReceivedEventArgs(SerialData eventType)
        {
            this.eventType = eventType;
        }

        // properties

        public SerialData EventType
        {
            get
            {
                return eventType;
            }
        }

        SerialData eventType;
    }

    public class SerialPinChangedEventArgs : EventArgs
    {
        internal SerialPinChangedEventArgs(SerialPinChange eventType)
        {
            this.eventType = eventType;
        }

        // properties

        public SerialPinChange EventType
        {
            get
            {
                return eventType;
            }
        }

        SerialPinChange eventType;
    }

    public class SerialErrorReceivedEventArgs : EventArgs
    {

        internal SerialErrorReceivedEventArgs(SerialError eventType)
        {
            this.eventType = eventType;
        }

        // properties

        public SerialError EventType
        {
            get
            {
                return eventType;
            }
        }

        SerialError eventType;
    }

    public interface IFrostedSerialPort
    {
        bool RtsEnable { get; set; }
        bool DtrEnable { get; set; }
        int BaudRate { get; set; }
        int BytesToRead { get; }

        void Write(string str);
        int WriteTimeout { get; set; }

        int ReadTimeout { get; set; }
        string ReadExisting();

        bool IsOpen { get; }

        void Open();
        void Close();
        void Dispose();
    }
    
    [MonitoringDescription("")]
    [System.ComponentModel.DesignerCategory("")]
    public class FrostedSerialPort : Component, IFrostedSerialPort
    {
		[DllImport("SetSerial", SetLastError = true)]
		static extern int set_baud(string portName, int baud_rate);

        public const int InfiniteTimeout = -1;
        const int DefaultReadBufferSize = 4096;
        const int DefaultWriteBufferSize = 2048;
        const int DefaultBaudRate = 9600;
        const int DefaultDataBits = 8;
        const Parity DefaultParity = Parity.None;
        const StopBits DefaultStopBits = StopBits.One;

        bool is_open;
        int baud_rate;
        Parity parity;
        StopBits stop_bits;
        Handshake handshake;
        int data_bits;
        bool break_state = false;
        bool dtr_enable = false;
        bool rts_enable = false;
        IFrostedSerialStream stream;
        Encoding encoding = Encoding.ASCII;
        string new_line = Environment.NewLine;
        string port_name;
        int read_timeout = InfiniteTimeout;
        int write_timeout = InfiniteTimeout;
        int readBufferSize = DefaultReadBufferSize;
        int writeBufferSize = DefaultWriteBufferSize;
        object error_received = new object();
        object data_received = new object();
        object pin_changed = new object();

        public FrostedSerialPort() :
            this(GetDefaultPortName(), DefaultBaudRate, DefaultParity, DefaultDataBits, DefaultStopBits)
        {
        }

        public FrostedSerialPort(IContainer container)
            : this()
        {
            // TODO: What to do here?
        }

        public FrostedSerialPort(string portName) :
            this(portName, DefaultBaudRate, DefaultParity, DefaultDataBits, DefaultStopBits)
        {
        }

        public FrostedSerialPort(string portName, int baudRate) :
            this(portName, baudRate, DefaultParity, DefaultDataBits, DefaultStopBits)
        {
        }

        public FrostedSerialPort(string portName, int baudRate, Parity parity) :
            this(portName, baudRate, parity, DefaultDataBits, DefaultStopBits)
        {
        }

        public FrostedSerialPort(string portName, int baudRate, Parity parity, int dataBits) :
            this(portName, baudRate, parity, dataBits, DefaultStopBits)
        {
        }

        public FrostedSerialPort(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            port_name = portName;
            baud_rate = baudRate;
            data_bits = dataBits;
            stop_bits = stopBits;
            this.parity = parity;
        }

        static string GetDefaultPortName()
        {
            string[] ports = GetPortNames();
            if (ports.Length > 0)
            {
                return ports[0];
            }
            else
            {
                int p = (int)Environment.OSVersion.Platform;
                if (p == 4 || p == 128 || p == 6)
                    return "ttyS0"; // Default for Unix
                else
                    return "COM1"; // Default for Windows
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public Stream BaseStream
        {
            get
            {
                CheckOpen();
                return (Stream)stream;
            }
        }

        [DefaultValueAttribute(DefaultBaudRate)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int BaudRate
        {
            get
            {
                return baud_rate;
            }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("value");

                if (is_open)
                    stream.SetAttributes(value, parity, data_bits, stop_bits, handshake);

                baud_rate = value;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public bool BreakState
        {
            get
            {
                return break_state;
            }
            set
            {
                CheckOpen();
                if (value == break_state)
                    return; // Do nothing.

                stream.SetBreakState(value);
                break_state = value;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public int BytesToRead
        {
            get
            {
                CheckOpen();
                return stream.BytesToRead;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public int BytesToWrite
        {
            get
            {
                CheckOpen();
                return stream.BytesToWrite;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public bool CDHolding
        {
            get
            {
                CheckOpen();
                return (stream.GetSignals() & SerialSignal.Cd) != 0;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public bool CtsHolding
        {
            get
            {
                CheckOpen();
                return (stream.GetSignals() & SerialSignal.Cts) != 0;
            }
        }

        [DefaultValueAttribute(DefaultDataBits)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int DataBits
        {
            get
            {
                return data_bits;
            }
            set
            {
                if (value < 5 || value > 8)
                    throw new ArgumentOutOfRangeException("value");

                if (is_open)
                    stream.SetAttributes(baud_rate, parity, value, stop_bits, handshake);

                data_bits = value;
            }
        }

        //[MonoTODO("Not implemented")]
        [Browsable(true)]
        [MonitoringDescription("")]
        [DefaultValue(false)]
        public bool DiscardNull
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                // LAMESPEC: Msdn states that an InvalidOperationException exception
                // is fired if the port is not open, which is *not* happening.

                throw new NotImplementedException();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public bool DsrHolding
        {
            get
            {
                CheckOpen();
                return (stream.GetSignals() & SerialSignal.Dsr) != 0;
            }
        }

        [DefaultValueAttribute(false)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public bool DtrEnable
        {
            get
            {
                return dtr_enable;
            }
            set
            {
                if (value == dtr_enable)
                    return;
                if (is_open)
                    stream.SetSignal(SerialSignal.Dtr, value);

                dtr_enable = value;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        [MonitoringDescription("")]
        public Encoding Encoding
        {
            get
            {
                return encoding;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                encoding = value;
            }
        }

        [DefaultValueAttribute(Handshake.None)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public Handshake Handshake
        {
            get
            {
                return handshake;
            }
            set
            {
                if (value < Handshake.None || value > Handshake.RequestToSendXOnXOff)
                    throw new ArgumentOutOfRangeException("value");

                if (is_open)
                    stream.SetAttributes(baud_rate, parity, data_bits, stop_bits, value);

                handshake = value;
            }
        }

        [Browsable(false)]
        public bool IsOpen
        {
            get
            {
                return is_open;
            }
        }

        [DefaultValueAttribute("\n")]
        [Browsable(false)]
        [MonitoringDescription("")]
        public string NewLine
        {
            get
            {
                return new_line;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                if (value.Length == 0)
                    throw new ArgumentException("NewLine cannot be null or empty.", "value");

                new_line = value;
            }
        }

        [DefaultValueAttribute(DefaultParity)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public Parity Parity
        {
            get
            {
                return parity;
            }
            set
            {
                if (value < Parity.None || value > Parity.Space)
                    throw new ArgumentOutOfRangeException("value");

                if (is_open)
                    stream.SetAttributes(baud_rate, value, data_bits, stop_bits, handshake);

                parity = value;
            }
        }

        //[MonoTODO("Not implemented")]
        [Browsable(true)]
        [MonitoringDescription("")]
        [DefaultValue(63)]
        public byte ParityReplace
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        [Browsable(true)]
        [MonitoringDescription("")]
        [DefaultValue("COM1")] // silly Windows-ism. We should ignore it.
        public string PortName
        {
            get
            {
                return port_name;
            }
            set
            {
                if (is_open)
                    throw new InvalidOperationException("Port name cannot be set while port is open.");
                if (value == null)
                    throw new ArgumentNullException("value");
                if (value.Length == 0 || value.StartsWith("\\\\"))
                    throw new ArgumentException("value");

                port_name = value;
            }
        }

        [DefaultValueAttribute(DefaultReadBufferSize)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int ReadBufferSize
        {
            get
            {
                return readBufferSize;
            }
            set
            {
                if (is_open)
                    throw new InvalidOperationException();
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("value");
                if (value <= DefaultReadBufferSize)
                    return;

                readBufferSize = value;
            }
        }

        [DefaultValueAttribute(InfiniteTimeout)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int ReadTimeout
        {
            get
            {
                return read_timeout;
            }
            set
            {
                if (value < 0 && value != InfiniteTimeout)
                    throw new ArgumentOutOfRangeException("value");

                if (is_open)
                    stream.ReadTimeout = value;

                read_timeout = value;
            }
        }

        //[MonoTODO("Not implemented")]
        [DefaultValueAttribute(1)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int ReceivedBytesThreshold
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("value");

                throw new NotImplementedException();
            }
        }

        [DefaultValueAttribute(false)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public bool RtsEnable
        {
            get
            {
                return rts_enable;
            }
            set
            {
                if (value == rts_enable)
                    return;
                if (is_open)
                    stream.SetSignal(SerialSignal.Rts, value);

                rts_enable = value;
            }
        }

        [DefaultValueAttribute(DefaultStopBits)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public StopBits StopBits
        {
            get
            {
                return stop_bits;
            }
            set
            {
                if (value < StopBits.One || value > StopBits.OnePointFive)
                    throw new ArgumentOutOfRangeException("value");

                if (is_open)
                    stream.SetAttributes(baud_rate, parity, data_bits, value, handshake);

                stop_bits = value;
            }
        }

        [DefaultValueAttribute(DefaultWriteBufferSize)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int WriteBufferSize
        {
            get
            {
                return writeBufferSize;
            }
            set
            {
                if (is_open)
                    throw new InvalidOperationException();
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("value");
                if (value <= DefaultWriteBufferSize)
                    return;

                writeBufferSize = value;
            }
        }

        [DefaultValueAttribute(InfiniteTimeout)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int WriteTimeout
        {
            get
            {
                return write_timeout;
            }
            set
            {
                if (value < 0 && value != InfiniteTimeout)
                    throw new ArgumentOutOfRangeException("value");

                if (is_open)
                    stream.WriteTimeout = value;

                write_timeout = value;
            }
        }

        // methods

        public void Close()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (!is_open)
                return;

            is_open = false;
            // Do not close the base stream when the finalizer is run; the managed code can still hold a reference to it.
            if (disposing)
                stream.Close();
            stream = null;
        }

        public void DiscardInBuffer()
        {
            CheckOpen();
            stream.DiscardInBuffer();
        }

        public void DiscardOutBuffer()
        {
            CheckOpen();
            stream.DiscardOutBuffer();
        }

        // On non-Android platforms simply return true as port access validation isn't applicable
        public static bool EnsureDeviceAccess()
        {
            return true;
        }

        public static string[] GetPortNames(bool filter = false)
        {
            int p = (int)Environment.OSVersion.Platform;
            List<string> serial_ports = new List<string>();

            // Are we on Unix?
            if (p == 4 || p == 128 || p == 6)
            {
                string[] ttys = Directory.GetFiles("/dev/", "tty*");

                // If filtering was not requested, return the raw listing of /dev/tty* - (subsequent filtering happens in client code)
                if (!filter) {
                    return ttys;
                }

                // Probe for Linux-styled devices: /dev/ttyS* or /dev/ttyUSB*
                foreach (string dev in ttys)
                {
                    if (dev.StartsWith("/dev/ttyS") || dev.StartsWith("/dev/ttyUSB") || dev.StartsWith("/dev/ttyACM"))
                    {
                        serial_ports.Add(dev);
                    }
                    else if (dev != "/dev/tty" && dev.StartsWith("/dev/tty") && !dev.StartsWith("/dev/ttyC"))
                    {
                        serial_ports.Add(dev);
                    }
                }
            }
            else
            {
				#if USE_STANDARD_SERIAL
				using (RegistryKey subkey = Registry.LocalMachine.OpenSubKey("HARDWARE\\DEVICEMAP\\SERIALCOMM"))
                {
                    if (subkey != null)
                    {
                        string[] names = subkey.GetValueNames();
                        foreach (string value in names)
                        {
                            string port = subkey.GetValue(value, "").ToString();
                            if (port != "")
                                serial_ports.Add(port);
                        }
                    }
                }
				#endif
            }
            return serial_ports.ToArray();
        }

        public static bool IsWindows
        {
            get
            {
                PlatformID id = Environment.OSVersion.Platform;
                return id == PlatformID.Win32Windows || id == PlatformID.Win32NT; // WinCE not supported
            }
        }

        public void Open()
        {
            if (is_open)
                throw new InvalidOperationException("Port is already open");

            stream = new FrostedSerialPortStream(port_name, baud_rate, data_bits, parity, stop_bits, dtr_enable,
                rts_enable, handshake, read_timeout, write_timeout, readBufferSize, writeBufferSize);

            is_open = true;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            CheckOpen();
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException("offset or count less than zero.");

            if (buffer.Length - offset < count)
                throw new ArgumentException("offset+count",
                                  "The size of the buffer is less than offset + count.");

            return stream.Read(buffer, offset, count);
        }

        public int Read(char[] buffer, int offset, int count)
        {
            CheckOpen();
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException("offset or count less than zero.");

            if (buffer.Length - offset < count)
                throw new ArgumentException("offset+count",
                                  "The size of the buffer is less than offset + count.");

            int c, i;
            for (i = 0; i < count && (c = ReadChar()) != -1; i++)
                buffer[offset + i] = (char)c;

            return i;
        }

        internal int read_byte()
        {
            byte[] buff = new byte[1];
            if (stream.Read(buff, 0, 1) > 0)
                return buff[0];

            return -1;
        }

        public int ReadByte()
        {
            CheckOpen();
            return read_byte();
        }

        public int ReadChar()
        {
            CheckOpen();

            byte[] buffer = new byte[16];
            int i = 0;

            do
            {
                int b = read_byte();
                if (b == -1)
                    return -1;
                buffer[i++] = (byte)b;
                char[] c = encoding.GetChars(buffer, 0, 1);
                if (c.Length > 0)
                    return (int)c[0];
            } while (i < buffer.Length);

            return -1;
        }

        public string ReadExisting()
        {
            CheckOpen();

            int count = BytesToRead;
            byte[] bytes = new byte[count];

            int n = stream.Read(bytes, 0, count);
            return new String(encoding.GetChars(bytes, 0, n));
        }

        public string ReadLine()
        {
            return ReadTo(new_line);
        }

        public string ReadTo(string value)
        {
            CheckOpen();
            if (value == null)
                throw new ArgumentNullException("value");
            if (value.Length == 0)
                throw new ArgumentException("value");

            // Turn into byte array, so we can compare
            byte[] byte_value = encoding.GetBytes(value);
            int current = 0;
            List<byte> seen = new List<byte>();

            while (true)
            {
                int n = read_byte();
                if (n == -1)
                    break;
                seen.Add((byte)n);
                if (n == byte_value[current])
                {
                    current++;
                    if (current == byte_value.Length)
                        return encoding.GetString(seen.ToArray(), 0, seen.Count - byte_value.Length);
                }
                else
                {
                    current = (byte_value[0] == n) ? 1 : 0;
                }
            }
            return encoding.GetString(seen.ToArray());
        }

        public void Write(string str)
        {
            CheckOpen();
            if (str == null)
                throw new ArgumentNullException("str");

            byte[] buffer = encoding.GetBytes(str);
            Write(buffer, 0, buffer.Length);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            CheckOpen();
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();

            if (buffer.Length - offset < count)
                throw new ArgumentException("offset+count",
                                 "The size of the buffer is less than offset + count.");

            stream.Write(buffer, offset, count);
        }

        public void Write(char[] buffer, int offset, int count)
        {
            CheckOpen();
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();

            if (buffer.Length - offset < count)
                throw new ArgumentException("offset+count",
                                 "The size of the buffer is less than offset + count.");

            byte[] bytes = encoding.GetBytes(buffer, offset, count);
            stream.Write(bytes, 0, bytes.Length);
        }

        public void WriteLine(string str)
        {
            Write(str + new_line);
        }

        void CheckOpen()
        {
            if (!is_open)
                throw new InvalidOperationException("Specified port is not open.");
        }

        internal void OnErrorReceived(SerialErrorReceivedEventArgs args)
        {
            SerialErrorReceivedEventHandler handler =
                (SerialErrorReceivedEventHandler)Events[error_received];

            if (handler != null)
                handler(this, args);
        }

        internal void OnDataReceived(SerialDataReceivedEventArgs args)
        {
            SerialDataReceivedEventHandler handler =
                (SerialDataReceivedEventHandler)Events[data_received];

            if (handler != null)
                handler(this, args);
        }

        internal void OnDataReceived(SerialPinChangedEventArgs args)
        {
            SerialPinChangedEventHandler handler =
                (SerialPinChangedEventHandler)Events[pin_changed];

            if (handler != null)
                handler(this, args);
        }

        // events
        [MonitoringDescription("")]
        public event SerialErrorReceivedEventHandler ErrorReceived
        {
            add { Events.AddHandler(error_received, value); }
            remove { Events.RemoveHandler(error_received, value); }
        }

        [MonitoringDescription("")]
        public event SerialPinChangedEventHandler PinChanged
        {
            add { Events.AddHandler(pin_changed, value); }
            remove { Events.RemoveHandler(pin_changed, value); }
        }

        [MonitoringDescription("")]
        public event SerialDataReceivedEventHandler DataReceived
        {
            add { Events.AddHandler(data_received, value); }
            remove { Events.RemoveHandler(data_received, value); }
        }

        public static IFrostedSerialPort Create(string serialPortName)
        {
            IFrostedSerialPort newPort = null;
            // if we can find a mac helper class (to get us 250k)
            string appBundle = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (File.Exists(Path.Combine(appBundle, "libFrostedSerialHelper.dylib")))
            {
                // use it
                newPort = new FrostedSerialPort(serialPortName);
            }
            else // use the c# native serial port
            {
				#if USE_STANDARD_SERIAL
				newPort = new CSharpSerialPortWrapper(serialPortName);
				#endif
            }

            return newPort;
        }

        public static IFrostedSerialPort CreateAndOpen(string serialPortName, int baudRate, bool DtrEnableOnConnect)
        {
            IFrostedSerialPort newPort = Create(serialPortName);

            bool isLinux = !(newPort is FrostedSerialPort) && !IsWindows;

            // Skip BaudRate assignment on Linux to avoid Invalid Baud exception - defaults to 9600
            if (!isLinux)
            {
                newPort.BaudRate = baudRate;
            }
            
            if (DtrEnableOnConnect)
            {
                newPort.DtrEnable = true;
            }

            // Set the read/write timeouts
            newPort.ReadTimeout = 500;
            newPort.WriteTimeout = 500;

            newPort.Open();

            if (isLinux) 
            {
                // Once mono has enforced its ANSI baud rate policy(in SerialPort.Open), reset the baud rate to the user specified 
                // value by calling set_baud in libSetSerial.so
                set_baud (serialPortName, baudRate);
            }

            return newPort;
        }
    }
	#if USE_STANDARD_SERIAL
    public class CSharpSerialPortWrapper : IFrostedSerialPort
    {

		System.IO.Ports.SerialPort port;

        internal CSharpSerialPortWrapper(string serialPortName)
        {
            if (FrostedSerialPort.IsWindows)
            {
                try
                {
                    SerialPortFixer.Execute(serialPortName);
                }
                catch (Exception)
                {
                }
            }
            port = new System.IO.Ports.SerialPort(serialPortName);
        }

        public int ReadTimeout
        {
            get { return port.ReadTimeout; }
            set { port.ReadTimeout = value; }
        }

        public string ReadExisting()
        {
            return port.ReadExisting();
        }

        public int BytesToRead
        {
            get { return port.BytesToRead; }
        }

        public void Dispose()
        {
            port.Dispose();
        }

        public bool IsOpen
        {
            get { return port.IsOpen; }
        }

        public void Open()
        {
            port.Open();
        }

        public void Close()
        {
            try
            {
                port.Close();
            }
            catch (Exception)
            { 
            }
        }

        public int WriteTimeout
        {
            get
            {
                return port.WriteTimeout;
            }
            set
            {
                port.WriteTimeout = value;
            }
        }

        public int BaudRate
        {
            get
            {
                return port.BaudRate;
            }
            set
            {
                port.BaudRate = value;
            }
        }

        public bool RtsEnable
        {
            get
            {
                return port.RtsEnable;
            }
            set
            {
                port.RtsEnable = value;
            }
        }

        public bool DtrEnable
        {
            get
            {
                return port.DtrEnable;
            }
            set
            {
                port.DtrEnable = value;
            }
        }

        public void Write(string str)
        {
            port.Write(str);
        }
    }
	#endif

    public delegate void SerialDataReceivedEventHandler(object sender, SerialDataReceivedEventArgs e);
    public delegate void SerialPinChangedEventHandler(object sender, SerialPinChangedEventArgs e);
    public delegate void SerialErrorReceivedEventHandler(object sender, SerialErrorReceivedEventArgs e);
}
