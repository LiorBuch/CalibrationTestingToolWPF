using CalibrationToolTester.GlobalLoger;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CalibrationToolTester.GlobalStatus
{
    public class Status : INotifyPropertyChanged, IDataErrorInfo
    {
        #region Fields

        private string _message;
        public string Message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Data validation

        private Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

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
                    returnValue = !_errors.ContainsKey(propertyName) ? null : string.Join(Environment.NewLine, _errors[propertyName]);
                }

                return returnValue;
            }
        }

        #endregion
    }
}
