using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MonoFusion.Exporter
{
	internal class TaskDialog
	{
		[DllImport("comctl32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
		public static extern void TaskDialogIndirect(
			ref TASKDIALOGCONFIG pTaskConfig,
			out int pnButton,
			out int pnRadioButton,
			out bool pfVerificationFlagChecked);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct TASKDIALOGCONFIG
		{
			public uint cbSize;
			public IntPtr hwndParent;
			public IntPtr hInstance;
			public uint dwFlags;
			public uint dwCommonButtons;
			public string pszWindowTitle;
			public IntPtr hMainIcon;
			public string pszMainInstruction;
			public string pszContent;
			public uint cButtons;
			public IntPtr pButtons;
			public int nDefaultButton;
			public uint cRadioButtons;
			public IntPtr pRadioButtons;
			public int nDefaultRadioButton;
			public string pszVerificationText;
			public string pszExpandedInformation;
			public string pszExpandedControlText;
			public string pszCollapsedControlText;
			public IntPtr hFooterIcon;
			public string pszFooter;
			public IntPtr pfCallback;
			public IntPtr lpCallbackData;
			public uint cxWidth;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct TASKDIALOG_BUTTON
		{
			public int nButtonID;
			public string pszButtonText;
		}

		public const uint TDCBF_OK_BUTTON = 0x1;
		public const uint TDCBF_CANCEL_BUTTON = 0x8;
		public const int IDOK = 1;
	}
}
