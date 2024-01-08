using CalibrationToolTester.InstrumentImplementation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CalibrationToolTester.GlobalLoger;
using System.Threading.Channels;
using System.Threading;
using System.Threading.Tasks;
using MIDUPROBJECTLib;
using System.Windows.Forms;
using InstrumentV3;
using Channel = MIDUPROBJECTLib.Channel;

namespace CalibrationToolTester.InstrumentImplementation
{
    /// <summary>
    /// 
    /// </summary>
    public class InstrumentWrapper : INotifyPropertyChanged, IDisposable, IDataErrorInfo
    {
        #region Constants

        private const string PRF_INVALID_VALUE_ERROR = "Invalid Prf value";
        private const string CHARGE_TIME_INVALID_VALUE_ERROR = "Invalid Charge time value";

        #endregion

        #region Fields

        public static CommandTranslationService commandsTranslation = new CommandTranslationService();

        private readonly object _instrumentLock = new object();

        private InstrumentAPI _instrument;
        public InstrumentAPI Instrument
        {
            get
            {
                return _instrument;
            }
            set
            {
                if (_instrument != value)
                {
                    _instrument = value;
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

                string translationFilesPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName + "\\DevicesTypes\\InstrumentTypes\\";
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

        private string _usSetupDataBasePath;
        public string UsSetupDataBasePath
        {
            get
            {
                return _usSetupDataBasePath;
            }
            set
            {
                if (_usSetupDataBasePath != value)
                {
                    _usSetupDataBasePath = value;

                    UsSetupsArray = _instrument?.GetSetupList(_usSetupDataBasePath);

                    OnPropertyChanged();
                }
            }
        }

        private string _usSetupDataBaseName;
        public string UsSetupDataBaseName
        {
            get
            {
                return _usSetupDataBaseName;
            }
            set
            {
                if (_usSetupDataBaseName != value)
                {
                    _usSetupDataBaseName = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _actualUsSetupName;
        public string ActualUsSetupName
        {
            get
            {
                return _actualUsSetupName;
            }
            set
            {
                bool isExistsSetup = false;

                _actualUsSetupName = value;

                if (string.IsNullOrEmpty(_usSetupDataBasePath) == false && string.IsNullOrEmpty(_actualUsSetupName) == false)
                {
                    if (_instrument != null)
                    {
                        if (_instrument.Initialized == true)
                        {
                            for (int i = 0; i < _usSetupsArray.Length; i++)
                            {
                                if (_actualUsSetupName == _usSetupsArray.GetValue(i).ToString())
                                {
                                    isExistsSetup = true;
                                    break;
                                }
                            }

                            if (isExistsSetup == true)
                            {
                                bool ret = _instrument.LoadSetup(_usSetupDataBasePath, _actualUsSetupName);
                            }
                        }
                    }
                }

                OnPropertyChanged();
            }
        }

        private Array _usSetupsArray;
        public Array UsSetupsArray
        {
            get
            {
                return _usSetupsArray;
            }
            set
            {
                _usSetupsArray = value;
                OnPropertyChanged();
            }
        }

        private UprWrapper _upr = new UprWrapper();
        public UprWrapper Upr
        {
            get
            {
                return _upr;
            }
            set
            {
                _upr = value;
                OnPropertyChanged();
            }
        }

        private bool _initialized;
        public bool Initialized
        {
            get
            {
                //_initialized = (_instrument != null) ? _instrument.Initialized : false;
                return _initialized;
            }
            set
            {
                _initialized = value;
                OnPropertyChanged();
            }
        }

        InstrumentCommand _actualCommand = new InstrumentCommand();
        public InstrumentCommand ActualCommand
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

        private bool _toolBoxVisible;
        public bool ToolBoxVisible
        {
            get
            {
                _toolBoxVisible = _instrument.Display.ToolBoxVisible;

                return _toolBoxVisible;
            }
            set
            {
                _toolBoxVisible = value;
                if (_instrument != null)
                {
                    _instrument.Display.ToolBoxVisible = _toolBoxVisible;
                    OnPropertyChanged();
                }
            }
        }

        private bool _visible;
        public bool Visible
        {
            get
            {
                return _visible;
            }
            set
            {
                _visible = value;
                if (_instrument != null)
                {
                    _instrument.Display.AScanVisible[_instrument.Channels.ActiveChannel] = _visible;

                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Constructor

        public InstrumentWrapper()
        {
            bool isInstrumentOpened = false;

            try
            {
                Logger.WriteMessage("InstrumentWrapper:InstrumentWrapper");

                string translationFilesPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName + "\\DevicesTypes\\InstrumentTypes\\";
                DirectoryInfo info = new DirectoryInfo(translationFilesPath);
                FileInfo[] files = info.GetFiles("*.xml");

                for (int i = 0; i < files.Length; i++)
                {
                    CommandsTranslationsFiles.Add(files[i].Name.Replace(files[i].Extension, ""));
                }

                ActualCommandsTranslationsFile = CommandsTranslationsFiles[0];

                InstrumentCommand.commandsTranslation = commandsTranslation;

                Task t = Task.Factory.StartNew(() =>
                {
                    isInstrumentOpened = OpenInstrument();
                }).ContinueWith(x =>
                {
                    if (!x.IsFaulted)
                    {
                        if (isInstrumentOpened)
                        {
                            if (_instrument != null)
                            {
                                if (_instrument.Initialized == true)
                                {
                                    _upr.UprObject = _instrument.UPRObject;
                                    for (int i = 0; i < _upr.UprChannels.Count; i++)
                                    {
                                        _upr.UprChannels[i].PropertyChanged += OnChannelPropertyChanged;
                                    }

                                    Initialized = true;
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="useRemoteInstrument"></param>
        /// <param name="remoteInstrumentServerName"></param>
        public InstrumentWrapper(bool useRemoteInstrument, string remoteInstrumentServerName)
        {
            bool isInstrumentOpened = false;

            try
            {
                Logger.WriteMessage("InstrumentWrapper:InstrumentWrapper");

                string translationFilesPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName + "\\DevicesTypes\\InstrumentTypes\\";
                DirectoryInfo info = new DirectoryInfo(translationFilesPath);
                FileInfo[] files = info.GetFiles("*.xml");

                for (int i = 0; i < files.Length; i++)
                {
                    CommandsTranslationsFiles.Add(files[i].Name.Replace(files[i].Extension, ""));
                }

                ActualCommandsTranslationsFile = CommandsTranslationsFiles[0];

                InstrumentCommand.commandsTranslation = commandsTranslation;

                Task t = Task.Factory.StartNew(() =>
                {
                    isInstrumentOpened = OpenInstrument(useRemoteInstrument, remoteInstrumentServerName);
                }).ContinueWith(x =>
                {
                    if (!x.IsFaulted)
                    {
                        if (isInstrumentOpened)
                        {
                            if (_instrument != null)
                            {
                                if (_instrument.Initialized == true)
                                {
                                    _upr.UprObject = _instrument.UPRObject;
                                    for (int i = 0; i < _upr.UprChannels.Count; i++)
                                    {
                                        _upr.UprChannels[i].PropertyChanged += OnChannelPropertyChanged;
                                    }

                                    Initialized = true;
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        ~InstrumentWrapper()
        {
            try
            {
                CloseInstrument();
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            try
            {
                Logger.WriteMessage("InstrumentWrapper:Dispose");

                CloseInstrument();
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
        public bool OpenInstrument()
        {
            bool returnValue = false;

            int instrumentInitializingWaitTime = 0;

            try
            {
                returnValue = KillInstrument();

                if (returnValue == true && _instrument == null)
                {
                    _instrument = new InstrumentAPI();
                }

                if (_instrument != null)
                {
                    while (_instrument.Initialized == false)
                    {
                        instrumentInitializingWaitTime++;
                        if (instrumentInitializingWaitTime > 100)
                        {
                            instrumentInitializingWaitTime = 0;
                            break;
                        }

                        Thread.Sleep(100);
                    }
                    if (_instrument.Initialized == true)
                    {
                        _instrument.OverrideUserLevel(6, null);

                        _instrument.LoadFactorySetup();

                        if (_instrument.Display != null)
                        {
                            _instrument.Display.AScanVisible[0] = false;
                        }

                        returnValue = true;
                    }
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
        /// <param name="useRemoteInstrument"></param>
        /// <param name="remoteInstrumentServerName"></param>
        /// <returns></returns>
        public bool OpenInstrument(bool useRemoteInstrument, string remoteInstrumentServerName)
        {
            bool returnValue = false;

            int instrumentInitializingWaitTime = 0;

            try
            {
                if (useRemoteInstrument == false)
                {
                    returnValue = KillInstrument();

                    if (returnValue == true && _instrument == null)
                    {
                        _instrument = new InstrumentAPI();
                    }
                }
                else
                {
                    var tServerType = Type.GetTypeFromProgID("InstrumentV3.InstrumentAPI", remoteInstrumentServerName, true);//"192.168.17.97"
                    _instrument = (InstrumentAPI)Activator.CreateInstance(tServerType);
                }

                if (_instrument != null)
                {
                    while (_instrument.Initialized == false)
                    {
                        instrumentInitializingWaitTime++;
                        if (instrumentInitializingWaitTime > 100)
                        {
                            instrumentInitializingWaitTime = 0;
                            break;
                        }

                        Thread.Sleep(100);
                    }
                    if (_instrument.Initialized == true)
                    {
                        _instrument.OverrideUserLevel(6, null);

                        _instrument.LoadFactorySetup();

                        if (_instrument.Display != null)
                        {
                            _instrument.Display.AScanVisible[0] = useRemoteInstrument;
                        }

                        returnValue = true;
                    }
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
        /// <returns></returns>
        public bool KillInstrument()
        {
            bool returnValue = false;

            Process[] _processes = null;
            bool isExited = false;
            int waitToExitTime = 0;

            _processes = Process.GetProcessesByName("InstrumentV3");
            if (_processes.Length > 0)
            {
                try
                {
                    _processes[0].Kill();
                    Thread.Sleep(1000);
                    isExited = _processes[0].HasExited;
                    while (isExited == false)
                    {
                        isExited = _processes[0].HasExited;

                        waitToExitTime++;
                        if (waitToExitTime > 50)
                        {
                            break;
                        }

                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    Logger.ExceptionHandler(ex, ex.Message);
                }
            }
            _processes = Process.GetProcessesByName("InstrumentV3");
            if (_processes.Length == 0)
            {
                _instrument = null;
                returnValue = true;
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool CloseInstrument()
        {
            bool returnValue = false;

            try
            {
                returnValue = KillInstrument();
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
        /// <returns></returns>
        public IntPtr GetInstrumentWindowHandler()
        {
            IntPtr returnValue = IntPtr.Zero;

            if (_instrument != null)
            {
                if (_instrument.Initialized == true)
                {
                    returnValue = new IntPtr(_instrument.Display.hWindow[0]);
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IntPtr GetInstrumentToolBoxWindowHandler()
        {
            IntPtr returnValue = IntPtr.Zero;

            if (_instrument != null)
            {
                if (_instrument.Initialized == true)
                {
                    returnValue = new IntPtr(_instrument.Display.ToolBoxhWindow);
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ExecuteActualCommand()
        {
            string returnValue = null;
            string translatedCommand = string.Empty;

            lock (_instrumentLock)
            {
                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();

                    //TODO:Vlad:check command and execute

                    int pulser = 0;
                    int channel = 0;
                    int channelEnable = 0;
                    int channelDelay = 0;
                    int channelRange = 0;
                    int desiredGate = 0;
                    int desiredGateRange = 0;
                    int desiredValue = 0;
                    int receiverMode = 0;
                    int prf = 0;
                    int externalPrfEnable = 0;
                    float desiredGain = 0.0f;
                    float desiredPrescale = 0.0f;
                    int desiredFilter = 0;

                    //TODO:Vlad:parse message and do something
                    if (_actualCommand.Mnemonics.Length == 2)
                    {
                        if (_actualCommand.Mnemonics[0].ToLower().Contains("test"))
                        {
                            if (_actualCommand.Mnemonics[1].ToLower().Contains("upr"))
                            {
                                returnValue = (TestUPR()) ? "" : returnValue;
                            }
                        }
                    }
                    else if (_actualCommand.Mnemonics.Length == 3)
                    {
                        if (_actualCommand.Mnemonics[0].ToLower().Contains("set"))
                        {
                            if (_actualCommand.Mnemonics[1].ToLower().Contains("upr"))
                            {
                                if (_actualCommand.Mnemonics[2].ToLower().Contains("prf"))
                                {
                                    if (_actualCommand.Arguments != null)
                                    {
                                        if (_actualCommand.Arguments.Length > 0)
                                        {
                                            if (Int32.TryParse(_actualCommand.Arguments[0], out prf) == true)
                                            {
                                                returnValue = (SetUprPRF(prf)) ? "" : returnValue;
                                            }
                                        }
                                    }
                                }
                            }
                            if (_actualCommand.Mnemonics[1].ToLower().Contains("pulser"))
                            {
                                if (_actualCommand.Mnemonics[2].ToLower().Contains("amplitude"))
                                {
                                    if (_actualCommand.Arguments != null)
                                    {
                                        if (_actualCommand.Arguments.Length > 2)
                                        {
                                            if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[1], out pulser) == true)
                                                {
                                                    if (Int32.TryParse(_actualCommand.Arguments[2], out desiredValue) == true)
                                                    {
                                                        returnValue = (SetPulserAmplitude(channel, pulser, desiredValue)) ? "" : returnValue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                if (_actualCommand.Mnemonics[2].ToLower().Contains("pulsewidth"))
                                {
                                    if (_actualCommand.Arguments != null)
                                    {
                                        if (_actualCommand.Arguments.Length > 2)
                                        {
                                            if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[1], out pulser) == true)
                                                {
                                                    if (Int32.TryParse(_actualCommand.Arguments[2], out desiredValue) == true)
                                                    {
                                                        returnValue = (SetPulserPulseWidth(channel, pulser, desiredValue)) ? "" : returnValue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (_actualCommand.Mnemonics[1].ToLower().Contains("receiver"))
                            {
                                if (_actualCommand.Mnemonics[2].ToLower().Contains("mode"))
                                {
                                    if (_actualCommand.Arguments != null)
                                    {
                                        if (_actualCommand.Arguments.Length > 1)
                                        {
                                            if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[1], out receiverMode) == true)
                                                {
                                                    returnValue = (_upr.SetReceiverMode(channel, (UprWrapper.ReceiverMode)receiverMode)) ? "" : returnValue;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (_actualCommand.Mnemonics.Length == 4)
                    {
                        if (_actualCommand.Mnemonics[0].ToLower().Contains("set"))
                        {
                            if (_actualCommand.Mnemonics[1].ToLower().Contains("upr"))
                            {
                                if (_actualCommand.Mnemonics[2].ToLower().Contains("channel"))
                                {
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("gain"))
                                    {
                                        if (_actualCommand.Arguments != null)
                                        {
                                            if (_actualCommand.Arguments.Length > 1)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                {
                                                    if (float.TryParse(_actualCommand.Arguments[1], out desiredGain) == true)
                                                    {
                                                        returnValue = (SetChannelGain(channel, desiredGain)) ? "" : returnValue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("prescale"))
                                    {
                                        if (_actualCommand.Arguments != null)
                                        {
                                            if (_actualCommand.Arguments.Length > 1)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                {
                                                    if (float.TryParse(_actualCommand.Arguments[1], out desiredPrescale) == true)
                                                    {
                                                        returnValue = (SetChannelPrescale(channel, desiredPrescale)) ? "" : returnValue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("enable"))
                                    {
                                        if (_actualCommand.Arguments != null)
                                        {
                                            if (_actualCommand.Arguments.Length > 0)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                {
                                                    if (Int32.TryParse(_actualCommand.Arguments[1], out channelEnable) == true)
                                                    {
                                                        returnValue = (OpenChannel(channel, channelEnable)) ? "" : returnValue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("delay"))
                                    {
                                        if (_actualCommand.Arguments != null)
                                        {
                                            if (_actualCommand.Arguments.Length > 1)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                {
                                                    if (Int32.TryParse(_actualCommand.Arguments[1], out channelDelay) == true)
                                                    {
                                                        returnValue = (SetChannelDelay(channel, channelDelay)) ? "" : returnValue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("range"))
                                    {
                                        if (_actualCommand.Arguments != null)
                                        {
                                            if (_actualCommand.Arguments.Length > 1)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                {
                                                    if (Int32.TryParse(_actualCommand.Arguments[1], out channelRange) == true)
                                                    {
                                                        returnValue = (SetChannelRange(channel, channelRange)) ? "" : returnValue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                if (_actualCommand.Mnemonics[2].ToLower().Contains("external"))
                                {
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("prf"))
                                    {
                                        if (_actualCommand.Arguments != null)
                                        {
                                            if (_actualCommand.Arguments.Length > 0)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[0], out externalPrfEnable) == true)
                                                {
                                                    returnValue = (SetUprExternalPRF(externalPrfEnable)) ? "" : returnValue;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (_actualCommand.Mnemonics[0].ToLower().Contains("add"))
                        {
                            if (_actualCommand.Mnemonics[1].ToLower().Contains("upr"))
                            {
                                if (_actualCommand.Mnemonics[2].ToLower().Contains("channel"))
                                {
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("gate"))
                                    {
                                        if (_actualCommand.Arguments != null)
                                        {
                                            if (_actualCommand.Arguments.Length > 0)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                {
                                                    returnValue = (AddChannelGate(channel)) ? "" : returnValue;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (_actualCommand.Mnemonics[0].ToLower().Contains("remove"))
                        {
                            if (_actualCommand.Mnemonics[1].ToLower().Contains("upr"))
                            {
                                if (_actualCommand.Mnemonics[2].ToLower().Contains("channel"))
                                {
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("gate"))
                                    {
                                        if (_actualCommand.Arguments != null)
                                        {
                                            if (_actualCommand.Arguments.Length > 1)
                                            {
                                                if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                {
                                                    if (Int32.TryParse(_actualCommand.Arguments[1], out desiredGate) == true)
                                                    {
                                                        returnValue = (DeleteChannelGate(channel, (short)desiredGate)) ? "" : returnValue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (_actualCommand.Mnemonics.Length == 5)
                    {
                        if (_actualCommand.Mnemonics[0].ToLower().Contains("set"))
                        {
                            if (_actualCommand.Mnemonics[1].ToLower().Contains("upr"))
                            {
                                if (_actualCommand.Mnemonics[2].ToLower().Contains("channel"))
                                {
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("pulser"))
                                    {
                                        if (_actualCommand.Mnemonics[4].ToLower().Contains("filter"))
                                        {
                                            if (_actualCommand.Arguments != null)
                                            {
                                                if (_actualCommand.Arguments.Length > 1)
                                                {
                                                    if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                    {
                                                        if (Int32.TryParse(_actualCommand.Arguments[1], out desiredFilter) == true)
                                                        {
                                                            returnValue = (SetChannelPulserFilter(channel, desiredFilter)) ? "" : returnValue;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("reciever"))
                                    {
                                        if (_actualCommand.Mnemonics[4].ToLower().Contains("filter"))
                                        {
                                            if (_actualCommand.Arguments != null)
                                            {
                                                if (_actualCommand.Arguments.Length > 1)
                                                {
                                                    if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                    {
                                                        if (Int32.TryParse(_actualCommand.Arguments[1], out desiredFilter) == true)
                                                        {
                                                            returnValue = (SetChannelRecieverFilter(channel, desiredFilter)) ? "" : returnValue;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (_actualCommand.Mnemonics[3].ToLower().Contains("gate"))
                                    {
                                        if (_actualCommand.Mnemonics[4].ToLower().Contains("range"))
                                        {
                                            if (_actualCommand.Arguments != null)
                                            {
                                                if (_actualCommand.Arguments.Length > 2)
                                                {
                                                    if (Int32.TryParse(_actualCommand.Arguments[0], out channel) == true)
                                                    {
                                                        if (Int32.TryParse(_actualCommand.Arguments[1], out desiredGate) == true)
                                                        {
                                                            if (Int32.TryParse(_actualCommand.Arguments[2], out desiredGateRange) == true)
                                                            {
                                                                returnValue = (SetChannelGateRange(channel, desiredGate, desiredGateRange)) ? "" : returnValue;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    stopwatch.Stop();
                    ActualCommand.ExecuteTime = stopwatch.ElapsedMilliseconds;
                    stopwatch.Reset();
                }
                catch (Exception ex)
                {
                    returnValue = null;
                    Logger.ExceptionHandler(ex, ex.Message);
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool CascadeWindows()
        {
            bool returnValue = false;

            if (_instrument != null)
            {
                if (_instrument.Initialized == true)
                {
                    _instrument.Display.CascadeWindows();

                    returnValue = true;
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool TitleWindows()
        {
            bool returnValue = false;

            if (_instrument != null)
            {
                if (_instrument.Initialized == true)
                {
                    _instrument.Display.TileWindows();

                    returnValue = true;
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool LoadFactorySetup()
        {
            bool returnValue = false;

            try
            {
                if (_instrument != null)
                {
                    if (_instrument.Initialized == true)
                    {
                        returnValue = _instrument.LoadFactorySetup();

                        if (returnValue == true)
                        {
                            ActualUsSetupName = string.Empty;
                        }
                    }
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
        /// <returns></returns>
        public bool BrowseDatabaseSetup()
        {
            bool returnValue = false;

            string filePath = string.Empty;

            try
            {
                FolderBrowserDialog openFileDialog = new FolderBrowserDialog();

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.SelectedPath;

                    if (string.IsNullOrEmpty(filePath) == false)
                    {
                        UsSetupDataBasePath = filePath;

                        returnValue = true;
                    }
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
        /// <param name="dataBasePath"></param>
        /// <param name="setupName"></param>
        /// <returns></returns>
        public bool LoadSetup(string dataBasePath, string setupName)
        {
            bool returnValue = false;

            //_instrument.LoadSetup(dataBasePath, setupName);

            Array setups = _instrument.GetSetupList(@"C:\ProgramData\ScanMaster\Instrument");

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool SaveSetup()
        {
            bool returnValue = false;

            if (_instrument != null)
            {
                if (_instrument.Initialized == true)
                {
                    returnValue = _instrument.SaveSetup(_usSetupDataBasePath + "\\" + "UPRSetups.mdb", _actualUsSetupName, "Setup");

                    if (returnValue == true)
                    {
                        UsSetupsArray = _instrument.GetSetupList(_usSetupDataBasePath);
                    }
                }
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool DeleteSetup()
        {
            bool returnValue = false;

            if (_instrument != null)
            {
                if (_instrument.Initialized == true)
                {
                    returnValue = _instrument.DeleteUSSetup(_usSetupDataBasePath + "\\" + "UPRSetups.mdb", _actualUsSetupName);

                    if (returnValue == true)
                    {
                        UsSetupsArray = _instrument.GetSetupList(_usSetupDataBasePath);
                    }
                }
            }

            return returnValue;
        }

        #region UPR test

        public bool TestUPR()
        {
            bool returnValue = false;

            UprWrapper.HardwareStatus uprStatus = _upr.UprHardwareStatus;

            returnValue = ((uprStatus & UprWrapper.HardwareStatus.HardwareFound) == UprWrapper.HardwareStatus.HardwareFound) ? true : false;

            return returnValue;
        }

        #endregion

        #region RPPCommands

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pulser"></param>
        /// <param name="channel"></param>
        /// <param name="desiredAmplitude"></param>
        /// <returns></returns>
        public bool SetPulserAmplitude(int channel, int pulser, int desiredAmplitude)
        {
            bool returnValue = false;
            int actualAmplitude = 0;

            try
            {
                if (desiredAmplitude >= 1 && desiredAmplitude <= 8)
                {
                    desiredAmplitude = desiredAmplitude - 1;

                    if (_instrument != null)
                    {
                        _instrument.Channels[channel].PulserParameter[pulser, "Amplitude"] = desiredAmplitude;
                        dynamic amplitude = _instrument.Channels[channel].PulserParameter[pulser, "Amplitude"];
                        actualAmplitude = (amplitude != null) ? (int)amplitude : 0;
                    }

                    returnValue = (actualAmplitude == desiredAmplitude) ? true : false;
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
        /// <param name="pulser"></param>
        /// <param name="channel"></param>
        /// <param name="desiredPulseWidth"></param>
        /// <returns></returns>
        public bool SetPulserPulseWidth(int channel, int pulser, int desiredPulseWidth)
        {
            bool returnValue = false;
            int actualPulseWidth = 0;

            try
            {
                if (desiredPulseWidth >= 10 && desiredPulseWidth <= 525)
                {
                    if (_instrument != null)
                    {
                        _instrument.Channels[channel].PulserParameter[pulser, "PulseWidth"] = desiredPulseWidth;
                        dynamic pulseWidth = _instrument.Channels[channel].PulserParameter[pulser, "PulseWidth"];
                        actualPulseWidth = (pulseWidth != null) ? (int)pulseWidth : 0;
                    }
                }

                returnValue = (actualPulseWidth == desiredPulseWidth) ? true : false;
            }
            catch (Exception ex)
            {
                returnValue = false;
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }

        #endregion

        #region PRF

        public bool SetUprPRF(int desiredValue)
        {
            bool returnValue = false;

            try
            {
                if (desiredValue >= _upr.MinPrf && desiredValue <= _upr.MaxPrf)
                {
                    _upr.Prf = desiredValue;
                    returnValue = (_upr.Prf == desiredValue) ? true : false;
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
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool SetUprExternalPRF(int enable)
        {
            bool returnValue = false;

            try
            {
                _upr.ExternalPrf = enable;
                returnValue = (_upr.ExternalPrf == enable);
            }
            catch (Exception ex)
            {
                returnValue = false;
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }

        #endregion

        #region Channels

        /// <summary>
        /// 
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public bool OpenChannel(int channel, int enable)
        {
            bool returnValue = false;
            bool enableChannel = false;

            try
            {
                if (_instrument != null && _upr != null)
                {
                    enableChannel = (enable > 0);

                    _upr.UprChannels[channel].Enabled = enableChannel;
                    _upr.UprChannels[channel].Visible = enableChannel;

                    _instrument.Channels[channel].Enable = enableChannel;

                    returnValue = (_upr.UprChannels[channel].Enabled == enableChannel);
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
        /// <param name="channel"></param>
        /// <param name="desiredGain"></param>
        /// <returns></returns>
        public bool SetChannelGain(int channel, float desiredGain)
        {
            bool returnValue = false;

            try
            {
                if (desiredGain >= 0.0 && desiredGain <= 51.0)
                {
                    if (_instrument != null && _upr != null)
                    {
                        _upr.UprChannels[channel].Gain = desiredGain;
                        _instrument.Channels[channel].PulserParameter[0, "Gain"] = desiredGain;
                        returnValue = (_upr.UprChannels[channel].Gain == desiredGain) ? true : false;
                    }
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
        /// <param name="channel"></param>
        /// <param name="desiredPrescale"></param>
        /// <returns></returns>
        public bool SetChannelPrescale(int channel, float desiredPrescale)
        {
            bool returnValue = false;

            try
            {
                if (desiredPrescale >= 0 && desiredPrescale <= 3)
                {
                    if (_instrument != null && _upr != null)
                    {
                        _upr.UprChannels[channel].PreamplifierGain = (short)desiredPrescale;
                        _instrument.Channels[channel].PreamplifierParameter[0, "Gain"] = (short)desiredPrescale;
                        returnValue = (_upr.UprChannels[channel].PreamplifierGain == (short)desiredPrescale) ? true : false;
                    }
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
        /// <param name="channel"></param>
        /// <param name="desiredFilter"></param>
        /// <returns></returns>
        public bool SetChannelPulserFilter(int channel, int desiredFilter)
        {
            bool returnValue = false;

            try
            {
                if (desiredFilter >= 0 && desiredFilter <= 100)
                {
                    if (_instrument != null && _upr != null)
                    {
                        //_upr.UprChannels[channel].PulserFilter = desiredFilter;
                        //returnValue = (_upr.UprChannels[channel].PulserFilter == desiredFilter) ? true : false;

                        _instrument.Channels[channel].PulserParameter[0, "DampReactive"] = desiredFilter;
                        returnValue = (Convert.ToInt32(_instrument.Channels[channel].PulserParameter[0, "DampReactive"]) == desiredFilter) ? true : false;
                    }
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
        /// <param name="channel"></param>
        /// <param name="desiredFilter"></param>
        /// <returns></returns>
        public bool SetChannelRecieverFilter(int channel, int desiredFilter)
        {
            bool returnValue = false;

            try
            {
                if (desiredFilter >= 0 && desiredFilter <= 10)
                {
                    if (_upr != null)
                    {
                        _upr.UprChannels[channel].RecieverFilter = desiredFilter;
                        returnValue = (_upr.UprChannels[channel].RecieverFilter == desiredFilter) ? true : false;
                    }
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
        /// <param name="channel"></param>
        /// <param name="desiredDelay"></param>
        /// <returns></returns>
        public bool SetChannelDelay(int channel, int desiredDelay)
        {
            bool returnValue = false;

            try
            {
                if (_upr != null)
                {
                    _upr.UprChannels[channel].Delay = desiredDelay;
                    returnValue = (_upr.UprChannels[channel].Delay == desiredDelay) ? true : false;
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
        /// <param name="channel"></param>
        /// <param name="desiredRange"></param>
        /// <returns></returns>
        public bool SetChannelRange(int channel, int desiredRange)
        {
            bool returnValue = false;

            try
            {
                if (_upr != null)
                {
                    _upr.UprChannels[channel].Range = desiredRange;
                    returnValue = (_upr.UprChannels[channel].Range == desiredRange) ? true : false;
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
        /// <param name="channel"></param>
        /// <param name="gate"></param>
        /// <param name="desiredRange"></param>
        /// <returns></returns>
        public bool SetChannelGateRange(int channel, int gate, int desiredRange)
        {
            bool returnValue = false;

            try
            {
                if (_upr != null)
                {
                    _upr.UprChannels[channel].Gates[gate].Range = desiredRange;
                    returnValue = (_upr.UprChannels[channel].Gates[gate].Range == desiredRange) ? true : false;
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
        /// <param name="channel"></param>
        /// <returns></returns>
        public bool AddChannelGate(int channel)
        {
            bool returnValue = false;

            try
            {
                if (_upr != null)
                {
                    returnValue = _upr.UprChannels[channel].AddGate();
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
        /// <param name="channel"></param>
        /// <param name="gate"></param>
        /// <returns></returns>
        public bool DeleteChannelGate(int channel, short gate)
        {
            bool returnValue = false;

            try
            {
                if (_upr != null)
                {
                    returnValue = _upr.UprChannels[channel].RemoveGate(gate);
                }
            }
            catch (Exception ex)
            {
                returnValue = false;
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }

        #endregion

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnChannelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Visible")
            {
                //_instrument.Channels[(sender as ChannelWrapper).ID].Enable = (sender as ChannelWrapper).Visible;
                _instrument.Display.AScanVisible[_instrument.Channels.ActiveChannel] = false;
                _instrument.Display.AScanVisible[_instrument.Channels.ActiveChannel] = true;

                _instrument.Display.TileWindows();
            }
        }

        #endregion

        #region Data validation

        private Dictionary<String, List<String>> _errors = new Dictionary<string, List<string>>();

        // Adds the specified error to the errors collection if it is not already 
        // present, inserting it in the first position if isWarning is false. 
        public void AddError(string propertyName, string error, bool isWarning)
        {
            try
            {
                if (_errors == null)
                {
                    _errors = new Dictionary<string, List<string>>();
                }
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
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);

            }
        }
        // Removes the specified error from the errors collection if it is present. 
        public void RemoveError(string propertyName, string error)
        {
            try
            {
                if (_errors == null)
                {
                    _errors = new Dictionary<string, List<string>>();
                }
                if (_errors.ContainsKey(propertyName) && _errors[propertyName].Contains(error))
                {
                    _errors[propertyName].Remove(error);
                    if (_errors[propertyName].Count == 0)
                    {
                        _errors.Remove(propertyName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
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
                string returnValue = null;

                if (_errors != null)
                {
                    returnValue = (!_errors.ContainsKey(propertyName) ? null : String.Join(Environment.NewLine, _errors[propertyName]));
                }

                return returnValue;
            }
        }

        #endregion
    }
    /// <summary>
    /// 
    /// </summary>
    public class UprWrapper : INotifyPropertyChanged, IDisposable, IDataErrorInfo
    {
        #region Constants

        private const string PRF_INVALID_VALUE_ERROR = "Invalid Prf value";
        private const string CHARGE_TIME_INVALID_VALUE_ERROR = "Invalid Charge time value";

        #endregion

        #region Nested

        //Method returns Array of BYTES( 0 or 1) defines the State of the Harwdare. 0 - Hardware Found, 1 -  Aquis.On , 2 - AScan Enable, 3 - AScan Completed, 4 - Ext.Trig.Is On, 5 - Ext.Trig.Availible, 6 - Ext.Trigger Enable, 7 - Dig.Input Monit.Enable

        [Flags]
        public enum HardwareStatus : byte
        {
            HardwareFound = 1,
            AquisOn = 2,
            AScanEnable = 4,
            AScanCompleted = 8,
            ExtTrigIsOn = 16,
            ExtTrigAvailible = 32,
            ExtTriggerEnable = 64,
            DigInputMonitEnable = 128
        }

        public enum ReceiverMode
        {
            PE,
            TT,
            TR
        }

        #endregion

        #region Fields

        private ObservableCollection<UprChannelWrapper> _uprChannels = new ObservableCollection<UprChannelWrapper>();
        public ObservableCollection<UprChannelWrapper> UprChannels
        {
            get
            {
                return _uprChannels;
            }
            set
            {
                _uprChannels = value;
                OnPropertyChanged();
            }
        }

        private Upr _uprObject;
        public Upr UprObject
        {
            get
            {
                return _uprObject;
            }
            set
            {
                _uprObject = value;

                Configuration = _uprObject.UPRConfiguration;

                MaxChannels = _uprObject.MaxChannel;

                for (int i = 0; i < MaxChannels; i++)
                {
                    _uprChannels.Add(new UprChannelWrapper() { UprChannel = _uprObject.Channels[i] });
                }

                OnPropertyChanged();
            }
        }

        private UPRConfig _configuration;
        public UPRConfig Configuration
        {
            get
            {
                return _configuration;
            }
            set
            {
                _configuration = value;

                OnPropertyChanged();
            }
        }

        private string _uprDriverType;
        public string UPRDriverType
        {
            get
            {
                if (_uprObject != null)
                {
                    _uprDriverType = _uprObject.UPRConfiguration.driverType.ToString();
                }

                return _uprDriverType;
            }
            set
            {
                _uprDriverType = value;
                OnPropertyChanged();
            }
        }

        private string _ascanBufferSize;
        public string AscanBufferSize
        {
            get
            {
                if (_uprObject != null)
                {
                    _ascanBufferSize = _uprObject.UPRConfiguration.ascanBufferSize.ToString();
                }

                return _ascanBufferSize;
            }
            set
            {
                _ascanBufferSize = value;
                OnPropertyChanged();
            }
        }

        private int _prf;
        public int Prf
        {
            get
            {
                if (_uprObject != null)
                {
                    _prf = _uprObject.PRF;
                }

                return _prf;
            }
            set
            {
                if (_prf != value)
                {
                    if (value < _uprObject.PRFMinimum || value > _uprObject.PRFMaximum)
                    {
                        AddError("Prf", PRF_INVALID_VALUE_ERROR, false);
                    }
                    else
                    {
                        RemoveError("Prf", PRF_INVALID_VALUE_ERROR);

                        _prf = value;

                        _uprObject.PRF = _prf;
                    }

                    OnPropertyChanged();
                }
            }
        }

        private int _externalPrf;
        public int ExternalPrf
        {
            get
            {
                if (_uprObject != null)
                {
                    _externalPrf = _uprObject.ExternalTriggerEnable;
                }

                return _externalPrf;
            }
            set
            {
                _externalPrf = value;
                if (_uprObject != null)
                {
                    _uprObject.ExternalTriggerEnable = _externalPrf;
                }

                OnPropertyChanged();
            }
        }

        private int _minPrf;
        public int MinPrf
        {
            get
            {
                if (_uprObject != null)
                {
                    _minPrf = _uprObject.PRFMinimum;
                }

                return _minPrf;
            }
        }

        private int _maxPrf;
        public int MaxPrf
        {
            get
            {
                if (_uprObject != null)
                {
                    _maxPrf = _uprObject.PRFMaximum;
                }

                return _maxPrf;
            }
        }

        private double _chargeTime;
        public double ChargeTime
        {
            get
            {
                if (_uprObject != null)
                {
                    _chargeTime = _uprObject.ChargeTime;
                }

                return _chargeTime;
            }
            set
            {
                if (_chargeTime != value)
                {
                    if (value < 0.0 || value > 1000.0)
                    {
                        AddError("ChargeTime", CHARGE_TIME_INVALID_VALUE_ERROR, false);
                    }
                    else
                    {
                        RemoveError("ChargeTime", CHARGE_TIME_INVALID_VALUE_ERROR);

                        _chargeTime = value;

                        _uprObject.ChargeTime = _chargeTime;
                    }

                    OnPropertyChanged();
                }
            }
        }

        private HardwareStatus _uprHardwareStatus;
        public HardwareStatus UprHardwareStatus
        {
            get
            {
                byte statusUint = 0;
                Array hardwareStatus = null;

                if (_uprObject != null)
                {
                    hardwareStatus = _uprObject.GetHardwareStatus();
                    if (hardwareStatus != null)
                    {
                        for (int i = 0; i < hardwareStatus.Length; i++)
                        {
                            byte status = ((byte)hardwareStatus.GetValue(i) > 0) ? (byte)1 : (byte)0;
                            status = (byte)(status << i);
                            statusUint = (byte)(statusUint | status);
                        }
                    }

                    _uprHardwareStatus = (HardwareStatus)statusUint;
                }

                return _uprHardwareStatus;
            }
            private set
            {
                _uprHardwareStatus = value;

                OnPropertyChanged();
            }
        }

        private int _id;
        public int ID
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        private static int _maxChannels;
        public int MaxChannels
        {
            get
            {
                return _maxChannels;
            }
            set
            {
                _maxChannels = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public UprWrapper()
        {
            try
            {

            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        ~UprWrapper()
        {
            try
            {

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

            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        #endregion

        #region Methods

        public bool SetReceiverMode(int channel, ReceiverMode receiverMode)
        {
            bool returnValue = false;

            try
            {
                //PE = 0,TT = 1,TR = 2
                _uprObject.PulserMode[channel] = (PULSERMODETYPE)receiverMode;

                returnValue = (receiverMode == (ReceiverMode)_uprObject.PulserMode[channel]);

                //_uprObject.Channels[0].UpdateUI();
            }
            catch (Exception ex)
            {
                returnValue = false;
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }

        #endregion

        #region Convertors

        ///// <summary>
        ///// Cast from instrument channel to wrapped channel
        ///// </summary>
        ///// <param name="signal"></param>
        //public static implicit operator ChannelWrapper(object channel)
        //{
        //    return new ChannelWrapper
        //    {
        //        //channel.Enable;
        //    };
        //}
        ///// <summary>
        ///// Cast from alarm to int
        ///// </summary>
        ///// <param name="alarm"></param>
        //public static explicit operator int(ChannelWrapper alarm)
        //{
        //    throw new NotImplementedException();
        //}

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
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
            try
            {
                if (_errors == null)
                {
                    _errors = new Dictionary<string, List<string>>();
                }
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
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);

            }
        }
        // Removes the specified error from the errors collection if it is present. 
        public void RemoveError(string propertyName, string error)
        {
            try
            {
                if (_errors == null)
                {
                    _errors = new Dictionary<string, List<string>>();
                }
                if (_errors.ContainsKey(propertyName) && _errors[propertyName].Contains(error))
                {
                    _errors[propertyName].Remove(error);
                    if (_errors[propertyName].Count == 0)
                    {
                        _errors.Remove(propertyName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
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
                string returnValue = null;

                if (_errors != null)
                {
                    returnValue = (!_errors.ContainsKey(propertyName) ? null : String.Join(Environment.NewLine, _errors[propertyName]));
                }

                return returnValue;
            }
        }

        #endregion
    }
    /// <summary>
    /// 
    /// </summary>
    public class UprChannelWrapper : INotifyPropertyChanged, IDisposable
    {
        #region Nested

        public enum ChannelUnit
        {
            SAMPLES = 0,
            TIME = 1,
            DEPTH = 2,
            VDEPTH = 3,
            HDEPTH = 4
        }

        #endregion

        #region Fields

        private ObservableCollection<UprChannelPulserWrapper> _pulsers = new ObservableCollection<UprChannelPulserWrapper>();
        public ObservableCollection<UprChannelPulserWrapper> Pulsers
        {
            get
            {
                return _pulsers;
            }
            set
            {
                _pulsers = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<UprChannelGateWrapper> _gates = new ObservableCollection<UprChannelGateWrapper>();
        public ObservableCollection<UprChannelGateWrapper> Gates
        {
            get
            {
                return _gates;
            }
            set
            {
                _gates = value;
                OnPropertyChanged();
            }
        }

        private Channel _uprChannel;
        public Channel UprChannel
        {
            get
            {
                return _uprChannel;
            }
            set
            {
                _uprChannel = value;

                ID = _uprChannel.Index + 1;

                Array pulsers = _uprChannel.Pulsers;
                for (int i = 0; i < pulsers.Length; i++)
                {
                    _pulsers.Add(new UprChannelPulserWrapper()
                    {
                        ID = i,
                        UprChannelPulser = (IRPPDefine2)pulsers.GetValue(i),
                        MaxChannels = this.MaxChannels
                    });
                }

                Gates gates = _uprChannel.Gates;
                for (int i = 0; i < gates.Count; i++)
                {
                    _gates.Add(new UprChannelGateWrapper()
                    {
                        ID = i,
                        UprChannelGate = gates[i],
                        Name = gates[i].Name
                    });
                }

                OnPropertyChanged();
            }
        }

        private UprChannelPulserWrapper _selectedUprChannelPulser;
        public UprChannelPulserWrapper SelectedUprChannelPulser
        {
            get
            {
                return _selectedUprChannelPulser;
            }
            set
            {
                _selectedUprChannelPulser = value;
                OnPropertyChanged();
            }
        }

        private int _id;
        public int ID
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        private static int _maxChannels;
        public int MaxChannels
        {
            get
            {
                return _maxChannels;
            }
            set
            {
                _maxChannels = value;
                OnPropertyChanged();
            }
        }

        private bool _enabled;
        public bool Enabled
        {
            get
            {
                _enabled = (_uprChannel.Enable < 0) ? true : false;

                return _enabled;
            }
            set
            {
                _enabled = value;
                _uprChannel.Enable = (_enabled == true) ? -1 : 0;

                OnPropertyChanged();
            }
        }

        private bool _visible;
        public bool Visible
        {
            get
            {
                _visible = (_uprChannel.Visible < 0) ? true : false;

                return _visible;
            }
            set
            {
                _visible = value;

                _uprChannel.Visible = (_visible == true) ? -1 : 0;

                OnPropertyChanged();
            }
        }

        private int _delay;
        public int Delay
        {
            get
            {
                if (_uprChannel != null)
                {
                    _delay = _uprChannel.Delay;
                }
                return _delay;
            }
            set
            {
                _delay = value;

                if (_uprChannel != null)
                {
                    _uprChannel.Delay = _delay;
                }

                OnPropertyChanged();
            }
        }

        private int _range;
        public int Range
        {
            get
            {
                if (_uprChannel != null)
                {
                    _range = _uprChannel.Range;
                }

                return _range;
            }
            set
            {
                _range = value;

                if (_uprChannel != null)
                {
                    _uprChannel.Range = _range;
                }

                OnPropertyChanged();
            }
        }

        private float _gain;
        public float Gain
        {
            get
            {
                HardwareDefined gain = (HardwareDefined)_uprChannel.Gain;
                _gain = gain.RealValue;

                return _gain;
            }
            set
            {
                _gain = value;

                HardwareDefined gain = (HardwareDefined)_uprChannel.Gain;
                gain.RealValue = _gain;

                OnPropertyChanged();
            }
        }

        private short _preamplifierGain;
        public short PreamplifierGain
        {
            get
            {
                Array preamp = _uprChannel.Preamplifiers;
                IRPPDefine2 preamplifier = (IRPPDefine2)preamp.GetValue(0);
                _preamplifierGain = Convert.ToInt16(preamplifier.Value["Gain"]);

                return _preamplifierGain;
            }
            set
            {
                _preamplifierGain = value;

                Array preamp = _uprChannel.Preamplifiers;
                IRPPDefine2 preamplifier = (IRPPDefine2)preamp.GetValue(0);
                object actualPreampGainValue = preamplifier.Value["Gain"];
                preamplifier.Value["Gain"] = _preamplifierGain;

                OnPropertyChanged();
            }
        }

        private int _pulserFilter;
        public int PulserFilter
        {
            get
            {
                _pulserFilter = Pulsers[0].Filter;

                return _pulserFilter;
            }
            set
            {
                _pulserFilter = value;

                Pulsers[0].Filter = _pulserFilter;

                OnPropertyChanged();
            }
        }

        private int _recieverFilter;
        public int RecieverFilter
        {
            get
            {
                _recieverFilter = ((HardwareDefined)UprChannel.Filter).ValueIndex;

                return _recieverFilter;
            }
            set
            {
                _recieverFilter = value;

                ((HardwareDefined)UprChannel.Filter).ValueIndex = _recieverFilter;

                OnPropertyChanged();
            }
        }

        private ChannelUnit _unit;
        public ChannelUnit Unit
        {
            get
            {
                _unit = (ChannelUnit)_uprChannel.TimeRuler.Units;

                return _unit;
            }
            set
            {
                if (_unit != value)
                {
                    _unit = value;

                    _uprChannel.TimeRuler.Units = (UNITSMODE)_unit;

                    OnPropertyChanged();
                }
            }
        }
        public Array ChannelUnitArray
        {
            get
            {
                return Enum.GetValues(typeof(ChannelUnit));
            }
        }

        #endregion

        #region Constructor

        public UprChannelWrapper()
        {
            try
            {

            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        ~UprChannelWrapper()
        {
            try
            {

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
        public bool AddGate()
        {
            bool returnValue = false;

            try
            {
                Gates.Clear();

                ObservableCollection<UprChannelGateWrapper> _newGates = new ObservableCollection<UprChannelGateWrapper>();

                returnValue = _uprChannel.Gates.AddGate();

                if (returnValue == true)
                {
                    Gates gates = _uprChannel.Gates;
                    for (int i = 0; i < gates.Count; i++)
                    {
                        _newGates.Add(new UprChannelGateWrapper()
                        {
                            ID = i,
                            UprChannelGate = gates[i],
                            Name = gates[i].Name
                        });
                    }

                    Gates = _newGates;
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
        /// <returns></returns>
        public bool RemoveGate(short gate)
        {
            bool returnValue = false;

            try
            {
                Gates.Clear();

                ObservableCollection<UprChannelGateWrapper> _newGates = new ObservableCollection<UprChannelGateWrapper>();

                returnValue = _uprChannel.Gates.DeleteGate(gate);

                if (returnValue == true)
                {
                    Gates gates = _uprChannel.Gates;
                    for (int i = 0; i < gates.Count; i++)
                    {
                        _newGates.Add(new UprChannelGateWrapper()
                        {
                            ID = i,
                            UprChannelGate = gates[i],
                            Name = gates[i].Name
                        });
                    }

                    Gates = _newGates;
                }
            }
            catch (Exception ex)
            {
                returnValue = false;
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }

        #endregion

        #region Convertors

        ///// <summary>
        ///// Cast from instrument channel to wrapped channel
        ///// </summary>
        ///// <param name="signal"></param>
        //public static implicit operator ChannelWrapper(object channel)
        //{
        //    return new ChannelWrapper
        //    {
        //        //channel.Enable;
        //    };
        //}
        ///// <summary>
        ///// Cast from alarm to int
        ///// </summary>
        ///// <param name="alarm"></param>
        //public static explicit operator int(ChannelWrapper alarm)
        //{
        //    throw new NotImplementedException();
        //}

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
    /// <summary>
    /// 
    /// </summary>
    public class UprChannelPulserWrapper : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        //dynamic name = _instrument.Channels[0].PulserParameter[0, "DampReactive"];
        //dynamic name = _instrument.Channels[0].PulserParameter[0, "DampResistive"];
        //dynamic name = _instrument.Channels[0].PulserParameter[0, "Type"];
        //Array pulsers = _uprObject.Channels[0].Pulsers;
        //IRPPDefine2 pulser = (IRPPDefine2)pulsers.GetValue(0);
        //Pulsers(PulserIndex).Value(“DampReactive")

        private IRPPDefine2 _uprChannelPulser;
        public IRPPDefine2 UprChannelPulser
        {
            get
            {
                return _uprChannelPulser;
            }
            set
            {
                _uprChannelPulser = value;

                Name = _uprChannelPulser.RemoteUnitName;

                OnPropertyChanged();
            }
        }

        private string _name;
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

        private int _id;
        public int ID
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        private static int _maxChannels;
        public int MaxChannels
        {
            get
            {
                return _maxChannels;
            }
            set
            {
                _maxChannels = value;
                OnPropertyChanged();
            }
        }

        private bool _unitStatus;
        public bool UnitStatus
        {
            get
            {
                _unitStatus = _uprChannelPulser.RemoteUnitStatus;

                return _unitStatus;
            }
            private set
            {
                _unitStatus = value;

                OnPropertyChanged();
            }
        }

        private int _uprChannel;
        public int UprChannel
        {
            get
            {
                return _uprChannel;
            }
            set
            {
                _uprChannel = value;

                if (_uprChannel >= 0 && _uprChannel <= _maxChannels)
                {
                    _uprChannelPulser.UPRChannel = (short)_uprChannel;
                }

                OnPropertyChanged();
            }
        }

        private int _amplitude;
        public int Amplitude
        {
            get
            {
                _amplitude = (int)_uprChannelPulser.Value["Amplitude"] + 1;
                return _amplitude;
            }
            set
            {
                _amplitude = value;

                if (_amplitude >= 1 && _amplitude <= 8)
                {
                    _amplitude = _amplitude - 1;

                    _uprChannelPulser.Value["Amplitude"] = _amplitude;
                }

                OnPropertyChanged();
            }
        }

        private int _pulseWidth;
        public int PulseWidth
        {
            get
            {
                _pulseWidth = (int)_uprChannelPulser.Value["PulseWidth"];
                return _pulseWidth;
            }
            set
            {
                _pulseWidth = value;

                if (_pulseWidth >= 10 && _pulseWidth <= 525)
                {
                    _uprChannelPulser.Value["PulseWidth"] = _pulseWidth;
                }

                OnPropertyChanged();
            }
        }

        private int _filter;
        public int Filter
        {
            get
            {
                _filter = (int)_uprChannelPulser.Value["DampReactive"];
                return _filter;
            }
            set
            {
                _filter = value;

                if (_filter >= 1 && _filter <= 2)
                {
                    _uprChannelPulser.Value["DampReactive"] = _filter;
                }

                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public UprChannelPulserWrapper()
        {
            try
            {

            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        ~UprChannelPulserWrapper()
        {
            try
            {

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

            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        #endregion

        #region Convertors

        ///// <summary>
        ///// Cast from instrument channel to wrapped channel
        ///// </summary>
        ///// <param name="signal"></param>
        //public static implicit operator ChannelWrapper(object channel)
        //{
        //    return new ChannelWrapper
        //    {
        //        //channel.Enable;
        //    };
        //}
        ///// <summary>
        ///// Cast from alarm to int
        ///// </summary>
        ///// <param name="alarm"></param>
        //public static explicit operator int(ChannelWrapper alarm)
        //{
        //    throw new NotImplementedException();
        //}

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
    /// <summary>
    /// 
    /// </summary>
    public class UprChannelGateWrapper : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        private Gate _uprChannelGate;
        public Gate UprChannelGate
        {
            get
            {
                return _uprChannelGate;
            }
            set
            {
                _uprChannelGate = value;

                Name = _uprChannelGate.Name;

                OnPropertyChanged();
            }
        }

        private string _name;
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

        private int _id;
        public int ID
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        private int _range;
        public int Range
        {
            get
            {
                if (_uprChannelGate != null)
                {
                    _range = _uprChannelGate.Range;
                }

                return _range;
            }
            set
            {
                _range = value;

                if (_uprChannelGate != null)
                {
                    _uprChannelGate.Range = _range;
                }

                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public UprChannelGateWrapper()
        {
            try
            {

            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        ~UprChannelGateWrapper()
        {
            try
            {

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

            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        #endregion

        #region Convertors

        ///// <summary>
        ///// Cast from instrument channel to wrapped channel
        ///// </summary>
        ///// <param name="signal"></param>
        //public static implicit operator ChannelWrapper(object channel)
        //{
        //    return new ChannelWrapper
        //    {
        //        //channel.Enable;
        //    };
        //}
        ///// <summary>
        ///// Cast from alarm to int
        ///// </summary>
        ///// <param name="alarm"></param>
        //public static explicit operator int(ChannelWrapper alarm)
        //{
        //    throw new NotImplementedException();
        //}

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
