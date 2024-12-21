using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Windows.UI.Notifications;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace MqttNotifier {
    class Program {
        static MqttClient client;
        static System.Threading.Timer timer;
        static ManualResetEvent _manualResetEvent = new ManualResetEvent(false);

        static void Log(string message) {
            Boolean debug = Convert.ToBoolean(MQTTNotifier.Properties.Settings.Default.debug);
            if (ConsoleHelper.IsRunningInConsole()) {
                Console.WriteLine(string.Format("{0}: {1}", DateTime.Now, message));
            } else if (debug) {
                File.AppendAllText("ToastNotificationApp.log", string.Format("{0}: {1}{2}", DateTime.Now, message, Environment.NewLine));
            }
        }

        static void Main(string[] args) {
            // Attach to the console if running from cmd.exe or PowerShell
            ConsoleHelper.AttachToParentConsole();
            if (ConsoleHelper.IsRunningInConsole()) {
                Console.WriteLine("\n\nPress [Ctrl+C] to exit.\n");
            }

            // Log program start message to console or file
            Log("Program started.");

            CreateAppUserModelId();
            //ShowToast("Test", "Test Message");

            CreateMqtt();
            timer = new System.Threading.Timer(CheckStatus, null, 0, 10000);

            // If this is running in a console, wait for user input to exit
            if (ConsoleHelper.IsRunningInConsole()) {
                Console.ReadLine();
            } else {
                // Keep the application running indefinitely (wait on the ManualResetEvent)
                _manualResetEvent.WaitOne();
            }
        }

        static void CreateAppUserModelId() {
            string keyPath = @"Software\Classes\AppUserModelId\MQTTNotifier";

            // Register our AppUserModelId.  Needed to be able to show Toast Notifications.
            using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(keyPath)) {
                if (registryKey != null) {
                    Log("Registering AppUserModelId so that we can send Toast Notifications");
                    registryKey.SetValue("DisplayName", "MQTTNotifier", RegistryValueKind.String);
                    registryKey.SetValue("IconUri", "ms-resource://Windows.UI.ShellCommon/Files/Images/NearShare.png", RegistryValueKind.String);
                }
            }
        }

        static private void CreateMqtt() {
            try {
                String broker = MQTTNotifier.Properties.Settings.Default.broker;
                int port = MQTTNotifier.Properties.Settings.Default.port;

                Log(string.Format("MQTT Broker: {0}:{1}", broker, port));
                client = new MqttClient(broker, port, false, null, null, MqttSslProtocols.None);
                client.MqttMsgPublishReceived += ReceiveMsgMqtt;
            } catch (Exception ex) {
                Log("MQTT Create failed: " + ex.Message);
            }
        }

        static private void CheckStatus(object sender) {
            if (!client.IsConnected) {
                try {
                    String[] topics = MQTTNotifier.Properties.Settings.Default.topics.Split('|');
                    String username = MQTTNotifier.Properties.Settings.Default.username;
                    String password = MQTTNotifier.Properties.Settings.Default.password;
                    
                    Log("Connecting to broker...");
                    byte code = client.Connect("MQTTNotifier_" + System.Environment.MachineName, username, password);
                    Log("MQTT connected.");
                    for (int i = 0; i < topics.Length; i++) {
                        client.Subscribe(new string[] { topics[i] }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                        Log("Subscribed to topic: " + topics[i]);
                    }
                } catch (Exception ex) {
                    Log("MQTT Reconnect/subscribe failed: " + ex.Message);
                }
            }
        }

        static private void ReceiveMsgMqtt(object sender, MqttMsgPublishEventArgs e) {
            try {
                string[] topics = e.Topic.Split('/');

                object parsedObject;
                // If this is a JSON message, then extract the title & message fields
                if (JsonHelper.TryParseJson(Encoding.UTF8.GetString(e.Message), out parsedObject)) {
                    var dict = parsedObject as Dictionary<string, object>;

                    if (dict != null && dict.Count > 0 && dict.ContainsKey("title") && dict.ContainsKey("message")) {
                        Log("Valid JSON notification found");
                        Log(string.Format("Title: {0}; Message: {1}", dict["title"], dict["message"]));
                        ShowToast(e.Topic, dict["title"].ToString(), dict["message"].ToString());
                        return;
                    }
                }

                ShowToast(e.Topic, "Alert Notification: " + topics[topics.Length - 1], Encoding.UTF8.GetString(e.Message));
            } catch (Exception ex) {
                Log("MQTT PublishReceived failed: " + ex.Message);
            }
        }

        static private void ShowToast(string attribution, string title, string message) {
            try {
                string toastXml = @"<toast><visual><binding template='ToastGeneric'><text placement='attribution'>%attribution%</text><text>%title%</text><text>%message%</text></binding></visual></toast>";
                toastXml = toastXml.Replace("%attribution%", attribution);
                toastXml = toastXml.Replace("%title%", title);
                toastXml = toastXml.Replace("%message%", message);

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(toastXml);

                var toast = new ToastNotification(doc);

                ToastNotificationManager.CreateToastNotifier("MQTTNotifier").Show(toast);
                Log("Toast notification: " + title + ": " + message);
            } catch (Exception ex) {
                Log("Show Toast Failed:" + ex.Message);
            }
        }    
    }

    internal class JsonHelper {
        public static bool TryParseJson(string jsonString, out object parsedObject) {
            parsedObject = null;

            if (string.IsNullOrWhiteSpace(jsonString)) {
                return false;
            }

            try {
                var serializer = new JavaScriptSerializer();
                parsedObject = serializer.Deserialize<Dictionary<string, object>>(jsonString);
                return true;
            } catch (Exception) {
                // If there are any errors, it's not valid JSON
                return false;
            }
        }
   }

   internal class ConsoleHelper {
        // Win32 functions needed to attach console
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(int dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetConsoleWindow();
        const int ATTACH_PARENT_PROCESS = -1;

        public static bool IsRunningInConsole() {
            // Check if there is an existing console window attached to this process
            IntPtr consoleWindow = GetConsoleWindow();
            return consoleWindow != IntPtr.Zero;
        }

        public static void AttachToParentConsole() {
            // Attach to the parent process's console if available
            if (!IsRunningInConsole()) {
                AttachConsole(ATTACH_PARENT_PROCESS);
            }
        }
   }
}