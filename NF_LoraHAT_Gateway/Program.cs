using System;
using System.Diagnostics;
using System.Threading;

using nanoFramework.Hardware.Esp32;
using System.Device.Gpio;
using System.IO.Ports;
using nanoFramework.Networking;
using nanoFramework.Azure.Devices.Client;
using nanoFramework.Azure.Devices.Provisioning.Client;
using nanoFramework.M2Mqtt.Messages;
using nanoFramework.M5Stack;
using Console = nanoFramework.M5Stack.Console;

namespace NF_LoraHAT_Gateway
{
    public class Program
    {
        static SerialPort serial;
        static GpioPin rxPin;
        static GpioPin txPin;

        static DeviceClient client;
        static ProvisioningDeviceClient provisioning;
        static DeviceRegistrationResult myDevice;

        const string ssid = "[your SSID]";
        const string password = "[your password]";

        const string dspAddress = "global.azure-devices-provisioning.net";

        const string idscope = "[ID SCOPE]";
        const string registrationid = "[device id]";
        const string saskey = "[primary key]";


        public static void Main()
        {
            M5StickCPlus.InitializeScreen();

            //Wifi setup
            if (!ConnectWifi())
            {
                Debug.WriteLine("Wifi connection failed...");
                return;
            }
            else
            {
                Debug.WriteLine("Wifi connected...");
            }

            Thread.Sleep(5000);

            //DPS setting
            provisioning = ProvisioningDeviceClient.Create(dspAddress, idscope, registrationid, saskey);

            myDevice = provisioning.Register(null, new CancellationTokenSource(30000).Token);

            if (myDevice.Status != ProvisioningRegistrationStatusType.Assigned)
            {
                Debug.WriteLine($"Registration is not assigned: {myDevice.Status}, error message: {myDevice.ErrorMessage}");
                return;
            }

            Debug.WriteLine($"Device successfully assigned:");

            IoTCentralConnect();

            //Lora setup
            LoraInit();

            Console.Clear();

            Console.WriteLine("Lora Initialized.");
            Console.WriteLine("Waiting message");


            //
            Thread.Sleep(Timeout.Infinite);
        }

        private static void IoTCentralConnect()
        {
            //IoTCentoral connect
            client = new DeviceClient(myDevice.AssignedHub, registrationid, saskey, MqttQoSLevel.AtMostOnce);

            var res = client.Open();

            if (!res)
            {
                Debug.WriteLine("can't open the device");
                return;
            }
            else
            {
                Debug.WriteLine("Open the device");
                Console.WriteLine("Open the device");
            }

            Thread.Sleep(5000);
        }

        private static bool ConnectWifi()
        {
            Debug.WriteLine("Connecting Wifi....");
            var success = WifiNetworkHelper.ConnectDhcp(ssid, password, requiresDateTime: true, token: new CancellationTokenSource(60000).Token);
            if (!success)
            {
                Debug.WriteLine($"Can't connect to the network, error: {WifiNetworkHelper.Status}");

                if (WifiNetworkHelper.HelperException != null)
                {
                    Debug.WriteLine($"ex: {WifiNetworkHelper.HelperException}");
                }
            }

            Debug.WriteLine($"Date and time is now {DateTime.UtcNow}");

            return success;
        }

        private static void LoraInit()
        {
            LoraReset();

            Configuration.SetPinFunction(26, DeviceFunction.COM2_RX);
            Configuration.SetPinFunction(0, DeviceFunction.COM2_TX);

            serial = new SerialPort("COM2", 115200, Parity.None, 8, StopBits.One);
            serial.DataReceived += Serial_DataReceived;

            serial.Open();

            Debug.WriteLine("config");
            serial.Write("config\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("2");
            serial.Write("2\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("a 1");     //ノードタイプ（子機）
            serial.Write("a 1\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("c 12");    //拡散率設定
            serial.Write("c 12\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("d 3");     //channel
            serial.Write("d 3\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("e 2345");  //PAN ID
            serial.Write("e 2345\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("f 0000");     //OWN ID
            serial.Write("f 0000\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("g ffff");     //dist ID
            serial.Write("g ffff\r\n");
            Thread.Sleep(100);
            Debug.WriteLine("z");
            serial.Write("z\r\n");
            Thread.Sleep(100);
        }

        private static void LoraReset()
        {
            var gpioController = new GpioController();

            rxPin = gpioController.OpenPin(26, PinMode.Output);
            txPin = gpioController.OpenPin(0, PinMode.Output);

            rxPin.Write(PinValue.Low);
            txPin.Write(PinValue.Low);

            Thread.Sleep(1000);

            rxPin.SetPinMode(PinMode.Input);
            txPin.SetPinMode(PinMode.Input);

            Thread.Sleep(1000);
        }

        private static void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (serial.BytesToRead == 0)
            {
                return;
            }

            var data = serial.ReadLine();

            if (data.Contains("{"))
            {
                Debug.Write(DateTime.UtcNow.ToString());
                Debug.Write(" ");
                Debug.WriteLine(data);
                client.SendMessage(data, new CancellationTokenSource(2000).Token);
                Console.WriteLine(DateTime.UtcNow.ToString());
            }
        }
    }
}
