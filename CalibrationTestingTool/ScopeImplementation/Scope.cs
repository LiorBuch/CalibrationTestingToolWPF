using CalibrationToolTester.GlobalLoger;
using Ivi.Visa;
using CalibrationToolTester.ScopeImplementation.Port;
using CalibrationToolTester.ScopeImplementation.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace CalibrationToolTester.ScopeImplementation
{
    /// <summary>
    /// 
    /// </summary>
    public class Scope : INotifyPropertyChanged, IDataErrorInfo, IDisposable
    {
        #region Constants

        private const string SCOPE_DISCONNECTED_ERROR = "Scope disconnected";

        #endregion

        #region Nested

        [Flags]
        public enum SESR : byte
        {
            None = 0,
            OPC = 1,
            RQC = 2,
            QYE = 4,
            DDE = 8,
            EXE = 16,
            CME = 32,
            URQ = 64,
            PON = 128
        }
        public enum AcquisitionMode
        {
            Sample,
            PeakDetect,
            Hires,
            Average,
            Envelope
        }
        public enum RS232BaudRate
        {
            B9600 = 9600,
            B19200 = 19200,
            B38400 = 38400,
            B57600 = 57600,
            B115200 = 115200
        }

        #endregion

        #region Fields

        private readonly object _scopeSendCommandLock = new object();

        public static CommandTranslationService commandsTranslation = new CommandTranslationService();

        private bool _isAsciiCommand = true;
        private bool _isWritingError = false;
        private bool _appendNewLine = true;
        private bool _initialized = false;

        private Thread _scopeStatusThread = null;
        private Thread _scopeMeasurementsThread = null;
        private Thread _scopeMeasurementsInfoThread = null;

        private int _digitsNumber = 3;
        public int DigitsNumber
        {
            get
            {
                return _digitsNumber;
            }
            set
            {
                _digitsNumber = value;
                _digitsNumber = (_digitsNumber > 0) ? _digitsNumber : 1;

                for (int i = 0; i < Measurements.Length; i++)
                {
                    Measurements[i].DigitsNumber = _digitsNumber;
                }
            }
        }

        private static int _measurementsThreadDelay = 25;
        public static int MeasurementsThreadDelay
        {
            get
            {
                return _measurementsThreadDelay;
            }
            set
            {
                _measurementsThreadDelay = value;
                _measurementsThreadDelay = (_measurementsThreadDelay <= 0) ? 1 : _measurementsThreadDelay;
            }
        }

        private static int _measurementsInfoThreadDelay = 50;
        public static int MeasurementsInfoThreadDelay
        {
            get
            {
                return _measurementsInfoThreadDelay;
            }
            set
            {
                _measurementsInfoThreadDelay = value;
                _measurementsInfoThreadDelay = (_measurementsInfoThreadDelay <= 0) ? 1 : _measurementsInfoThreadDelay;
            }
        }

        private static int _scopeStatusThreadDelay = 500;
        public static int ScopeStatusThreadDelay
        {
            get
            {
                return _scopeStatusThreadDelay;
            }
            set
            {
                _scopeStatusThreadDelay = value;
                _scopeStatusThreadDelay = (_scopeStatusThreadDelay <= 0) ? 1 : _scopeStatusThreadDelay;
            }
        }

        private string _name = string.Empty;
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        private bool _innerFlagScopeMeasurementsThread = false;
        private bool _isInsideScopeMeasurementsThread = false;
        public bool InnerFlagScopeMeasurementsThread
        {
            get
            {
                return _innerFlagScopeMeasurementsThread;
            }
            set
            {
                _innerFlagScopeMeasurementsThread = (_isConnected == true) ? value : false;
                OnPropertyChanged();
            }
        }

        private bool _innerFlagScopeMeasurementsInfoThread = false;
        private bool _isInsideScopeMeasurementsInfoThread = false;
        public bool InnerFlagScopeMeasurementsInfoThread
        {
            get
            {
                return _innerFlagScopeMeasurementsInfoThread;
            }
            set
            {
                _innerFlagScopeMeasurementsInfoThread = (_isConnected == true) ? value : false;
                OnPropertyChanged();
            }
        }

        private bool _innerFlagScopeStatusThread;
        public bool InnerFlagScopeStatusThread
        {
            get
            {
                return _innerFlagScopeStatusThread;
            }
            set
            {
                _innerFlagScopeStatusThread = (_isConnected == true) ? value : false;
                OnPropertyChanged();
            }
        }

        private PortType _communicationType = PortType.GPIB;
        public PortType CommunicationType
        {
            get
            {
                return _communicationType;
            }
            set
            {
                _communicationType = value;
                _communicationTypeInt = (int)_communicationType;
                OnPropertyChanged();
            }
        }

        private int _communicationTypeInt = 0;
        public int CommunicationTypeInt
        {
            get
            {
                return _communicationTypeInt;
            }
            set
            {
                _communicationTypeInt = value;
                _communicationType = (PortType)_communicationTypeInt;
                OnPropertyChanged();
            }
        }
        public Array CommunicationTypeArray
        {
            get
            {
                return Enum.GetValues(typeof(PortType));
            }
        }

        private string _actualAddress;
        public string ActualAddress
        {
            get
            {
                return _actualAddress;
            }
            set
            {
                if (_actualAddress != value)
                {
                    _actualAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        private RS232BaudRate _rs232Baudrate = RS232BaudRate.B115200;
        public RS232BaudRate Rs232Baudrate
        {
            get
            {
                return _rs232Baudrate;
            }
            set
            {
                _rs232Baudrate = value;
                OnPropertyChanged();
            }
        }
        public Array RS232BaudRateArray
        {
            get
            {
                return Enum.GetValues(typeof(RS232BaudRate));
            }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _connectionStatus = string.Empty;
        public string ConnectionStatus
        {
            get
            {
                return _connectionStatus;
            }
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;

                    if (_connectionStatus.ToLower().Contains("disconnected"))
                    {
                        AddError("ConnectionStatus", SCOPE_DISCONNECTED_ERROR, false);
                    }
                    else
                    {
                        RemoveError("ConnectionStatus", SCOPE_DISCONNECTED_ERROR);
                    }

                    OnPropertyChanged();
                }
            }
        }

        private SESR _eventStatusRegister;
        public SESR EventStatusRegister
        {
            get
            {
                return _eventStatusRegister;
            }
            set
            {
                if (_eventStatusRegister != value)
                {
                    _eventStatusRegister = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _event;
        public int Event
        {
            get
            {
                return _event;
            }
            set
            {
                _event = value;
                OnPropertyChanged();
            }
        }

        private Measurement[] _measurements = new Measurement[4]
        {
            new Measurement() { ID = 1},
            new Measurement() { ID = 2 },
            new Measurement() { ID = 3 },
            new Measurement() { ID = 4 }
        };
        public Measurement[] Measurements
        {
            get
            {
                return _measurements;
            }
            set
            {
                if (_measurements != value)
                {
                    _measurements = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _dataMeasurementsUpdated = false;
        public bool DataMeasurementsUpdated
        {
            get
            {
                return _dataMeasurementsUpdated;
            }
            set
            {
                _dataMeasurementsUpdated = value;
                OnPropertyChanged();
            }
        }

        private AcquisitionMode _acuireMode;
        public AcquisitionMode AcuireMode
        {
            get
            {
                return _acuireMode;
            }
            set
            {
                ScopeCommand scopeCommand = new ScopeCommand();
                string reply = string.Empty;

                _acuireMode = value;

                scopeCommand.CommandMessage = "acquire:mode" + " " + _acuireMode;
                reply = Write(scopeCommand);

                scopeCommand.CommandMessage = "acquire:mode?";
                reply = Write(scopeCommand);

                if (string.IsNullOrEmpty(reply) == false)
                {
                    if (reply.ToLower().Contains("sam"))
                    {
                        _acuireMode = AcquisitionMode.Sample;
                    }
                    if (reply.ToLower().Contains("ave"))
                    {
                        _acuireMode = AcquisitionMode.Average;
                    }
                    if (reply.ToLower().Contains("env"))
                    {
                        _acuireMode = AcquisitionMode.Envelope;
                    }
                    if (reply.ToLower().Contains("hi"))
                    {
                        _acuireMode = AcquisitionMode.Hires;
                    }
                    if (reply.ToLower().Contains("peak"))
                    {
                        _acuireMode = AcquisitionMode.PeakDetect;
                    }
                }

                OnPropertyChanged();
            }
        }

        private List<int> _measurementsIndexes = new List<int>();
        public List<int> MeasurementsIndexes
        {
            get
            {
                return _measurementsIndexes;
            }
            set
            {
                if (_measurementsIndexes != value)
                {
                    _measurementsIndexes = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _actualCommandsTranslationsFile = string.Empty;
        public string ActualCommandsTranslationsFile
        {
            get
            {
                return _actualCommandsTranslationsFile;
            }
            set
            {
                _actualCommandsTranslationsFile = value;

                string translationFilesPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName + "\\DevicesTypes\\ScopeTypes\\";
                commandsTranslation.LoadTranslations(translationFilesPath + _actualCommandsTranslationsFile + ".xml");

                OnPropertyChanged();
            }
        }

        ScopeCommand _actualCommand = new ScopeCommand();
        public ScopeCommand ActualCommand
        {
            get
            {
                return _actualCommand;
            }
            set
            {
                if (_actualCommand != value)
                {
                    _actualCommand = value;

                    if (_actualCommand.Arguments != null)
                    {
                        for (int i = 0; i < _actualCommand.Arguments.Length; i++)
                        {
                            if (_actualCommand.Mnemonics.Length == 3)
                            {
                                if (_actualCommand.Mnemonics[1].Contains("meas"))
                                {
                                    int measurementIndex = 0;
                                    string index = _actualCommand.Mnemonics[1].Substring(4);

                                    if (Int32.TryParse(index, out measurementIndex))
                                    {
                                        if (_measurementsIndexes.Contains(measurementIndex) == false)
                                        {
                                            _measurementsIndexes.Add(measurementIndex);
                                        }
                                        measurementIndex = measurementIndex - 1;

                                        if (_actualCommand.Mnemonics[2].Contains("name"))
                                        {
                                            string name = string.Empty;
                                            if (_actualCommand.Arguments[i] != null)
                                            {
                                                Measurements[measurementIndex].Name = _actualCommand.Arguments[i].Trim(new char[] { '"' });
                                                _actualCommand.CommandMessage = string.Empty;
                                                break;
                                            }
                                        }
                                        if (_actualCommand.Mnemonics[2].Contains("desiredValue"))
                                        {
                                            double desiredValue = 0.0;
                                            if (Double.TryParse(_actualCommand.Arguments[i], out desiredValue) == true)
                                            {
                                                Measurements[measurementIndex].DesiredValue = desiredValue;
                                                _actualCommand.CommandMessage = string.Empty;
                                                break;
                                            }
                                        }
                                        if (_actualCommand.Mnemonics[2].Contains("threshold"))
                                        {
                                            double thresholdValue = 0.0;
                                            if (Double.TryParse(_actualCommand.Arguments[i], out thresholdValue) == true)
                                            {
                                                Measurements[measurementIndex].ThresholdValue = thresholdValue;
                                                _actualCommand.CommandMessage = string.Empty;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            if (_actualCommand.Mnemonics.Length == 4)
                            {
                                if (_actualCommand.Mnemonics[1].Contains("meas"))
                                {
                                    int measurementIndex = 0;
                                    string index = _actualCommand.Mnemonics[1].Substring(4);

                                    if (Int32.TryParse(index, out measurementIndex))
                                    {
                                        if (_measurementsIndexes.Contains(measurementIndex) == false)
                                        {
                                            _measurementsIndexes.Add(measurementIndex);
                                        }
                                        measurementIndex = measurementIndex - 1;

                                        if (_actualCommand.Mnemonics[2].Contains("units") && _actualCommand.Mnemonics[3].Contains("prefix"))
                                        {
                                            string name = string.Empty;
                                            if (_actualCommand.Arguments[i] != null)
                                            {
                                                Measurements[measurementIndex].UnitsPrefix = _actualCommand.Arguments[i].Trim(new char[] { '"' });
                                                _actualCommand.CommandMessage = string.Empty;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<string> _commandsTranslationsFiles = new ObservableCollection<string>();
        public ObservableCollection<string> CommandsTranslationsFiles
        {
            get
            {
                return _commandsTranslationsFiles;
            }
            set
            {
                if (_commandsTranslationsFiles != value)
                {
                    _commandsTranslationsFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<ScopeCommand> _commands = new ObservableCollection<ScopeCommand>();
        public ObservableCollection<ScopeCommand> Commands
        {
            get
            {
                return _commands;
            }
            set
            {
                if (_commands != value)
                {
                    _commands = value;
                    OnPropertyChanged();
                }
            }
        }

        private PortOperatorBase _scopePort;
        public PortOperatorBase ScopePort
        {
            get
            {
                return _scopePort;
            }
            set
            {
                if (_scopePort != value)
                {
                    _scopePort = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<string> _rs232Ports = new ObservableCollection<string>();
        public ObservableCollection<string> RS232Ports
        {
            get
            {
                return _rs232Ports;
            }
            set
            {
                if (_rs232Ports != value)
                {
                    _rs232Ports = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<string> _gpibPorts = new ObservableCollection<string>();
        public ObservableCollection<string> GPIBPorts
        {
            get
            {
                return _gpibPorts;
            }
            set
            {
                if (_gpibPorts != value)
                {
                    _gpibPorts = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<ScopeCommand> _actualBufferCommands = new ObservableCollection<ScopeCommand>();
        private ObservableCollection<ScopeCommand> _bufferCommands = new ObservableCollection<ScopeCommand>();
        public ObservableCollection<ScopeCommand> BufferCommands
        {
            get
            {
                return _bufferCommands;
            }
            set
            {
                if (_bufferCommands != value)
                {
                    _bufferCommands = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Constructor

        public Scope()
        {
            string[] content1 = null;
            string[] content2 = null;

            List<string> list1 = new List<string>();
            List<string> list2 = new List<string>();

            try
            {
                Logger.WriteMessage("Scope:Scope");

                string translationFilesPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName + "\\DevicesTypes\\ScopeTypes\\";
                DirectoryInfo info = new DirectoryInfo(translationFilesPath);
                FileInfo[] files = info.GetFiles("*.xml");

                for (int i = 0; i < files.Length; i++)
                {
                    CommandsTranslationsFiles.Add(files[i].Name.Replace(files[i].Extension, ""));
                }

                ActualCommandsTranslationsFile = CommandsTranslationsFiles[0];

                ScopeCommand.commandsTranslation = commandsTranslation;

                content1 = PortUltility.FindAddresses(PortType.RS232);
                content2 = PortUltility.FindRS232Type(content1);

                for (int i = 0; i < content2.Length; i++)
                {
                    if (content2[i].Contains("LPT"))
                    {
                        continue;
                    }
                    list1.Add(content1[i]);
                    list2.Add(content2[i]);
                }

                content1 = list1.ToArray();
                content2 = list2.ToArray();

                for (int i = 0; i < content2.Length; i++)
                {
                    RS232Ports.Add(content2[i]);
                }

                content1 = PortUltility.FindAddresses(PortType.USB);
                content1 = PortUltility.FindAddresses(PortType.GPIB);

                for (int i = 0; i < content1.Length; i++)
                {
                    GPIBPorts.Add(content1[i]);
                }

                content1 = PortUltility.FindAddresses(PortType.LAN);

                //ActualAddress = GPIBPorts[0];
                ConnectionStatus = "Disconnected";

                _initialized = true;
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        public void Dispose()
        {
            try
            {
                Logger.WriteMessage("Scope:Dispose");

                StopStatusThread();
                StopMeasurementsThread();

                ClearDisplayMenu();

                _scopePort?.Close();
                _initialized = false;
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool CreatePortInstance()
        {
            bool returnValue = false;

            if (string.IsNullOrEmpty(ActualAddress) == false)
            {
                if (_communicationType == PortType.RS232)
                {
                    try
                    {
                        _scopePort = new RS232PortOperator(ActualAddress, (int)Rs232Baudrate, SerialParity.None, SerialStopBitsMode.One, 8);
                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        returnValue = false;
                    }
                }
                if (_communicationType == PortType.USB)
                {
                    try
                    {
                        _scopePort = new USBPortOperator(ActualAddress);
                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        returnValue = false;
                    }
                }
                if (_communicationType == PortType.GPIB)
                {
                    try
                    {
                        _scopePort = new GPIBPortOperator(ActualAddress);
                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        returnValue = false;
                    }
                }
                if (_communicationType == PortType.LAN)
                {
                    try
                    {
                        _scopePort = new LANPortOperator(ActualAddress);
                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        returnValue = false;
                    }
                }

                if (returnValue == true)
                {
                    _scopePort.Timeout = 3000;
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        public void OpenPort()
        {
            string replyValue = string.Empty;
            ScopeCommand getNameCommand = null;
            bool returnValue = false;

            try
            {
                if (_initialized)
                {
                    if (_scopePort == null || _scopePort.IsPortOpen == false)
                    {
                        returnValue = CreatePortInstance();
                    }
                    if (returnValue == true)
                    {
                        if (_scopePort?.IsPortOpen == false)
                        {
                            _scopePort?.Open();
                            if (_scopePort is RS232PortOperator)
                            {
                                BindOrRemoveDataReceivedEvent();
                            }
                        }
                        else
                        {
                            StopStatusThread();
                            StopMeasurementsThread();

                            Name = string.Empty;

                            _scopePort?.Close();
                        }

                        IsConnected = (_scopePort != null) ? _scopePort.IsPortOpen : false;
                        ConnectionStatus = (IsConnected == true) ? "Connected" : "Disconnected";

                        if (IsConnected == true)
                        {
                            getNameCommand = new ScopeCommand() { CommandMessage = "get_id", IsJournaled = false };

                            if (getNameCommand.IsValid == true)
                            {
                                replyValue = Write(getNameCommand);
                                if (string.IsNullOrEmpty(replyValue) == false)
                                {
                                    string[] splittedReply = replyValue.Split(new char[] { ',' }, StringSplitOptions.None);
                                    if (splittedReply.Length > 2)
                                    {
                                        Name = splittedReply[0] + " " + splittedReply[1];
                                    }
                                }
                            }

                            StartStatusThread();
                            StartMeasurementsThread();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private void BindOrRemoveDataReceivedEvent()
        {
            if (_scopePort is RS232PortOperator portOperator)
            {
                if (_scopePort.RealTimeReceive)
                {
                    ((RS232PortOperator)_scopePort).DataReceived += PortOperator_DataReceived;
                }
                else
                {
                    ((RS232PortOperator)_scopePort).DataReceived -= PortOperator_DataReceived;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Read()
        {
            Read(true, 10);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isUntilNewLine"></param>
        /// <param name="specifiedCount"></param>
        public void Read(bool isUntilNewLine, int specifiedCount)
        {
            string result;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (_isAsciiCommand)
                {
                    result = isUntilNewLine ? _scopePort?.ReadLine() : _scopePort?.Read(specifiedCount);
                }
                else
                {
                    byte[] bytes = isUntilNewLine ? _scopePort?.ReadToBytes() : _scopePort?.ReadToBytes(specifiedCount);
                    if (ByteEx.TryParseByteToByteString(bytes, out string byteString))
                    {
                        result = byteString;
                    }
                    else
                    {
                        throw new InvalidCastException("Unable to convert the data received from the receive buffer");
                    }
                }
            }
            catch (IOTimeoutException)
            {
                //result = Resources.ReadTimeout;
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }

            //Invoke(new Action(() => DisplayToTextBox($"[Time:{stopwatch.ElapsedMilliseconds}ms] Read:  {result}")));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] WriteBuffer()
        {
            string[] returnValue = null;

            try
            {
                returnValue = new string[Commands.Count];
                for (int i = 0; i < Commands.Count; i++)
                {
                    returnValue[i] = Write(Commands[i]);
                }

                Commands.Clear();
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <param name="addToBuffer"></param>
        /// <param name="isValidCommand"></param>
        /// <returns></returns>
        public string Write(ScopeCommand command)
        {
            string returnValue = string.Empty;
            string asciiString = string.Empty;
            byte[] byteArray = null;
            string commandMessage = string.Empty;

            Stopwatch stopwatch = new Stopwatch();

            _isWritingError = false;

            lock (_scopeSendCommandLock)
            {
                try
                {
                    WaitToFinishCommandProcess(5);

                    commandMessage = string.Copy(command.CommandMessage);

                    if (string.IsNullOrEmpty(commandMessage) == false)
                    {
                        if (_isAsciiCommand)
                        {
                            asciiString = commandMessage;
                        }
                        else
                        {
                            if (StringEx.TryParseByteStringToByte(commandMessage, out byte[] bytes))
                            {
                                byteArray = bytes;
                            }
                            else
                            {
                                _isWritingError = true;
                                return returnValue;
                            }
                        }

                        if (_scopePort?.IsPortOpen == true)
                        {
                            stopwatch.Start();

                            if (_isAsciiCommand)
                            {
                                if (_appendNewLine)
                                {
                                    returnValue = _scopePort?.WriteLine(asciiString);
                                }
                                else
                                {
                                    _scopePort?.Write(asciiString);
                                }
                            }
                            else
                            {
                                if (_appendNewLine)
                                {
                                    _scopePort?.WriteLine(byteArray);
                                }
                                else
                                {
                                    _scopePort?.Write(byteArray);
                                }
                            }

                            stopwatch.Stop();
                            command.ExecuteTime = stopwatch.ElapsedMilliseconds;
                        }

                        if (command.IsJournaled == true)
                        {
                            _actualBufferCommands.Add(command);
                            BufferCommands = _actualBufferCommands;
                        }
                    }
                }
                catch (Exception ex)
                {
                    #region Exception

                    stopwatch.Stop();
                    command.ExecuteTime = stopwatch.ElapsedMilliseconds;

                    if (command.IsJournaled == true)
                    {
                        _actualBufferCommands.Add(command);
                        BufferCommands = _actualBufferCommands;
                    }

                    command.IsValid = false;
                    command.CountSent++;

                    returnValue = null;

                    Logger.ExceptionHandler(ex, ex.Message);

                    #endregion
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        public void WaitToFinishCommandProcess(int waitToFinishTime)
        {
            bool busy = false;
            bool operationComplete = false;
            string ack = string.Empty;
            int waitTime = 0;

            ScopeCommand getBusyCommand = new ScopeCommand() { CommandMessage = "get_busy", IsJournaled = false };
            ScopeCommand getOpcCommand = new ScopeCommand() { CommandMessage = "get_opc", IsJournaled = false };

            try
            {
                if (_scopePort != null)
                {
                    if (_scopePort.IsPortOpen)
                    {
                        ack = _scopePort?.WriteLine(getBusyCommand.CommandMessage);
                        busy = ack.Contains("1");
                        while (busy == true)
                        {
                            ack = _scopePort?.WriteLine(getBusyCommand.CommandMessage);
                            busy = ack.Contains("1");

                            waitTime++;
                            if (waitTime >= (_scopePort?.Timeout / waitToFinishTime))
                            {
                                break;
                            }

                            Thread.Sleep(waitToFinishTime);
                        }

                        waitTime = 0;

                        ack = _scopePort?.WriteLine(getOpcCommand.CommandMessage);
                        operationComplete = ack.Contains("1");
                        while (operationComplete == false)
                        {
                            ack = _scopePort?.WriteLine(getOpcCommand.CommandMessage);
                            operationComplete = ack.Contains("1");

                            waitTime++;
                            if (waitTime >= (_scopePort?.Timeout / waitToFinishTime))
                            {
                                break;
                            }

                            Thread.Sleep(waitToFinishTime);
                        }

                        Thread.Sleep(waitToFinishTime);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PortOperator_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.BytesToRead > 0)
            {
                Thread.Sleep(10);
                Read(false, e.BytesToRead);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private void StartStatusThread()
        {
            try
            {
                _innerFlagScopeStatusThread = true;
                _scopeStatusThread = new Thread(new ThreadStart(GetScopeStatus));
                _scopeStatusThread.Name = "Check scope status";
                _scopeStatusThread.IsBackground = true;
                _scopeStatusThread.Priority = ThreadPriority.Normal;
                _scopeStatusThread.Start();
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private void StopStatusThread()
        {
            try
            {
                if (_innerFlagScopeStatusThread == true)
                {
                    _innerFlagScopeStatusThread = false;
                    _scopeStatusThread.Join();
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private void GetScopeStatus()
        {
            string replyValue = string.Empty;
            byte statusValue = 0;
            SESR status = SESR.None;
            int eventValue = 0;
            ScopeCommand getStatusCommand = new ScopeCommand() { CommandMessage = "get_event_status_register", IsJournaled = false };
            ScopeCommand getEventCommand = new ScopeCommand() { CommandMessage = "get_event", IsJournaled = false };

            int checkConnection = 0;

            Logger.WriteMessage("Start check scope status thread:" + Thread.CurrentThread.ManagedThreadId.ToString());

            while (_innerFlagScopeStatusThread)
            {
                try
                {
                    replyValue = Write(getStatusCommand);
                    if (String.IsNullOrEmpty(replyValue) == false)
                    {
                        if (byte.TryParse(replyValue, out statusValue))
                        {
                            status = (SESR)statusValue;
                            EventStatusRegister = status;
                        }
                    }

                    replyValue = Write(getEventCommand);
                    if (String.IsNullOrEmpty(replyValue) == false)
                    {
                        if (int.TryParse(replyValue, out eventValue))
                        {
                            Event = eventValue;
                        }
                    }

                    checkConnection++;
                    if (checkConnection >= 10)//every 5 seconds
                    {
                        checkConnection = 0;

                        if (_scopePort.IsPortOpen == true)
                        {
                            IsConnected = true;
                            ConnectionStatus = "Connected";
                        }
                        else
                        {
                            IsConnected = false;
                            ConnectionStatus = "Disconnected";

                            //try open connection
                            OpenPort();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.ExceptionHandler(ex, ex.Message);
                }

                Thread.Sleep(_scopeStatusThreadDelay);
            }

            Logger.WriteMessage("Stop check scope status thread:" + Thread.CurrentThread.ManagedThreadId.ToString());
        }
        /// <summary>
        /// 
        /// </summary>
        private void StartMeasurementsThread()
        {
            try
            {
                _innerFlagScopeMeasurementsThread = true;
                _scopeMeasurementsThread = new Thread(new ThreadStart(GetScopeMeasurements));
                _scopeMeasurementsThread.Name = "Check scope measurements";
                _scopeMeasurementsThread.IsBackground = true;
                _scopeMeasurementsThread.Priority = ThreadPriority.Normal;
                _scopeMeasurementsThread.Start();

                _innerFlagScopeMeasurementsInfoThread = true;
                _scopeMeasurementsInfoThread = new Thread(new ThreadStart(GetScopeMeasurementsInfo));
                _scopeMeasurementsInfoThread.Name = "Check scope measurements info";
                _scopeMeasurementsInfoThread.IsBackground = true;
                _scopeMeasurementsInfoThread.Priority = ThreadPriority.Normal;
                _scopeMeasurementsInfoThread.Start();
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private void StopMeasurementsThread()
        {
            try
            {
                if (_innerFlagScopeMeasurementsThread == true)
                {
                    _innerFlagScopeMeasurementsThread = false;
                    _scopeMeasurementsThread.Join();
                }

                if (_innerFlagScopeMeasurementsInfoThread == true)
                {
                    _innerFlagScopeMeasurementsInfoThread = false;
                    _scopeMeasurementsInfoThread.Join();
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private void GetScopeMeasurements()
        {
            string replyValue = string.Empty;

            int measurementIndex = 1;
            ScopeCommand getMeasurementCommand = new ScopeCommand()
            {
                IsJournaled = false
            };

            double[] actualMeasurementValue = new double[4];
            double[] previousMeasurementValue = new double[4];
            double filterGain = 0.1;
            uint[] counterBadSample = new uint[4];
            bool isIndexFromList = false;

            Logger.WriteMessage("Start check scope measurements thread:" + Thread.CurrentThread.ManagedThreadId.ToString());

            while (_innerFlagScopeMeasurementsThread)
            {
                DataMeasurementsUpdated = false;

                try
                {
                    _isInsideScopeMeasurementsThread = true;

                    #region Get measurement value

                    isIndexFromList = false;
                    for (int i = 0; i < MeasurementsIndexes.Count; i++)
                    {
                        if (measurementIndex == MeasurementsIndexes[i])
                        {
                            isIndexFromList = true;
                            break;
                        }
                    }

                    if (isIndexFromList)
                    {
                        getMeasurementCommand.CommandMessage = "measurement:meas" + measurementIndex + ":value?";

                        replyValue = Write(getMeasurementCommand);

                        if (String.IsNullOrEmpty(replyValue) == false)
                        {
                            double measurementValue = 0.0;
                            if (Double.TryParse(replyValue, out measurementValue) == true)
                            {
                                actualMeasurementValue[measurementIndex - 1] = measurementValue - filterGain * (measurementValue - previousMeasurementValue[measurementIndex - 1]);
                                previousMeasurementValue[measurementIndex - 1] = measurementValue;
                            }

                            Measurements[measurementIndex - 1].ActualValue = actualMeasurementValue[measurementIndex - 1];// measurementValue;
                        }
                    }

                    measurementIndex++;
                    if (measurementIndex > Measurements.Length)
                    {
                        measurementIndex = 1;
                        DataMeasurementsUpdated = true;
                    }

                    #endregion
                }
                catch (Exception ex)
                {
                    Logger.ExceptionHandler(ex, ex.Message);
                }

                _isInsideScopeMeasurementsThread = false;

                Thread.Sleep(_measurementsThreadDelay);
            }

            Logger.WriteMessage("Stop check scope measurements thread:" + Thread.CurrentThread.ManagedThreadId.ToString());
        }
        /// <summary>
        /// 
        /// </summary>
        private void GetScopeMeasurementsInfo()
        {
            string replyValue = string.Empty;

            int measurementIndex = 1;
            ScopeCommand getMeasurementCommand = new ScopeCommand()
            {
                IsJournaled = false
            };

            bool isIndexFromList = false;

            Logger.WriteMessage("Start check scope measurements info thread:" + Thread.CurrentThread.ManagedThreadId.ToString());

            while (_innerFlagScopeMeasurementsInfoThread)
            {
                try
                {
                    _isInsideScopeMeasurementsInfoThread = true;

                    isIndexFromList = false;

                    for (int i = 0; i < MeasurementsIndexes.Count; i++)
                    {
                        if (measurementIndex == MeasurementsIndexes[i])
                        {
                            isIndexFromList = true;
                            break;
                        }
                    }

                    if (isIndexFromList)
                    {
                        #region Get measurement units

                        getMeasurementCommand.CommandMessage = "measurement:meas" + measurementIndex + ":units?";

                        replyValue = Write(getMeasurementCommand);

                        if (String.IsNullOrEmpty(replyValue) == false)
                        {
                            string measurementUnits = replyValue.Trim(new char[] { '\"', '\n' });
                            Measurements[measurementIndex - 1].Units = measurementUnits;
                        }

                        #endregion

                        #region Get measurement type

                        getMeasurementCommand.CommandMessage = "measurement:meas" + measurementIndex + ":type?";

                        replyValue = Write(getMeasurementCommand);

                        if (String.IsNullOrEmpty(replyValue) == false)
                        {
                            string measurementType = replyValue.Trim(new char[] { '\n' });
                            Measurements[measurementIndex - 1].Type = measurementType;
                        }

                        #endregion

                        #region Get measurement source1

                        getMeasurementCommand.CommandMessage = "measurement:meas" + measurementIndex + ":source1?";

                        replyValue = Write(getMeasurementCommand);

                        if (String.IsNullOrEmpty(replyValue) == false)
                        {
                            string measurementSource1 = replyValue.Trim(new char[] { '\n' });
                            Measurements[measurementIndex - 1].Source_1 = measurementSource1;
                        }

                        #endregion
                    }

                    measurementIndex++;
                    if (measurementIndex > Measurements.Length)
                    {
                        measurementIndex = 1;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ExceptionHandler(ex, ex.Message);
                }

                _isInsideScopeMeasurementsInfoThread = false;

                Thread.Sleep(_measurementsInfoThreadDelay);
            }

            Logger.WriteMessage("Stop check scope measurements info thread:" + Thread.CurrentThread.ManagedThreadId.ToString());
        }
        /// <summary>
        /// 
        /// </summary>
        public void ClearBufferCommands()
        {
            _actualBufferCommands.Clear();
            BufferCommands.Clear();
        }
        /// <summary>
        /// 
        /// </summary>
        public void CreateScriptFile()
        {

        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool SetMeasurements()
        {
            bool returnValue = false;

            string reply = string.Empty;

            for (int i = 0; i < _measurements.Length; i++)
            {
                _measurements[i].Type = string.Empty;
                _measurements[i].Source_1 = "CH" + _measurements[i].ID.ToString();
                _measurements[i].State = Measurement.MeasurementState.On;

                ActualCommand.CommandMessage = "measurement:meas" + _measurements[i].ID.ToString() + ":type" + " " + _measurements[i].Type;
                reply = Write(ActualCommand);

                ActualCommand.CommandMessage = "measurement:meas" + _measurements[i].ID.ToString() + ":source1" + " " + _measurements[i].Source_1;
                reply = Write(ActualCommand);

                //IMPORTANT:The source specified by MEASUrement:MEAS<x>:SOURCE1 must be selected for the measurement to be
                //displayed.The source can be selected using the SELect:CH<x> command.
                ActualCommand.CommandMessage = "select:" + _measurements[i].Source_1 + " " + "on";
                reply = Write(ActualCommand);

                ActualCommand.CommandMessage = "measurement:meas" + _measurements[i].ID.ToString() + ":state" + " " + _measurements[i].State;
                reply = Write(ActualCommand);
            }

            //Select channel 1
            ActualCommand.CommandMessage = "select:ch1" + " " + "on";
            reply = Write(ActualCommand);

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool GetMeasurements()
        {
            bool returnValue = false;
            string reply = string.Empty;

            for (int i = 0; i < _measurements.Length; i++)
            {
                ActualCommand.CommandMessage = "measurement:meas" + _measurements[i].ID + ":value?";
                reply = Write(ActualCommand);
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool ClearDisplayMenu()
        {
            bool returnValue = false;
            string reply = string.Empty;

            ActualCommand.CommandMessage = "clearmenu";
            reply = Write(ActualCommand);

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public bool EnableChannel(object x)
        {
            bool returnValue = false;

            string reply = string.Empty;
            string onOff = string.Empty;
            string channel = string.Empty;
            bool enable = false;

            object[] data = (object[])x;

            if (data != null && data.Length > 1)
            {
                channel = data[0].ToString();
                enable = (bool)data[1];

                onOff = (enable) ? "on" : "off";
                ActualCommand.CommandMessage = "select" + ":" + channel + " " + onOff;
                reply = Write(ActualCommand);
            }

            return returnValue;
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Data validation

        private Dictionary<String, List<String>> _errors = new Dictionary<string, List<string>>();

        // Adds the specified error to the errors collection if it is not already 
        // present, inserting it in the first position if isWarning is false. 
        public void AddError(string propertyName, string error, bool isWarning)
        {
            if (!_errors.ContainsKey(propertyName))
            {
                _errors[propertyName] = new List<string>();
            }
            if (!_errors[propertyName].Contains(error))
            {
                if (isWarning)
                {
                    _errors[propertyName].Add(error);
                }
                else
                {
                    _errors[propertyName].Insert(0, error);
                }
            }
        }
        // Removes the specified error from the errors collection if it is present. 
        public void RemoveError(string propertyName, string error)
        {
            if (_errors.ContainsKey(propertyName) && _errors[propertyName].Contains(error))
            {
                _errors[propertyName].Remove(error);
                if (_errors[propertyName].Count == 0)
                {
                    _errors.Remove(propertyName);
                }
            }
        }

        #endregion

        #region IDataErrorInfo Members

        public string Error
        {
            get
            {
                return string.Empty;
            }
        }
        public string this[string propertyName]
        {
            get
            {
                return (!_errors.ContainsKey(propertyName) ? null : String.Join(Environment.NewLine, _errors[propertyName]));
            }
        }

        #endregion
    }
}
