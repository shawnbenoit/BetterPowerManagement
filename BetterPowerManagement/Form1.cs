using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BetterPowerManagement
{
    struct planItem
    {
        public string friendlyName;
        public string planGuid;
    }

    public partial class Form1 : Form
    {
        [DllImport("PowrProf.dll")]
        public static extern UInt32 PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, UInt32 AcessFlags, UInt32 Index, ref Guid Buffer, ref UInt32 BufferSize);

        [DllImport("PowrProf.dll")]
        public static extern UInt32 PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, IntPtr PowerSettingGuid, IntPtr Buffer, ref UInt32 BufferSize);

        [DllImport("PowrProf.dll")]
        public static extern UInt32 PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        public enum AccessFlags : uint
        {
            ACCESS_SCHEME = 16,
            ACCESS_SUBGROUP = 17,
            ACCESS_INDIVIDUAL_SETTING = 18
        }

        private static string ReadFriendlyName(Guid schemeGuid)
        {
            uint sizeName = 1024;
            IntPtr pSizeName = Marshal.AllocHGlobal((int)sizeName);

            string friendlyName;

            try
            {
                PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pSizeName, ref sizeName);
                friendlyName = Marshal.PtrToStringUni(pSizeName);
            }
            finally
            {
                Marshal.FreeHGlobal(pSizeName);
            }

            return friendlyName;
        }


        public static IEnumerable<Guid> GetAll()
        {
            var schemeGuid = Guid.Empty;

            uint sizeSchemeGuid = (uint)Marshal.SizeOf(typeof(Guid));
            uint schemeIndex = 0;

            while (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)AccessFlags.ACCESS_SCHEME, schemeIndex, ref schemeGuid, ref sizeSchemeGuid) == 0)
            {
                yield return schemeGuid;
                schemeIndex++;
            }
        }

        public Form1()
        {
            InitializeComponent();
            timer1.Start();
            listSchemes();
            SelectActivePowerScheme();
            listBox1.DoubleClick += listBox1_DoubleClick; // Subscribe to the DoubleClick event
        }

        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        public string friendlyName;
        public Guid guidID;
        //TODO

        planItem planList = new planItem();
        ArrayList planArray = new ArrayList();

        public void listSchemes()
        {
            // List existing power schemes
            var existingSchemes = GetAll();
            foreach (var schemeGuid in existingSchemes)
            {
                string friendlyName = ReadFriendlyName(schemeGuid);
                planArray.Add(new planItem { friendlyName = friendlyName, planGuid = schemeGuid.ToString() });
            }

            // Add custom power schemes
            AddCustomPowerScheme("Ultra Performance Mode", "74c77414-b0ba-4b5a-96af-57705c7bb6dd", "Ultra Performance Mode.pow");
            AddCustomPowerScheme("Ultra Power Saver Mode", "34291b00-9605-4f2a-9e9f-40714f2c840d", "Ultra Power Saver Mode.pow");

            foreach (planItem name in planArray)
            {
                Console.WriteLine($"\"{name.friendlyName.ToString()}\"");
                listBox1.Items.Add(name.friendlyName);
            }
        }

        private void AddCustomPowerScheme(string friendlyName, string guid, string fileName)
        {
            planList.friendlyName = friendlyName;
            planList.planGuid = guid;
            planArray.Add(planList);
        }

        private void SelectActivePowerScheme()
        {
            IntPtr activePolicyGuidPtr;
            uint result = PowerGetActiveScheme(IntPtr.Zero, out activePolicyGuidPtr);
            if (result == 0)
            {
                Guid activePolicyGuid = (Guid)Marshal.PtrToStructure(activePolicyGuidPtr, typeof(Guid));
                string activePolicyGuidStr = activePolicyGuid.ToString();

                for (int i = 0; i < listBox1.Items.Count; i++)
                {
                    foreach (planItem item in planArray)
                    {
                        if (item.planGuid == activePolicyGuidStr)
                        {
                            listBox1.SelectedIndex = i;
                            return;
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"Failed to get active power scheme. Error code: {result}");
            }
        }

        private bool PowerSchemeExists(string schemeGuid)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = $"/query {schemeGuid}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return !output.Contains("The power scheme, subgroup or setting specified does not exist.");
        }

        private void ImportPowerScheme(string schemeFile, string friendlyName)
        {
            string fullPath = Path.Combine(Environment.CurrentDirectory, "Resources", schemeFile);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"File {fullPath} does not exist.");
                return;
            }

            Console.WriteLine($"Importing power scheme from {fullPath}...");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c powercfg /import \"{fullPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
            Console.WriteLine($"Power scheme imported from {fullPath}.");

            // Get the new GUID of the imported power scheme
            var newGuidProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c powercfg /list",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            newGuidProcess.Start();
            string output = newGuidProcess.StandardOutput.ReadToEnd();
            newGuidProcess.WaitForExit();

            string newGuid = null;
            foreach (string line in output.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
            {
                if (line.Contains(friendlyName))
                {
                    newGuid = line.Split(' ')[3];
                    break;
                }
            }

            if (newGuid != null)
            {
                Console.WriteLine($"Renaming imported power scheme to {friendlyName}...");
                // Rename the imported power scheme
                var renameProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c powercfg /changename {newGuid} \"{friendlyName}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    }
                };
                renameProcess.Start();
                renameProcess.WaitForExit();
                Console.WriteLine($"Power scheme renamed to {friendlyName}.");

                // Set the imported power scheme as active
                SetActivePlan(newGuid);
            }
            else
            {
                Console.WriteLine("Failed to get the new GUID of the imported power scheme.");
            }
        }

        public void SetActivePlan(string guidNumber)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c powercfg -setactive {guidNumber}",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
            Console.WriteLine($"Power scheme {guidNumber} set as active.");
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            double chargeStatus = ((SystemInformation.PowerStatus.BatteryLifePercent) * 100);
            label1.Text = "%" + chargeStatus.ToString();

            if (chargeStatus > 85)
            {
                label1.ForeColor = Color.Green;
                progressBar1.ForeColor = Color.Green;
            }
            else if (chargeStatus < 86 && chargeStatus > 71)
            {
                label1.ForeColor = Color.GreenYellow;
                progressBar1.ForeColor = Color.GreenYellow;
            }
            else if (chargeStatus < 72 && chargeStatus > 58)
            {
                label1.ForeColor = Color.Goldenrod;
                progressBar1.ForeColor = Color.Goldenrod;
            }
            else if (chargeStatus < 59 && chargeStatus > 45)
            {
                label1.ForeColor = Color.Orange;
                progressBar1.ForeColor = Color.Orange;
            }
            else if (chargeStatus < 46 && chargeStatus > 32)
            {
                label1.ForeColor = Color.DarkOrange;
                progressBar1.ForeColor = Color.DarkOrange;
            }
            else if (chargeStatus < 33 && chargeStatus > 19)
            {
                label1.ForeColor = Color.OrangeRed;
                progressBar1.ForeColor = Color.OrangeRed;
            }
            else
            {
                label1.ForeColor = Color.Red;
                progressBar1.ForeColor = Color.Red;
            }

            progressBar1.Value = (int)chargeStatus;
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                string selItem = listBox1.GetItemText(listBox1.SelectedItem);
                string selItemGUID = null;
                string selItemFile = null;

                foreach (planItem item in planArray)
                {
                    if (selItem == item.friendlyName)
                    {
                        selItemGUID = item.planGuid;
                        selItemFile = item.friendlyName == "Ultra Performance Mode" ? "Ultra Performance Mode.pow" : "Ultra Power Saver Mode.pow";
                        break;
                    }
                }

                if (selItemGUID != null)
                {
                    if (PowerSchemeExists(selItemGUID))
                    {
                        SetActivePlan(selItemGUID);
                    }
                    else
                    {
                        var result = MessageBox.Show($"The power scheme {selItem} does not exist. Do you want to install it?", "Install Power Scheme", MessageBoxButtons.YesNo);
                        if (result == DialogResult.Yes)
                        {
                            ImportPowerScheme(selItemFile, selItem);
                        }
                        else
                        {
                            SelectActivePowerScheme();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Selected item GUID is null.");
                }
            }
            else
            {
                Console.WriteLine("No item selected in ListBox1.");
            }
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            dragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = this.Location;
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        private void listBox1_MouseDown(object sender, MouseEventArgs e)
        {
            // Prevent the installation prompt from appearing when dragging the app window
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = this.Location;
            }
        }

        private void listBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        private void listBox1_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            var cplPath = Path.Combine(Environment.SystemDirectory, "control.exe");
            Process.Start(cplPath, "powercfg.cpl");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
