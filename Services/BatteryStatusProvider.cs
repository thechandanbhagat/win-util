using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using WinUtil.Models;

namespace WinUtil.Services;

internal interface IBatteryStatusProvider
{
    Task<BatterySnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}

internal sealed class BatteryStatusProvider : IBatteryStatusProvider
{
    private const byte NoBattery = 128;
    private const byte UnknownBatteryPercentage = byte.MaxValue;
    private static readonly TimeSpan BluetoothQueryTimeout = TimeSpan.FromSeconds(20);
    private const string BluetoothBatteryCommand = """
        $ErrorActionPreference = 'Stop'
        Add-Type -AssemblyName System.Runtime.WindowsRuntime
        [Windows.Devices.Bluetooth.BluetoothLEDevice,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null
        [Windows.Devices.Bluetooth.BluetoothDevice,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null
        [Windows.Devices.Bluetooth.BluetoothConnectionStatus,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null

        $asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
            $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
        })[0]

        function Await($WinRtTask, $ResultType) {
            $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
            $netTask = $asTask.Invoke($null, @($WinRtTask))
            $netTask.Wait(-1) | Out-Null
            $netTask.Result
        }

        function Test-BluetoothDeviceConnected([string]$deviceAddressHex, [bool]$isLowEnergy) {
            try {
                $address = [Convert]::ToUInt64($deviceAddressHex, 16)
                $device = if ($isLowEnergy) {
                    Await ([Windows.Devices.Bluetooth.BluetoothLEDevice]::FromBluetoothAddressAsync($address)) ([Windows.Devices.Bluetooth.BluetoothLEDevice])
                } else {
                    Await ([Windows.Devices.Bluetooth.BluetoothDevice]::FromBluetoothAddressAsync($address)) ([Windows.Devices.Bluetooth.BluetoothDevice])
                }

                if ($null -eq $device) {
                    return $true
                }

                try {
                    return $device.ConnectionStatus -eq [Windows.Devices.Bluetooth.BluetoothConnectionStatus]::Connected
                }
                finally {
                    $device.Dispose()
                }
            }
            catch {
                return $true
            }
        }

        $devices = Get-PnpDevice -Class Bluetooth -PresentOnly | Where-Object {
            $_.FriendlyName -and ($_.InstanceId -like 'BTHLE\DEV_*' -or $_.InstanceId -like 'BTHENUM\DEV_*')
        } | ForEach-Object {
            $battery = Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName 'DEVPKEY_Device_BatteryLevel' -ErrorAction SilentlyContinue
            if ($null -ne $battery.Data) {
                $percentage = [int]$battery.Data
                if ($percentage -ge 0 -and $percentage -le 100) {
                    $isLowEnergy = $_.InstanceId -like 'BTHLE\DEV_*'
                    $addressProperty = Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName 'DEVPKEY_Bluetooth_DeviceAddress' -ErrorAction SilentlyContinue
                    $isConnected = if ($addressProperty.Data) {
                        Test-BluetoothDeviceConnected -deviceAddressHex $addressProperty.Data -isLowEnergy $isLowEnergy
                    } else {
                        $true
                    }

                    if ($isConnected) {
                        [PSCustomObject]@{ Name = $_.FriendlyName; Percentage = $percentage }
                    }
                }
            }
        }
        $jsonDevices = @($devices | ForEach-Object { $_ | ConvertTo-Json -Compress })
        [System.Console]::Out.Write("[$($jsonDevices -join ',')]")
        """;

    Task<BatterySnapshot> IBatteryStatusProvider.GetSnapshotAsync(CancellationToken cancellationToken) => GetSnapshotAsync(cancellationToken);

    private async Task<BatterySnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var bluetoothResult = await GetBluetoothBatteriesAsync(cancellationToken);
        var systemBattery = GetSystemBattery();
        var devices = systemBattery is null
            ? bluetoothResult.Devices
            : new[] { systemBattery }.Concat(bluetoothResult.Devices).ToArray();

        return new BatterySnapshot(devices, bluetoothResult.ErrorMessage);
    }

    private static BatteryDevice? GetSystemBattery()
    {
        if (!GetSystemPowerStatus(out var powerStatus)
            || powerStatus.BatteryFlag == NoBattery
            || powerStatus.BatteryLifePercent == UnknownBatteryPercentage)
        {
            return null;
        }

        return new BatteryDevice("This device", powerStatus.BatteryLifePercent);
    }

    private static async Task<BluetoothBatteryQueryResult> GetBluetoothBatteriesAsync(CancellationToken cancellationToken)
    {
        using var timeoutCancellation = new CancellationTokenSource(BluetoothQueryTimeout);
        using var queryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
        using var process = new Process
        {
            StartInfo = CreateBluetoothQueryStartInfo()
        };

        try
        {
            if (!process.Start())
            {
                return BluetoothBatteryQueryResult.Failed;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(queryCancellation.Token);
            var errorTask = process.StandardError.ReadToEndAsync(queryCancellation.Token);

            try
            {
                await process.WaitForExitAsync(queryCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                return new BluetoothBatteryQueryResult([], CreateBluetoothErrorMessage(error));
            }

            var queriedDevices = JsonSerializer.Deserialize<BluetoothBatteryDevice[]>(output) ?? [];
            var devices = queriedDevices
                .Where(device => !string.IsNullOrWhiteSpace(device.Name) && device.Percentage is >= 0 and <= 100)
                .GroupBy(device => device.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new BatteryDevice(group.Key, group.Max(device => device.Percentage)))
                .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return new BluetoothBatteryQueryResult(devices, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new BluetoothBatteryQueryResult([], "Bluetooth battery discovery timed out.");
        }
        catch (JsonException)
        {
            return new BluetoothBatteryQueryResult([], "Bluetooth battery discovery returned unreadable data.");
        }
        catch (Exception)
        {
            return BluetoothBatteryQueryResult.Failed;
        }
    }

    private static ProcessStartInfo CreateBluetoothQueryStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            CreateNoWindow = true,
            FileName = "powershell.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(BluetoothBatteryCommand);
        return startInfo;
    }

    private static string CreateBluetoothErrorMessage(string error) => string.IsNullOrWhiteSpace(error)
        ? "Bluetooth battery discovery could not complete."
        : "Bluetooth battery discovery could not complete. Check that the Windows PnPDevice module is available.";

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    private sealed record BluetoothBatteryDevice(string Name, int Percentage);

    private sealed record BluetoothBatteryQueryResult(IReadOnlyList<BatteryDevice> Devices, string? ErrorMessage)
    {
        internal static BluetoothBatteryQueryResult Failed { get; } = new(
            [],
            "Bluetooth battery discovery could not complete.");
    }
}
