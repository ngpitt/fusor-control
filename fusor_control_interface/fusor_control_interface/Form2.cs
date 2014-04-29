using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Linq;

namespace fusor_control_interface
{
    public partial class Form2 : Form
    {
        public Form2(Form1 form1)
        {
            InitializeComponent();

            this.form1 = form1;
            this.FormClosing += new FormClosingEventHandler(Form2_Close);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            update_delagate = new UpdateDelagate(UpdateForm);
            cleanup_delagate = new CleanupDelagate(Cleanup);

            label11.Text = "Samples: " + form1.pressure_samples.Count;
            label15.Text = "Samples: " + form1.voltage_output_samples.Count;
            label24.Text = "Samples: " + form1.voltage_input_samples.Count;
            label28.Text = "Samples: " + form1.count_samples.Count;
            label32.Text = "Samples: " + form1.current_input_samples.Count;

            update_thread = new Thread(UpdateThread);
            update_thread.Start();
        }

        private bool run = false;
        private double pressure, count, voltage, current;
        private Form1 form1;
        private Thread update_thread;
        private delegate void UpdateDelagate(bool pump, bool hv, double pressure, double count, double voltage, double current);
        private delegate void CleanupDelagate();
        private UpdateDelagate update_delagate;
        private CleanupDelagate cleanup_delagate;

        private void button1_Click(object sender, EventArgs e)
        {
            SerialWrite(Form1.SET_PUMP_OUTPUT + " 1");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SerialWrite(Form1.SET_PUMP_OUTPUT + " 0");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SerialWrite(Form1.SET_HV_OUTPUT + " 1");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SerialWrite(Form1.SET_HV_OUTPUT + " 0");
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            form1.regulator_setpoint = (double)numericUpDown1.Value;
            SerialWrite(Form1.SET_REGULATOR_SETPOINT + " " + form1.regulator_setpoint);
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            form1.regulator_kp = (double)numericUpDown2.Value;
            SerialWrite(Form1.SET_REGULATOR_TUNING + " " + form1.regulator_kp + " " + form1.regulator_ki + " " + form1.regulator_kd);
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            form1.regulator_ki = (double)numericUpDown3.Value;
            SerialWrite(Form1.SET_REGULATOR_TUNING + " " + form1.regulator_kp + " " + form1.regulator_ki + " " + form1.regulator_kd);
        }

        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            form1.regulator_kd = (double)numericUpDown4.Value;
            SerialWrite(Form1.SET_REGULATOR_TUNING + " " + form1.regulator_kp + " " + form1.regulator_ki + " " + form1.regulator_kd);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            List<double[]> reverse_pressure_samples = new List<double[]>();

            try
            {
                form1.pressure_samples.Add(new double[] { pressure, (double)numericUpDown5.Value });
            }
            catch
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            for (int i = 0; i < form1.pressure_samples.Count; i++)
            {
                reverse_pressure_samples.Add(new double[] { form1.pressure_samples[i][1], form1.pressure_samples[i][0] });
            }

            CalculateSpline(form1.pressure_samples, form1.forward_pressure_spline);
            CalculateSpline(reverse_pressure_samples, form1.reverse_pressure_spline);

            label11.Text = "Samples: " + form1.pressure_samples.Count;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            form1.pressure_samples.Clear();
            form1.forward_pressure_spline.Clear();
            form1.reverse_pressure_spline.Clear();

            label11.Text = "Samples: " + form1.pressure_samples.Count;
        }

        private void numericUpDown6_ValueChanged(object sender, EventArgs e)
        {
            SerialWrite(Form1.SET_VOLTAGE_OUTPUT + " " + numericUpDown6.Value);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            try
            {
                form1.voltage_output_samples.Add(new double[] { (double)numericUpDown6.Value, (double)numericUpDown7.Value });
            }
            catch
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            CalculateSpline(form1.voltage_output_samples, form1.voltage_output_spline);

            label15.Text = "Samples: " + form1.voltage_output_samples.Count;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            form1.voltage_output_samples.Clear();
            form1.voltage_output_spline.Clear();

            label15.Text = "Samples: " + form1.voltage_output_samples.Count;
        }

