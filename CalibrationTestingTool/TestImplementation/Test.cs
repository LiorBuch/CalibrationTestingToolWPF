using CalibrationToolTester.GlobalLoger;
using CalibrationToolTester.ScopeImplementation;
using CalibrationToolTester.SignalGeneratorImplementation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows.Forms;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Bson;
//using System.Text.Json;
//using System.Text.Json.Serialization;

namespace CalibrationToolTester.TestImplementation
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class TestsCollection : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        //TODO:Vlad:execute tests classification to groups
        private ObservableCollection<Test> _tests = new ObservableCollection<Test>();
        public ObservableCollection<Test> Tests
        {
            get
            {
                return _tests;
            }
            set
            {
                _tests = value;

                if (_tests != null)
                {
                    ObservableCollection<string> loadedTestsNames = new ObservableCollection<string>();

                    //Groups.Clear();
                    //Groups.Add("Unsorted");

                    for (int i = 0; i < _tests.Count; i++)
                    {
                        _tests[i].ID = i;

                        if (string.IsNullOrEmpty(_tests[i].Group) == false)
                        {
                            if (Groups.Contains(_tests[i].Group) == false)
                            {
                                Groups.Add(_tests[i].Group);
                            }
                        }

                        _tests[i].ResetLines();
                        _tests[i].PropertyChanged -= OnTestPropertyChanged;
                        _tests[i].PropertyChanged += OnTestPropertyChanged;

                        loadedTestsNames.Add(_tests[i].Name);
                    }

                    if (Groups.Contains("Unsorted") == false)
                    {
                        Groups.Add("Unsorted");
                    }

                    Groups = new ObservableCollection<string>(Groups.OrderBy(a => a));

                    TestsNames = loadedTestsNames;

                    _testsGroups.Clear();

                    for (int i = 0; i < Groups.Count; i++)
                    {
                        _testsGroups.Add(new TestsGroup());
                        _testsGroups[i].ID = i;
                        _testsGroups[i].GroupName = Groups[i];
                        _testsGroups[i].GroupHeader = Groups[i];
                    }
                    for (int i = 0; i < _tests.Count; i++)
                    {
                        for (int j = 0; j < _testsGroups.Count; j++)
                        {
                            if (string.IsNullOrEmpty(_tests[i].Group) == false)
                            {
                                if (_testsGroups[j].GroupName.Contains(_tests[i].Group))
                                {
                                    _testsGroups[j].Tests.Add(_tests[i]);
                                    break;
                                }
                            }
                            else
                            {
                                if (_testsGroups[j].GroupName.Contains("Unsorted"))
                                {
                                    _testsGroups[j].Tests.Add(_tests[i]);
                                    break;
                                }
                            }
                        }
                    }

                    OnPropertyChanged();
                }
            }
        }

        [NonSerialized]
        private ObservableCollection<TestsGroup> _testsGroups = new ObservableCollection<TestsGroup>();
        public ObservableCollection<TestsGroup> TestsGroups
        {
            get
            {
                return _testsGroups;
            }
            set
            {
                _testsGroups = value;
                OnPropertyChanged();
            }
        }

        private string _actualGroup = string.Empty;
        public string ActualGroup
        {
            get
            {
                return _actualGroup;
            }
            set
            {
                _actualGroup = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private string _testGroupFilter = string.Empty;
        public string TestGroupFilter
        {
            get
            {
                return _testGroupFilter;
            }
            set
            {
                _testGroupFilter = value;

                FilteredTestsNames.Clear();
                if (string.IsNullOrEmpty(_testGroupFilter) == false)
                {
                    for (int i = 0; i < _tests.Count; i++)
                    {
                        if (string.IsNullOrEmpty(_tests[i].Group) == false)
                        {
                            if (_tests[i].Group.Contains(_testGroupFilter))
                            {
                                FilteredTestsNames.Add(_tests[i].Name);
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < _tests.Count; i++)
                    {
                        FilteredTestsNames.Add(_tests[i].Name);
                    }
                }

                OnPropertyChanged();
            }
        }

        private ObservableCollection<string> _groups = new ObservableCollection<string>();
        public ObservableCollection<string> Groups
        {
            get
            {
                return _groups;
            }
            set
            {
                _groups = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<string> _testsNames = new ObservableCollection<string>();
        public ObservableCollection<string> TestsNames
        {
            get
            {
                return _testsNames;
            }
            set
            {
                _testsNames = value;

                if (string.IsNullOrEmpty(_testGroupFilter) == false)
                {
                    FilteredTestsNames = new ObservableCollection<string>(_testsNames.Where((item) => item.Contains(_testGroupFilter)));
                }
                else
                {
                    FilteredTestsNames = _testsNames;
                }

                OnPropertyChanged();
            }
        }

        private ObservableCollection<string> _filteredTestsNames = new ObservableCollection<string>();
        public ObservableCollection<string> FilteredTestsNames
        {
            get
            {
                return _filteredTestsNames;
            }
            set
            {
                _filteredTestsNames = value;

                OnPropertyChanged();
            }
        }

        private string _testerName = string.Empty;
        public string TesterName
        {
            get
            {
                return _testerName;
            }
            set
            {
                _testerName = value;
                OnPropertyChanged();
            }
        }

        private string _testedDeviceName = string.Empty;
        public string TestedDeviceName
        {
            get
            {
                return _testedDeviceName;
            }
            set
            {
                _testedDeviceName = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private string _fileName;
        public string FileName
        {
            get
            {
                return _fileName;
            }
            set
            {
                _fileName = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private int _previousExecutedTestID;
        public int PreviousExecutedTestID
        {
            get
            {
                return _previousExecutedTestID;
            }
            set
            {
                if (value >= 0 && value < _tests.Count)
                {
                    _previousExecutedTestID = value;
                    OnPropertyChanged();
                }
            }
        }

        [NonSerialized]
        private int _actualExecutedTestID;
        public int ActualExecutedTestID
        {
            get
            {
                return _actualExecutedTestID;
            }
            set
            {
                if (value >= 0 && value < _tests.Count)
                {
                    _actualExecutedTestID = value;

                    PreviousExecutedTestID = (_actualExecutedTestID - 1);
                    NextExecutedTestID = (_actualExecutedTestID + 1);

                    if (_tests.Count > 0)
                    {
                        ActualExecutedTest = _tests[_actualExecutedTestID];
                    }
                }
                else
                {
                    _actualExecutedTestID = 0;
                    PreviousExecutedTestID = NextExecutedTestID = 0;
                    ActualExecutedTest = null;
                }

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private string _actualExecutedTestName;
        public string ActualExecutedTestName
        {
            get
            {
                return _actualExecutedTestName;
            }
            set
            {
                if (value != null)
                {
                    _actualExecutedTestName = value;

                    for (int i = 0; i < _tests.Count; i++)
                    {
                        if (_actualExecutedTestName.Equals(_tests[i].Name) == true)
                        {
                            ActualExecutedTestID = _tests[i].ID;
                            break;
                        }
                    }

                    OnPropertyChanged();
                }
            }
        }

        [NonSerialized]
        private int _nextExecutedTestID;
        public int NextExecutedTestID
        {
            get
            {
                return _nextExecutedTestID;
            }
            set
            {
                if (value >= 0 && value < _tests.Count)
                {
                    _nextExecutedTestID = value;
                    OnPropertyChanged();
                }
            }
        }

        [NonSerialized]
        private Test _actualExecutedTest;
        public Test ActualExecutedTest
        {
            get
            {
                return _actualExecutedTest;
            }
            private set
            {
                _actualExecutedTest = value;

                if (_actualExecutedTest != null)
                {
                    _actualExecutedTestID = _actualExecutedTest.ID;
                    _actualExecutedTestName = _actualExecutedTest.Name;
                }

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private int _executedTestsCount;
        public int ExecutedTestsCount
        {
            get
            {
                return _executedTestsCount;
            }
            set
            {
                _executedTestsCount = (value >= 0) ? value : 0;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private int _failedTestsCount;
        public int FailedTestsCount
        {
            get
            {
                return _failedTestsCount;
            }
            set
            {
                _failedTestsCount = (value >= 0) ? value : 0;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private long _executionTime;
        public long ExecutionTime
        {
            get
            {
                return _executionTime;
            }
            set
            {
                _executionTime = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public TestsCollection()
        {
            try
            {
                //float num_1 = 0.2545458789532354532f;
                //float num_2 = 0.2545458789532354531f;
                //bool result = (num_1 > num_2);

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
                Logger.WriteMessage("TestsCollection:Dispose");

                _tests?.Clear();
                _tests = null;
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
        public bool SaveTests()
        {
            bool returnValue = false;

            string filePath = string.Empty;
            IFormatter formatter;
            Stream stream = null;

            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                saveFileDialog.Filter = "Text Files | *.txt";
                saveFileDialog.DefaultExt = "txt";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = saveFileDialog.FileName;

                    if (string.IsNullOrEmpty(filePath) == false)
                    {
                        formatter = new BinaryFormatter();

                        stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

                        formatter.Serialize(stream, _tests);
                        //formatter.Serialize(stream, this);

                        stream.Close();

                        returnValue = true;
                    }
                }
            }
            catch (Exception ex)
            {
                stream.Close();

                returnValue = false;

                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        //public bool JsonSaveTests()
        //{
        //    bool returnValue = false;

        //    string filePath = string.Empty;
        //    //IFormatter formatter;
        //    Stream stream = null;

        //    try
        //    {
        //        SaveFileDialog saveFileDialog = new SaveFileDialog();

        //        saveFileDialog.Filter = "Text Files | *.json";
        //        saveFileDialog.DefaultExt = "json";

        //        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        //        {
        //            filePath = saveFileDialog.FileName;

        //            if (string.IsNullOrEmpty(filePath) == false)
        //            {
        //                //formatter = new BinaryFormatter();

        //                stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

        //                JsonSerializer.SerializeAsync(stream, _tests);

        //                //formatter.Serialize(stream, _tests);

        //                //Serialize(stream, _tests);

        //                //string json = JsonSerializer.Serialize(_tests);
        //                //File.WriteAllText(filePath, json);

        //                stream.Close();

        //                returnValue = true;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        stream.Close();

        //        returnValue = false;

        //        Logger.ExceptionHandler(ex, ex.Message);
        //    }

        //    return returnValue;
        //}
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool LoadTests()
        {
            bool returnValue = false;

            string filePath = string.Empty;
            IFormatter formatter;
            Stream stream = null;

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();

                openFileDialog.Filter = "Text Files | *.txt";
                openFileDialog.DefaultExt = "txt";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;

                    if (string.IsNullOrEmpty(filePath) == false)
                    {
                        FileName = filePath;
                        formatter = new BinaryFormatter();
                        stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

                        Tests?.Clear();
                        Tests = null;
                        Tests = (ObservableCollection<Test>)formatter.Deserialize(stream);

                        ActualExecutedTestID = 0;

                        stream.Close();

                        returnValue = true;
                    }
                }
            }
            catch (Exception ex)
            {
                stream.Close();

                returnValue = false;

                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }

        //public TestsCollection LoadTests()
        //{
        //    TestsCollection returnValue = null;

        //    string filePath = string.Empty;
        //    IFormatter formatter;
        //    Stream stream = null;

        //    try
        //    {
        //        OpenFileDialog openFileDialog = new OpenFileDialog();

        //        openFileDialog.Filter = "Text Files | *.txt";
        //        openFileDialog.DefaultExt = "txt";

        //        if (openFileDialog.ShowDialog() == DialogResult.OK)
        //        {
        //            filePath = openFileDialog.FileName;

        //            if (string.IsNullOrEmpty(filePath) == false)
        //            {
        //                FileName = filePath;
        //                formatter = new BinaryFormatter();
        //                stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        //                returnValue = (TestsCollection)formatter.Deserialize(stream);

        //                if (returnValue.Tests != null)
        //                {
        //                    ObservableCollection<string> loadedTestsNames = new ObservableCollection<string>();

        //                    returnValue.Groups.Clear();

        //                    for (int i = 0; i < returnValue.Tests.Count; i++)
        //                    {
        //                        returnValue.Tests[i].Reset();

        //                        returnValue.Tests[i].ID = i;

        //                        if (string.IsNullOrEmpty(returnValue.Tests[i].Group) == false)
        //                        {
        //                            returnValue.Groups.Add(returnValue.Tests[i].Group);
        //                        }

        //                        returnValue.Tests[i].ResetLines();
        //                        returnValue.Tests[i].PropertyChanged -= OnTestPropertyChanged;
        //                        returnValue.Tests[i].PropertyChanged += OnTestPropertyChanged;

        //                        loadedTestsNames.Add(returnValue.Tests[i].Name);
        //                    }

        //                    returnValue.TestsNames = loadedTestsNames;

        //                    _testsGroups.Clear();
        //                    for (int i = 0; i < returnValue.Groups.Count; i++)
        //                    {
        //                        _testsGroups.Add(new TestsGroup());
        //                        _testsGroups[i].GroupName = Groups[i];
        //                        _testsGroups[i].GroupHeader = Groups[i];

        //                        for (int j = 0; j < returnValue.Tests.Count; j++)
        //                        {
        //                            if (string.IsNullOrEmpty(returnValue.Tests[j].Group) == false)
        //                            {
        //                                if (returnValue.Tests[j].Group.Contains(Groups[i]))
        //                                {
        //                                    _testsGroups[i].Tests.Add(returnValue.Tests[j]);
        //                                }
        //                            }
        //                        }
        //                    }
        //                }

        //                stream.Close();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        stream.Close();

        //        returnValue = null;

        //        Logger.ExceptionHandler(ex, ex.Message);
        //    }

        //    return returnValue;
        //}

        //public object Deserialize<T>(Stream serializationStream)
        //{
        //    JsonSerializer serializer = new JsonSerializer();
        //    T instance;

        //    BsonReader reader = new BsonReader(serializationStream);
        //    instance = serializer.Deserialize<T>(reader);

        //    return instance;
        //}

        //public void Serialize(Stream serializationStream, object graph)
        //{
        //    //JsonSerializer serializer = new JsonSerializer();

        //    //using (BsonWriter writer = new BsonWriter(serializationStream))
        //    //{
        //    JsonSerializer.Serialize(graph);
        //    //}
        //}

        #endregion

        #region INotifyPropertyChanged Members

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnTestPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            int executedTestCount = 0;
            int failedTestCount = 0;

            try
            {
                if (e.PropertyName == "IsExecuted" || e.PropertyName == "ExecutionResult")
                {
                    for (int i = 0; i < _tests.Count; i++)
                    {
                        executedTestCount = (_tests[i].IsExecuted == true && _tests[i].TestResult != Test.TestResultType.NotExecuted) ? executedTestCount + 1 : executedTestCount;
                        failedTestCount = (_tests[i].TestResult == Test.TestResultType.Failed) ? failedTestCount + 1 : failedTestCount;
                    }

                    ExecutedTestsCount = executedTestCount;
                    FailedTestsCount = failedTestCount;
                }
                if (e.PropertyName == "Group")
                {
                    _testsGroups.Clear();

                    for (int i = 0; i < Groups.Count; i++)
                    {
                        _testsGroups.Add(new TestsGroup());
                        _testsGroups[i].ID = i;
                        _testsGroups[i].GroupName = Groups[i];
                        _testsGroups[i].GroupHeader = Groups[i];
                    }
                    for (int i = 0; i < _tests.Count; i++)
                    {
                        for (int j = 0; j < _testsGroups.Count; j++)
                        {
                            if (string.IsNullOrEmpty(_tests[i].Group) == false)
                            {
                                if (_testsGroups[j].GroupName.Contains(_tests[i].Group))
                                {
                                    _testsGroups[j].Tests.Add(_tests[i]);
                                    break;
                                }
                            }
                            else
                            {
                                if (_testsGroups[j].GroupName.Contains("Unsorted"))
                                {
                                    _testsGroups[j].Tests.Add(_tests[i]);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        #endregion
    }
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class TestsGroup : INotifyPropertyChanged, IDisposable
    {
        #region Fields

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

        private string _groupName = string.Empty;
        public string GroupName
        {
            get
            {
                return _groupName;
            }
            set
            {
                _groupName = value;
                OnPropertyChanged();
            }
        }

        private string _groupHeader = string.Empty;
        public string GroupHeader
        {
            get
            {
                return _groupHeader;
            }
            set
            {
                _groupHeader = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<Test> _tests = new ObservableCollection<Test>();
        public ObservableCollection<Test> Tests
        {
            get
            {
                return _tests;
            }
            set
            {
                _tests = value;

                OnPropertyChanged();
            }
        }

        private int _executedTestsCount = 0;
        public int ExecutedTestsCount
        {
            get
            {
                _executedTestsCount = 0;
                for (int i = 0; i < _tests.Count; i++)
                {
                    _executedTestsCount = (_tests[i].IsExecuted) ? _executedTestsCount + 1 : _executedTestsCount;
                }

                return _executedTestsCount;
            }
            private set
            {
                _executedTestsCount = value;
                OnPropertyChanged();
            }
        }

        private bool _viewMeasurementChart = false;
        public bool ViewMeasurementChart
        {
            get
            {
                return _viewMeasurementChart;
            }
            set
            {
                _viewMeasurementChart = value;
                for (int i = 0; i < _tests.Count; i++)
                {
                    _tests[i].ViewMeasurementChart = _viewMeasurementChart;
                }

                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public TestsGroup()
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
                Logger.WriteMessage("TestsGroup:Dispose");
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        [field: NonSerialized]
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
    }
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class Test : INotifyPropertyChanged, IDisposable, ICloneable
    {
        #region Nested

        public enum TestType
        {
            Nominal,
            Evaluated
        }
        [Flags]
        public enum FailReasonType
        {
            None = 0,
            Line = 1,
            Measurements = 2
        }
        public enum TestResultType
        {
            NotExecuted,
            Failed,
            Passed
        }

        #endregion

        #region Fields

        [NonSerialized]
        private static Scope _actualScope;
        public Scope ActualScope
        {
            get
            {
                return _actualScope;
            }
            set
            {
                _actualScope = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private static SignalGenerator _actualSignalGenerator;
        public SignalGenerator ActualSignalGenerator
        {
            get
            {
                return _actualSignalGenerator;
            }
            set
            {
                _actualSignalGenerator = value;
                OnPropertyChanged();
            }
        }

        private static int _measurementsUpdateRounds = 1;
        public static int MeasurementsUpdateRounds
        {
            get
            {
                return _measurementsUpdateRounds;
            }
            set
            {
                _measurementsUpdateRounds = value;
                _measurementsUpdateRounds = (_measurementsUpdateRounds <= 0) ? 1 : _measurementsUpdateRounds;
            }
        }

        [NonSerialized]
        private ObservableCollection<Measurement> _testMeasurements = new ObservableCollection<Measurement>();
        public ObservableCollection<Measurement> TestMeasurements
        {
            get
            {
                return _testMeasurements;
            }
            set
            {
                _testMeasurements = value;

                OnPropertyChanged();
            }
        }

        #region Measurements

        private bool _viewMeasurementChart = false;
        public bool ViewMeasurementChart
        {
            get
            {
                return _viewMeasurementChart;
            }
            set
            {
                _viewMeasurementChart = value;
                OnPropertyChanged();
            }
        }

        #region Measurement 1

        [NonSerialized]
        private string _measurementName_1 = string.Empty;
        public string MeasurementName_1
        {
            get
            {
                return _measurementName_1;
            }
            set
            {
                _measurementName_1 = value;
            }
        }

        [NonSerialized]
        private string _measurementUnits_1 = string.Empty;
        public string MeasurementUnits_1
        {
            get
            {
                return _measurementUnits_1;
            }
            set
            {
                _measurementUnits_1 = value;
            }
        }

        [NonSerialized]
        private string _measurementUnitsPrefix_1 = string.Empty;
        public string MeasurementUnitsPrefix_1
        {
            get
            {
                return _measurementUnitsPrefix_1;
            }
            set
            {
                _measurementUnitsPrefix_1 = value;
            }
        }

        [NonSerialized]
        private double _measurementValue_1 = 0.0;
        public double MeasurementValue_1
        {
            get
            {
                return _measurementValue_1;
            }
            set
            {
                _measurementValue_1 = value;
                MeasurementValueStr_1 = Convert.ToString(_measurementValue_1);
            }
        }

        [NonSerialized]
        private string _measurementValueStr_1 = string.Empty;
        public string MeasurementValueStr_1
        {
            get
            {
                return _measurementValueStr_1;
            }
            set
            {
                _measurementValueStr_1 = value;
            }
        }


        [NonSerialized]
        private double _measurementDesiredValue_1 = 0.0;
        public double MeasurementDesiredValue_1
        {
            get
            {
                return _measurementDesiredValue_1;
            }
            set
            {
                _measurementDesiredValue_1 = value;
                MeasurementDesiredValueStr_1 = Convert.ToString(_measurementDesiredValue_1);
            }
        }

        [NonSerialized]
        private string _measurementDesiredValueStr_1 = string.Empty;
        public string MeasurementDesiredValueStr_1
        {
            get
            {
                return _measurementDesiredValueStr_1;
            }
            set
            {
                _measurementDesiredValueStr_1 = value;
            }
        }

        [NonSerialized]
        private double _measurementToleranceValue_1 = 0.0;
        public double MeasurementToleranceValue_1
        {
            get
            {
                return _measurementToleranceValue_1;
            }
            set
            {
                _measurementToleranceValue_1 = value;
                MeasurementToleranceValueStr_1 = Convert.ToString(_measurementToleranceValue_1);
            }
        }

        [NonSerialized]
        private string _measurementToleranceValueStr_1 = string.Empty;
        public string MeasurementToleranceValueStr_1
        {
            get
            {
                return _measurementToleranceValueStr_1;
            }
            set
            {
                _measurementToleranceValueStr_1 = value;
            }
        }

        #endregion

        #region Measurement 2

        [NonSerialized]
        private string _measurementName_2 = string.Empty;
        public string MeasurementName_2
        {
            get
            {
                return _measurementName_2;
            }
            set
            {
                _measurementName_2 = value;
            }
        }

        [NonSerialized]
        private string _measurementUnits_2 = string.Empty;
        public string MeasurementUnits_2
        {
            get
            {
                return _measurementUnits_2;
            }
            set
            {
                _measurementUnits_2 = value;
            }
        }

        [NonSerialized]
        private string _measurementUnitsPrefix_2 = string.Empty;
        public string MeasurementUnitsPrefix_2
        {
            get
            {
                return _measurementUnitsPrefix_2;
            }
            set
            {
                _measurementUnitsPrefix_2 = value;
            }
        }

        [NonSerialized]
        private double _measurementValue_2 = 0.0;
        public double MeasurementValue_2
        {
            get
            {
                return _measurementValue_2;
            }
            set
            {
                _measurementValue_2 = value;
                MeasurementValueStr_2 = Convert.ToString(_measurementValue_2);
            }
        }

        [NonSerialized]
        private string _measurementValueStr_2 = string.Empty;
        public string MeasurementValueStr_2
        {
            get
            {
                return _measurementValueStr_2;
            }
            set
            {
                _measurementValueStr_2 = value;
            }
        }

        [NonSerialized]
        private double _measurementDesiredValue_2 = 0.0;
        public double MeasurementDesiredValue_2
        {
            get
            {
                return _measurementDesiredValue_2;
            }
            set
            {
                _measurementDesiredValue_2 = value;
                MeasurementDesiredValueStr_2 = Convert.ToString(_measurementDesiredValue_2);
            }
        }

        [NonSerialized]
        private string _measurementDesiredValueStr_2 = string.Empty;
        public string MeasurementDesiredValueStr_2
        {
            get
            {
                return _measurementDesiredValueStr_2;
            }
            set
            {
                _measurementDesiredValueStr_2 = value;
            }
        }

        [NonSerialized]
        private double _measurementToleranceValue_2 = 0.0;
        public double MeasurementToleranceValue_2
        {
            get
            {
                return _measurementToleranceValue_2;
            }
            set
            {
                _measurementToleranceValue_2 = value;
                MeasurementToleranceValueStr_2 = Convert.ToString(_measurementToleranceValue_2);
            }
        }

        [NonSerialized]
        private string _measurementToleranceValueStr_2 = string.Empty;
        public string MeasurementToleranceValueStr_2
        {
            get
            {
                return _measurementToleranceValueStr_2;
            }
            set
            {
                _measurementToleranceValueStr_2 = value;
            }
        }

        #endregion

        #region Measurement 3

        [NonSerialized]
        private string _measurementName_3 = string.Empty;
        public string MeasurementName_3
        {
            get
            {
                return _measurementName_3;
            }
            set
            {
                _measurementName_3 = value;
            }
        }

        [NonSerialized]
        private string _measurementUnits_3 = string.Empty;
        public string MeasurementUnits_3
        {
            get
            {
                return _measurementUnits_3;
            }
            set
            {
                _measurementUnits_3 = value;
            }
        }

        [NonSerialized]
        private string _measurementUnitsPrefix_3 = string.Empty;
        public string MeasurementUnitsPrefix_3
        {
            get
            {
                return _measurementUnitsPrefix_3;
            }
            set
            {
                _measurementUnitsPrefix_3 = value;
            }
        }

        [NonSerialized]
        private double _measurementValue_3 = 0.0;
        public double MeasurementValue_3
        {
            get
            {
                return _measurementValue_3;
            }
            set
            {
                _measurementValue_3 = value;
                MeasurementValueStr_3 = Convert.ToString(_measurementValue_3);
            }
        }

        [NonSerialized]
        private string _measurementValueStr_3 = string.Empty;
        public string MeasurementValueStr_3
        {
            get
            {
                return _measurementValueStr_3;
            }
            set
            {
                _measurementValueStr_3 = value;
            }
        }

        [NonSerialized]
        private double _measurementDesiredValue_3 = 0.0;
        public double MeasurementDesiredValue_3
        {
            get
            {
                return _measurementDesiredValue_3;
            }
            set
            {
                _measurementDesiredValue_3 = value;
                MeasurementDesiredValueStr_3 = Convert.ToString(_measurementDesiredValue_3);
            }
        }

        [NonSerialized]
        private string _measurementDesiredValueStr_3 = string.Empty;
        public string MeasurementDesiredValueStr_3
        {
            get
            {
                return _measurementDesiredValueStr_3;
            }
            set
            {
                _measurementDesiredValueStr_3 = value;
            }
        }

        [NonSerialized]
        private double _measurementToleranceValue_3 = 0.0;
        public double MeasurementToleranceValue_3
        {
            get
            {
                return _measurementToleranceValue_3;
            }
            set
            {
                _measurementToleranceValue_3 = value;
                MeasurementToleranceValueStr_3 = Convert.ToString(_measurementToleranceValue_3);
            }
        }

        [NonSerialized]
        private string _measurementToleranceValueStr_3 = string.Empty;
        public string MeasurementToleranceValueStr_3
        {
            get
            {
                return _measurementToleranceValueStr_3;
            }
            set
            {
                _measurementToleranceValueStr_3 = value;
            }
        }

        #endregion

        #region Measurement 4

        [NonSerialized]
        private string _measurementName_4 = string.Empty;
        public string MeasurementName_4
        {
            get
            {
                return _measurementName_4;
            }
            set
            {
                _measurementName_4 = value;
            }
        }

        [NonSerialized]
        private string _measurementUnits_4 = string.Empty;
        public string MeasurementUnits_4
        {
            get
            {
                return _measurementUnits_4;
            }
            set
            {
                _measurementUnits_4 = value;
            }
        }


        [NonSerialized]
        private string _measurementUnitsPrefix_4 = string.Empty;
        public string MeasurementUnitsPrefix_4
        {
            get
            {
                return _measurementUnitsPrefix_4;
            }
            set
            {
                _measurementUnitsPrefix_4 = value;
            }
        }

        [NonSerialized]
        private double _measurementValue_4 = 0.0;
        public double MeasurementValue_4
        {
            get
            {
                return _measurementValue_4;
            }
            set
            {
                _measurementValue_4 = value;
                MeasurementValueStr_4 = Convert.ToString(_measurementValue_4);
            }
        }

        [NonSerialized]
        private string _measurementValueStr_4 = string.Empty;
        public string MeasurementValueStr_4
        {
            get
            {
                return _measurementValueStr_4;
            }
            set
            {
                _measurementValueStr_4 = value;
            }
        }

        [NonSerialized]
        private double _measurementDesiredValue_4 = 0.0;
        public double MeasurementDesiredValue_4
        {
            get
            {
                return _measurementDesiredValue_4;
            }
            set
            {
                _measurementDesiredValue_4 = value;
                MeasurementDesiredValueStr_4 = Convert.ToString(_measurementDesiredValue_4);
            }
        }

        [NonSerialized]
        private string _measurementDesiredValueStr_4 = string.Empty;
        public string MeasurementDesiredValueStr_4
        {
            get
            {
                return _measurementDesiredValueStr_4;
            }
            set
            {
                _measurementDesiredValueStr_4 = value;
            }
        }

        [NonSerialized]
        private double _measurementToleranceValue_4 = 0.0;
        public double MeasurementToleranceValue_4
        {
            get
            {
                return _measurementToleranceValue_4;
            }
            set
            {
                _measurementToleranceValue_4 = value;
                MeasurementToleranceValueStr_4 = Convert.ToString(_measurementToleranceValue_4);
            }
        }

        [NonSerialized]
        private string _measurementToleranceValueStr_4 = string.Empty;
        public string MeasurementToleranceValueStr_4
        {
            get
            {
                return _measurementToleranceValueStr_4;
            }
            set
            {
                _measurementToleranceValueStr_4 = value;
            }
        }

        #endregion

        #endregion

        private ObservableCollection<TestLine> _testLines = new ObservableCollection<TestLine>();
        public ObservableCollection<TestLine> TestLines
        {
            get
            {
                return _testLines;
            }
            set
            {
                _testLines = value;

                ResetLines();

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
                _name = value;

                OnPropertyChanged();
            }
        }

        private TestType _type = TestType.Evaluated;
        public TestType Type
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
        public Array TestTypeArray
        {
            get
            {
                return Enum.GetValues(typeof(TestType));
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get
            {
                return _description;
            }
            set
            {
                _description = value;
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

        private string _group = string.Empty;
        public string Group
        {
            get
            {
                return _group;
            }
            set
            {
                _group = value;
                OnPropertyChanged();
            }
        }

        private int _linesCount;
        public int LinesCount
        {
            get
            {
                _linesCount = _testLines.Count;

                return _linesCount;
            }
            set
            {
                _linesCount = value;
                OnPropertyChanged();
            }
        }

        private int _minLinesCount;
        public int MinLinesCount
        {
            get
            {
                return _minLinesCount;
            }
            set
            {
                _minLinesCount = value;
                OnPropertyChanged();
            }
        }

        private int _maxLinesCount;
        public int MaxLinesCount
        {
            get
            {
                _maxLinesCount = _testLines.Count - 1;
                return _maxLinesCount;
            }
            set
            {
                _maxLinesCount = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private int _executedLinesCount = 0;
        public int ExecutedLinesCount
        {
            get
            {
                return _executedLinesCount;
            }
            set
            {
                _executedLinesCount = (value >= 0) ? value : 0;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private int _skipedLinesCount = 0;
        public int SkipedLinesCount
        {
            get
            {
                return _skipedLinesCount;
            }
            set
            {
                _skipedLinesCount = (value >= 0) ? value : 0;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private int _failedLinesCount = 0;
        public int FailedLinesCount
        {
            get
            {
                return _failedLinesCount;
            }
            set
            {
                _failedLinesCount = (value >= 0) ? value : 0;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private TestLine _actualExecutedLine;
        public TestLine ActualExecutedLine
        {
            get
            {
                return _actualExecutedLine;
            }
            set
            {
                _actualExecutedLine = value;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private long _executionTime;
        public long ExecutionTime
        {
            get
            {
                return _executionTime;
            }
            set
            {
                _executionTime = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private bool _isExecuted;
        public bool IsExecuted
        {
            get
            {
                return _isExecuted;
            }
            set
            {
                _isExecuted = value;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private static bool _isPaused = false;
        public static bool IsPaused
        {
            get
            {
                return _isPaused;
            }
            set
            {
                _isPaused = value;
                //OnPropertyChanged();
            }
        }

        private bool _isExecute;
        public bool IsExecute
        {
            get
            {
                return _isExecute;
            }
            set
            {
                _isExecute = value;

                OnPropertyChanged();
            }
        }

        private bool _isStopOnLineFailed = false;
        public bool IsStopOnLineFailed
        {
            get
            {
                return _isStopOnLineFailed;
            }
            set
            {
                _isStopOnLineFailed = value;
                OnPropertyChanged();
            }
        }

        private bool _isStopOnComplete = false;
        public bool IsStopOnComplete
        {
            get
            {
                return _isStopOnComplete;
            }
            set
            {
                _isStopOnComplete = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private bool _isDebug;
        public bool IsDebug
        {
            get
            {
                return _isDebug;
            }
            set
            {
                _isDebug = value;

                OnPropertyChanged();
            }
        }

        private bool _isEdit;
        public bool IsEdit
        {
            get
            {
                return _isEdit;
            }
            set
            {
                _isEdit = value;

                OnPropertyChanged();
            }
        }

        private bool _viewAllLines = false;
        public bool ViewAllLines
        {
            get
            {
                return _viewAllLines;
            }
            set
            {
                _viewAllLines = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private string _debugMessage = string.Empty;
        public string DebugMessage
        {
            get
            {
                return _debugMessage;
            }
            set
            {
                _debugMessage = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private TestResultType _testResult = TestResultType.NotExecuted;
        public TestResultType TestResult
        {
            get
            {
                return _testResult;
            }
            set
            {
                _testResult = value;

                TestResultString = _testResult.ToString();

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private string _testResultString = string.Empty;
        public string TestResultString
        {
            get
            {
                return _testResultString;
            }
            set
            {
                _testResultString = value;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private FailReasonType _failReason;
        public FailReasonType FailReason
        {
            get
            {
                return _failReason;
            }
            set
            {
                _failReason = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private bool _isRunning = false;
        public bool IsRunning
        {
            get
            {
                return _isRunning;
            }
            set
            {
                _isRunning = value;

                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// 
        /// </summary>
        public Test()
        {
            try
            {
                _name = string.Empty;
                _isExecuted = false;
                _testLines.Clear();
                _testResult = TestResultType.NotExecuted;
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
        public bool AddLine()
        {
            bool returnValue = false;

            ObservableCollection<TestLine> actualTestLines = null;

            try
            {
                actualTestLines = TestLines;

                actualTestLines.Add(new TestLine() { DeviceType = TestLine.LineDeviceType.Scope });

                TestLines = actualTestLines;

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
        /// <param name="lineToDelete"></param>
        /// <returns></returns>
        public bool DeleteLine(TestLine lineToDelete)
        {
            bool returnValue = false;

            ObservableCollection<TestLine> actualTestLines = null;

            try
            {
                actualTestLines = TestLines;

                actualTestLines.Remove(lineToDelete);

                TestLines = actualTestLines;

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
        /// <param name="index"></param>
        /// <returns></returns>
        public bool InsertLine(int index)
        {
            bool returnValue = false;

            ObservableCollection<TestLine> actualTestLines = null;
            TestLine lineToInsert = null;

            try
            {
                actualTestLines = TestLines;

                lineToInsert = new TestLine();
                actualTestLines.Insert(index, lineToInsert);

                TestLines = actualTestLines;

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
        /// <returns></returns>
        public bool Reset()
        {
            bool returnValue = false;

            try
            {
                TestResult = Test.TestResultType.NotExecuted;
                FailReason = Test.FailReasonType.None;

                ExecutionTime = 0;
                ExecutedLinesCount = 0;
                FailedLinesCount = 0;
                IsExecuted = false;
                DebugMessage = string.Empty;

                IsRunning = false;

                for (int i = 0; i < TestLines.Count; i++)
                {
                    TestLines[i].Reset();
                }

                if (TestLines.Count > 0)
                {
                    ActualExecutedLine = TestLines[0];
                }

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
        /// <returns></returns>
        public bool ResetLines()
        {
            bool returnValue = false;

            try
            {
                if (_testLines != null)
                {
                    for (int i = 0; i < _testLines.Count; i++)
                    {
                        _testLines[i].ID = i;

                        _testLines[i].Reset();
                        _testLines[i].PropertyChanged -= OnLinePropertyChanged;
                        _testLines[i].PropertyChanged += OnLinePropertyChanged;
                    }

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
        /// <returns></returns>
        public object Clone()
        {
            Test clonedTest = null;

            try
            {
                //shallow copy
                clonedTest = (Test)this.MemberwiseClone();

                //deep copy
                ObservableCollection<TestLine> actualTestLines = new ObservableCollection<TestLine>();

                for (int i = 0; i < _testLines.Count; i++)
                {
                    actualTestLines.Add((TestLine)_testLines[i].Clone());
                }

                clonedTest.TestMeasurements = new ObservableCollection<Measurement>();
                clonedTest.TestLines = actualTestLines;
                clonedTest._actualExecutedLine = null;
                clonedTest.Group = string.Copy(this.Group);
                clonedTest.IsExecuted = false;
                clonedTest.TestResult = TestResultType.NotExecuted;
                clonedTest.ExecutionTime = 0;

                //BUG:Vlad:problem with two or more identical names
                clonedTest.Name = string.Empty;// string.Copy(Name) + "_copy";
            }
            catch (Exception ex)
            {
                clonedTest = null;
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return clonedTest;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool Evaluate()
        {
            bool returnValue = false;

            int measurementsValueStateIntegrator = 0;

            try
            {
                _testMeasurements = new ObservableCollection<Measurement>();

                if (ActualScope != null)
                {
                    if (ActualScope.MeasurementsIndexes.Count > 0)
                    {
                        WaitForUpdateMeasurements(_measurementsUpdateRounds);

                        for (int i = 0; i < ActualScope.MeasurementsIndexes.Count; i++)
                        {
                            Measurement measurement = ActualScope.Measurements[ActualScope.MeasurementsIndexes[i] - 1].Clone();
                            measurementsValueStateIntegrator = (measurement.ValueState == true) ? measurementsValueStateIntegrator + 1 : measurementsValueStateIntegrator;

                            _testMeasurements.Add(measurement);
                        }

                        if (_testMeasurements.Count > 0)
                        {
                            MeasurementName_1 = _testMeasurements[0].Name;
                            MeasurementUnits_1 = _testMeasurements[0].Units;
                            MeasurementUnitsPrefix_1 = _testMeasurements[0].UnitsPrefix;
                            MeasurementValue_1 = _testMeasurements[0].ActualValue;
                            MeasurementDesiredValue_1 = _testMeasurements[0].DesiredValue;
                            MeasurementToleranceValue_1 = _testMeasurements[0].ThresholdValue;
                        }

                        if (_testMeasurements.Count > 1)
                        {
                            MeasurementName_2 = _testMeasurements[1].Name;
                            MeasurementUnits_2 = _testMeasurements[1].Units;
                            MeasurementUnitsPrefix_2 = _testMeasurements[1].UnitsPrefix;
                            MeasurementValue_2 = _testMeasurements[1].ActualValue;
                            MeasurementDesiredValue_2 = _testMeasurements[1].DesiredValue;
                            MeasurementToleranceValue_2 = _testMeasurements[1].ThresholdValue;
                        }

                        if (_testMeasurements.Count > 2)
                        {
                            MeasurementName_3 = _testMeasurements[2].Name;
                            MeasurementUnits_3 = _testMeasurements[2].Units;
                            MeasurementUnitsPrefix_3 = _testMeasurements[2].UnitsPrefix;
                            MeasurementValue_3 = _testMeasurements[2].ActualValue;
                            MeasurementDesiredValue_3 = _testMeasurements[2].DesiredValue;
                            MeasurementToleranceValue_3 = _testMeasurements[2].ThresholdValue;
                        }

                        if (_testMeasurements.Count > 3)
                        {
                            MeasurementName_4 = _testMeasurements[3].Name;
                            MeasurementUnits_4 = _testMeasurements[3].Units;
                            MeasurementUnitsPrefix_4 = _testMeasurements[3].UnitsPrefix;
                            MeasurementValue_4 = _testMeasurements[3].ActualValue;
                            MeasurementDesiredValue_4 = _testMeasurements[3].DesiredValue;
                            MeasurementToleranceValue_4 = _testMeasurements[3].ThresholdValue;
                        }

                        //TODO:Vlad:implement just indicated test
                        if (Type == TestType.Evaluated)
                        {
                            TestResult = ((measurementsValueStateIntegrator >= ActualScope.MeasurementsIndexes.Count) && (FailedLinesCount == 0)) ? Test.TestResultType.Passed : Test.TestResultType.Failed;

                            if (measurementsValueStateIntegrator < ActualScope.MeasurementsIndexes.Count)
                            {
                                FailReason |= Test.FailReasonType.Measurements;
                            }
                            if (FailedLinesCount > 0)
                            {
                                FailReason |= Test.FailReasonType.Line;
                            }
                        }
                        else
                        {
                            TestResult = Test.TestResultType.Passed;
                            FailReason = Test.FailReasonType.None;
                        }
                    }
                    else
                    {
                        TestResult = (FailedLinesCount == 0) ? Test.TestResultType.Passed : Test.TestResultType.Failed;

                        if (FailedLinesCount > 0)
                        {
                            FailReason |= Test.FailReasonType.Line;
                        }
                    }
                }

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
        public void WaitForUpdateMeasurements(int rounds)
        {
            uint waitUpdateMeasurementsData = 0;

            try
            {
                for (int i = 0; i < rounds; i++)
                {
                    while (ActualScope.DataMeasurementsUpdated == false)
                    {
                        waitUpdateMeasurementsData++;
                        if (waitUpdateMeasurementsData > 100)//wait 1 sec
                        {
                            break;
                        }

                        Thread.Sleep(10);
                    }

                    waitUpdateMeasurementsData = 0;

                    while (ActualScope.DataMeasurementsUpdated == true)
                    {
                        waitUpdateMeasurementsData++;
                        if (waitUpdateMeasurementsData > 100)//wait 1 sec
                        {
                            break;
                        }

                        Thread.Sleep(10);
                    }

                    waitUpdateMeasurementsData = 0;

                    while (ActualScope.DataMeasurementsUpdated == false)
                    {
                        waitUpdateMeasurementsData++;
                        if (waitUpdateMeasurementsData > 100)//wait 1 sec
                        {
                            break;
                        }

                        Thread.Sleep(10);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            int executedLinesCount = 0;
            int skipedLinesCount = 0;
            int failedLinesCount = 0;

            try
            {
                if (e.PropertyName == "Executed" || e.PropertyName == "ExecutionResult" || e.PropertyName == "Skip")
                {
                    for (int i = 0; i < _testLines.Count; i++)
                    {
                        executedLinesCount = (_testLines[i].Executed == true) ? executedLinesCount + 1 : executedLinesCount;
                        skipedLinesCount = (_testLines[i].Skip == true) ? skipedLinesCount + 1 : skipedLinesCount;
                        failedLinesCount = (_testLines[i].ExecutionResult == TestLine.LineExecutionResult.Failed) ? failedLinesCount + 1 : failedLinesCount;
                    }

                    ExecutedLinesCount = executedLinesCount;
                    SkipedLinesCount = skipedLinesCount;
                    FailedLinesCount = failedLinesCount;
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        #endregion
    }
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class TestLine : INotifyPropertyChanged, IDataErrorInfo, ICloneable
    {
        #region Constants

        private const string LINE_CONTENT_EMPTY_ERROR = "Invalid line content";
        private const string LINE_EXECUTION_FAILED = "Line execution failed";

        #endregion

        #region Nested

        public enum LineStatus
        {
            Executed,
            NotExecuted
        }
        public enum LineExecutionResult
        {
            None,
            Ok,
            Failed
        }
        public enum LineType
        {
            Command,
            Query,
            Text,
            Comment,
            Notification
        }
        public enum LineDeviceType
        {
            Manual,
            Instrument,
            Scope,
            SignalGenerator
        }
        public enum LineExecutionType
        {
            Manual,
            Auto
        }

        #endregion

        #region Fields

        private string _lineContent = string.Empty;
        public string LineContent
        {
            get
            {
                return _lineContent;
            }
            set
            {
                PreviousLineContent = _lineContent;

                if (string.IsNullOrEmpty(value) == true)
                {
                    AddError("LineContent", LINE_CONTENT_EMPTY_ERROR, false);
                }
                else
                {
                    RemoveError("LineContent", LINE_CONTENT_EMPTY_ERROR);

                    Type = (value.Contains("//")) ? LineType.Comment : Type;
                    _lineContent = value;

                    OnPropertyChanged();
                }
            }
        }

        [NonSerialized]
        private string _previousLineContent = string.Empty;
        public string PreviousLineContent
        {
            get
            {
                return _previousLineContent;
            }
            set
            {
                if (_previousLineContent != value)
                {
                    _previousLineContent = value;
                    OnPropertyChanged();
                }
            }
        }

        [NonSerialized]
        private LineType _type;
        public LineType Type
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

        [NonSerialized]
        private LineStatus _status;
        public LineStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private LineExecutionResult _executionResult;
        public LineExecutionResult ExecutionResult
        {
            get
            {
                return _executionResult;
            }
            set
            {
                if (value == LineExecutionResult.Failed)
                {
                    AddError("ExecutionResult", LINE_EXECUTION_FAILED, false);
                }
                if (value == LineExecutionResult.None || value == LineExecutionResult.Ok)
                {
                    if (_executionResult == LineExecutionResult.Failed)
                    {
                        RemoveError("ExecutionResult", LINE_EXECUTION_FAILED);
                    }
                }

                _executionResult = value;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private LineExecutionType _executionType;
        public LineExecutionType ExecutionType
        {
            get
            {
                return _executionType;
            }
            set
            {
                _executionType = value;
                OnPropertyChanged();
            }
        }

        private LineDeviceType _deviceType = LineDeviceType.Manual;
        public LineDeviceType DeviceType
        {
            get
            {
                return _deviceType;
            }
            set
            {
                _deviceType = value;
                OnPropertyChanged();
            }
        }
        public Array DeviceTypeArray
        {
            get
            {
                return Enum.GetValues(typeof(LineDeviceType));
            }
        }

        private int _delayAfterExecution = 1;
        public int DelayAfterExecution
        {
            get
            {
                return _delayAfterExecution;
            }
            set
            {
                _delayAfterExecution = (value >= 0) ? value : 0;
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

        [NonSerialized]
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
                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private bool _executed;
        public bool Executed
        {
            get
            {
                return _executed;
            }
            set
            {
                _executed = value;

                Status = (_executed == true) ? LineStatus.Executed : LineStatus.NotExecuted;

                OnPropertyChanged();
            }
        }

        private bool _skip = false;
        public bool Skip
        {
            get
            {
                return _skip;
            }
            set
            {
                _skip = value;

                OnPropertyChanged();
            }
        }

        [NonSerialized]
        private long _executionTime;
        public long ExecutionTime
        {
            get
            {
                return _executionTime;
            }
            set
            {
                _executionTime = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public TestLine()
        {
            try
            {
                _type = LineType.Comment;
                _status = LineStatus.NotExecuted;
                _visible = true;
                _executed = false;
                _lineContent = string.Empty;
                _executionType = LineExecutionType.Manual;
                _executionResult = LineExecutionResult.None;
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
        public object Clone()
        {
            TestLine clonedTestLine = null;

            try
            {
                //shallow copy
                clonedTestLine = (TestLine)this.MemberwiseClone();

                //deep copy
                clonedTestLine.Executed = false;
                clonedTestLine.Skip = this.Skip;
                clonedTestLine.ExecutionResult = TestLine.LineExecutionResult.None;
                clonedTestLine.ExecutionTime = 0;
                clonedTestLine.PropertyChanged = null;

                clonedTestLine.LineContent = string.Copy(LineContent);

                clonedTestLine.Errors = new Dictionary<string, List<string>>();
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return clonedTestLine;
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
                Executed = false;
                ExecutionResult = TestLine.LineExecutionResult.None;
                ExecutionTime = 0;

                Errors?.Clear();

                returnValue = true;
            }
            catch (Exception ex)
            {
                returnValue = false;
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
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

        [NonSerialized]
        private Dictionary<String, List<String>> _errors = new Dictionary<string, List<string>>();
        public Dictionary<String, List<String>> Errors
        {
            get
            {
                return _errors;
            }
            set
            {
                _errors = value;

                OnPropertyChanged();
            }
        }

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

                if (propertyName.ToLower().Contains("linecontent"))
                {
                    OnPropertyChanged("LineContent");
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

                if (propertyName.ToLower().Contains("linecontent"))
                {
                    OnPropertyChanged("LineContent");
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
