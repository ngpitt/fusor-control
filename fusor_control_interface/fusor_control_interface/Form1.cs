using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;

namespace fusor_control_interface
{
    public partial class Form1 : Form
    {
        public static int
            SET_REGULATOR_SETPOINT = 1,
            SET_REGULATOR_TUNING = 2,
            SET_PRESSURE_SETPOINT = 3,
            SET_PRESSURE_TUNING = 4,
            SET_PRESSURE_LIMITS = 5,
            SET_PUMP_OUTPUT = 6,
            SET_HV_OUTPUT = 7,
            SET_VOLTAGE_OUTPUT = 8,
            GET_PUMP_INPUT = 9,
            GET_HV_INPUT = 10,
            GET_PRESSURE_INPUT = 11,
            GET_VOLTAGE_INPUT = 12,
            GET_CURRENT_INPUT = 13,
            GET_COUNT_INPUT = 14;
        public string port_name;
        public int update_interval;
        public SafeSerialPort serial_port;
        public double pressure_min, pressure_max, pressure_kp, pressure_ki, pressure_kd, regulator_setpoint, regulator_kp, regulator_ki, regulator_kd;
        public List<double[]> pressure_samples, count_samples, voltage_output_samples, voltage_input_samples, current_input_samples,
            forward_pressure_spline, reverse_pressure_spline, count_spline, voltage_output_spline, voltage_input_spline, current_input_spline;

        public Form1()
        {
            InitializeComponent();

            this.FormClosing += new FormClosingEventHandler(Form1_Close);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            update_delagate = new UpdateDelagate(UpdateForm);
            cleanup_delagate = new CleanupDelagate(Cleanup);

            log_file_dialog = new SaveFileDialog();
            log_file_dialog.Filter = "CSV (*.csv)|*.csv";
            log_file_dialog.DefaultExt = "csv";

            port_name = Properties.Settings.Default.PortName;
            update_interval = Properties.Settings.Default.UpdateInterval;
            log_file_dialog.FileName = Properties.Settings.Default.LogFileName;

            pressure_samples = Deserialize<List<double[]>>(Properties.Settings.Default.PressureSamples);
            count_samples = Deserialize<List<double[]>>(Properties.Settings.Default.CountSamples);
            voltage_output_samples = Deserialize<List<double[]>>(Properties.Settings.Default.VoltageOutputSamples);
            voltage_input_samples = Deserialize<List<double[]>>(Properties.Settings.Default.VoltageInputSamples);
            current_input_samples = Deserialize<List<double[]>>(Properties.Settings.Default.CurrentInputSamples);

            forward_pressure_spline = Deserialize<List<double[]>>(Properties.Settings.Default.ForwardPressureSpline);
            reverse_pressure_spline = Deserialize<List<double[]>>(Properties.Settings.Default.ReversePressureSpline);
            count_spline = Deserialize<List<double[]>>(Properties.Settings.Default.CountSpline);
            voltage_output_spline = Deserialize<List<double[]>>(Properties.Settings.Default.VoltageOutputSpline);
            voltage_input_spline = Deserialize<List<double[]>>(Properties.Settings.Default.VoltageInputSpline);
            current_input_spline = Deserialize<List<double[]>>(Properties.Settings.Default.CurrentInputSpline);

            pressure_min = Properties.Settings.Default.PressureMin;
            pressure_max = Properties.Settings.Default.PressureMax;
            pressure_kp = Properties.Settings.Default.PressureKp;
            pressure_ki = Properties.Settings.Default.PressureKi;
            pressure_kd = Properties.Settings.Default.PressureKd;
            regulator_setpoint = Properties.Settings.Default.RegulatorSetpoint;
            regulator_kp = Properties.Settings.Default.RegulatorKp;
            regulator_ki = Properties.Settings.Default.RegulatorKi;
            regulator_kd = Properties.Settings.Default.RegulatorKd;

            foreach (ToolStripMenuItem item in updateIntervalToolStripMenuItem.DropDownItems)
            {
                item.Click += new EventHandler(updateIntervalToolStripMenuItem_Click);

                if (Convert.ToInt32(item.Tag) == update_interval)
                {
                    item.Checked = true;
                }
            }

            RefreshPorts();
            SetControls(false);
            disconnectToolStripMenuItem.Enabled = false;
        }

