using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Diagnostics;

namespace WindowsSniffer
{ 
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public DispatcherTimer _lockedTime = new DispatcherTimer();
        public DispatcherTimer _workingTime = new DispatcherTimer();

        public double workSeconds = 0;
        public double lockSeconds = 0;

        public DateTime _workTime;

        public DateTime _startLockCount;
        public DateTime _endLockCount;
        public DateTime _startUnLockCount;
        public DateTime _endUnLockCount;

        public DateTime _startSuspendCount;
        public DateTime _endSuspendCount;
        public DateTime _startResumeCount;
        public DateTime _endResumeCount;


        public Boolean isResumed = false;
        TimeSpan _totalSleepTime = new TimeSpan();

        public MainWindow()
        {
            InitializeComponent();

            DetectSystemLock();
            _lockedTime.Tick += _lockedTime_Tick;
            _lockedTime.Interval = new TimeSpan(0, 0, 1);

            _workingTime.Tick += _workingTime_Tick;
            _workingTime.Interval = new TimeSpan(0, 0, 1);
            _workingTime.Start();

        }
        /// <summary>
        /// Record work time as soon as the program starts running or when the dispatcher is set to start()
        /// This delegate is called every second, starts at windows session unlock and stops at windows session lock
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _workingTime_Tick(object sender, EventArgs e)
        {
            _workTime = DateTime.Today.AddSeconds(++workSeconds);
            WorkingCounterLbl.Content = _workTime.ToString("HH:mm:ss");

            // Forcing the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Record lock time as soon as the windows session lock is triggered by the user or when this dispatcher is set to start()
        /// This delegate is called every second, starts at windows session lock and stops at windows session unlock
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _lockedTime_Tick(object sender, EventArgs e)
        {
            
            DateTime  lockTime = DateTime.Today.AddSeconds(++lockSeconds);
            LockedCounterLbl.Content = lockTime.ToString(@"HH\:mm\:ss");

            // Forcing the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();
            Debug.Print("lockSeconds :" + lockSeconds);
        }

        public void DetectSystemLock()
        {
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

        }

        void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            
            // try to use different dispatcher for resume and suspend.
            switch (e.Mode)
            {
                case PowerModes.Suspend:                   
                    _startSuspendCount = DateTime.Now;
                    _endResumeCount = DateTime.Now;

                    _workingTime.Stop();
                    _lockedTime.Start();
                    Debug.Print("Inside Suspend");
                    Debug.Print("_startSuspendCount: " + _startSuspendCount);
 
                    break;

                case PowerModes.Resume:
                    _endSuspendCount = DateTime.Now;
                    _startResumeCount = DateTime.Now;

                    _lockedTime.Stop();
                    _workingTime.Start();

                    ////update suspend time into locktime label
                    //TimeSpan totalSuspendTime = _endSuspendCount.Subtract(_startSuspendCount); //CHANGE THIS TO LOCK TIME COUNT
                    //lockSeconds = lockSeconds + totalSuspendTime.TotalSeconds;
                    //LockedCounterLbl.Content = DateTime.Today.AddSeconds(lockSeconds).ToString(@"HH\:mm\:ss");                    
                    isResumed = true;
                    Debug.Print("Inside Resume");
                    Debug.Print("_endSuspendCount: " + _endSuspendCount);
                    WriteUserStatus(PowerModes.Resume.ToString());
                    break;


            }
        }

        void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                    _lockedTime.Start();
                    _workingTime.Stop();

                    _startLockCount = DateTime.Now;
                    _endUnLockCount = DateTime.Now;                    
                    WriteUserStatus(SessionSwitchReason.SessionLock.ToString());
                    Debug.Print("Inside Lock");
                    Debug.Print("_startLockCount: " + _startLockCount);
                    break;

                case SessionSwitchReason.SessionUnlock:
                    _lockedTime.Stop();
                    _workingTime.Start();

                    _endLockCount = DateTime.Now;
                    _startUnLockCount = DateTime.Now;

                    if (isResumed)
                    {
                        //update suspend time into locktime label
                        TimeSpan totalLockAndSuspendTime = _endLockCount.Subtract(_startLockCount); //using lock/unlocktime because this case happens only after system resumed and system unlocked. The time captured will include both suspend and lock time.
                        lockSeconds = lockSeconds + totalLockAndSuspendTime.TotalSeconds; //retain previous lock time
                        LockedCounterLbl.Content = DateTime.Today.AddSeconds(lockSeconds).ToString(@"HH\:mm\:ss");
                        isResumed = false; //to avoid repetition when system undergoes only lock/unclock case
                    }
                    WriteUserStatus(SessionSwitchReason.SessionUnlock.ToString());
                    Debug.Print("Inside Unlock");
                    Debug.Print("_endLockCount: " + _endLockCount);
                    break;

                case SessionSwitchReason.SessionLogon:
                    WriteUserStatus(SessionSwitchReason.SessionLogon.ToString());
                    break;

                case SessionSwitchReason.SessionLogoff:
                    WriteUserStatus(SessionSwitchReason.SessionLogoff.ToString());
                    break;
            }

        }

        private void WriteUserStatus(String reason)
        {
            string lockUnLockTime = string.Empty;
            string suspendResumeTime = string.Empty;
            string logData = string.Empty;
            bool isSessionChanged = false;
            bool isPowerModesChanged = false;


            TimeSpan totalLockTime = _endLockCount.Subtract(_startLockCount);
            TimeSpan totalSuspendTime = _endSuspendCount.Subtract(_startSuspendCount);

            //The first time, when this program starts or before the program started, there won't be any unlock time recorded (_startUnLockCount = 0)
            //The time the program was on was counted as unlocked time (if _startUnLockCount == new DateTime() then display the _worktime);
            TimeSpan totalUnLockTime = _startUnLockCount == new DateTime() ? _workTime.Subtract(new DateTime()) : _endUnLockCount.Subtract(_startUnLockCount);
            TimeSpan totalResumeTime = _endResumeCount.Subtract(_startResumeCount);

            string filePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\UserLog";
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            using (StreamWriter file = new StreamWriter(filePath + "\\UserLog.txt",true))
            {
                
                if (reason == SessionSwitchReason.SessionLock.ToString() || reason == SessionSwitchReason.SessionUnlock.ToString())
                {
                    isSessionChanged = true;
                    lockUnLockTime = reason == "SessionUnlock" ? "  Locked Time  : " + totalLockTime.ToString(@"hh\:mm\:ss") : "  UnLocked Time: " + totalUnLockTime.ToString(@"hh\:mm\:ss");
                }
                else if (reason == PowerModes.Resume.ToString()) //calc suspend time, when system resumed
                {
                    isPowerModesChanged = true;
                    suspendResumeTime = "  Suspend Time  : " + totalSuspendTime.ToString(@"hh\:mm\:ss");
                }

                logData = Environment.UserName + "     " + reason + "  " + DateTime.Now;
                file.WriteLineAsync(logData + "\r" + lockUnLockTime);
            }
                        
            MainTextBox.AppendText("\n  " + logData);
            if (isSessionChanged)
                MainTextBox.AppendText("\r" + lockUnLockTime);

            if (isPowerModesChanged)
                MainTextBox.AppendText("\r" + suspendResumeTime);

            MainTextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            MainTextBox.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
            MainTextBox.ScrollToEnd();
        }


    }
}
