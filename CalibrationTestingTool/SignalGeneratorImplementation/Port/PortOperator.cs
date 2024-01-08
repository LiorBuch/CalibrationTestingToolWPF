using CalibrationToolTester.GlobalLoger;
using Ivi.Visa;
using NationalInstruments.Visa;
using System;
using System.Linq;
using System.Text;
using System.Threading;

namespace CalibrationToolTester.SignalGeneratorImplementation.Port
{
    internal interface IPortOperator
    {
        void Open();

        void Close();

        void Write(string command);

        string Read();
    }
    internal interface IPortType
    {
        PortType PortType { get; }
    }
    /// <summary>
    ///
    /// </summary>
    public abstract class PortOperatorBase : IPortOperator
    {
        #region Fields

        //private readonly object _portLock = new object();

        protected IMessageBasedSession Session { private set; get; }

        public string Address { set; get; }
        public int Timeout { set; get; } = 2000;

        //IMPORTANT:Vlad:Sleep time after write.Check it.
        private int _sleepAfterWrite = 5;

        public int SleepAfterWrite
        {
            get
            {
                return _sleepAfterWrite;
            }
            set
            {
                _sleepAfterWrite = value;
            }
        }

        private bool _realTimeReceive = false;

        public bool RealTimeReceive
        {
            get
            {
                return _realTimeReceive;
            }
            set
            {
                _realTimeReceive = value;
            }
        }

        public event EventHandler<PortEventArgs> PortOpenning;

        public event EventHandler<PortEventArgs> PortClosing;

        #endregion Fields

        #region Constructor

