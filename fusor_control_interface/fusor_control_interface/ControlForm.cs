using System;
using System.Collections;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;

namespace fusor_control_interface
{
    enum Commands
    {
        SET_REGULATOR_SETPOINT = 1,
        SET_REGULATOR_TUNINGS,
        SET_PRESSURE_SETPOINT,
        SET_PRESSURE_TUNINGS,
        SET_PRESSURE_LIMITS,
        SET_PUMP_OUTPUT,
        SET_HV_OUTPUT,
        SET_VOLTAGE_OUTPUT,
        GET_PUMP_INPUT,
        GET_HV_INPUT,
        GET_PRESSURE_INPUT,
        GET_VOLTAGE_INPUT,
        GET_CURRENT_INPUT,
        GET_COUNT_INPUT,
    }

    enum Samples
    {
        PRESSURE_INPUT,
        VOLTAGE_INPUT,
        VOLTAGE_OUTPUT,
        COUNT_INPUT,
        CURRENT_INPUT,
    }

    enum Splines
    {
        PRESSURE_INPUT,
        PRESSURE_OUTPUT,
        VOLTAGE_OUTPUT,
        VOLTAGE_INPUT,
        COUNT_INPUT,
        CURRENT_INPUT,
    }

    enum Constants
    {
        REGULATOR_SETPOINT,
        REGULATOR_KP,
        REGULATOR_KI,
        REGULATOR_KD,
        PRESSURE_MIN,
        PRESSURE_MAX,
        PRESSURE_KP,
        PRESSURE_KI,
        PRESSURE_KD,
    }

    public partial class ControlForm : Form
    {
        public string port_name;
        public int update_interval;
        public SerialPort serial_port;
        public SortedList[] samples;
        public ArrayList[] splines;
        public double[] constants;

        public ControlForm()
        {
            InitializeComponent();

            this.Shown += new EventHandler(controlFormShown);
            this.FormClosing += new FormClosingEventHandler(controlFormClose);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            update_delagate = new UpdateDelagate(updateForm);
            cleanup_delagate = new CleanupDelagate(cleanupForm);
            log_file_dialog = new SaveFileDialog();
        }

        public void setMenu(bool value)
        {
            foreach (ToolStripMenuItem item in menuStrip1.Items)
            {
                recurseMenu(value, item);
            }
            serialToolStripMenuItem.Enabled = true;
            disconnectToolStripMenuItem.Enabled = !value;
        }

        private bool run = false;
        private SaveFileDialog log_file_dialog;
        private StreamWriter log_file;
        private Thread update_thread;
        private delegate void UpdateDelagate(bool pump, int pressure, bool hv, int voltage, int current, int count);
        private delegate void CleanupDelagate();
        private UpdateDelagate update_delagate;
        private CleanupDelagate cleanup_delagate;

        private void controlFormShown(object sender, EventArgs e)
        {
            log_file_dialog.Filter = "CSV (*.csv)|*.csv";
            log_file_dialog.DefaultExt = "csv";

            port_name = Properties.Settings.Default.PortName;
            update_interval = Properties.Settings.Default.UpdateInterval;
            log_file_dialog.FileName = Properties.Settings.Default.LogFileName;
            samples = Properties.Settings.Default.Samples;
            splines = Properties.Settings.Default.Splines;
            constants = Properties.Settings.Default.Constants;

            for (int i = 0; i < splines[(int)Splines.VOLTAGE_OUTPUT].Count; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    Console.Write(((double[])splines[(int)Splines.VOLTAGE_OUTPUT][i])[j] + " ");
                }
                Console.WriteLine();
            }

            if (samples == null)
            {
                samples = new SortedList[Enum.GetNames(typeof(Samples)).Length];

                for (int i = 0; i < Enum.GetNames(typeof(Samples)).Length; i++)
                {
                    samples[i] = new SortedList();
                }
            }

