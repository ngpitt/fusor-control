using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace fusor_control_interface
{
    public partial class Form1 : Form
    {
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

        private bool run = false, log = false;
        private string port_name = null;
        private ushort update_interval = 250;
        private ulong time;
        private SafeSerialPort serial_port;
        private Thread update_thread = null;
        private StreamWriter log_file = null;
        private SaveFileDialog save_file_dialog;
        private delegate void StatusUpdateDelagate(bool pump_status, double pressure, bool hv_status, double voltage, double current, double scaler_rate);
        private delegate void CleanupDelagate();
        private StatusUpdateDelagate update_delagate;
        private CleanupDelagate cleanup_delagate;

        private void UpdateStatus(bool pump_status, double pressure, bool hv_status, double voltage, double current, double scaler_rate)
        {
            label2.Text = "Status: " + (pump_status ? "Normal" : "Accelerating/Stopped");
            label4.Text = "mTorr: " + pressure.ToString("f1");
            label7.Text = "HV: " + (hv_status ? "On" : "Off");
            label8.Text = "kV: " + voltage.ToString("f1");
            label9.Text = "mA: " + current.ToString("f1");
            label10.Text = "Rate: " + scaler_rate;
        }

        private void Cleanup()
        {
            run = false;
            update_thread.Join();
            serial_port.Dispose();

            if (log_file != null)
            {
                log_file.Close();
                log_file = null;
            }

            SetControls(false);
            menuStrip1.Enabled = true;
            portsToolStripMenuItem.Enabled = true;
            connectToolStripMenuItem.Enabled = true;
            disconnectToolStripMenuItem.Enabled = false;
            loggingToolStripMenuItem.Enabled = true;
            updateIntervalToolStripMenuItem.Enabled = true;
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
                MessageBox.Show("Serial port disconnected.", "Reactor Control", MessageBoxButtons.OK);
            }
        }

        private void UpdateThread()
        {
            bool pump_status, hv_status;
            short sleep;
            double pressure, voltage, current, scaler_rate;
            Stopwatch stopwatch = new Stopwatch();

            run = true;
            time = 0;

            if (log_file != null)
            {
                log_file.WriteLine("Time (ms),Pressure (mTorr),Voltage (kV),Current (mA),Scaler Rate");
            }

            while (run)
            {
                stopwatch.Restart();

                try
                {
                    serial_port.WriteLine("0");
                    pump_status = (serial_port.ReadLine().Substring(0, 1) == "1");
                    serial_port.WriteLine("1");
                    pressure = Convert.ToDouble(serial_port.ReadLine());
                    serial_port.WriteLine("2");
                    hv_status = (serial_port.ReadLine().Substring(0, 1) == "1");
                    serial_port.WriteLine("3");
                    voltage = Convert.ToDouble(serial_port.ReadLine());
                    serial_port.WriteLine("4");
                    current = Convert.ToDouble(serial_port.ReadLine());
                    serial_port.WriteLine("5");
                    scaler_rate = Convert.ToDouble(serial_port.ReadLine());

                    this.BeginInvoke(update_delagate, pump_status, pressure, hv_status, voltage, current, scaler_rate);

                    if (log_file != null)
                    {
                        log_file.WriteLine(time + "," + pressure + "," + voltage + "," + current + "," + scaler_rate);
                    }
                }
                catch
                {
                    this.BeginInvoke(cleanup_delagate);
                    MessageBox.Show("Serial port disconnected.", "Reactor Control", MessageBoxButtons.OK);
                }

                time += update_interval;
                sleep = (short)(update_interval - stopwatch.ElapsedMilliseconds);

                if (sleep > 0)
                {
                    Thread.Sleep(sleep);
                }
            }
        }

        private void SetControls(bool value)
        {
            foreach (Control control in this.Controls)
            {
                control.Enabled = value;
            }
        }

        private void RefreshPorts()
        {
            bool found = false;
            string[] ports = SafeSerialPort.GetPortNames();
            ToolStripMenuItem[] items = new ToolStripMenuItem[ports.Length + 1];

            items[0] = new ToolStripMenuItem();
            items[0].Text = "Refresh";
            items[0].ShortcutKeys = Keys.Control | Keys.R;
            items[0].Click += new EventHandler(refreshToolStripMenuItem_Click);

            for (int i = 1; i < items.Length; i++)
            {
                items[i] = new ToolStripMenuItem();
                items[i].Text = ports[i - 1];
                items[i].Tag = ports[i - 1];
                items[i].Click += new EventHandler(portsToolStripMenuItem_Click);

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

            portsToolStripMenuItem.DropDownItems.Clear();
            portsToolStripMenuItem.DropDownItems.AddRange(items);
            serialToolStripMenuItem.ShowDropDown();
            portsToolStripMenuItem.ShowDropDown();
        }

        public Form1()
        {
            InitializeComponent();

            this.FormClosing += new FormClosingEventHandler(Form1_Close);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            save_file_dialog = new SaveFileDialog();
            save_file_dialog.Filter = "CSV (*.csv)|*.csv";
            save_file_dialog.DefaultExt = "csv";

            update_delagate = new StatusUpdateDelagate(UpdateStatus);
            cleanup_delagate = new CleanupDelagate(Cleanup);

            RefreshPorts();

            foreach (ToolStripMenuItem item in updateIntervalToolStripMenuItem.DropDownItems)
            {
                item.Click += new EventHandler(updateIntervalToolStripMenuItem_Click);
            }

            SetControls(false);
            menuStrip1.Enabled = true;
            disconnectToolStripMenuItem.Enabled = false;
        }

        private void Form1_Close(object sender, FormClosingEventArgs e)
        {
            if (update_thread != null && update_thread.IsAlive)
            {
                if (MessageBox.Show("Do you really want to quit?", "Reactor Control", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Cleanup();
                }
                else
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!run)
            {
                RefreshPorts();
            }
        }

        private void portsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem port = (ToolStripMenuItem)sender;

            port_name = port.Tag.ToString();

            foreach (ToolStripMenuItem item in portsToolStripMenuItem.DropDownItems)
            {
                item.Checked = false;
            }

            port.Checked = true;

            serialToolStripMenuItem.ShowDropDown();
        }

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (port_name == null)
            {
                serialToolStripMenuItem.ShowDropDown();
                portsToolStripMenuItem.ShowDropDown();
                MessageBox.Show("No serial port selected.", "Reactor Control", MessageBoxButtons.OK);
            }
            else
            {
                serial_port = new SafeSerialPort(port_name, 115200);
                serial_port.ReadTimeout = 1000;
                serial_port.WriteTimeout = 1000;

                try
                {
                    serial_port.Open();
                }
                catch
                {
                    MessageBox.Show("Serial device not found.", "Reactor Control", MessageBoxButtons.OK);
                }

                if (log)
                {
                    try
                    {
                        log_file = new StreamWriter(save_file_dialog.FileName);
                    }
                    catch
                    {
                        MessageBox.Show("Error opening log file.", "Reactor Control", MessageBoxButtons.OK);
                    }
                    log = false;
                }

                update_thread = new Thread(UpdateThread);
                update_thread.Start();

                SetControls(true);
                portsToolStripMenuItem.Enabled = false;
                connectToolStripMenuItem.Enabled = false;
                disconnectToolStripMenuItem.Enabled = true;
                loggingToolStripMenuItem.Enabled = false;
                updateIntervalToolStripMenuItem.Enabled = false;
            }
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cleanup();
        }

        private void saveToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (save_file_dialog.ShowDialog() == DialogResult.OK)
            {
                log = true;
            }
        }

        private void updateIntervalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem interval = (ToolStripMenuItem)sender;

            update_interval = Convert.ToUInt16(interval.Tag.ToString());

            foreach (ToolStripMenuItem item in updateIntervalToolStripMenuItem.DropDownItems)
            {
                item.Checked = false;
            }

            interval.Checked = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SerialWrite("6 1");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SerialWrite("6 0");
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            SerialWrite("7 " + numericUpDown1.Value);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SerialWrite("8 1");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SerialWrite("8 0");
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            SerialWrite("9 " + numericUpDown2.Value);
        }
    }
}