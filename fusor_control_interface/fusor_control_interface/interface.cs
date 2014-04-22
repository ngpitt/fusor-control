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

        private bool run = true, log = false;
        private string port_name = null;
        private ushort update_interval = 250;
        private ulong time = 0;
        private SafeSerialPort serial_port;
        private Thread update_thread = null;
        private StreamWriter log_file = null;
        private SaveFileDialog save_file_dialog;
        private delegate void StatusUpdateDelagate(bool pump_status, double pressure, bool hv_status, double voltage, double current, double scaler_rate);
        private StatusUpdateDelagate update_delagate;

        private void UpdateStatus(bool pump_status, double pressure, bool hv_status, double voltage, double current, double scaler_rate)
        {
            label2.Text = "Status: " + (pump_status ? "Normal" : "Accelerating/Stopped");
            label4.Text = "mTorr: " + pressure.ToString("f1");
            label7.Text = "HV: " + (hv_status ? "On" : "Off");
            label8.Text = "kV: " + voltage.ToString("f1");
            label9.Text = "mA: " + current.ToString("f1");
            label10.Text = "Rate: " + scaler_rate;
        }

        private void SerialWrite(string message)
        {
            try
            {
                serial_port.WriteLine(message);
            }
            catch
            {
                run = false;
                update_thread.Join();
                serial_port.Dispose();
                if (log_file != null)
                {
                    log_file.Close();
                    log_file = null;
                }
                SetControls(this, false);
                menuStrip1.Enabled = true;
                portsToolStripMenuItem.Enabled = true;
                connectToolStripMenuItem.Enabled = true;
                disconnectToolStripMenuItem.Enabled = false;
                loggingToolStripMenuItem.Enabled = true;
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
            if (log_file != null)
            {
                log_file.WriteLine("Time (ms),Pressure (mTorr),Voltage (kV),Current (mA),Scaler Rate");
            }
            while (run)
            {
                stopwatch.Restart();
                try
                {
                    serial_port.WriteLine("get pump status");
                    pump_status = (serial_port.ReadLine().Substring(0, 1) == "1");
                    serial_port.WriteLine("get pressure");
                    pressure = Convert.ToDouble(serial_port.ReadLine());
                    serial_port.WriteLine("get hv status");
                    hv_status = (serial_port.ReadLine().Substring(0, 1) == "1");
                    serial_port.WriteLine("get voltage");
                    voltage = Convert.ToDouble(serial_port.ReadLine());
                    serial_port.WriteLine("get current");
                    current = Convert.ToDouble(serial_port.ReadLine());
                    serial_port.WriteLine("get scaler rate");
                    scaler_rate = Convert.ToDouble(serial_port.ReadLine());
                    if (run)
                    {
                        this.Invoke(update_delagate, pump_status, pressure, hv_status, voltage, current, scaler_rate);
                    }
                    if (log_file != null)
                    {
                        log_file.WriteLine(time + "," + pressure + "," + voltage + "," + current + "," + scaler_rate);
                    }
                }
                catch
                {
                    run = false;
                }
                time += update_interval;
                sleep = (short)(update_interval - stopwatch.ElapsedMilliseconds);
                if (sleep > 0)
                {
                    Thread.Sleep(sleep);
                }
            }
        }

        private void SetControls(Control controls, bool value)
        {
            foreach (Control control in controls.Controls)
            {
                control.Enabled = value;
            }
        }

        private void RefreshPorts(object sender, EventArgs e)
        {
            bool found = false;
            string[] ports = SafeSerialPort.GetPortNames();
            ToolStripMenuItem[] items = new ToolStripMenuItem[ports.Length + 1];

            items[0] = new ToolStripMenuItem();
            items[0].Text = "Refresh";
            items[0].ShortcutKeys = Keys.Control | Keys.R;
            items[0].Click += new EventHandler(RefreshPorts);
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
            save_file_dialog.Filter = "csv file (*.csv)|*.csv";
            update_delagate = new StatusUpdateDelagate(UpdateStatus);
            RefreshPorts(null, null);
            foreach (ToolStripMenuItem item in updateIntervalToolStripMenuItem.DropDownItems)
            {
                item.Click += new EventHandler(updateIntervalToolStripMenuItem_Click);
            }
            SetControls(this, false);
            menuStrip1.Enabled = true;
            disconnectToolStripMenuItem.Enabled = false;
        }

        private void Form1_Close(object sender, FormClosingEventArgs e)
        {
            if (update_thread != null && update_thread.IsAlive)
            {
                if (MessageBox.Show("Do you really want to quit?", "Reactor Control", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    run = false;
                    update_thread.Join();
                    serial_port.Close();
                }
                else
                {
                    e.Cancel = true;
                    return;
                }
            }
            if (log_file != null)
            {
                log_file.Close();
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
                MessageBox.Show("No serial port selected.", "Reactor Control", MessageBoxButtons.OK);
            }
            else
            {
                serial_port = new SafeSerialPort(port_name, 57600);
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
                time = 0;
                update_thread = new Thread(UpdateThread);
                update_thread.Start();
                SetControls(this, true);
                portsToolStripMenuItem.Enabled = false;
                connectToolStripMenuItem.Enabled = false;
                disconnectToolStripMenuItem.Enabled = true;
                loggingToolStripMenuItem.Enabled = false;
            }
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            run = false;
            update_thread.Join();
            serial_port.Close();
            if (log_file != null)
            {
                log_file.Close();
                log_file = null;
            }
            SetControls(this, false);
            menuStrip1.Enabled = true;
            portsToolStripMenuItem.Enabled = true;
            connectToolStripMenuItem.Enabled = true;
            disconnectToolStripMenuItem.Enabled = false;
            loggingToolStripMenuItem.Enabled = true;
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
            SerialWrite("set pump 1");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SerialWrite("set pump 0");
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            SerialWrite(numericUpDown1.Value.ToString());
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SerialWrite("set hv 1");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SerialWrite("set hv 0");
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            SerialWrite("set voltage " + numericUpDown2.Value);
        }
    }
}
