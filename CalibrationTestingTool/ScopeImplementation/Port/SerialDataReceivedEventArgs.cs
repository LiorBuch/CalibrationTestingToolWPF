using System;

namespace CalibrationToolTester.ScopeImplementation.Port
{
    public class SerialDataReceivedEventArgs : EventArgs
    {
        public int BytesToRead { get; }

        public SerialDataReceivedEventArgs(int bytesToRead)
        {
            BytesToRead = bytesToRead;
        }
    }
}