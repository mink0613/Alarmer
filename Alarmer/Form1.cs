using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Linq;

namespace Alarmer
{
    public partial class Form1 : Form
    {
        private List<AlarmData> _dataList;

        private AutoResetEvent _checkEvent = new AutoResetEvent(false);

        private bool _isStop;

        public Form1()
        {
            InitializeComponent();

            _isStop = false;
            _dataList = new List<AlarmData>();
            lvResult.View = View.Details;
            tbDate.TextChanged += TbDate_TextChanged;

            FormClosing += Form1_FormClosing;
            cbType.SelectedIndex = 0;
            cbType.SelectedValueChanged += CbType_SelectedValueChanged;

            SystemEvents.PowerModeChanged += OnPowerChange;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            InitNotifyIcon();
            SetStartup();

            LoadData();
            RunCheckThread();
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                    break;
                case SessionSwitchReason.SessionUnlock:
                    _checkEvent.Set();
                    break;
                default:
                    break;
            }
        }

        private void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    _checkEvent.Set();
                    //MessageBox.Show("Resumed2"); // for debugging
                    break;
                case PowerModes.Suspend:
                    break;
                case PowerModes.StatusChange:
                    _checkEvent.Set();
                    //MessageBox.Show("Resumed"); // for debugging
                    break;
            }
        }

        private void CbType_SelectedValueChanged(object sender, EventArgs e)
        {
            if (cbType.SelectedIndex == 0) // weekly
            {
                cbDays.Visible = true;
                tbDate.Visible = false;
            }
            else // monthly
            {
                cbDays.Visible = false;
                tbDate.Visible = true;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isStop == false)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }

            e.Cancel = !_isStop;
        }

        private void InitNotifyIcon()
        {
            ContextMenu ctx = new ContextMenu();
            ctx.MenuItems.Add(new MenuItem("Check Schedule", new EventHandler((s, ex) =>
            {
                CheckAlarm(true);
            })));
            ctx.MenuItems.Add(new MenuItem("Close", new EventHandler((s, ex) => 
            {
                _isStop = true;
                _checkEvent.Set();
                this.Close();
            })));
            notifyIcon1.ContextMenu = ctx;
        }

        private void SetStartup()
        {
#if RELEASE
            RegistryKey rk = Registry.CurrentUser.OpenSubKey
                ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (rk.GetValue("Alarmer") != null)
            {
                rk.DeleteValue("Alarmer");
            }

            rk.SetValue("Alarmer", Application.ExecutablePath);
#endif
        }

        private void TbDate_TextChanged(object sender, EventArgs e)
        {
            string text = (sender as TextBox).Text;
            if (text == null || text.Length == 0)
            {
                return;
            }

            int val = 0;

            if (int.TryParse(text, out val) == false)
            {
                tbDate.TextChanged -= TbDate_TextChanged;
                tbDate.Text = tbDate.Text.Substring(0, tbDate.Text.Length - 1);
                tbDate.Select(tbDate.Text.Length, 0);
                tbDate.TextChanged += TbDate_TextChanged;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string name = tbName.Text;
            if (name == null || name.Length == 0)
            {
                return;
            }

            object day;
            if (cbType.SelectedIndex == 0) // weekly
            {
                switch (cbDays.SelectedIndex)
                {
                    case 0:
                        day = DayOfWeek.Monday;
                        break;
                    case 1:
                        day = DayOfWeek.Tuesday;
                        break;
                    case 2:
                        day = DayOfWeek.Wednesday;
                        break;
                    case 3:
                        day = DayOfWeek.Thursday;
                        break;
                    case 4:
                        day = DayOfWeek.Friday;
                        break;
                    default:
                        return;
                }
            }
            else // monthly
            {
                if (tbDate.Text == null || tbDate.Text.Length == 0)
                {
                    return;
                }

                day = Convert.ToInt32(tbDate.Text);
            }

            Save(name, cbType.SelectedIndex, day);
            UpdateListView();

            tbName.Text = string.Empty;
            tbDate.Text = string.Empty;
            cbType.SelectedIndex = 0;
            cbDays.SelectedIndex = 0;
        }

        private void UpdateListView()
        {
            lvResult.Items.Clear();

            lvResult.BeginUpdate();

            _dataList = _dataList.OrderBy(x => x.Date).ToList();

            foreach (AlarmData data in _dataList)
            {
                ListViewItem item = new ListViewItem(data.Name);
                if (data.Type == AlarmType.Weekly)
                {
                    item.SubItems.Add(data.Day.ToString());
                }
                else
                {
                    item.SubItems.Add(data.Date.ToString());
                }

                lvResult.Items.Add(item);
            }

            lvResult.EndUpdate();
        }

        private void LoadData()
        {
            string path = Application.StartupPath + "\\info.txt";
            if (!File.Exists(path))
            {
                return;
            }

            StreamReader reader = new StreamReader(path);
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                string[] items = line.Split('\t');
                AlarmData data = new AlarmData();
                data.Name = items[0];
                data.Type = (AlarmType)Enum.Parse(typeof(AlarmType), items[1]);
                if (data.Type == AlarmType.Weekly)
                {
                    data.Day = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), items[2]);
                }
                else
                {
                    data.Date = Convert.ToInt32(items[2]);
                }

                _dataList.Add(data);
            }

            reader.Close();
            UpdateListView();
        }

        private void Save(string name, int type, object day)
        {
            AlarmData data = new AlarmData();
            data.Name = name;
            data.Type = type == 0 ? AlarmType.Weekly : AlarmType.Monthly;
            if (data.Type == AlarmType.Weekly)
            {
                data.Day = (DayOfWeek)day;
            }
            else
            {
                data.Date = (int)day;
            }

            _dataList.Add(data);

            WriteToFile();
        }

        private void WriteToFile()
        {
            StreamWriter writer = new StreamWriter(Application.StartupPath + "\\info.txt");
            foreach (var info in _dataList)
            {
                if (info.Type == AlarmType.Weekly)
                {
                    writer.WriteLine(info.Name + "\t" + info.Type + "\t" + info.Day);
                }
                else
                {
                    writer.WriteLine(info.Name + "\t" + info.Type + "\t" + info.Date);
                }
            }

            writer.Flush();
            writer.Close();
        }

        private void RunCheckThread()
        {
            new Thread(() =>
            {
                while (!_isStop)
                {
                    CheckAlarm(false);

                    double nextAlarmInMilliseconds = 0;
                    DateTime nextAlarm = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 9, 10, 0);
                    
                    if (DateTime.Now.Hour < 9 || (DateTime.Now.Hour == 9 && DateTime.Now.Minute < 10))
                    {

                    }
                    else
                    {
                        nextAlarm = nextAlarm.AddDays(1);
                    }

                    var diff = nextAlarm.Subtract(DateTime.Now);
                    nextAlarmInMilliseconds = diff.TotalMilliseconds;

                    _checkEvent.WaitOne((int)nextAlarmInMilliseconds);
                }
            }).Start();
        }

        private void CheckAlarm(bool isShowAlarm)
        {
            string result = string.Empty;

            foreach (AlarmData data in _dataList)
            {
                if (data.Type == AlarmType.Monthly) // monthly
                {
                    if (DateTime.Today.Day == data.Date)
                    {
                        result += data.Name + "\n";
                    }
                    else
                    {
                        int dateDiff = data.Date - DateTime.Today.Day;
                        if (dateDiff >= 0 && dateDiff < 3)
                        {
                            DateTime alarmDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, data.Date);
                            if (alarmDate.DayOfWeek == DayOfWeek.Saturday || alarmDate.DayOfWeek == DayOfWeek.Sunday)
                            {
                                result += data.Name + "\n";
                            }
                        }
                    }
                }
                else // weekly
                {
                    if (data.Day == DateTime.Today.DayOfWeek)
                    {
                        result += data.Name + "\n";
                    }
                }
            }

            if (result != string.Empty)
            {
                isShowAlarm = true;
                result = result.TrimEnd();
            }
            else
            {
                if (isShowAlarm)
                {
                    if (result == string.Empty)
                    {
                        result = "Today's TO-DO\n\n\n" +
                            "None!";
                    }
                }
            }

            if (isShowAlarm)
            {
                MessageBox.Show("Today's TO-DO List!!\n\n\n" + result);
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            this.Activate();
            this.ShowInTaskbar = true;
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (lvResult.SelectedItems.Count == 0)
            {
                return;
            }

            int index = lvResult.Items.IndexOf(lvResult.SelectedItems[0]);
            _dataList.RemoveAt(index);

            UpdateListView();
            WriteToFile();
        }
    }
}
