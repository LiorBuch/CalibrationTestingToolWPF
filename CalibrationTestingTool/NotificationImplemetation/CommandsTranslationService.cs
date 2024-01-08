using CalibrationToolTester.GlobalLoger;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.XPath;

namespace CalibrationToolTester.NotificationImplementation
{
    /// <summary>
    /// 
    /// </summary>
    public interface ICommandTranslationService
    {
        bool DoesTranslationExist(string resourceKey);
        string Translate(string resourceKey);
        void LoadTranslations(string resourceFileName);
        void SaveTranslations(string resourceFileName);
    }
    /// <summary>
    /// 
    /// </summary>
    public class CommandTranslationService : ICommandTranslationService
    {
        #region Fields

        private readonly Dictionary<string, string> resourceDictionary = new Dictionary<string, string>();

        private string _resourceFileName;
        public string ResourceFileName
        {
            get
            {
                return _resourceFileName;
            }
            set
            {
                _resourceFileName = value;
            }
        }

        private List<string> _validCommands = new List<string>();
        public List<string> ValidCommands
        {
            get
            {
                return _validCommands;
            }
            set
            {
                _validCommands = value;
            }
        }

        private List<string> _validUserCommands = new List<string>();
        public List<string> ValidUserCommands
        {
            get
            {
                return _validUserCommands;
            }
            set
            {
                _validUserCommands = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resourceKey"></param>
        /// <returns></returns>
        public bool DoesTranslationExist(string resourceKey)
        {
            bool returnValue = true;

            try
            {
                returnValue = resourceDictionary.ContainsKey(resourceKey);
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
        /// <param name="resourceKey"></param>
        /// <returns></returns>
        public string Translate(string resourceKey)
        {
            string returnValue = string.Empty;

            try
            {
                if (resourceDictionary.TryGetValue(resourceKey, out returnValue) == false)
                {
                    returnValue = resourceKey;
                }
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
        /// <param name="resourceFileName"></param>
        public void LoadTranslations(string resourceFileName)
        {
            try
            {
                ResourceFileName = resourceFileName;

                foreach (XElement xpathSelectElement in LoadResourceFile(resourceFileName).XPathSelectElements("./Dictionary/Value"))
                {
                    XAttribute xattribute1 = xpathSelectElement.Attribute((XName)"userCommand");
                    XAttribute xattribute2 = xpathSelectElement.Attribute((XName)"deviceCommand");

                    if (!string.IsNullOrEmpty(xattribute1?.Value))
                    {
                        if (xattribute2 != null)
                        {
                            if (xattribute2.Value != null)
                            {
                                try
                                {
                                    resourceDictionary.Add(xattribute1.Value, xattribute2.Value);
                                    ValidUserCommands.Add(string.Copy(xattribute1.Value));
                                    ValidCommands.Add(string.Copy(xattribute2.Value));
                                    continue;
                                }
                                catch (ArgumentException ex)
                                {
                                    continue;
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
        /// <summary>
        /// 
        /// </summary>
        public void SaveTranslations(string resourceFileName)
        {
            SaveResourceFile(resourceFileName);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userCommand"></param>
        /// <param name="deviceCommand"></param>
        public void AddCommandTranslation(string userCommand, string deviceCommand)
        {
            try
            {
                resourceDictionary.Add(userCommand, deviceCommand);
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="resourceFileName"></param>
        /// <returns></returns>
        private XDocument LoadResourceFile(string resourceFileName)
        {
            XDocument xdocument = (XDocument)null;

            try
            {
                xdocument = XDocument.Load(resourceFileName);
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return xdocument;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="resourceFileName"></param>
        /// <returns></returns>
        private XDocument SaveResourceFile(string resourceFileName)
        {
            XDocument xdocument = (XDocument)null;

            try
            {
                List<string> keys = new List<string>(resourceDictionary.Keys);
                List<string> values = new List<string>(resourceDictionary.Values);

                for (int i = 0; i < resourceDictionary.Count; i++)
                {
                    XElement xElement = new XElement("Value");

                    xElement.Add(new XElement("userCommand", keys[0]));
                    xElement.Add(new XElement("deviceCommand", values[0]));

                    xdocument.Element("Dictionary").Add(xElement);
                }

                xdocument.Save(resourceFileName);
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return xdocument;
        }

        #endregion
    }
}