            if (splines == null)
            {
                splines = new ArrayList[Enum.GetNames(typeof(Splines)).Length];

                for (int i = 0; i < Enum.GetNames(typeof(Splines)).Length; i++)
                {
                    splines[i] = new ArrayList();
                }
            }

            if (constants == null)
            {
                constants = new double[Enum.GetNames(typeof(Constants)).Length];
            }

            foreach (ToolStripMenuItem item in updateIntervalToolStripMenuItem.DropDownItems)
            {
                item.Click += new EventHandler(updateIntervalToolStripMenuItem_Click);

                if (Convert.ToInt32(item.Tag) == update_interval)
                {
                    item.Checked = true;
                }
            }

            refreshPorts(false);
            setControls(false);
            setMenu(true);
        }

        private void controlFormClose(object sender, FormClosingEventArgs e)
        {
            if (run)
            {
                if (MessageBox.Show("The program is currently running.\nDo you really want to quit?", "Fusor Control", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    cleanupForm();
                }
                else
                {
                    e.Cancel = true;
                    return;
                }
            }

            Properties.Settings.Default.PortName = port_name;
            Properties.Settings.Default.UpdateInterval = update_interval;
            Properties.Settings.Default.LogFileName = log_file_dialog.FileName;
            Properties.Settings.Default.Samples = samples;
            Properties.Settings.Default.Splines = splines;
            Properties.Settings.Default.Constants = constants;

            Properties.Settings.Default.Save();
        }

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (port_name == null)
            {
                settingsToolStripMenuItem.ShowDropDown();
                portToolStripMenuItem.ShowDropDown();
                portToolStripMenuItem.Select();
                MessageBox.Show("No serial port selected.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (update_interval == 0)
            {
                settingsToolStripMenuItem.ShowDropDown();
                updateIntervalToolStripMenuItem.ShowDropDown();
                updateIntervalToolStripMenuItem.Select();
                MessageBox.Show("No interval selected.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (log_file_dialog.FileName == "")
            {
                settingsToolStripMenuItem.ShowDropDown();
                saveLogAsToolStripMenuItem.Select();
                MessageBox.Show("No log file specified.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            for (int i = 0; i < Enum.GetNames(typeof(Samples)).Length; i++)
            {
                if (samples[i].Count < 2)
                {
                    settingsToolStripMenuItem.ShowDropDown();
                    calibrationToolStripMenuItem.Select();
                    MessageBox.Show("Two calibration samples required per field.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            serial_port = new SerialPort(port_name, 115200);
            serial_port.ReadTimeout = 1000;
            serial_port.WriteTimeout = 1000;

            try
            {
                serial_port.Open();
            }
            catch
            {
                MessageBox.Show("Serial device not found.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (File.Exists(log_file_dialog.FileName))
            {
                if (MessageBox.Show("The current log file aready exists.\nDo you want to overwrite it?", "Fusor Control", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                {
                    if (log_file_dialog.ShowDialog() == DialogResult.Cancel)
                    {
                        return;
                    }
                }
            }

            try
            {
                log_file = new StreamWriter(log_file_dialog.FileName);
            }
            catch
            {
                MessageBox.Show("Error opening log file.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            setControls(true);
            setMenu(false);

            serialWrite((int)Commands.SET_REGULATOR_SETPOINT + " " + constants[(int)Constants.REGULATOR_SETPOINT]);
            serialWrite((int)Commands.SET_REGULATOR_TUNINGS + " " + constants[(int)Constants.REGULATOR_KP] + " " + constants[(int)Constants.REGULATOR_KI] + " " + constants[(int)Constants.REGULATOR_KD]);
            serialWrite((int)Commands.SET_PRESSURE_TUNINGS + " " + constants[(int)Constants.PRESSURE_KP] + " " + constants[(int)Constants.PRESSURE_KI] + " " + constants[(int)Constants.PRESSURE_KD]);
            serialWrite((int)Commands.SET_PRESSURE_LIMITS + " " + constants[(int)Constants.PRESSURE_MIN] + " " + constants[(int)Constants.PRESSURE_MAX]);

            update_thread = new Thread(updateThread);
            update_thread.Start();
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cleanupForm();
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            refreshPorts(true);
        }

        private void portToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem port = (ToolStripMenuItem)sender;

            port_name = port.Tag.ToString();

            foreach (ToolStripMenuItem item in portToolStripMenuItem.DropDownItems)
            {
                item.Checked = false;
            }

            port.Checked = true;
        }

        private void updateIntervalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem interval = (ToolStripMenuItem)sender;

            update_interval = Convert.ToInt32(interval.Tag);

            foreach (ToolStripMenuItem item in updateIntervalToolStripMenuItem.DropDownItems)
            {
                item.Checked = false;
            }

            interval.Checked = true;
        }

        private void saveLogAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            log_file_dialog.ShowDialog();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_PUMP_OUTPUT + " 1");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_PUMP_OUTPUT + " 0");
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_PRESSURE_SETPOINT + " " + calculateValue((int)numericUpDown1.Value, splines[(int)Splines.PRESSURE_OUTPUT]));
        }

        private void button3_Click(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_HV_OUTPUT + " 1");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_HV_OUTPUT + " 0");
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_VOLTAGE_OUTPUT + " " + calculateValue((int)numericUpDown2.Value, splines[(int)Splines.VOLTAGE_OUTPUT]));
        }

        private void calibrationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CalibrationForm form2 = new CalibrationForm(this);

            if (port_name == null)
            {
                settingsToolStripMenuItem.ShowDropDown();
                portToolStripMenuItem.ShowDropDown();
                portToolStripMenuItem.Select();
                MessageBox.Show("No serial port selected.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (update_interval == 0)
            {
                settingsToolStripMenuItem.ShowDropDown();
                updateIntervalToolStripMenuItem.ShowDropDown();
                updateIntervalToolStripMenuItem.Select();
                MessageBox.Show("No interval selected.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            serial_port = new SerialPort(port_name, 115200);
            serial_port.ReadTimeout = 1000;
            serial_port.WriteTimeout = 1000;

            try
            {
                serial_port.Open();
            }
            catch
            {
                MessageBox.Show("Serial device not found.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            setMenu(false);
            serialToolStripMenuItem.Enabled = false;
            disconnectToolStripMenuItem.Enabled = false;
            form2.Show();
        }

        private void refreshPorts(bool value)
        {
            bool found = false;
            string[] ports = SerialPort.GetPortNames();
            ToolStripMenuItem[] items = new ToolStripMenuItem[ports.Length + 1];

            items[0] = new ToolStripMenuItem();
            items[0].Name = "refreshToolStripMenuItem";
            items[0].Text = "Refresh";
            items[0].ShortcutKeys = Keys.Control | Keys.R;
            items[0].Click += new EventHandler(refreshToolStripMenuItem_Click);

            for (int i = 1; i < items.Length; i++)
            {
                items[i] = new ToolStripMenuItem();
                items[i].Name = "portToolStripMenuItem" + i;
                items[i].Text = ports[i - 1];
                items[i].Tag = ports[i - 1];
                items[i].Click += new EventHandler(portToolStripMenuItem_Click);

                if (ports[i - 1] == port_name)
                {
                    items[i].Checked = true;
                    found = true;
                }
            }

            if (!found)
            {
                port_name = null;
            }

            portToolStripMenuItem.DropDownItems.Clear();
            portToolStripMenuItem.DropDownItems.AddRange(items);

            if (value)
            {
                settingsToolStripMenuItem.ShowDropDown();
                portToolStripMenuItem.ShowDropDown();
            }
        }

        private void setControls(bool value)
        {
            foreach (Control control in this.Controls)
            {
                control.Enabled = value;
            }
            menuStrip1.Enabled = true;
        }

        private void recurseMenu(bool value, ToolStripMenuItem item)
        {
            if (item.DropDownItems.Count > 0)
            {
                foreach (ToolStripMenuItem child_item in item.DropDownItems)
                {
                    recurseMenu(value, child_item);
                }
            }
            item.Enabled = value;
        }

        private void updateThread()
        {
            bool pump, hv;
            int pressure, voltage, current, count, sleep;
            ulong time = 0;
            Stopwatch stopwatch = new Stopwatch();

            run = true;
            log_file.WriteLine("Time (ms),Pressure (mTorr),Voltage (kV),Current (mA),Neutron Count");

            while (run)
            {
                stopwatch.Restart();

                try
                {
                    serial_port.WriteLine(Convert.ToString((int)Commands.GET_PUMP_INPUT));
                    pump = serial_port.ReadLine().Substring(0, 1) == "1";

                    serial_port.WriteLine(Convert.ToString((int)Commands.GET_PRESSURE_INPUT));
                    pressure = calculateValue(Convert.ToInt32(serial_port.ReadLine()), splines[(int)Splines.PRESSURE_INPUT]);

                    serial_port.WriteLine(Convert.ToString((int)Commands.GET_HV_INPUT));
                    hv = serial_port.ReadLine().Substring(0, 1) == "1";

                    serial_port.WriteLine(Convert.ToString((int)Commands.GET_VOLTAGE_INPUT));
                    voltage = calculateValue(Convert.ToInt32(serial_port.ReadLine()), splines[(int)Splines.VOLTAGE_INPUT]);

                    serial_port.WriteLine(Convert.ToString((int)Commands.GET_CURRENT_INPUT));
                    current = calculateValue(Convert.ToInt32(serial_port.ReadLine()), splines[(int)Splines.CURRENT_INPUT]);

                    serial_port.WriteLine(Convert.ToString((int)Commands.GET_COUNT_INPUT));
                    count = calculateValue(Convert.ToInt32(serial_port.ReadLine()), splines[(int)Splines.COUNT_INPUT]);

                    this.BeginInvoke(update_delagate, pump, pressure, hv, voltage, current, count);

                    log_file.WriteLine(time + "," + pressure + "," + voltage + "," + current + "," + count);
                }
                catch
                {
                    MessageBox.Show("Serial port disconnected.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.BeginInvoke(cleanup_delagate);
                    return;
                }

                time += (ulong)update_interval;
                sleep = (int)(update_interval - stopwatch.ElapsedMilliseconds);

                if (sleep > 0)
                {
                    Thread.Sleep(sleep);
                }
            }
        }

        private void updateForm(bool pump, int pressure, bool hv, int voltage, int current, int count)
        {
            label2.Text = "Status: " + (pump ? "Normal" : "Accelerating/Stopped");
            label4.Text = "mTorr: " + pressure;
            label7.Text = "HV: " + (hv ? "On" : "Off");
            label8.Text = "kV: " + voltage;
            label9.Text = "mA: " + current;
            label10.Text = "Count: " + count;
        }

        private void cleanupForm()
        {
            run = false;
            update_thread.Join();
            serial_port.Dispose();
            log_file.Dispose();
            setControls(false);
            setMenu(true);
        }

        private void serialWrite(string message)
        {
            try
            {
                serial_port.WriteLine(message);
            }
            catch
            {
                MessageBox.Show("Serial port disconnected.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
                cleanupForm();
            }
        }

        private int calculateValue(int input, ArrayList spline)
        {
            int spline_index, output = 0;

            for (spline_index = 0; spline_index < spline.Count; spline_index++)
            {
                if (input <= ((double[])spline[spline_index])[4])
                {
                    break;
                }
            }

            if (spline_index == spline.Count)
            {
                spline_index--;
            }

            for (int i = 0; i < 4; i++)
            {
                output += (int)Math.Round(((double[])spline[spline_index])[i] * Math.Pow(input - ((double[])spline[spline_index])[4], i));
            }

            return output;
        }
    }
}