using System;
using System.Linq;
using HidSharp;

var devices = DeviceList.Local.GetHidDevices().ToArray();
Console.WriteLine($"Total HID devices: {devices.Length}");
foreach (var d in devices)
{
    Console.WriteLine($"VID=0x{d.VendorID:X4} PID=0x{d.ProductID:X4} Path={d.DevicePath}");
}
