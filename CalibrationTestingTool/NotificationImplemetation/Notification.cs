using CalibrationToolTester.GlobalLoger;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace CalibrationToolTester.NotificationImplementation
{
    public class Notification : INotifyPropertyChanged, IDisposable, IDataErrorInfo
    {
        #region Constants

        private const string PRF_INVALID_VALUE_ERROR = "Invalid Prf value";
        private const string CHARGE_TIME_INVALID_VALUE_ERROR = "Invalid Charge time value";

        #endregion

        #region Fields

        public static CommandTranslationService commandsTranslation = new CommandTranslationService();

        private readonly object _instrumentLock = new object();

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

                string translationFilesPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName + "\\DevicesTypes\\NotificationTypes\\";
                commandsTranslation.LoadTranslations(translationFilesPath + _actualCommandsTranslationsFile + ".xml");

                OnPropertyChanged();
            }
        }

        NotificationCommand _actualCommand = new NotificationCommand();
        public NotificationCommand ActualCommand
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

        #endregion

        #region Constructor

        public Notification()
        {
            try
            {
                string translationFilesPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName + "\\DevicesTypes\\NotificationTypes\\";
                DirectoryInfo info = new DirectoryInfo(translationFilesPath);
                FileInfo[] files = info.GetFiles("*.xml");

                for (int i = 0; i < files.Length; i++)
                {
                    CommandsTranslationsFiles.Add(files[i].Name.Replace(files[i].Extension, ""));
                }

                ActualCommandsTranslationsFile = CommandsTranslationsFiles[0];

                NotificationCommand.commandsTranslation = commandsTranslation;
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        ~Notification()
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
                    int desiredValue = 0;

                    //TODO:Vlad:parse message and do something
                    if (_actualCommand.Mnemonics.Length == 2)
                    {
                        if (_actualCommand.Mnemonics[1].ToLower().Contains("parameter"))
                        {
                            if (_actualCommand.Arguments.Length == 1)
                            {
                                DialogResult result = MessageBox.Show("Confirm checking " + _actualCommand.Arguments[0] + " parameter", "Complete test line",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                returnValue = (result == DialogResult.OK) ? "" : returnValue;
                            }
                        }
                        if (_actualCommand.Mnemonics[0].ToLower().Contains("message"))
                        {
                            if (_actualCommand.Mnemonics[1].ToLower().Contains("show"))
                            {
                                if (_actualCommand.Arguments.Length == 1)
                                {
                                    DialogResult result = MessageBox.Show(_actualCommand.Arguments[0].Trim(new char[] { ' ', '"' }));
                                    returnValue = (result == DialogResult.OK) ? "" : returnValue;
                                }
                                if (_actualCommand.Arguments.Length == 2)
                                {
                                    if (_actualCommand.Arguments[0].ToLower().Contains("ok") == true && _actualCommand.Arguments[1].ToLower().Contains("cancel"))
                                    {
                                        DialogResult result = MessageBox.Show(_actualCommand.CommandMessage, string.Empty, MessageBoxButtons.OKCancel);
                                        returnValue = (result == DialogResult.OK) ? "" : returnValue;
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
}
