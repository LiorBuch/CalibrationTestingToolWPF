using System;

namespace CalibrationToolTester.SignalGeneratorImplementation.Port
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