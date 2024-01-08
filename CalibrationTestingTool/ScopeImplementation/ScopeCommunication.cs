using CalibrationToolTester.GlobalLoger;
using CalibrationToolTester.ScopeImplementation.Connection;
using System;

namespace CalibrationToolTester.ScopeImplementation
{
    public class ScopeCommunication
    {
        private EthernetConnection ethernetConnection = null;

        public ScopeCommunication(ScopeGlobal.Communication communicationType)
        {
            try
            {
                if (communicationType == ScopeGlobal.Communication.Ethernet)
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

        ~ScopeCommunication()
        {
        }
    }
}