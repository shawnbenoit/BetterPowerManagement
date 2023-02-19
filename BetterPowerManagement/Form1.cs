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

			while(PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)AccessFlags.ACCESS_SCHEME, schemeIndex, ref schemeGuid, ref sizeSchemeGuid) == 0)
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
			var guidPlans = GetAll();
			int i = 0;

			foreach(Guid guidPlan in guidPlans)
			{
				planList.friendlyName = ReadFriendlyName(guidPlan);
				planList.planGuid = guidPlan.ToString();
				planArray.Add(planList);
				i++;
			}

			foreach(planItem name in planArray)
			{
				Console.WriteLine($"\"{name.friendlyName.ToString()}\"");
				listBox1.Items.Add(name.friendlyName);
			}
		}

		public void printArrayList()
		{
			foreach(planItem plan in planArray)
			{
				Console.WriteLine(plan.friendlyName + " " + plan.planGuid);
			}
		}

		public void SetActivePlan(string planName, string giudNumber)
		{
			string planNameString = planName;
			string guidNumberString = giudNumber;

			var cmd = new Process { StartInfo = { FileName = "powercfg" } };
			using(cmd) //This is here because Process implements IDisposable
			{
				//var inputPath = Path.Combine(Environment.CurrentDirectory + "\\Resources\\UltraPerformanceMode.pow");
				//This hides the resulting popup window
				cmd.StartInfo.CreateNoWindow = true;
				cmd.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

				//Prepare a guid for this new import
				var guidString = Guid.NewGuid().ToString("D"); //Guid without braces

				//Import the new power plan
				cmd.StartInfo.Arguments = $"/setactive \"{planNameString}\" {guidNumberString}";
				cmd.Start();
			}
		}

		public void ImportPlan(string planName, string giudNumber)
		{
			string planNameString = planName;
			string guidNumberString = giudNumber;

			var cmd = new Process { StartInfo = { FileName = "powercfg" } };
			using(cmd) //This is here because Process implements IDisposable
			{
				string inputPath = Path.Combine(Environment.CurrentDirectory + "\\Resources\\" + planNameString);
				//This hides the resulting popup window
				cmd.StartInfo.CreateNoWindow = true;
				cmd.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

				//Import the new power plan
				cmd.StartInfo.Arguments = $"/import \"{inputPath}\" {guidNumberString}";
				cmd.Start();
			}
		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			double chargeStatus = ((SystemInformation.PowerStatus.BatteryLifePercent) * 100);
			label1.Text = "Battery Status: %" + chargeStatus.ToString();

			if(chargeStatus > 85)
			{
				label1.ForeColor = Color.Green;
			}
			else if(chargeStatus < 86 && chargeStatus > 71)
			{
				label1.ForeColor = Color.GreenYellow;
			}
			else if(chargeStatus < 72 && chargeStatus > 58)
			{
				label1.ForeColor = Color.Goldenrod;
			}
			else if(chargeStatus < 59 && chargeStatus > 45)
			{
				label1.ForeColor = Color.Orange;
			}
			else if(chargeStatus < 46 && chargeStatus > 32)
			{
				label1.ForeColor = Color.DarkOrange;
			}
			else if(chargeStatus < 33 && chargeStatus > 19)
			{
				label1.ForeColor = Color.OrangeRed;
			}
			else
			{
				label1.ForeColor = Color.Red;
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			string planName = "Ultra Performance Mode.pow";
			string guidString = Guid.NewGuid().ToString("D");
			ImportPlan(planName, guidString);
		}

		private void button2_Click(object sender, EventArgs e)
		{
			string planName = "Ultra Power Saver Mode.pow";
			string guidString = Guid.NewGuid().ToString("D");
			ImportPlan(planName, guidString);
		}

		private void button4_Click(object sender, EventArgs e)
		{
			printArrayList();
		}

		private void listBox1_Click(object sender, EventArgs e)
		{
			if(listBox1.Items.Count != 0)
			{
				string selItem = listBox1.GetItemText(listBox1.SelectedItem);
				string selItemGUID;

				foreach(planItem item in planArray)
				{
					if(selItem != item.friendlyName)
					{
						//Console.WriteLine("No Match");
					}
					else
					{
						Console.WriteLine("Calling SetActivePlan");
						selItem = item.friendlyName;
						selItemGUID = item.planGuid;
						SetActivePlan(selItem, selItemGUID);
					}
				}

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
			if(dragging)
			{
				Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
				this.Location = Point.Add(dragFormPoint, new Size(dif));
			}
		}

		private void Form1_MouseUp(object sender, MouseEventArgs e)
		{
			{
				dragging = false;
			}
		}
		private void listBox1_MouseMove(object sender, MouseEventArgs e)
		{
			if(dragging)
			{
				Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
				this.Location = Point.Add(dragFormPoint, new Size(dif));
			}
		}

		private void listBox1_MouseUp(object sender, MouseEventArgs e)
		{
			{
				dragging = false;
			}
		}

		private void listBox1_MouseDown(object sender, MouseEventArgs e)
		{
			dragging = true;
			dragCursorPoint = Cursor.Position;
			dragFormPoint = this.Location;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			this.Close();
		}

	}
}