        /// <summary>
        ///
        /// </summary>
        /// <param name="session"></param>
        public PortOperatorBase(IMessageBasedSession session)
        {
            Session = session;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="session"></param>
        /// <param name="address"></param>
        public PortOperatorBase(IMessageBasedSession session, string address) : this(session)
        {
            Address = address;
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        ///
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnPortOpenning(PortEventArgs e)
        {
            PortOpenning?.Invoke(this, e);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnPortClosing(PortEventArgs e)
        {
            PortClosing?.Invoke(this, e);
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsPortOpen { private set; get; } = false;

        /// <summary>
        ///
        /// </summary>
        public virtual void Open()
        {
            PortEventArgs e = new PortEventArgs(Address);
            OnPortOpenning(e);
            if (!e.Cancel)
            {
                Session.TimeoutMilliseconds = Timeout;
                this.IsPortOpen = true;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public virtual void Close()
        {
            PortEventArgs e = new PortEventArgs(Address);
            OnPortClosing(e);
            if (!e.Cancel)
            {
                Session.Dispose();
                this.IsPortOpen = false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="command"></param>
        public virtual void Write(string command)
        {
            try
            {
                Session.RawIO.Write(command);
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
                this.Close();
            }

            Thread.Sleep(_sleepAfterWrite);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="command"></param>
        /// <param name="waitToResponse"></param>
        /// <returns></returns>
        public virtual string Write(string command, bool waitToResponse)
        {
            string returnValue = string.Empty;

            try
            {
                Session.RawIO.Write(command);

                if (waitToResponse)
                {
                    returnValue = Session.RawIO.ReadString();
                    returnValue = returnValue.Trim('\n');
                }

                Thread.Sleep(_sleepAfterWrite);
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
                this.Close();
            }

            return returnValue;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="command"></param>
        public virtual void Write(byte[] command)
        {
            try
            {
                Session.RawIO.Write(command);
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
                this.Close();
            }

            Thread.Sleep(_sleepAfterWrite);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="command"></param>
        public virtual void WriteLine(byte[] command)
        {
            try
            {
                Session.RawIO.Write(command.Concat(new byte[] { 0x0A }).ToArray());
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
                this.Close();
            }

            Thread.Sleep(_sleepAfterWrite);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public virtual string WriteLine(string command)
        {
            string returnValue = string.Empty;

            if (command.Contains("?"))
            {
                returnValue = Write($"{command}\n", true);
                returnValue = returnValue.Trim('\n');
            }
            else
            {
                Write($"{command}\n", false);
            }

            return returnValue;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public byte[] ReadToBytes()
        {
            byte[] returnValue = null;

            returnValue = Session.RawIO.Read();

            return returnValue;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public byte[] ReadToBytes(int count)
        {
            byte[] returnValue = null;

            returnValue = Session.RawIO.Read(count);

            return returnValue;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public string Read()
        {
            string returnValue = string.Empty;

            returnValue = Encoding.ASCII.GetString(Session.RawIO.Read());

            return returnValue;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public string Read(int count)
        {
            string returnValue = string.Empty;
            ReadStatus readStatus = ReadStatus.Unknown;

            returnValue = Encoding.ASCII.GetString(Session.RawIO.Read(count, out readStatus));

            return returnValue;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public string ReadLine()
        {
            string returnValue = string.Empty;

            returnValue = Read();
            returnValue = returnValue.EndsWith("\n") ? returnValue.TrimEnd(new char[] { '\n' }) : returnValue;

            return returnValue;
        }

        #endregion Methods
    }
    /// <summary>
    ///
    /// </summary>
    public class RS232PortOperator : PortOperatorBase, IPortType
    {
        public int BaudRate { private set; get; }

        public SerialParity Parity { private set; get; }

        public SerialStopBitsMode StopBits { private set; get; }

        public int DataBits { private set; get; }

        public PortType PortType { get => PortType.RS232; }

        public SerialFlowControlModes FlowControl { set; get; } = SerialFlowControlModes.None;

        private SerialSession serialSession;

        private EventHandler<SerialDataReceivedEventArgs> dataReceived;

        public event EventHandler<SerialDataReceivedEventArgs> DataReceived
        {
            add
            {
                serialSession.AnyCharacterReceived += SerialSession_AnyCharacterReceived;
                dataReceived += value;
            }
            remove
            {
                serialSession.AnyCharacterReceived -= SerialSession_AnyCharacterReceived;
                dataReceived -= value;
            }
        }

        private void SerialSession_AnyCharacterReceived(object sender, VisaEventArgs e)
        {
            OnDataReceived(new SerialDataReceivedEventArgs(serialSession.BytesAvailable));
        }

        protected virtual void OnDataReceived(SerialDataReceivedEventArgs e)
        {
            dataReceived?.Invoke(this, e);
        }

        public RS232PortOperator(string address, int baudRate, SerialParity parity, SerialStopBitsMode stopBits, int dataBits) : base(new SerialSession(address), address)
        {
            BaudRate = baudRate;
            Parity = parity;
            StopBits = stopBits;
            if (dataBits >= 5 && dataBits <= 8)
            {
                DataBits = dataBits;
            }
            serialSession = (SerialSession)Session;
        }

        public void SetReadTerminationCharacterEnabled(bool enabled)
        {
            serialSession.ReadTermination = enabled ? SerialTerminationMethod.TerminationCharacter : SerialTerminationMethod.None;
        }

        public override void Open()
        {
            base.Open();
            serialSession.BaudRate = BaudRate;
            switch (Parity)
            {
                case SerialParity.None:
                    serialSession.Parity = SerialParity.None; break;
                case SerialParity.Odd:
                    serialSession.Parity = SerialParity.Odd; break;
                case SerialParity.Even:
                    serialSession.Parity = SerialParity.Even; break;
                case SerialParity.Mark:
                    serialSession.Parity = SerialParity.Mark; break;
                case SerialParity.Space:
                    serialSession.Parity = SerialParity.Space; break;
            }
            switch (StopBits)
            {
                case SerialStopBitsMode.One:
                    serialSession.StopBits = SerialStopBitsMode.One; break;
                case SerialStopBitsMode.OneAndOneHalf:
                    serialSession.StopBits = SerialStopBitsMode.OneAndOneHalf; break;
                case SerialStopBitsMode.Two:
                    serialSession.StopBits = SerialStopBitsMode.Two; break;
            }
            serialSession.DataBits = (short)DataBits;
            switch (FlowControl)
            {
                case SerialFlowControlModes.None:
                    serialSession.FlowControl = SerialFlowControlModes.None; break;
                case SerialFlowControlModes.XOnXOff:
                    serialSession.FlowControl = SerialFlowControlModes.XOnXOff; break;
                case SerialFlowControlModes.RtsCts:
                    serialSession.FlowControl = SerialFlowControlModes.RtsCts; break;
                case SerialFlowControlModes.DtrDsr:
                    serialSession.FlowControl = SerialFlowControlModes.DtrDsr; break;
            }
        }
    }
    /// <summary>
    ///
    /// </summary>
    public class USBPortOperator : PortOperatorBase, IPortType
    {
        public USBPortOperator(string address) : base(new UsbSession(address), address)
        {
            if (!address.ToUpper().Contains("USB"))
                throw new ArgumentException($"The address does not contain the word USB");
        }

        public PortType PortType { get => PortType.USB; }
    }
    /// <summary>
    ///
    /// </summary>
    public class GPIBPortOperator : PortOperatorBase, IPortType
    {
        public GPIBPortOperator(string address) : base(new GpibSession(address), address)
        {
            if (!address.ToUpper().Contains("GPIB"))
                throw new ArgumentException($"The address does not contain GPIB words");
        }

        public PortType PortType { get => PortType.GPIB; }
    }
    /// <summary>
    ///
    /// </summary>
    public class LANPortOperator : PortOperatorBase, IPortType
    {
        public LANPortOperator(string address) : base(new TcpipSession(address), address)
        {
            if (!address.ToUpper().Contains("TCPIP"))
                throw new ArgumentException($"The address does not contain the word TCPIP");
        }

        public PortType PortType { get => PortType.LAN; }
    }
    /// <summary>
    ///
    /// </summary>
    public class PortEventArgs : EventArgs
    {
        public string Address { private set; get; }
        public bool Cancel { set; get; }

        public PortEventArgs(string address)
        {
            Address = address;
        }
    }
}