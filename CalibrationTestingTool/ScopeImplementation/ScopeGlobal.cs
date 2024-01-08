namespace CalibrationToolTester.ScopeImplementation
{
    public static class ScopeGlobal
    {
        public enum Communication
        {
            Ethernet,
            USB,
            Serial,
            GPIB
        }

        public enum CommandGroups
        {
            AcquisitionCommands,
            AliasCommands,
            ApplicationMenuCommands,
            CalibrationDiagnosticCommands,
            CursorCommands,
            DisplayCommands,
            FileSystemCommands,
            HardcopyCommands,
            HistogramCommands,
            HorizontalCommands,
            LimitTestCommands,
            MaskCommands,
            MeasurementCommands,
            MiscellaneousCommands,
            RS232Commands,
            SaveRecallCommands,
            StatusErrorCommands,
            TriggerCommands,
            VerticalCommands,
            WaveformCommands,
            ZoomCommands
        }
    }
}