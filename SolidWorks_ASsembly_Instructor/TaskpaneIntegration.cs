using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using SolidWorks_ASsembly_Instructor.Properties;
using System;
using System.Configuration.Assemblies;
using System.Net;
using System.Runtime.InteropServices;

namespace SolidWorks_ASsembly_Instructor
{
    /// <summary>
    /// Adaptet from https://www.youtube.com/watch?v=7DlG6OQeJP0
    /// With help of this: https://stackoverflow.com/questions/74966397/making-c-sharp-class-library-com-visible-in-visual-studio-2022
    /// </summary>
    /// 

    [ComVisible(true)]
    public class TaskpaneIntegration : ISwAddin
    {
        /// <summary>
        /// The cookie to the current instance of SolidWorks we are running inside of
        /// </summary>
        private int mSwCookie;

        /// <summary>
        /// The taskpane view for our add-inn
        /// </summary>
        private TaskpaneView mTaskpaneView;

        /// <summary>
        /// The UI control that is goint to beinside the Solidworks taskpane view
        /// </summary>
        private TaskpaneHostUI mTaskpaneHost;

        /// <summary>
        /// The current isntande of the SolidWorks application
        /// </summary>
        private SldWorks mSolidWorksApplication;

        public const string SWTASKPANE_PROGID = "Match.pm_Robot.SolidWorks_ASsembly_Instructor";

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            mSolidWorksApplication = (SldWorks)ThisSW;
            mSwCookie = Cookie;

            //setup Callback info
            var ok = mSolidWorksApplication.SetAddinCallbackInfo2(0, this, mSwCookie);

            if (ok)
            {
                LoadUI();
                //mSolidWorksApplication.SendMsgToUser("ConnectionDone");

                return true;
            }
            else
            {
                return false;
            }

        }

        public bool DisconnectFromSW()
        {
            UnloadUI();
            return true;
        }

        private void LoadUI()
        {
            mTaskpaneView = mSolidWorksApplication.CreateTaskpaneView2($@".\face.bmp", "SolidWorks_ASsembly_Instructor");

            mTaskpaneHost = (TaskpaneHostUI)mTaskpaneView.AddControl(TaskpaneIntegration.SWTASKPANE_PROGID, string.Empty);
            mTaskpaneHost.app = mSolidWorksApplication;
        }

        private void UnloadUI()
        {
            mTaskpaneHost = null;
            mTaskpaneView.DeleteView();
            Marshal.ReleaseComObject(mTaskpaneView);

            mTaskpaneView = null;
        }

        [ComRegisterFunction()]
        private static void ComRegister(Type t)
        {
            var keyPath = string.Format(@"SOFTWARE\SolidWorks\AddIns\{0:b}", t.GUID);

            //Create our registry folder for the add-in
            using (var rk = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath))
            {
                //Load add-inn when solid work opens
                rk.SetValue(null, 1);

                //set Titel and description

                rk.SetValue("Title", "SWASI");
                rk.SetValue("Description", "SolidWorks_ASsembly_Instructor");
            }
        }

        [ComUnregisterFunction()]
        private static void ComUnregister(Type t)
        {
            var keyPath = string.Format(@"SOFTWARE\SolidWorks\AddIns\{0:b}", t.GUID);

            Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(keyPath);
        }

    }
}