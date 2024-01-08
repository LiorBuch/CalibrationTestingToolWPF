using CalibrationToolTester.GlobalLoger;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CalibrationToolTester.ScopeImplementation
{
    public class Measurement : INotifyPropertyChanged, IDataErrorInfo
    {
        #region Constants

        private const string MEASUREMENT_EMPTY_ERROR = "Invalid measurement";

        #endregion Constants

        #region Nested

        public enum MeasurementState
        {
            On,
            Off
        }

        #endregion Nested

        #region Fields

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

                OnPropertyChanged();
            }
        }

        private string _type = string.Empty;
        public string Type
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

        private MeasurementState _state;
        public MeasurementState State
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
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

        private string _name = string.Empty;
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                //limit name to 10 characters
                if (string.IsNullOrEmpty(value) == false)
                {
                    if (value.Length > 10)
                    {
                        value = value.Substring(0, 10);
                    }
                }

                _name = value;

                OnPropertyChanged();
            }
        }

        private string _units = string.Empty;
        public string Units
        {
            get
            {
                return _units;
            }
            set
            {
                _units = value;
                OnPropertyChanged();
            }
        }

        private string _unitsPrefix = string.Empty;

        public string UnitsPrefix
        {
            get
            {
                return _unitsPrefix;
            }
            set
            {
                if (_unitsPrefix != value)
                {
                    _unitsPrefix = value;

                    ActualValue = ActualValue;
                    DesiredValue = DesiredValue;
                    ThresholdValue = ThresholdValue;

                    OnPropertyChanged();
                }
            }
        }

        private string _source_1 = string.Empty;

        public string Source_1
        {
            get
            {
                return _source_1;
            }
            set
            {
                _source_1 = value;
                OnPropertyChanged();
            }
        }

        private string _source_2 = string.Empty;

        public string Source_2
        {
            get
            {
                return _source_2;
            }
            set
            {
                _source_2 = value;
                OnPropertyChanged();
            }
        }

        private double _actualValue;

        public double ActualValue
        {
            get
            {
                return _actualValue;
            }
            set
            {
                _actualValue = value;

                _actualValue = ConvertValueToPrefixRange(_actualValue);

                Delta = Math.Abs(_actualValue - _desiredValue);

                OnPropertyChanged();
            }
        }

        //TODO:Vlad:rename to desired value
        private double _desiredValue;

        public double DesiredValue
        {
            get
            {
                return _desiredValue;
            }
            set
            {
                _desiredValue = value;

                _desiredValue = ConvertValueToPrefixRange(_desiredValue);

                Delta = Math.Abs(_actualValue - _desiredValue);

                OnPropertyChanged();
            }
        }

        private double _delta;

        public double Delta
        {
            get
            {
                return _delta;
            }
            set
            {
                _delta = value;

                ValueState = (_delta < _thresholdValue);

                OnPropertyChanged();
            }
        }

        private double _thresholdValue;

        public double ThresholdValue
        {
            get
            {
                return _thresholdValue;
            }
            set
            {
                _thresholdValue = Math.Abs(value);

                _thresholdValue = ConvertValueToPrefixRange(_thresholdValue);

                ValueState = (_delta < _thresholdValue);

                OnPropertyChanged();
            }
        }

        private bool _valueState;

        public bool ValueState
        {
            get
            {
                return _valueState;
            }
            set
            {
                _valueState = value;
                OnPropertyChanged();
            }
        }

        #endregion Fields

        #region Methods

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public Measurement Clone()
        {
            Measurement clonedMeasurement = null;

            try
            {
                clonedMeasurement = (Measurement)this.MemberwiseClone();
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return clonedMeasurement;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public bool Reset()
        {
            bool returnValue = false;

            try
            {
                ID = 0;
                Name = string.Empty;
                Type = string.Empty;
                ActualValue = 0.0;
                DesiredValue = 0.0;
                ThresholdValue = 0.0;
                Delta = 0.0;
                ValueState = false;
                Units = string.Empty;
                UnitsPrefix = string.Empty;
                Source_1 = string.Empty;
                Source_2 = string.Empty;

                returnValue = true;
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
        /// <param name="value"></param>
        /// <returns></returns>
        public double ConvertValueToPrefixRange(double value)
        {
            double returnValue = 0.0;

            try
            {
                if (UnitsPrefix == "E")
                {
                    value = Math.Round(value / Math.Pow(10, 18), _digitsNumber);
                }
                else if (UnitsPrefix == "P")
                {
                    value = Math.Round(value / Math.Pow(10, 15), _digitsNumber);
                }
                else if (UnitsPrefix == "T")
                {
                    value = Math.Round(value / Math.Pow(10, 12), _digitsNumber);
                }
                else if (UnitsPrefix == "G")
                {
                    value = Math.Round(value / Math.Pow(10, 9), _digitsNumber);
                }
                else if (UnitsPrefix == "M")
                {
                    value = Math.Round(value / Math.Pow(10, 6), _digitsNumber);
                }
                else if (UnitsPrefix == "k")
                {
                    value = Math.Round(value / Math.Pow(10, 3), _digitsNumber);
                }
                else if (UnitsPrefix == "m")
                {
                    value = Math.Round(value / Math.Pow(10, -3), _digitsNumber);
                }
                else if (UnitsPrefix == "u")
                {
                    value = Math.Round(value / Math.Pow(10, -6), _digitsNumber);
                }
                else if (UnitsPrefix == "n")
                {
                    value = Math.Round(value / Math.Pow(10, -9), _digitsNumber);
                }
                else if (UnitsPrefix == "p")
                {
                    value = Math.Round(value / Math.Pow(10, -12), _digitsNumber);
                }
                else if (UnitsPrefix == "f")
                {
                    value = Math.Round(value / Math.Pow(10, -15), _digitsNumber);
                }
                else if (UnitsPrefix == "a")
                {
                    value = Math.Round(value / Math.Pow(10, -18), _digitsNumber);
                }

                returnValue = value;
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }

        #endregion Methods

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged Members

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

        #region IDataErrorInfo Members

        private Dictionary<String, List<String>> _errors = new Dictionary<string, List<string>>();

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

        #endregion IDataErrorInfo Members
    }
}