using LibreHardwareMonitor.Hardware;

namespace Lidless;

internal sealed class ThermalMonitor : IDisposable
{
    private Computer? computer;
    private bool unavailable;

    internal float? ReadCpuCelsius()
    {
        if (unavailable) return null;
        try
        {
            computer ??= Open();
            float? hottest = null;
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu) continue;
                hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Temperature || sensor.Value is not { } value ||
                        value < 15 || value > 125) continue;
                    hottest = hottest is null ? value : Math.Max(hottest.Value, value);
                }
            }
            return hottest;
        }
        catch
        {
            unavailable = true;
            computer?.Close();
            computer = null;
            return null;
        }
    }

    private static Computer Open()
    {
        var computer = new Computer { IsCpuEnabled = true };
        computer.Open();
        return computer;
    }

    public void Dispose() => computer?.Close();
}