        private bool run = false;
        private SaveFileDialog log_file_dialog;
        private StreamWriter log_file;
        private Thread update_thread;
        private delegate void UpdateDelagate(bool pump, int pressure, bool hv, int voltage, int current, int count);
        private delegate void CleanupDelagate();
        private UpdateDelagate update_delagate;
        private CleanupDelagate cleanup_delagate;

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (port_name == null)
            {
                settingsToolStripMenuItem.ShowDropDown();
                portToolStripMenuItem.ShowDropDown();
                portToolStripMenuItem.Select();
                MessageBox.Show("No serial port selected.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            if (update_interval == 0)
            {
                settingsToolStripMenuItem.ShowDropDown();
                updateIntervalToolStripMenuItem.ShowDropDown();
                updateIntervalToolStripMenuItem.Select();
                MessageBox.Show("No interval selected.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            if (log_file_dialog.FileName == "")
            {
                settingsToolStripMenuItem.ShowDropDown();
                saveLogAsToolStripMenuItem.Select();
                MessageBox.Show("No log file specified.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            if (File.Exists(log_file_dialog.FileName))
            {
                if (MessageBox.Show("The current log file aready exists.\nDo you want to overwrite it?", "Fusor Control", MessageBoxButtons.YesNo) == DialogResult.No)
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
                MessageBox.Show("Error opening log file.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            serial_port = new SafeSerialPort(port_name, 115200);
            serial_port.ReadTimeout = 1000;
            serial_port.WriteTimeout = 1000;

            try
            {
                serial_port.Open();
            }
            catch
            {
                MessageBox.Show("Serial device not found.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            SetControls(true);
            SetMenu(false);
            disconnectToolStripMenuItem.Enabled = true;

            update_thread = new Thread(UpdateThread);
            update_thread.Start();
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cleanup();
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshPorts();
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
            SerialWrite(SET_PUMP_OUTPUT + " 1");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SerialWrite(SET_PUMP_OUTPUT + " 0");
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            SerialWrite(SET_PRESSURE_SETPOINT + " " + numericUpDown1.Value);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SerialWrite(SET_HV_OUTPUT + " 1");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SerialWrite(SET_HV_OUTPUT + " 0");
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            SerialWrite(SET_VOLTAGE_OUTPUT + " " + numericUpDown2.Value);
        }


        private void calibrationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form2 form2;

            if (port_name == null)
            {
                settingsToolStripMenuItem.ShowDropDown();
                portToolStripMenuItem.ShowDropDown();
                portToolStripMenuItem.Select();
                MessageBox.Show("No serial port selected.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            if (update_interval == 0)
            {
                settingsToolStripMenuItem.ShowDropDown();
                updateIntervalToolStripMenuItem.ShowDropDown();
                updateIntervalToolStripMenuItem.Select();
                MessageBox.Show("No interval selected.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            serial_port = new SafeSerialPort(port_name, 115200);
            serial_port.ReadTimeout = 1000;
            serial_port.WriteTimeout = 1000;

            try
            {
                serial_port.Open();
            }
            catch
            {
                MessageBox.Show("Serial device not found.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            form2 = new Form2(this);
            form2.Show();
        }

        private void Form1_Close(object sender, FormClosingEventArgs e)
        {
            if (run)
            {
                if (MessageBox.Show("Do you really want to quit?", "Fusor Control", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Cleanup();
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

            Properties.Settings.Default.PressureSamples = Serialize(pressure_samples);
            Properties.Settings.Default.CountSamples = Serialize(count_samples);
            Properties.Settings.Default.VoltageOutputSamples = Serialize(voltage_output_samples);
            Properties.Settings.Default.VoltageInputSamples = Serialize(voltage_input_samples);
            Properties.Settings.Default.CurrentInputSamples = Serialize(current_input_samples);

            Properties.Settings.Default.ForwardPressureSpline = Serialize(forward_pressure_spline);
            Properties.Settings.Default.ReversePressureSpline = Serialize(reverse_pressure_spline);
            Properties.Settings.Default.CountSpline = Serialize(count_spline);
            Properties.Settings.Default.VoltageOutputSpline = Serialize(voltage_output_spline);
            Properties.Settings.Default.VoltageInputSpline = Serialize(voltage_input_spline);
            Properties.Settings.Default.CurrentInputSpline = Serialize(current_input_spline);

            Properties.Settings.Default.PressureMin = pressure_min;
            Properties.Settings.Default.PressureMax = pressure_max;
            Properties.Settings.Default.PressureKp = pressure_kp;
            Properties.Settings.Default.PressureKi = pressure_ki;
            Properties.Settings.Default.PressureKd = pressure_kd;
            Properties.Settings.Default.RegulatorSetpoint = regulator_setpoint;
            Properties.Settings.Default.RegulatorKp = regulator_kp;
            Properties.Settings.Default.RegulatorKi = regulator_ki;
            Properties.Settings.Default.RegulatorKd = regulator_kd;

            Properties.Settings.Default.Save();
        }

        private T Deserialize<T>(string input) where T : new()
        {
            if (input == null)
            {
                return new T();
            }
            return (T)Deserialize(input, typeof(T));
        }

        private object Deserialize(string input, Type type)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(type);
            StringReader stringReader = new StringReader(input);

            return xmlSerializer.Deserialize(stringReader);
        }

        private string Serialize(object input)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(input.GetType());
            StringWriter textWriter = new StringWriter();

            xmlSerializer.Serialize(textWriter, input);

            return textWriter.ToString();
        }

        private void RefreshPorts()
        {
            bool found = false;
            string[] ports = SafeSerialPort.GetPortNames();
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
            settingsToolStripMenuItem.ShowDropDown();
            portToolStripMenuItem.ShowDropDown();
        }

        private void SetControls(bool value)
        {
            foreach (Control control in this.Controls)
            {
                control.Enabled = value;
            }
            menuStrip1.Enabled = true;
        }

        private void SetMenu(bool value)
        {
            foreach (ToolStripMenuItem item in menuStrip1.Items)
            {
                RecurseMenu(value, item);
            }
            serialToolStripMenuItem.Enabled = true;
        }

        private void RecurseMenu(bool value, ToolStripMenuItem item)
        {
            if (item.DropDownItems.Count > 0)
            {
                foreach (ToolStripMenuItem child_item in item.DropDownItems)
                {
                    RecurseMenu(value, child_item);
                }
            }
            item.Enabled = value;
        }

        private void UpdateThread()
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
                    serial_port.WriteLine(GET_PUMP_INPUT.ToString());
                    pump = (serial_port.ReadLine().Substring(0, 1) == "1");
                    serial_port.WriteLine(GET_PRESSURE_INPUT.ToString());
                    pressure = Convert.ToInt32(serial_port.ReadLine());
                    serial_port.WriteLine(GET_HV_INPUT.ToString());
                    hv = (serial_port.ReadLine().Substring(0, 1) == "1");
                    serial_port.WriteLine(GET_VOLTAGE_INPUT.ToString());
                    voltage = Convert.ToInt32(serial_port.ReadLine());
                    serial_port.WriteLine(GET_CURRENT_INPUT.ToString());
                    current = Convert.ToInt32(serial_port.ReadLine());
                    serial_port.WriteLine(GET_COUNT_INPUT.ToString());
                    count = Convert.ToInt32(serial_port.ReadLine());

                    this.BeginInvoke(update_delagate, pump, pressure, hv, voltage, current, count);
                    log_file.WriteLine(time + "," + pressure + "," + voltage + "," + current + "," + count);
                }
                catch
                {
                    this.BeginInvoke(cleanup_delagate);
                    MessageBox.Show("Serial port disconnected.", "Fusor Control", MessageBoxButtons.OK);
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

        private void UpdateForm(bool pump, int pressure, bool hv, int voltage, int current, int count)
        {
            label2.Text = "Status: " + (pump ? "Normal" : "Accelerating/Stopped");
            label4.Text = "mTorr: " + pressure;
            label7.Text = "HV: " + (hv ? "On" : "Off");
            label8.Text = "kV: " + voltage;
            label9.Text = "mA: " + current;
            label10.Text = "Count: " + count;
        }

        private void Cleanup()
        {
            run = false;
            update_thread.Join();
            serial_port.Close();
            serial_port.Dispose();
            log_file.Close();
            SetControls(false);
            SetMenu(true);
            disconnectToolStripMenuItem.Enabled = false;
        }

        private void SerialWrite(string message)
        {
            try
            {
                serial_port.WriteLine(message);
            }
            catch
            {
                Cleanup();
                MessageBox.Show("Serial port disconnected.", "Fusor Control", MessageBoxButtons.OK);
            }
        }
    }

    public class SafeSerialPort : SerialPort
    {
        private Stream theBaseStream;

        public SafeSerialPort(string portName, int baudRate)
            : base(portName, baudRate)
        {

        }

        public new void Open()
        {
            try
            {
                base.Open();
                theBaseStream = BaseStream;
                GC.SuppressFinalize(BaseStream);
            }
            catch
            {

            }
        }

        public new void Dispose()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (base.Container != null))
            {
                base.Container.Dispose();
            }
            try
            {
                if (theBaseStream.CanRead)
                {
                    theBaseStream.Close();
                    GC.ReRegisterForFinalize(theBaseStream);
                }
            }
            catch
            {

            }
            base.Dispose(disposing);
        }
    }
}