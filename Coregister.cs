using System.Runtime.InteropServices;

namespace MonoFusion.Exporter
{
	internal class Coregister
	{
		[DllImport("ole32.dll")]
		public static extern int CoRegisterMessageFilter(IntPtr lpMessageFilter, out IntPtr lplpMessageFilter);

		public class RetryMessageFilter
		{
			public static IntPtr CreateInstance()
			{
				// vtable: HandleInComingCall, RetryRejectedCall, MessagePending
				// + IUnknown: QueryInterface, AddRef, Release
				var vtable = new IntPtr[6];
				var vtableHandle = GCHandle.Alloc(vtable, GCHandleType.Pinned);

				vtable[0] = Marshal.GetFunctionPointerForDelegate(new QueryInterfaceDelegate(QueryInterface));
				vtable[1] = Marshal.GetFunctionPointerForDelegate(new AddRefDelegate(AddRef));
				vtable[2] = Marshal.GetFunctionPointerForDelegate(new ReleaseDelegate(Release));
				vtable[3] = Marshal.GetFunctionPointerForDelegate(new HandleInComingCallDelegate(HandleInComingCall));
				vtable[4] = Marshal.GetFunctionPointerForDelegate(new RetryRejectedCallDelegate(RetryRejectedCall));
				vtable[5] = Marshal.GetFunctionPointerForDelegate(new MessagePendingDelegate(MessagePending));

				IntPtr vtablePtr = vtableHandle.AddrOfPinnedObject();
				IntPtr obj = Marshal.AllocHGlobal(IntPtr.Size);
				Marshal.WriteIntPtr(obj, vtablePtr);
				return obj;
			}

			delegate int QueryInterfaceDelegate(IntPtr self, ref Guid riid, out IntPtr ppvObject);
			delegate uint AddRefDelegate(IntPtr self);
			delegate uint ReleaseDelegate(IntPtr self);
			delegate uint HandleInComingCallDelegate(IntPtr self, uint dwCallType, IntPtr htaskCaller, uint dwTickCount, IntPtr lpInterfaceInfo);
			delegate uint RetryRejectedCallDelegate(IntPtr self, IntPtr htaskCallee, uint dwTickCount, uint dwRejectType);
			delegate uint MessagePendingDelegate(IntPtr self, IntPtr htaskCallee, uint dwTickCount, uint dwPendingType);

			static int QueryInterface(IntPtr self, ref Guid riid, out IntPtr ppvObject) { ppvObject = self; return 0; }
			static uint AddRef(IntPtr self) => 1;
			static uint Release(IntPtr self) => 1;
			static uint HandleInComingCall(IntPtr self, uint dwCallType, IntPtr htaskCaller, uint dwTickCount, IntPtr lpInterfaceInfo) => 0;
			static uint RetryRejectedCall(IntPtr self, IntPtr htaskCallee, uint dwTickCount, uint dwRejectType) => 99;
			static uint MessagePending(IntPtr self, IntPtr htaskCallee, uint dwTickCount, uint dwPendingType) => 2;
		}
	}
}
