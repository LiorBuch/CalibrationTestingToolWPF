using CalibrationToolTester.GlobalLoger;
using System;
using System.Net.Sockets;

namespace CalibrationToolTester.SignalGeneratorImplementation.Connection
{
    public class EthernetConnection : UdpClient
    {
        private string _ipAddress;

        public string IPAddress
        {
            get
            {
                return _ipAddress;
            }
            set
            {
                _ipAddress = value;
            }
        }

        private int _port;

        public int Port
        {
            get
            {
                return _port;
            }
            set
            {
                _port = value;
            }
        }

        public EthernetConnection()
        {
            try
            {
                Client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }
        }

        public bool Connect()
        {
            bool returnValue = false;

            try
            {
                Client.Connect(System.Net.IPAddress.Parse(_ipAddress), _port);

                returnValue = (Client.Connected);
            }
            catch (Exception ex)
            {
                Logger.ExceptionHandler(ex, ex.Message);
            }

            return returnValue;
        }
    }
}