        private void numericUpDown8_ValueChanged(object sender, EventArgs e)
        {
            form1.pressure_min = (double)numericUpDown8.Value;
            SerialWrite(Form1.SET_PRESSURE_LIMITS + " " + form1.pressure_min + " " + form1.pressure_max);
        }

        private void numericUpDown9_ValueChanged(object sender, EventArgs e)
        {
            form1.pressure_max = (double)numericUpDown9.Value;
            SerialWrite(Form1.SET_PRESSURE_LIMITS + " " + form1.pressure_min + " " + form1.pressure_max);
        }

        private void numericUpDown10_ValueChanged(object sender, EventArgs e)
        {
            form1.pressure_kp = (double)numericUpDown10.Value;
            SerialWrite(Form1.SET_REGULATOR_TUNING + " " + form1.pressure_kp + " " + form1.regulator_ki + " " + form1.regulator_kd);
        }

        private void numericUpDown11_ValueChanged(object sender, EventArgs e)
        {
            form1.pressure_ki = (double)numericUpDown11.Value;
            SerialWrite(Form1.SET_REGULATOR_TUNING + " " + form1.pressure_kp + " " + form1.regulator_ki + " " + form1.regulator_kd);
        }

        private void numericUpDown12_ValueChanged(object sender, EventArgs e)
        {
            form1.pressure_kd = (double)numericUpDown12.Value;
            SerialWrite(Form1.SET_REGULATOR_TUNING + " " + form1.pressure_kp + " " + form1.regulator_ki + " " + form1.regulator_kd);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            try
            {
                form1.voltage_input_samples.Add(new double[] { voltage, (double)numericUpDown13.Value });
            }
            catch
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            CalculateSpline(form1.voltage_input_samples, form1.voltage_input_spline);

            label24.Text = "Samples: " + form1.voltage_input_samples.Count;
        }

        private void button10_Click(object sender, EventArgs e)
        {
            form1.voltage_input_samples.Clear();
            form1.voltage_input_spline.Clear();

            label24.Text = "Samples: " + form1.voltage_input_samples.Count;
        }

        private void button11_Click(object sender, EventArgs e)
        {
            try
            {
                form1.count_samples.Add(new double[] { count, (double)numericUpDown14.Value });
            }
            catch
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            CalculateSpline(form1.count_samples, form1.count_spline);

            label28.Text = "Samples: " + form1.count_samples.Count;
        }

        private void button12_Click(object sender, EventArgs e)
        {
            form1.count_samples.Clear();
            form1.count_spline.Clear();

            label28.Text = "Samples: " + form1.count_samples.Count;
        }

