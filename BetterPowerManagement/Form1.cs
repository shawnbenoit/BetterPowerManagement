﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BetterPowerManagement
{


	public partial class Form1 : Form
	{
		struct PowerScheme
		{
			public string friendlyName;
			public Guid guidID;
		}

		List<PowerScheme> schemes = new List<PowerScheme>();

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

		public string ListNumber()
		{
			string result = "powerScheme" + schemes.Count.ToString();
			return result;
		}

		public void listSchemes()
		{

			listView1.View = View.Details;
			listView1.FullRowSelect = true;
			listView1.Columns.Add("Plan", 160, HorizontalAlignment.Left);
			listView1.Columns.Add("GUID", 240, HorizontalAlignment.Left);



			var guidPlans = GetAll();

			foreach(Guid guidPlan in guidPlans)
			{
				string pscheme = ListNumber();
				//PowerScheme {@pscheme} = new PowerScheme();
				//powerScheme.friendlyName
				listView1.Items.Add(ReadFriendlyName(guidPlan), "Plan");
				//listView1.Items.Add(guidPlan.ToString(), "GUID");
			}

		}

		private void ListView1_ItemActivate(Object sender, EventArgs e)
		{

			//MessageBox.Show("You are in the ListView.ItemActivate event.");
		}

		private void listView1_SelectedIndexChanged(object sender, EventArgs e)
		{
			string selectedItemName = listView1.SelectedItems.ToString();

			//System.Diagnostics.Process process = new System.Diagnostics.Process();
			//System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
			//startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
			//startInfo.FileName = "cmd.exe";
			//startInfo.Arguments = "/c powercfg /setactive " + guid;
			//process.StartInfo = startInfo;
			//process.Start();

			label3.Text = ListNumber(); //selectedItemName.Substring(0);// ToString();

		}

		private void button1_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			label1.Text = "Battery Status: %" + ((SystemInformation.PowerStatus.BatteryLifePercent) * 100).ToString();
		}
	}
}
