using CalibrationToolTester.InstrumentImplementation;
using System;
using CalibrationToolTester.GlobalLoger;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CalibrationToolTester.InstrumentImplementation
{
    public class InstrumentCommand : INotifyPropertyChanged, IDataErrorInfo
    {
        public enum CommandType
        {
            Command,
            Query
        }

        #region Constants

        private const string COMMAND_EMPTY_ERROR = "Invalid command";

        #endregion Constants

        #region Fields

        public static CommandTranslationService commandsTranslation = null;

        private Dictionary<String, List<String>> _errors = new Dictionary<string, List<string>>();

        private CommandType _type;
        public CommandType Type
        {
            get
            {
                return _type;
            }
            set
            {
                _type = value;
                OnPropertyChanged();
            }
        }

        private string _header = string.Empty;
        public string Header
        {
            get
            {
                return _header;
            }
            set
            {
                string[] splittedValue = value.Split(new char[] { ':' }, StringSplitOptions.None);

                _header = value;

                Mnemonics = splittedValue;
                Type = (_header.Contains("?")) ? CommandType.Query : CommandType.Command;

                _commandMessage = CreateMessage();

                OnPropertyChanged();
            }
        }

        private string[] _mnemonics;
        public string[] Mnemonics
        {
            get
            {
                return _mnemonics;
            }
            set
            {
                _mnemonics = value;

                _commandMessage = CreateMessage();

                OnPropertyChanged();
            }
        }

        private string[] _arguments;
        public string[] Arguments
        {
            get
            {
                return _arguments;
            }
            set
            {
                _arguments = value;

                _commandMessage = CreateMessage();

                OnPropertyChanged();
            }
        }

        private string _commandMessage;
        public string CommandMessage
        {
            get
            {
                return _commandMessage;
            }
            set
            {
                //split command
                string[] splittedValue = value.Split(new char[] { ' ' }, StringSplitOptions.None);
                //translate command
                splittedValue[0] = commandsTranslation.Translate(splittedValue[0]);
                //split translated instrument command
                string[] splittedTranslatedValue = splittedValue[0].Split(new char[] { ' ' }, StringSplitOptions.None);

                if (splittedTranslatedValue.Length > 1)
                {
                    splittedValue = splittedTranslatedValue;
                }

                if (IsCommandValid(splittedValue[0]))
                {
                    Header = splittedValue[0];
                    Arguments = (splittedValue.Length > 1) ? splittedValue[1].Split(new char[] { ',' }, StringSplitOptions.None) : null;

                    _commandMessage = CreateMessage();
                    _commandMessage = _commandMessage.ToLower();

                    OnPropertyChanged();
                }
            }
        }

        private string _reply;
        public string Reply
        {
            get
            {
                return _reply;
            }
            set
            {
                _reply = value;
                OnPropertyChanged();
            }
        }

        private static int _countSent;
        public int CountSent
        {
            get
            {
                return _countSent;
            }
            set
            {
                _countSent = value;
                OnPropertyChanged();
            }
        }

        private bool _isValid;
        public bool IsValid
        {
            get
            {
                return _isValid;
            }
            set
            {
                _isValid = value;
                OnPropertyChanged();
            }
        }

        private bool _isJournaled;
        public bool IsJournaled
        {
            get
            {
                return _isJournaled;
            }
            set
            {
                _isJournaled = value;
                OnPropertyChanged();
            }
        }

        private long _executeTime;
        public long ExecuteTime
        {
            get
            {
                return _executeTime;
            }
            set
            {
                _executeTime = value;
                OnPropertyChanged();
            }
        }

        #endregion Fields

        #region Constructor

        public InstrumentCommand()
        {
            try
            {
                _commandMessage = string.Empty;
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public string CreateMessage()
        {
            string returnValue = string.Empty;

            if (_mnemonics != null)
            {
                for (int i = 0; i < _mnemonics.Length - 1; i++)
                {
                    returnValue += _mnemonics[i] + ":";
                }

                returnValue += _mnemonics[_mnemonics.Length - 1];
            }
            else
            {
                returnValue = _header;
            }

            if (_arguments != null)
            {
                returnValue = returnValue + " ";
                for (int i = 0; i < _arguments.Length - 1; i++)
                {
                    returnValue = returnValue + _arguments[i] + ",";
                }
                returnValue = returnValue + _arguments[_arguments.Length - 1];
            }

            return returnValue;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool IsCommandValid(string value)
        {
            IsValid = false;

            if (String.IsNullOrEmpty(value) == true)
            {
                AddError("Command", COMMAND_EMPTY_ERROR, false);
                IsValid = false;
            }
            else
            {
                RemoveError("Command", COMMAND_EMPTY_ERROR);
            }

            if (String.IsNullOrEmpty(value) == false)
            {
                if (value.Contains("/**") || value.Contains("**/"))
                {
                    IsValid = false;
                }

                for (int i = 0; i < commandsTranslation.ValidCommands.Count; i++)
                {
                    if (value.ToLower().Equals(commandsTranslation.ValidCommands[i].Split(new char[] { ' ' }, StringSplitOptions.None)[0].ToLower()))
                    {
                        IsValid = true;
                        break;
                    }
                }
            }

            return IsValid;
        }

        #endregion Methods

        #region Data validation

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

        #endregion Data validation

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged Members

        #region IDataErrorInfo Members

        public string Error
        {
            get { throw new NotImplementedException(); }
        }

        public string this[string propertyName]
        {
            get
            {
                return (!_errors.ContainsKey(propertyName) ? null : String.Join(Environment.NewLine, _errors[propertyName]));
            }
        }

        #endregion IDataErrorInfo Members
    }
}
