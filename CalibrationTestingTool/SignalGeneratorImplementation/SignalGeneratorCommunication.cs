using CalibrationToolTester.GlobalLoger;
using CalibrationToolTester.SignalGeneratorImplementation.Connection;
using System;

namespace CalibrationToolTester.SignalGeneratorImplementation
{
    public class SignalGeneratorCommunication
    {
        private EthernetConnection ethernetConnection = null;

        public SignalGeneratorCommunication(SignalGeneratorGlobal.Communication communicationType)
        {
            try
            {
                if (communicationType == SignalGeneratorGlobal.Communication.Ethernet)
                {
                    ethernetConnection = new EthernetConnection();
                    ethernetConnection.IPAddress = "192.168.17.1";
                    ethernetConnection.Port = 3000;
                }
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        ~SignalGeneratorCommunication()
        {
        }
    }
}