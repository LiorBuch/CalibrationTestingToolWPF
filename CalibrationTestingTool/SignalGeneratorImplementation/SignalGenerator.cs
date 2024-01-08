using CalibrationToolTester.GlobalLoger;
using Ivi.Visa;
using CalibrationToolTester.ScopeImplementation;
using CalibrationToolTester.SignalGeneratorImplementation.Port;
using CalibrationToolTester.SignalGeneratorImplementation.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CalibrationToolTester.SignalGeneratorImplementation
{
    public class SignalGenerator : INotifyPropertyChanged, IDataErrorInfo, IDisposable
    {
        #region Constants

        private const string GENERATOR_DISCONNECTED_ERROR = "Signal generator disconnected";

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
        public enum SignalGeneratorOperatingMode
        {
            Normal = 0,
            Pulse = 1,
            FixedDutyCycle = 2,
            Pll = 3
        }
        public enum SignalGeneratorControlMode
        {
            Off = 0,
            FM = 1,
            AM = 2,
            PWM = 3,
            VCO
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

        private readonly object _signalGeneratorSendCommandLock = new object();

        public static CommandTranslationService commandsTranslation = new CommandTranslationService();

        private bool _isAsciiCommand = true;
        private bool _isWritingError = false;
        private bool _appendNewLine = true;
        private bool _initialized = false;

        private Thread _signalGeneratorStatusThread = null;
        private bool _innerFlagSignalGeneratorStatusThread = false;

        private static int _signalGeneratorStatusThreadDelay = 500;
        public static int SignalGeneratorStatusThreadDelay
        {
            get
            {
                return _signalGeneratorStatusThreadDelay;
            }
            set
            {
                _signalGeneratorStatusThreadDelay = value;
                _signalGeneratorStatusThreadDelay = (_signalGeneratorStatusThreadDelay <= 0) ? 1 : _signalGeneratorStatusThreadDelay;
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
                _actualAddress = value;
                OnPropertyChanged();
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
                _isConnected = value;
                OnPropertyChanged();
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
                        AddError("ConnectionStatus", GENERATOR_DISCONNECTED_ERROR, false);
                    }
                    else
                    {
                        RemoveError("ConnectionStatus", GENERATOR_DISCONNECTED_ERROR);
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
                _eventStatusRegister = value;
                OnPropertyChanged();
            }
        }

        private SignalGeneratorOperatingMode _operatingMode;
        public SignalGeneratorOperatingMode OperatingMode
        {
            get
            {
                return _operatingMode;
            }
            set
            {
                SignalGeneratorCommand signalGeneratorCommand = new SignalGeneratorCommand();
                string reply = string.Empty;

                _operatingMode = value;
                signalGeneratorCommand.CommandMessage = (_operatingMode == SignalGeneratorOperatingMode.Normal) ? "set_normal_operating_mode" : signalGeneratorCommand.CommandMessage;
                signalGeneratorCommand.CommandMessage = (_operatingMode == SignalGeneratorOperatingMode.Pulse) ? "set_pulse_operating_mode" : signalGeneratorCommand.CommandMessage;
                signalGeneratorCommand.CommandMessage = (_operatingMode == SignalGeneratorOperatingMode.FixedDutyCycle) ? "set_fixed_operating_mode" : signalGeneratorCommand.CommandMessage;
                signalGeneratorCommand.CommandMessage = (_operatingMode == SignalGeneratorOperatingMode.Pll) ? "set_pll_operating_mode" : signalGeneratorCommand.CommandMessage;

                reply = Write(signalGeneratorCommand);

                OnPropertyChanged();
            }
        }

        private string _controlMode;
        public string ControlMode
        {
            get
            {
                return _controlMode;
            }
            set
            {
                _controlMode = value;
                OnPropertyChanged();
            }
        }

        private double _frequency;
        public double Frequency
        {
            get
            {
                return _frequency;
            }
            set
            {
                _frequency = value;
                OnPropertyChanged();
            }
        }

        private double _amplitude;
        public double Amplitude
        {
            get
            {
                return _amplitude;
            }
            set
            {
                _amplitude = value;
                OnPropertyChanged();
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

                string translationFilesPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName + "\\DevicesTypes\\SignalGeneratorTypes\\";
                commandsTranslation.LoadTranslations(translationFilesPath + _actualCommandsTranslationsFile + ".xml");

                OnPropertyChanged();
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

        SignalGeneratorCommand _actualCommand = new SignalGeneratorCommand();
        public SignalGeneratorCommand ActualCommand
        {
            get
            {
                return _actualCommand;
            }
            set
            {
                _actualCommand = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<SignalGeneratorCommand> _commands = new ObservableCollection<SignalGeneratorCommand>();
        public ObservableCollection<SignalGeneratorCommand> Commands
        {
            get
            {
                return _commands;
            }
            set
            {
                _commands = value;
                OnPropertyChanged();
            }
        }

        private PortOperatorBase _signalGeneratorPort;
        public PortOperatorBase SignalGeneratorPort
        {
            get
            {
                return _signalGeneratorPort;
            }
            set
            {
                _signalGeneratorPort = value;
                OnPropertyChanged();
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

        private ObservableCollection<SignalGeneratorCommand> _actualBufferCommands = new ObservableCollection<SignalGeneratorCommand>();
        private ObservableCollection<SignalGeneratorCommand> _bufferCommands = new ObservableCollection<SignalGeneratorCommand>();
        public ObservableCollection<SignalGeneratorCommand> BufferCommands
        {
            get
            {
                return _bufferCommands;
            }
            set
            {
                _bufferCommands = value;
                OnPropertyChanged();
            }
        }

        private Scope _scopeObject = null;
        public Scope ScopeObject
        {
            get
            {
                return _scopeObject;
            }
            set
            {
                _scopeObject = value;
                OnPropertyChanged();
            }
        }

        double _pidAmplitudeKpValue = 0.1;
        public double PIDAmplitudeKpValue
        {
            get
            {
                return _pidAmplitudeKpValue;
            }
            set
            {
                _pidAmplitudeKpValue = value;
                OnPropertyChanged();
            }
        }

        double _pidAmplitudeKiValue = 0.1;
        public double PIDAmplitudeKiValue
        {
            get
            {
                return _pidAmplitudeKiValue;
            }
            set
            {
                _pidAmplitudeKiValue = value;
                OnPropertyChanged();
            }
        }

        double _pidAmplitudeKdValue = 0.1;
        public double PIDAmplitudeKdValue
        {
            get
            {
                return _pidAmplitudeKdValue;
            }
            set
            {
                _pidAmplitudeKdValue = value;
                OnPropertyChanged();
            }
        }

        private double _pidMaxIntegratorValue = 0.5;
        public double PIDMaxIntegratorValue
        {
            get
            {
                return _pidMaxIntegratorValue;
            }
            set
            {
                _pidMaxIntegratorValue = value;
                OnPropertyChanged();
            }
        }

        private double _pidMaxAmplitudeActuatorValue = 3.0;
        public double PIDMaxAmplitudeActuatorValue
        {
            get
            {
                return _pidMaxAmplitudeActuatorValue;
            }
            set
            {
                _pidMaxAmplitudeActuatorValue = value;
                OnPropertyChanged();
            }
        }

        double _pidAmplitudeThresholdValue = 0.1;
        public double PIDAmplitudeThresholdValue
        {
            get
            {
                return _pidAmplitudeThresholdValue;
            }
            set
            {
                _pidAmplitudeThresholdValue = value;
                OnPropertyChanged();
            }
        }

        private int _pidAmplitudeTunningTimeWaitValue = 5000;
        public int PIDAmplitudeTunningTimeWaitValue
        {
            get
            {
                return _pidAmplitudeTunningTimeWaitValue;
            }
            set
            {
                _pidAmplitudeTunningTimeWaitValue = value;
                OnPropertyChanged();
            }
        }

        double _pidFrequencyKpValue = 0.1;
        public double PIDFrequencyKpValue
        {
            get
            {
                return _pidFrequencyKpValue;
            }
            set
            {
                _pidFrequencyKpValue = value;
                OnPropertyChanged();
            }
        }

        double _pidFrequencyKiValue = 0.1;
        public double PIDFrequencyKiValue
        {
            get
            {
                return _pidFrequencyKiValue;
            }
            set
            {
                _pidFrequencyKiValue = value;
                OnPropertyChanged();
            }
        }

        double _pidFrequencyKdValue = 0.1;
        public double PIDFrequencyKdValue
        {
            get
            {
                return _pidFrequencyKdValue;
            }
            set
            {
                _pidFrequencyKdValue = value;
                OnPropertyChanged();
            }
        }

        private double _pidFrequencyMaxIntegratorValue = 1000;
        public double PIDFrequencyMaxIntegratorValue
        {
            get
            {
                return _pidFrequencyMaxIntegratorValue;
            }
            set
            {
                _pidFrequencyMaxIntegratorValue = value;
                OnPropertyChanged();
            }
        }

        private double _pidMaxFrequencyActuatorValue = 3.0;
        public double PIDMaxFrequencyActuatorValue
        {
            get
            {
                return _pidMaxFrequencyActuatorValue;
            }
            set
            {
                _pidMaxFrequencyActuatorValue = value;
                OnPropertyChanged();
            }
        }

        double _pidFrequencyThresholdValue = 100000;
        public double PIDFrequencyThresholdValue
        {
            get
            {
                return _pidFrequencyThresholdValue;
            }
            set
            {
                _pidFrequencyThresholdValue = value;
                OnPropertyChanged();
            }
        }

        private int _pidFrequencyTunningTimeWaitValue = 30000;
        public int PIDFrequencyTunningTimeWaitValue
        {
            get
            {
                return _pidFrequencyTunningTimeWaitValue;
            }
            set
            {
                _pidFrequencyTunningTimeWaitValue = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public SignalGenerator()
        {
            string[] content1 = null;
            string[] content2 = null;

            List<string> list1 = new List<string>();
            List<string> list2 = new List<string>();

            try
            {
                Logger.WriteMessage("SignalGenerator:SignalGenerator");

                string filePath = AppDomain.CurrentDomain.BaseDirectory;
                Configuration testAppConfiguration = ConfigurationManager.OpenExeConfiguration("SignalGeneratorImplementation.dll");
                // Get the appSettings section
                AppSettingsSection testAppConfigAppSettings = (AppSettingsSection)testAppConfiguration.GetSection("appSettings");
                if (testAppConfigAppSettings != null)
                {
                    #region

                    PIDAmplitudeKpValue = Convert.ToDouble(testAppConfigAppSettings.Settings["PIDAmplitudeKpValue"].Value);
                    PIDAmplitudeKiValue = Convert.ToDouble(testAppConfigAppSettings.Settings["PIDAmplitudeKiValue"].Value);
                    PIDAmplitudeKdValue = Convert.ToDouble(testAppConfigAppSettings.Settings["PIDAmplitudeKdValue"].Value);

                    PIDMaxIntegratorValue = Convert.ToDouble(testAppConfigAppSettings.Settings["PIDMaxIntegratorValue"].Value);
                    PIDMaxAmplitudeActuatorValue = Convert.ToDouble(testAppConfigAppSettings.Settings["PIDMaxAmplitudeActuatorValue"].Value);
                    PIDAmplitudeThresholdValue = Convert.ToDouble(testAppConfigAppSettings.Settings["PIDAmplitudeThresholdValue"].Value);
                    PIDAmplitudeTunningTimeWaitValue = Convert.ToInt32(testAppConfigAppSettings.Settings["PIDAmplitudeTunningTimeWaitValue"].Value);

                    #endregion
                }
                else
                {
                    PIDAmplitudeKpValue = 0.1;
                    PIDAmplitudeKiValue = 0.05;
                    PIDAmplitudeKdValue = 0.05;
                    PIDMaxAmplitudeActuatorValue = 1.0;
                    PIDAmplitudeThresholdValue = 0.1;

                    PIDAmplitudeTunningTimeWaitValue = 5000;
                }

                string translationFilesPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName + "\\DevicesTypes\\SignalGeneratorTypes\\";
                DirectoryInfo info = new DirectoryInfo(translationFilesPath);
                FileInfo[] files = info.GetFiles("*.xml");

                for (int i = 0; i < files.Length; i++)
                {
                    CommandsTranslationsFiles.Add(files[i].Name.Replace(files[i].Extension, ""));
                }

                ActualCommandsTranslationsFile = CommandsTranslationsFiles[0];

                SignalGeneratorCommand.commandsTranslation = commandsTranslation;

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
                Logger.WriteMessage("SignalGenerator:Dispose");

                StopStatusThread();

                _signalGeneratorPort?.Close();
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
            bool hasAddress = false;
            bool hasException = false;

            if (string.IsNullOrEmpty(ActualAddress) == false)
            {
                if (_communicationType == PortType.RS232)
                {
                    try
                    {
                        _signalGeneratorPort = new RS232PortOperator(ActualAddress, (int)Rs232Baudrate, SerialParity.None, SerialStopBitsMode.One, 8);
                        hasAddress = true;
                    }
                    catch (Exception ex)
                    {

                        hasException = true;
                    }
                }
                if (_communicationType == PortType.USB)
                {
                    try
                    {
                        _signalGeneratorPort = new USBPortOperator(ActualAddress);
                        hasAddress = true;
                    }
                    catch (Exception ex)
                    {
                        hasException = true;
                    }
                }
                if (_communicationType == PortType.GPIB)
                {
                    try
                    {
                        _signalGeneratorPort = new GPIBPortOperator(ActualAddress);
                        hasAddress = true;
                    }
                    catch (Exception ex)
                    {
                        hasException = true;
                    }
                }
                if (_communicationType == PortType.LAN)
                {
                    try
                    {
                        _signalGeneratorPort = new LANPortOperator(ActualAddress);
                        hasAddress = true;
                    }
                    catch (Exception ex)
                    {
                        hasException = true;
                    }
                }

                if (!hasException && hasAddress)
                {
                    _signalGeneratorPort.Timeout = 3000;
                }
            }

            return hasAddress;
        }
        /// <summary>
        /// 
        /// </summary>
        public void OpenPort()
        {
            string replyValue = string.Empty;
            SignalGeneratorCommand getNameCommand = new SignalGeneratorCommand() { CommandMessage = "get_id", IsJournaled = false };
            bool returnValue = false;

            try
            {
                if (_initialized)
                {
                    if (_signalGeneratorPort == null || _signalGeneratorPort.IsPortOpen == false)
                    {
                        returnValue = CreatePortInstance();
                    }

                    if (returnValue == true)
                    {
                        if (_signalGeneratorPort.IsPortOpen == false)
                        {
                            _signalGeneratorPort.Open();
                            if (_signalGeneratorPort is RS232PortOperator)
                            {
                                BindOrRemoveDataReceivedEvent();
                            }
                        }
                        else
                        {
                            StopStatusThread();

                            Name = string.Empty;

                            _signalGeneratorPort.Close();
                        }

                        IsConnected = _signalGeneratorPort.IsPortOpen;
                        ConnectionStatus = (IsConnected == true) ? "Connected" : "Disconnected";

                        if (IsConnected == true)
                        {
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
            if (_signalGeneratorPort is RS232PortOperator portOperator)
            {
                if (_signalGeneratorPort.RealTimeReceive)
                {
                    ((RS232PortOperator)_signalGeneratorPort).DataReceived += PortOperator_DataReceived;
                }
                else
                {
                    ((RS232PortOperator)_signalGeneratorPort).DataReceived -= PortOperator_DataReceived;
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
                    result = isUntilNewLine ? _signalGeneratorPort?.ReadLine() : _signalGeneratorPort?.Read(specifiedCount);
                }
                else
                {
                    byte[] bytes = isUntilNewLine ? _signalGeneratorPort?.ReadToBytes() : _signalGeneratorPort?.ReadToBytes(specifiedCount);
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

            returnValue = new string[Commands.Count];
            for (int i = 0; i < Commands.Count; i++)
            {
                returnValue[i] = Write(Commands[i]);
            }

            Commands.Clear();

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <param name="addToBuffer"></param>
        /// <param name="isValidCommand"></param>
        /// <returns></returns>
        public string Write(SignalGeneratorCommand command)
        {
            string returnValue = string.Empty;

            string asciiString = string.Empty;
            byte[] byteArray = null;
            string commandMessage = string.Empty;
            bool pidControllerCommandExecuted = false;

            Stopwatch stopwatch = new Stopwatch();

            _isWritingError = false;

            try
            {
                if (command.Arguments != null)
                {
                    if (command.Header.ToLower().Contains("amp"))
                    {
                        if (command.Arguments.Length > 0 && command.Arguments.Length < 2)
                        {
                            #region Convert amplitude argument to scientific notation

                            string[] arguments = command.Arguments;
                            arguments[0] = ConvertDoubleStringToScientificNotation(arguments[0]);
                            command.Arguments = arguments;

                            #endregion 
                        }
                        if (command.Arguments.Length > 2)
                        {
                            string[] additionalParameters = new string[1] { "" };
                            additionalParameters[0] = (command.Arguments.Length > 3) ? command.Arguments[3] : "amp";
                            pidControllerCommandExecuted = SetControlledAmplitude(command, additionalParameters);
                        }
                    }
                    if (command.Header.ToLower().Contains("frq"))
                    {
                        if (command.Arguments.Length > 2)
                        {
                            pidControllerCommandExecuted = SetControlledFrequency(command);
                        }
                    }
                }

                if (pidControllerCommandExecuted == false)
                {
                    lock (_signalGeneratorSendCommandLock)
                    {
                        WaitToFinishCommandProcess(10);

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
                            if (_signalGeneratorPort?.IsPortOpen == true)
                            {
                                stopwatch.Start();

                                if (_isAsciiCommand)
                                {
                                    if (_appendNewLine)
                                    {
                                        returnValue = _signalGeneratorPort?.WriteLine(asciiString);
                                    }
                                    else
                                    {
                                        _signalGeneratorPort?.Write(asciiString);
                                    }
                                }
                                else
                                {
                                    if (_appendNewLine)
                                    {
                                        _signalGeneratorPort?.WriteLine(byteArray);
                                    }
                                    else
                                    {
                                        _signalGeneratorPort?.Write(byteArray);
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

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="waitToFinishTime"></param>
        public void WaitToFinishCommandProcess(int waitToFinishTime)
        {
            bool busy = false;
            bool operationComplete = false;
            string ack = string.Empty;
            int waitTime = 0;

            ScopeCommand getOpcCommand = new ScopeCommand() { CommandMessage = "get_opc", IsJournaled = false };

            try
            {
                if (_signalGeneratorPort != null)
                {
                    waitTime = 0;

                    ack = _signalGeneratorPort?.WriteLine(getOpcCommand.CommandMessage);
                    operationComplete = ack.Contains("1");
                    while (operationComplete == false)
                    {
                        ack = _signalGeneratorPort?.WriteLine(getOpcCommand.CommandMessage);
                        operationComplete = ack.Contains("1");

                        waitTime++;
                        if (waitTime >= (_signalGeneratorPort?.Timeout / waitToFinishTime))
                        {
                            break;
                        }

                        Thread.Sleep(waitToFinishTime);
                    }

                    Thread.Sleep(waitToFinishTime);
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
                _innerFlagSignalGeneratorStatusThread = true;
                _signalGeneratorStatusThread = new Thread(new ThreadStart(GetSignalGeneratorStatus));
                _signalGeneratorStatusThread.IsBackground = true;
                _signalGeneratorStatusThread.Priority = ThreadPriority.Normal;
                _signalGeneratorStatusThread.Start();
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
                if (_innerFlagSignalGeneratorStatusThread == true)
                {
                    _innerFlagSignalGeneratorStatusThread = false;
                    _signalGeneratorStatusThread.Join(1000);
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
        private void GetSignalGeneratorStatus()
        {
            string replyValue = string.Empty;
            SESR status = SESR.None;
            SignalGeneratorCommand getStatusCommand = new SignalGeneratorCommand() { CommandMessage = "get_event_status_register", IsJournaled = false };

            int checkConnection = 0;

            while (_innerFlagSignalGeneratorStatusThread)
            {
                try
                {
                    replyValue = Write(getStatusCommand);
                    if (String.IsNullOrEmpty(replyValue) == false)
                    {
                        status = (SESR)Convert.ToByte(replyValue);
                        if (status != SESR.None)
                        {
                            EventStatusRegister = status;
                        }
                    }

                    checkConnection++;
                    if (checkConnection >= 10)//every 5 seconds
                    {
                        checkConnection = 0;

                        if (_signalGeneratorPort.IsPortOpen == true)
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

                }

                Thread.Sleep(_signalGeneratorStatusThreadDelay);
            }
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
        public bool SetWaveForm(object x)
        {
            bool returnValue = false;

            string reply = string.Empty;
            string waveForm = string.Empty;
            bool enable = false;

            object[] data = (object[])x;

            if (data != null && data.Length > 1)
            {
                waveForm = data[0].ToString();
                enable = (bool)data[1];

                if (enable == true)
                {
                    ActualCommand.CommandMessage = waveForm;
                    reply = Write(ActualCommand);
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public bool SetControlMode(object x)
        {
            bool returnValue = false;

            string reply = string.Empty;
            string controlMode = string.Empty;
            bool enable = false;

            object[] data = (object[])x;

            if (data != null && data.Length > 1)
            {
                controlMode = data[0].ToString();
                ControlMode = controlMode;

                enable = (bool)data[1];

                if (enable == true)
                {
                    ActualCommand.CommandMessage = controlMode;
                    reply = Write(ActualCommand);
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="frequency"></param>
        /// <returns></returns>
        public bool SetFrequency(object frequency)
        {
            bool returnValue = false;
            string reply = string.Empty;
            string[] splittedReply = null;
            double desiredFrequency = 0.0;
            double result = 0.0;

            returnValue = Double.TryParse(frequency as string, out desiredFrequency);

            if (returnValue == true)
            {
                returnValue = false;
                if (desiredFrequency != _frequency)
                {
                    ActualCommand.CommandMessage = "set_frequency " + desiredFrequency.ToString();
                    reply = Write(ActualCommand);

                    ActualCommand.CommandMessage = "get_frequency";
                    reply = Write(ActualCommand);
                    splittedReply = reply.Split(new char[] { ' ' });

                    if (splittedReply != null)
                    {
                        if (splittedReply.Length > 1)
                        {
                            reply = splittedReply[1];
                            if (string.IsNullOrEmpty(reply) == false)
                            {
                                returnValue = Double.TryParse(reply, out result);
                            }
                        }
                    }
                }
            }

            Frequency = (returnValue == true) ? result : 0.0;

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public bool SetControlledAmplitude(SignalGeneratorCommand command, params string[] additionalParameters)
        {
            bool returnValue = false;

            string stringReturnValue = string.Empty;

            #region Local parameters

            Task<string> task = null;
            int taskWaitTimeCounter = 0;

            string asciiString = string.Empty;
            byte[] byteArray = null;
            string commandMessage = string.Empty;
            bool isPIDControllerCommand = false;

            SignalGeneratorCommand getAmplitudeCommand = new SignalGeneratorCommand()
            {
                IsJournaled = false
            };

            ScopeCommand messageStateCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand messageShowCommand = new ScopeCommand()
            {
                IsJournaled = false
            };

            ScopeCommand getSource1SelectCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setSource1SelectCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getMeasurementCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setMeasurementTypeCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getMeasurementTypeCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setMeasurementSource1Command = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getMeasurementSource1Command = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setSource1VerticalScaleCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getSource1VerticalScaleCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setHorizontalScaleCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getHorizontalScaleCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setTriggerMainSourceCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getTriggerMainSourceCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setTriggerMainLevel50Command = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getTriggerMainLevel50Command = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setTriggerMainLevelCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getTriggerMainLevelCommand = new ScopeCommand()
            {
                IsJournaled = false
            };

            ScopeCommand setImmedSource1Command = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setImmedTypeCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getImmedValueCommand = new ScopeCommand()
            {
                IsJournaled = false
            };

            string desiredMeasurementType = string.Empty;

            string previousMessageState = string.Empty;
            string previousMessage = string.Empty;

            string previousSource1Select = string.Empty;
            string previousMeasurementType = string.Empty;
            string previousMeasurementSource1 = string.Empty;
            string previousSource1VerticalScale = string.Empty;
            string previousSource1HorizontalScale = string.Empty;
            string previousMainTriggerSource = string.Empty;

            string replyValue = string.Empty;

            double actualAmplitudeValue = 0.0;
            double actualGeneratorAmplitudeValue = 0.0;
            double previousAmplitudeValue = 0.0;
            double desiredAmplitudeValue = 0.0;
            double calculatedAmplitudeValue = 0.0;

            double actualFrequencyValue = 0.0;
            double desiredFrequencyValue = 0.0;
            double calculatedFrequencyValue = 0.0;

            int scopeMeasurementIndex = 0;
            int scopeMeasurementSource1 = 0;
            int commandTimeout = 0;

            Stopwatch stopwatch = new Stopwatch();
            Stopwatch pidStopwatch = new Stopwatch();

            double errorPID = 0.0;
            double integralPID = 0.0;
            double derivativePID = 0.0;
            double pidTime = 0.0;
            double actuatorPIDValue = 0.0;

            double proportionalPart = 0.0;
            double integralPart = 0.0;
            double derivativePart = 0.0;

            List<double> debugActualAmplitude = new List<double>();
            List<double> debugDesiredAmplitude = new List<double>();
            List<double> debugCommandlAmplitude = new List<double>();

            double diffrenceGeneratorScopeAmplitudeValue = 0.0;

            double actualMeasurementValue = 0.0;
            double previousMeasurementValue = 0.0;

            double triggerMainLevel = 0.0;

            #endregion

            _isWritingError = false;

            try
            {
                lock (_signalGeneratorSendCommandLock)
                {
                    stopwatch.Start();

                    #region Process arguments

                    double.TryParse(command.Arguments[0], out desiredAmplitudeValue);
                    int.TryParse(command.Arguments[1], out scopeMeasurementIndex);
                    int.TryParse(command.Arguments[2], out scopeMeasurementSource1);

                    if (additionalParameters != null)
                    {
                        if (additionalParameters.Length > 0)
                        {
                            desiredMeasurementType = additionalParameters[0];
                        }
                    }
                    if (string.IsNullOrEmpty(desiredMeasurementType) == true)
                    {
                        desiredMeasurementType = "amp";
                    }

                    #endregion

                    #region Save current and set relevant measurement parameters

                    #region Get commands

                    messageStateCommand.CommandMessage = "message:state?";
                    replyValue = ScopeObject.Write(messageStateCommand);
                    previousMessageState = replyValue.Trim().ToLower();

                    messageShowCommand.CommandMessage = "message:show?";
                    replyValue = ScopeObject.Write(messageShowCommand);
                    previousMessage = replyValue.Trim().ToLower();

                    getSource1SelectCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1?";
                    replyValue = ScopeObject.Write(getSource1SelectCommand);
                    previousMeasurementSource1 = replyValue.Trim().ToLower();

                    getSource1VerticalScaleCommand.CommandMessage = "ch" + scopeMeasurementSource1 + ":scale?";
                    replyValue = ScopeObject.Write(getSource1VerticalScaleCommand);
                    previousSource1VerticalScale = replyValue.Trim().ToLower();

                    getHorizontalScaleCommand.CommandMessage = "horizontal:scale?";
                    replyValue = ScopeObject.Write(getHorizontalScaleCommand);
                    previousSource1HorizontalScale = replyValue.Trim().ToLower();

                    getTriggerMainSourceCommand.CommandMessage = "trigger:main:edge:source?";
                    replyValue = ScopeObject.Write(getTriggerMainSourceCommand);
                    previousMainTriggerSource = replyValue.Trim().ToLower();

                    getMeasurementTypeCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":type?";
                    replyValue = ScopeObject.Write(getMeasurementTypeCommand);
                    previousMeasurementType = replyValue.Trim().ToLower();

                    getMeasurementSource1Command.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1?";
                    replyValue = ScopeObject.Write(getMeasurementSource1Command);
                    previousMeasurementSource1 = replyValue.Trim().ToLower();

                    getMeasurementCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":value?";

                    getImmedValueCommand.CommandMessage = "measurement:immed:value?";

                    getTriggerMainLevelCommand.CommandMessage = "trigger:main:level?";
                    replyValue = ScopeObject.Write(getTriggerMainLevelCommand);
                    if (String.IsNullOrEmpty(replyValue) == false)
                    {
                        Double.TryParse(replyValue, out triggerMainLevel);
                    }

                    #endregion

                    #region Set commands

                    setTriggerMainSourceCommand.CommandMessage = "trigger:main:edge:source " + "ch" + scopeMeasurementSource1;
                    replyValue = ScopeObject.Write(setTriggerMainSourceCommand);

                    #region Old code

                    //setMeasurementTypeCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":type " + desiredMeasurementType;
                    //replyValue = ScopeObject.Write(setMeasurementTypeCommand);

                    //setMeasurementSource1Command.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1 " + "ch" + scopeMeasurementSource1;
                    //replyValue = ScopeObject.Write(setMeasurementSource1Command);

                    //setTriggerMainLevel50Command.CommandMessage = "trigger:main setlevel";
                    //replyValue = ScopeObject.Write(setTriggerMainLevel50Command);

                    //if (desiredAmplitudeValue > 0.020)
                    //{
                    //    setTriggerMainLevelCommand.CommandMessage = "trigger:main:level " + Convert.ToString(-desiredAmplitudeValue / 2.0);
                    //}
                    //else
                    //{
                    //    setTriggerMainLevelCommand.CommandMessage = "trigger:main:level " + Convert.ToString(-desiredAmplitudeValue);
                    //}
                    //replyValue = ScopeObject.Write(setTriggerMainLevelCommand); 

                    #endregion

                    setImmedTypeCommand.CommandMessage = "measurement:immed :type" + desiredMeasurementType;
                    replyValue = ScopeObject.Write(setImmedTypeCommand);

                    setImmedSource1Command.CommandMessage = "measurement:immed :source1 " + "ch" + scopeMeasurementSource1;
                    replyValue = ScopeObject.Write(setImmedSource1Command);

                    #endregion

                    #endregion

                    #region Tunning amplitude

                    //messageStateCommand.CommandMessage = "message:state on";
                    //replyValue = ScopeObject.Write(messageStateCommand);
                    messageShowCommand.CommandMessage = "message:show \"Amplitude_tunning\"";
                    replyValue = ScopeObject.Write(messageShowCommand);

                    #region Initialize generator amplitude to desired value

                    //getAmplitudeCommand.CommandMessage = "amp?";
                    //if (string.IsNullOrEmpty(getAmplitudeCommand.CommandMessage) == false)
                    //{
                    //    if (_isAsciiCommand)
                    //    {
                    //        asciiString = getAmplitudeCommand.CommandMessage;
                    //    }
                    //    else
                    //    {
                    //        if (StringEx.TryParseByteStringToByte(getAmplitudeCommand.CommandMessage, out byte[] bytes))
                    //        {
                    //            byteArray = bytes;
                    //        }
                    //        else
                    //        {
                    //            _isWritingError = true;
                    //            return returnValue;
                    //        }
                    //    }
                    //    if (_signalGeneratorPort?.IsPortOpen == true)
                    //    {
                    //        if (_isAsciiCommand)
                    //        {
                    //            if (_appendNewLine)
                    //            {
                    //                stringReturnValue = _signalGeneratorPort?.WriteLine(asciiString);
                    //            }
                    //            else
                    //            {
                    //                _signalGeneratorPort?.Write(asciiString);
                    //            }
                    //        }
                    //        else
                    //        {
                    //            if (_appendNewLine)
                    //            {
                    //                _signalGeneratorPort?.WriteLine(byteArray);
                    //            }
                    //            else
                    //            {
                    //                _signalGeneratorPort?.Write(byteArray);
                    //            }
                    //        }
                    //    }
                    //}
                    //WaitToFinishCommandProcess(10);

                    //if (double.TryParse(stringReturnValue.Split(new char[] { ' ' })[1], out actualGeneratorAmplitudeValue))
                    //{
                    //    command.Arguments[0] = actualGeneratorAmplitudeValue.ToString();
                    //}

                    //if (desiredAmplitudeValue < 0.1)//if desired value less than 100 mV
                    //{
                    //    command.Arguments[0] = Convert.ToString(desiredAmplitudeValue * 2.0);//set init value *2.0
                    //}

                    #region Convert amplitude argument to scientific notation

                    command.Arguments[0] = ConvertDoubleStringToScientificNotation(command.Arguments[0]);

                    #endregion
                    //set amplitude
                    commandMessage = command.Header + " " + command.Arguments[0];
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
                        if (_signalGeneratorPort?.IsPortOpen == true)
                        {
                            if (_isAsciiCommand)
                            {
                                if (_appendNewLine)
                                {
                                    stringReturnValue = _signalGeneratorPort?.WriteLine(asciiString);
                                }
                                else
                                {
                                    _signalGeneratorPort?.Write(asciiString);
                                }
                            }
                            else
                            {
                                if (_appendNewLine)
                                {
                                    _signalGeneratorPort?.WriteLine(byteArray);
                                }
                                else
                                {
                                    _signalGeneratorPort?.Write(byteArray);
                                }
                            }
                        }
                    }
                    WaitToFinishCommandProcess(10);
                    Thread.Sleep(1000);

                    setTriggerMainLevelCommand.CommandMessage = "trigger:main:level " + Convert.ToString(triggerMainLevel);
                    replyValue = ScopeObject.Write(setTriggerMainLevelCommand);

                    for (int i = 0; i < 1; i++)
                    {
                        replyValue = ScopeObject.Write(getImmedValueCommand);
                        if (String.IsNullOrEmpty(replyValue) == false)
                        {
                            if (Double.TryParse(replyValue, out actualMeasurementValue) == true)
                            {
                                ScopeObject.Measurements[scopeMeasurementIndex - 1].ActualValue = actualMeasurementValue;
                                //Use low pass filter
                                actualAmplitudeValue = actualMeasurementValue; // actualMeasurementValue - 0.1 * (actualMeasurementValue - previousAmplitudeValue);
                                //previousAmplitudeValue = actualMeasurementValue;
                            }
                        }
                    }

                    //replyValue = ScopeObject.Write(setTriggerMainLevelCommand);

                    #endregion

                    #region Tune amplitude task

                    CancellationTokenSource tokenSource = new CancellationTokenSource();
                    CancellationToken token = tokenSource.Token;
                    task = Task.Factory.StartNew(() =>
                    {
                        do
                        {
                            try
                            {
                                #region Convert amplitude argument to scientific notation

                                command.Arguments[0] = ConvertDoubleStringToScientificNotation(command.Arguments[0]);

                                #endregion

                                commandMessage = command.Header + " " + command.Arguments[0];

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
                                            return stringReturnValue;
                                        }
                                    }
                                    if (_signalGeneratorPort?.IsPortOpen == true)
                                    {
                                        if (_isAsciiCommand)
                                        {
                                            if (_appendNewLine)
                                            {
                                                stringReturnValue = _signalGeneratorPort?.WriteLine(asciiString);
                                            }
                                            else
                                            {
                                                _signalGeneratorPort?.Write(asciiString);
                                            }
                                        }
                                        else
                                        {
                                            if (_appendNewLine)
                                            {
                                                _signalGeneratorPort?.WriteLine(byteArray);
                                            }
                                            else
                                            {
                                                _signalGeneratorPort?.Write(byteArray);
                                            }
                                        }
                                    }
                                }

                                WaitToFinishCommandProcess(10);

                                for (int i = 0; i < 1; i++)
                                {
                                    replyValue = ScopeObject.Write(getImmedValueCommand);
                                    if (String.IsNullOrEmpty(replyValue) == false)
                                    {
                                        if (Double.TryParse(replyValue, out actualMeasurementValue) == true)
                                        {
                                            ScopeObject.Measurements[scopeMeasurementIndex - 1].ActualValue = actualMeasurementValue;
                                            //Use low pass filter
                                            actualAmplitudeValue = actualMeasurementValue; //actualMeasurementValue - 0.1 * (actualMeasurementValue - previousAmplitudeValue);
                                            //previousAmplitudeValue = actualMeasurementValue;
                                        }
                                    }
                                }

                                pidStopwatch.Stop();
                                pidTime = (double)(pidStopwatch.ElapsedMilliseconds) / 1000.0;

                                errorPID = (desiredAmplitudeValue - actualAmplitudeValue);
                                proportionalPart = PIDAmplitudeKpValue * errorPID;

                                derivativePID = (pidTime != 0) ? (errorPID / pidTime) : 0.0;
                                derivativePart = PIDAmplitudeKdValue * derivativePID;

                                integralPID += errorPID;//set limit to integrator
                                integralPart = PIDAmplitudeKiValue * integralPID;
                                if (integralPart > PIDMaxIntegratorValue)
                                {
                                    integralPart = PIDMaxIntegratorValue;
                                }
                                if (integralPart < -PIDMaxIntegratorValue)
                                {
                                    integralPart = -PIDMaxIntegratorValue;
                                }

                                actuatorPIDValue = desiredAmplitudeValue + proportionalPart + integralPart + derivativePart;
                                actuatorPIDValue = Math.Round(actuatorPIDValue, 3);
                                if (actuatorPIDValue < 0.01) actuatorPIDValue = 0.01;//min 10 mV
                                if (actuatorPIDValue > PIDMaxAmplitudeActuatorValue) actuatorPIDValue = PIDMaxAmplitudeActuatorValue;

                                command.Arguments[0] = actuatorPIDValue.ToString();

                                debugActualAmplitude.Add(actualAmplitudeValue);
                                debugDesiredAmplitude.Add(desiredAmplitudeValue);
                                debugCommandlAmplitude.Add(actuatorPIDValue);

                                pidStopwatch.Restart();
                            }
                            catch (Exception ex)
                            {
                                Logger.ExceptionHandler(ex, ex.Message);
                            }
                        }
                        while (Math.Abs(actualAmplitudeValue - desiredAmplitudeValue) > PIDAmplitudeThresholdValue && !token.IsCancellationRequested);

                        return stringReturnValue;

                    }, token);

                    taskWaitTimeCounter = 0;

                    while (!task.IsCompleted)
                    {
                        #region
                        taskWaitTimeCounter++;
                        task.Wait(50);
                        if (taskWaitTimeCounter > (PIDAmplitudeTunningTimeWaitValue / 50))
                        {
                            tokenSource.Cancel();
                            task.Wait();
                            break;
                        }
                        #endregion
                    }

                    stringReturnValue = task.Result;

                    #endregion

                    #endregion

                    #region Revert saved scope configurations

                    //if (string.IsNullOrEmpty(previousMeasurementSource1) == false)
                    //{
                    //    getSource1SelectCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1 " + previousMeasurementSource1;
                    //}
                    //if (string.IsNullOrEmpty(previousSource1VerticalScale) == false)
                    //{
                    //    getSource1VerticalScaleCommand.CommandMessage = "ch" + scopeMeasurementSource1 + ":scale " + previousSource1VerticalScale;
                    //}
                    //if (string.IsNullOrEmpty(previousSource1HorizontalScale) == false)
                    //{
                    //    getHorizontalScaleCommand.CommandMessage = "horizontal:scale " + previousSource1HorizontalScale;
                    //}
                    if (string.IsNullOrEmpty(previousMainTriggerSource) == false)
                    {
                        getTriggerMainSourceCommand.CommandMessage = "trigger:main:edge:source " + previousMainTriggerSource;
                    }
                    //if (string.IsNullOrEmpty(previousMeasurementType) == false)
                    //{
                    //    setMeasurementTypeCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":type " + previousMeasurementType;
                    //}
                    //if (string.IsNullOrEmpty(previousMeasurementSource1) == false)
                    //{
                    //    setMeasurementSource1Command.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1 " + previousMeasurementSource1;
                    //}
                    if (string.IsNullOrEmpty(previousMessage) == false)
                    {
                        messageShowCommand.CommandMessage = "message:show " + previousMessage;
                    }
                    //if (string.IsNullOrEmpty(previousMessageState) == false)
                    //{
                    //    messageStateCommand.CommandMessage = "message:state " + previousMessageState;
                    //}

                    //replyValue = ScopeObject.Write(getSource1SelectCommand);
                    //replyValue = ScopeObject.Write(getSource1VerticalScaleCommand);
                    //replyValue = ScopeObject.Write(getHorizontalScaleCommand);
                    replyValue = ScopeObject.Write(getTriggerMainSourceCommand);
                    //replyValue = ScopeObject.Write(setMeasurementTypeCommand);
                    //replyValue = ScopeObject.Write(setMeasurementSource1Command);

                    replyValue = ScopeObject.Write(messageShowCommand);
                    replyValue = ScopeObject.Write(messageStateCommand);

                    #endregion

                    ScopeObject.MeasurementsIndexes.Clear();

                    stopwatch.Stop();
                    command.ExecuteTime = stopwatch.ElapsedMilliseconds;

                    returnValue = true;
                }
            }
            catch (Exception ex)
            {
                returnValue = false;
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public bool SetControlledFrequency(SignalGeneratorCommand command)
        {
            bool returnValue = false;

            string stringReturnValue = string.Empty;

            #region Local parameters

            Task<string> task = null;
            int taskWaitTimeCounter = 0;

            string asciiString = string.Empty;
            byte[] byteArray = null;
            string commandMessage = string.Empty;
            bool isPIDControllerCommand = false;

            SignalGeneratorCommand getAmplitudeCommand = new SignalGeneratorCommand()
            {
                IsJournaled = false
            };

            ScopeCommand messageStateCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand messageShowCommand = new ScopeCommand()
            {
                IsJournaled = false
            };

            ScopeCommand getSource1SelectCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setSource1SelectCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getMeasurementCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setMeasurementTypeCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getMeasurementTypeCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setMeasurementSource1Command = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getMeasurementSource1Command = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setSource1VerticalScaleCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getSource1VerticalScaleCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setHorizontalScaleCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getHorizontalScaleCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setTriggerMainSourceCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getTriggerMainSourceCommand = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand setTriggerMainLevel50Command = new ScopeCommand()
            {
                IsJournaled = false
            };
            ScopeCommand getTriggerMainLevel50Command = new ScopeCommand()
            {
                IsJournaled = false
            };

            string previousMessageState = string.Empty;
            string previousMessage = string.Empty;

            string previousSource1Select = string.Empty;
            string previousMeasurementType = string.Empty;
            string previousMeasurementSource1 = string.Empty;
            string previousSource1VerticalScale = string.Empty;
            string previousSource1HorizontalScale = string.Empty;
            string previousMainTriggerSource = string.Empty;

            string replyValue = string.Empty;

            double actualAmplitudeValue = 0.0;
            double actualFrequencyValue = 0.0;
            double desiredFrequencyValue = 0.0;
            double calculatedFrequencyValue = 0.0;

            int scopeMeasurementIndex = 0;
            int scopeMeasurementSource1 = 0;
            int commandTimeout = 0;

            Stopwatch stopwatch = new Stopwatch();
            Stopwatch pidStopwatch = new Stopwatch();

            double errorPID = 0.0;
            double integralPID = 0.0;
            double derivativePID = 0.0;
            double pidTime = 0.0;
            double actuatorPIDValue = 0.0;

            double proportionalPart = 0.0;
            double integralPart = 0.0;
            double derivativePart = 0.0;

            #endregion

            try
            {
                lock (_signalGeneratorSendCommandLock)
                {
                    stopwatch.Start();

                    double.TryParse(command.Arguments[0], out desiredFrequencyValue);
                    int.TryParse(command.Arguments[1], out scopeMeasurementIndex);
                    int.TryParse(command.Arguments[2], out scopeMeasurementSource1);

                    #region Save current and set relevant measurement parameters

                    #region Get commands

                    getAmplitudeCommand.CommandMessage = "amp?";
                    if (string.IsNullOrEmpty(getAmplitudeCommand.CommandMessage) == false)
                    {
                        if (_isAsciiCommand)
                        {
                            asciiString = getAmplitudeCommand.CommandMessage;
                        }
                        else
                        {
                            if (StringEx.TryParseByteStringToByte(getAmplitudeCommand.CommandMessage, out byte[] bytes))
                            {
                                byteArray = bytes;
                            }
                            else
                            {
                                _isWritingError = true;
                                return returnValue;
                            }
                        }
                        if (_signalGeneratorPort?.IsPortOpen == true)
                        {
                            if (_isAsciiCommand)
                            {
                                if (_appendNewLine)
                                {
                                    stringReturnValue = _signalGeneratorPort?.WriteLine(asciiString);
                                }
                                else
                                {
                                    _signalGeneratorPort?.Write(asciiString);
                                }
                            }
                            else
                            {
                                if (_appendNewLine)
                                {
                                    _signalGeneratorPort?.WriteLine(byteArray);
                                }
                                else
                                {
                                    _signalGeneratorPort?.Write(byteArray);
                                }
                            }
                        }
                    }

                    Double.TryParse(stringReturnValue.Split(new char[] { ' ' })[1], out actualAmplitudeValue);

                    messageStateCommand.CommandMessage = "message:state?";
                    replyValue = ScopeObject.Write(messageStateCommand);
                    previousMessageState = replyValue.Trim().ToLower();

                    messageShowCommand.CommandMessage = "message:show?";
                    replyValue = ScopeObject.Write(messageShowCommand);
                    previousMessage = replyValue.Trim().ToLower();

                    getSource1SelectCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1?";
                    replyValue = ScopeObject.Write(getSource1SelectCommand);
                    previousMeasurementSource1 = replyValue.Trim().ToLower();

                    getSource1VerticalScaleCommand.CommandMessage = "ch" + scopeMeasurementSource1 + ":scale?";
                    replyValue = ScopeObject.Write(getSource1VerticalScaleCommand);
                    previousSource1VerticalScale = replyValue.Trim().ToLower();

                    getHorizontalScaleCommand.CommandMessage = "horizontal:scale?";
                    replyValue = ScopeObject.Write(getHorizontalScaleCommand);
                    previousSource1HorizontalScale = replyValue.Trim().ToLower();

                    getTriggerMainSourceCommand.CommandMessage = "trigger:main:edge:source?";
                    replyValue = ScopeObject.Write(getTriggerMainSourceCommand);
                    previousMainTriggerSource = replyValue.Trim().ToLower();

                    getMeasurementTypeCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":type?";
                    replyValue = ScopeObject.Write(getMeasurementTypeCommand);
                    previousMeasurementType = replyValue.Trim().ToLower();

                    getMeasurementSource1Command.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1?";
                    replyValue = ScopeObject.Write(getMeasurementSource1Command);
                    previousMeasurementSource1 = replyValue.Trim().ToLower();

                    #endregion

                    #region Set commands

                    setSource1VerticalScaleCommand.CommandMessage = "ch" + scopeMeasurementSource1 + ":scale " + (actualAmplitudeValue / 2.0).ToString();
                    replyValue = ScopeObject.Write(setSource1VerticalScaleCommand);

                    setTriggerMainSourceCommand.CommandMessage = "trigger:main:edge:source " + "ch" + scopeMeasurementSource1;
                    replyValue = ScopeObject.Write(setTriggerMainSourceCommand);

                    setMeasurementTypeCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":type freq";
                    replyValue = ScopeObject.Write(setMeasurementTypeCommand);

                    setMeasurementSource1Command.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1 " + "ch" + scopeMeasurementSource1;
                    replyValue = ScopeObject.Write(setMeasurementSource1Command);

                    setTriggerMainLevel50Command.CommandMessage = "trigger:main setlevel";
                    replyValue = ScopeObject.Write(setTriggerMainLevel50Command);

                    #endregion

                    #endregion

                    #region Tunning frequency

                    //messageStateCommand.CommandMessage = "message:state on";
                    //replyValue = ScopeObject.Write(messageStateCommand);
                    messageShowCommand.CommandMessage = "message:show \"Frequency_tunning\"";
                    replyValue = ScopeObject.Write(messageShowCommand);

                    commandMessage = command.Header + " " + command.Arguments[0];

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
                        if (_signalGeneratorPort?.IsPortOpen == true)
                        {
                            if (_isAsciiCommand)
                            {
                                if (_appendNewLine)
                                {
                                    stringReturnValue = _signalGeneratorPort?.WriteLine(asciiString);
                                }
                                else
                                {
                                    _signalGeneratorPort?.Write(asciiString);
                                }
                            }
                            else
                            {
                                if (_appendNewLine)
                                {
                                    _signalGeneratorPort?.WriteLine(byteArray);
                                }
                                else
                                {
                                    _signalGeneratorPort?.Write(byteArray);
                                }
                            }
                        }
                    }

                    WaitToFinishCommandProcess(10);
                    Thread.Sleep(200);

                    //request measurement
                    getMeasurementCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":value?";
                    replyValue = ScopeObject.Write(getMeasurementCommand);

                    if (String.IsNullOrEmpty(replyValue) == false)
                    {
                        double measurementValue = 0.0;
                        Double.TryParse(replyValue, out measurementValue);

                        ScopeObject.Measurements[scopeMeasurementIndex - 1].ActualValue = measurementValue;
                        actualFrequencyValue = measurementValue;
                    }

                    double.TryParse(command.Arguments[0], out desiredFrequencyValue);

                    calculatedFrequencyValue = desiredFrequencyValue + (desiredFrequencyValue - actualFrequencyValue);

                    command.Arguments[0] = calculatedFrequencyValue.ToString();

                    CancellationTokenSource tokenSource = new CancellationTokenSource();
                    CancellationToken token = tokenSource.Token;
                    task = Task.Factory.StartNew(() =>
                    {
                        while (Math.Abs(actualFrequencyValue - desiredFrequencyValue) > PIDFrequencyThresholdValue && !token.IsCancellationRequested)
                        {
                            try
                            {
                                double actualMeasurementValue = 0.0;
                                double previousMeasurementValue = 0.0;
                                int getMeasurementCounter = 0;

                                commandMessage = command.Header + " " + command.Arguments[0];

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
                                            return stringReturnValue;
                                        }
                                    }
                                    if (_signalGeneratorPort?.IsPortOpen == true)
                                    {
                                        if (_isAsciiCommand)
                                        {
                                            if (_appendNewLine)
                                            {
                                                stringReturnValue = _signalGeneratorPort?.WriteLine(asciiString);
                                            }
                                            else
                                            {
                                                _signalGeneratorPort?.Write(asciiString);
                                            }
                                        }
                                        else
                                        {
                                            if (_appendNewLine)
                                            {
                                                _signalGeneratorPort?.WriteLine(byteArray);
                                            }
                                            else
                                            {
                                                _signalGeneratorPort?.Write(byteArray);
                                            }
                                        }
                                    }
                                }

                                WaitToFinishCommandProcess(10);

                                replyValue = ScopeObject.Write(getMeasurementCommand);
                                if (String.IsNullOrEmpty(replyValue) == false)
                                {
                                    if (Double.TryParse(replyValue, out actualMeasurementValue))
                                    {
                                        previousMeasurementValue = actualMeasurementValue;
                                    }
                                }

                                while (previousMeasurementValue == actualMeasurementValue)
                                {
                                    replyValue = ScopeObject.Write(getMeasurementCommand);
                                    if (String.IsNullOrEmpty(replyValue) == false)
                                    {
                                        Double.TryParse(replyValue, out actualMeasurementValue);
                                    }

                                    getMeasurementCounter++;
                                    if (getMeasurementCounter > 5)//max 5 times get measurement
                                    {
                                        break;
                                    }
                                }

                                //get measurement one more
                                replyValue = ScopeObject.Write(getMeasurementCommand);
                                if (String.IsNullOrEmpty(replyValue) == false)
                                {
                                    if (Double.TryParse(replyValue, out actualMeasurementValue) == true)
                                    {
                                        ScopeObject.Measurements[scopeMeasurementIndex - 1].ActualValue = actualMeasurementValue;
                                        actualFrequencyValue = actualMeasurementValue;
                                    }
                                }

                                pidStopwatch.Stop();
                                pidTime = (double)(pidStopwatch.ElapsedMilliseconds) / 1000.0;

                                errorPID = (desiredFrequencyValue - actualFrequencyValue);
                                proportionalPart = PIDFrequencyKpValue * errorPID;

                                derivativePID = (pidTime != 0) ? (errorPID / pidTime) : 0.0;
                                derivativePart = PIDFrequencyKdValue * derivativePID;

                                integralPID += errorPID;//set limit to integrator
                                integralPart = PIDFrequencyKiValue * integralPID;
                                if (integralPart > PIDFrequencyMaxIntegratorValue)
                                {
                                    integralPart = PIDFrequencyMaxIntegratorValue;
                                }
                                if (integralPart < -PIDFrequencyMaxIntegratorValue)
                                {
                                    integralPart = -PIDFrequencyMaxIntegratorValue;
                                }

                                actuatorPIDValue = desiredFrequencyValue + proportionalPart + integralPart + derivativePart;
                                actuatorPIDValue = Math.Round(actuatorPIDValue, 3);

                                command.Arguments[0] = actuatorPIDValue.ToString();

                                pidStopwatch.Restart();
                            }
                            catch (Exception ex)
                            {
                                Logger.ExceptionHandler(ex, ex.Message);
                                //break;
                            }
                        }

                        return stringReturnValue;

                    }, token);

                    taskWaitTimeCounter = 0;

                    while (!task.IsCompleted)
                    {
                        #region
                        taskWaitTimeCounter++;
                        //Logger.WriteMessage("Task wait time counter:" + taskWaitTimeCounter.ToString());
                        task.Wait(50);
                        if (taskWaitTimeCounter > (PIDFrequencyTunningTimeWaitValue / 50)) //wait 
                        {
                            tokenSource.Cancel();
                            task.Wait();
                            break;
                        }
                        #endregion
                    }

                    stringReturnValue = task.Result;

                    #endregion

                    #region Revert saved scope configurations

                    if (string.IsNullOrEmpty(previousMeasurementSource1) == false)
                    {
                        getSource1SelectCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1 " + previousMeasurementSource1;
                    }
                    if (string.IsNullOrEmpty(previousSource1VerticalScale) == false)
                    {
                        getSource1VerticalScaleCommand.CommandMessage = "ch" + scopeMeasurementSource1 + ":scale " + previousSource1VerticalScale;
                    }
                    if (string.IsNullOrEmpty(previousSource1HorizontalScale) == false)
                    {
                        getHorizontalScaleCommand.CommandMessage = "horizontal:scale " + previousSource1HorizontalScale;
                    }
                    if (string.IsNullOrEmpty(previousMainTriggerSource) == false)
                    {
                        getTriggerMainSourceCommand.CommandMessage = "trigger:main:edge:source " + previousMainTriggerSource;
                    }
                    if (string.IsNullOrEmpty(previousMeasurementType) == false)
                    {
                        setMeasurementTypeCommand.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":type " + previousMeasurementType;
                    }
                    if (string.IsNullOrEmpty(previousMeasurementSource1) == false)
                    {
                        setMeasurementSource1Command.CommandMessage = "measurement:meas" + scopeMeasurementIndex + ":source1 " + previousMeasurementSource1;
                    }
                    if (string.IsNullOrEmpty(previousMessage) == false)
                    {
                        messageShowCommand.CommandMessage = "message:show " + previousMessage;
                    }
                    //if (string.IsNullOrEmpty(previousMessageState) == false)
                    //{
                    //    messageStateCommand.CommandMessage = "message:state " + previousMessageState;
                    //}

                    replyValue = ScopeObject.Write(getSource1SelectCommand);
                    replyValue = ScopeObject.Write(getSource1VerticalScaleCommand);
                    replyValue = ScopeObject.Write(getHorizontalScaleCommand);
                    replyValue = ScopeObject.Write(getTriggerMainSourceCommand);
                    replyValue = ScopeObject.Write(setMeasurementTypeCommand);
                    replyValue = ScopeObject.Write(setMeasurementSource1Command);

                    replyValue = ScopeObject.Write(messageShowCommand);
                    replyValue = ScopeObject.Write(messageStateCommand);

                    #endregion

                    ScopeObject.MeasurementsIndexes.Clear();

                    stopwatch.Stop();
                    command.ExecuteTime = stopwatch.ElapsedMilliseconds;

                    returnValue = true;
                }
            }
            catch (Exception ex)
            {
                returnValue = false;
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="doubleString"></param>
        /// <returns></returns>
        public string ConvertDoubleStringToScientificNotation(string doubleString)
        {
            string returnValue = string.Empty;

            double doubleValue = 0.0;

            try
            {
                double.TryParse(doubleString, out doubleValue);
                returnValue = doubleValue.ToString("E2");
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
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
                throw new NotImplementedException();
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