        private void button13_Click(object sender, EventArgs e)
        {
            try
            {
                form1.current_input_samples.Add(new double[] { current, (double)numericUpDown15.Value });
            }
            catch
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK);
                return;
            }

            CalculateSpline(form1.current_input_samples, form1.current_input_spline);

            label32.Text = "Samples: " + form1.current_input_samples.Count;
        }

        private void button14_Click(object sender, EventArgs e)
        {
            form1.current_input_samples.Clear();
            form1.current_input_spline.Clear();

            label32.Text = "Samples: " + form1.current_input_samples.Count;
        }

        private void Form2_Close(object sender, FormClosingEventArgs e)
        {
            if (run)
            {
                run = false;
                update_thread.Join();
                form1.serial_port.Close();
                form1.serial_port.Dispose();
            }
        }

        private void UpdateThread()
        {
            bool pump, hv;
            int pressure, voltage, current, count, sleep;
            ulong time = 0;
            Stopwatch stopwatch = new Stopwatch();

            run = true;

            while (run)
            {
                stopwatch.Restart();

                try
                {
                    form1.serial_port.WriteLine(Form1.GET_PUMP_INPUT.ToString());
                    pump = (form1.serial_port.ReadLine().Substring(0, 1) == "1");
                    form1.serial_port.WriteLine(Form1.GET_HV_INPUT.ToString());
                    hv = (form1.serial_port.ReadLine().Substring(0, 1) == "1");
                    form1.serial_port.WriteLine(Form1.GET_PRESSURE_INPUT.ToString());
                    pressure = Convert.ToInt32(form1.serial_port.ReadLine());
                    form1.serial_port.WriteLine(Form1.GET_VOLTAGE_INPUT.ToString());
                    voltage = Convert.ToInt32(form1.serial_port.ReadLine());
                    form1.serial_port.WriteLine(Form1.GET_CURRENT_INPUT.ToString());
                    current = Convert.ToInt32(form1.serial_port.ReadLine());
                    form1.serial_port.WriteLine(Form1.GET_COUNT_INPUT.ToString());
                    count = Convert.ToInt32(form1.serial_port.ReadLine());

                    this.BeginInvoke(update_delagate, pump, hv, pressure, count, voltage, current);
                }
                catch
                {
                    this.BeginInvoke(cleanup_delagate);
                    MessageBox.Show("Serial port disconnected.", "Fusor Control", MessageBoxButtons.OK);
                    return;
                }

                time += (ulong)form1.update_interval;
                sleep = (int)(form1.update_interval - stopwatch.ElapsedMilliseconds);

                if (sleep > 0)
                {
                    Thread.Sleep(sleep);
                }
            }
        }

        private void UpdateForm(bool pump, bool hv, double pressure, double count, double voltage, double current)
        {
            this.pressure = pressure;
            this.count = count;
            this.voltage = voltage;
            this.current = current;
            label3.Text = "Status: " + (pump ? "Normal" : "Accelerating/Stopped");
            label4.Text = "Status: " + (hv ? "On" : "Off");
            label9.Text = "Input: " + pressure;
            label22.Text = "Input: " + voltage;
            label26.Text = "Input: " + count;
            label30.Text = "Input: " + current;
        }

        private void Cleanup()
        {
            run = false;
            update_thread.Join();
            form1.serial_port.Close();
            form1.serial_port.Dispose();
            this.Close();
        }

        private void SerialWrite(string message)
        {
            try
            {
                form1.serial_port.WriteLine(message);
            }
            catch
            {
                Cleanup();
                MessageBox.Show("Serial port disconnected.", "Fusor Control", MessageBoxButtons.OK);
            }
        }

        private void CalculateSpline(List<double[]> points, List<double[]> spline)
        {
            int n = points.Count - 1;
            double[] x = new double[n + 1];
            double[] y = new double[n + 1];
            double[] a = new double[n + 1];
            double[] b = new double[n];
            double[] d = new double[n];
            double[] h = new double[n];
            double[] alpha = new double[n];
            double[] c = new double[n + 1];
            double[] l = new double[n + 1];
            double[] mu = new double[n + 1];
            double[] z = new double[n + 1];

            points = points.OrderBy(o => o[0]).ToList();

            for (int i = 0; i <= n; i++)
            {
                x[i] = points[i][0];
                y[i] = points[i][1];
            }

            for (int i = 0; i <= n; i++)
            {
                a[i] = y[i];
            }

            for (int i = 0; i <= n - 1; i++)
            {
                h[i] = x[i + 1] - x[i];
            }

            for (int i = 1; i <= n - 1; i++)
            {
                alpha[i] = 3 / h[i] * (a[i + 1] - a[i]) - 3 / h[i - 1] * (a[i] - a[i - 1]);
            }

            l[0] = 1;
            mu[0] = z[0] = 0;

            for (int i = 1; i <= n - 1; i++)
            {
                l[i] = 2 * (x[i + 1] - x[i - 1]) - h[i - 1] * mu[i - 1];
                mu[i] = h[i] / l[i];
                z[i] = (alpha[i] - h[i - 1] * z[i - 1]) / l[i];
            }

            l[n] = 1;
            z[n] = c[n] = 0;

            for (int j = n - 1; j >= 0; j--)
            {
                c[j] = z[j] - mu[j] * c[j + 1];
                b[j] = (a[j + 1] - a[j]) / h[j] - (h[j] * (c[j + 1] + 2 * c[j])) / 3;
                d[j] = (c[j + 1] - c[j]) / (3 * h[j]);
            }

            spline.Clear();

            for (int i = 0; i <= n - 1; i++)
            {
                spline.Add(new double[] { x[i], a[i], b[i], c[i], d[i] });
            }
        }
    }
}